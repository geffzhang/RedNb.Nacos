using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core;
using RedNb.Nacos.GrpcClient.Config;
using RedNb.Nacos.GrpcClient.Naming;
using RedNb.Nacos.GrpcClient.Protos;
using ProtoMetadata = RedNb.Nacos.GrpcClient.Protos.Metadata;

namespace RedNb.Nacos.GrpcClient;

/// <summary>
/// gRPC client for Nacos server communication.
/// Implements connection management, request/response handling, and server push processing.
/// </summary>
public class NacosGrpcClient : IAsyncDisposable
{
    private readonly NacosClientOptions _options;
    private readonly ILogger? _logger;
    private readonly string _clientId;
    private readonly string _module;
    private readonly JsonSerializerOptions _jsonOptions;
    
    /// <summary>
    /// The Nacos 3.x unary request method (<c>Request/request</c>, no proto package —
    /// the server registers the bare service name). Every client-to-server request,
    /// ServerCheck included, travels over this method: Nacos 3.2.4's bi-stream
    /// acceptor only accepts ConnectionSetup and client push acknowledgements and
    /// drops anything else ("unknown payload receive", server-side remote log).
    /// Declared by hand because Grpc.Tools generates an uncompilable stub for a
    /// proto method whose name starts with a lowercase letter — see the note in
    /// <c>Protos/nacos_grpc.proto</c>.
    /// </summary>
    private static readonly Method<Payload, Payload> RequestMethod = new(
        MethodType.Unary,
        "Request",
        "request",
        Marshallers.Create<Payload>(payload => payload.ToByteArray(), bytes => Payload.Parser.ParseFrom(bytes)),
        Marshallers.Create<Payload>(payload => payload.ToByteArray(), bytes => Payload.Parser.ParseFrom(bytes)));

    private GrpcChannel? _channel;
    private BiRequestStream.BiRequestStreamClient? _biStreamClient;
    private AsyncDuplexStreamingCall<Payload, Payload>? _biStream;
    
    private readonly CancellationTokenSource _cts;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, Action<string, string>> _pushHandlers = new();
    
    /// <summary>
    /// Completed when the server answers the ConnectionSetup with a
    /// <c>SetupAckRequest</c>, i.e. when the connection is registered server-side.
    /// </summary>
    private volatile TaskCompletionSource<bool>? _setupAck;

    private volatile bool _connected;
    private volatile bool _disposed;
    private string? _currentServer;
    private string? _connectionId;
    private DateTime _lastActiveTime;
    private Task? _keepAliveTask;
    private Task? _receiveTask;

    /// <summary>
    /// Keep alive interval in milliseconds.
    /// </summary>
    private const int KeepAliveIntervalMs = 5000;

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    private const int ConnectionTimeoutMs = 3000;

    /// <summary>
    /// Reconnection delay in milliseconds.
    /// </summary>
    private const int ReconnectDelayMs = 3000;

    /// <summary>
    /// Creates a gRPC client.
    /// </summary>
    /// <param name="options">The client options.</param>
    /// <param name="logger">The optional logger.</param>
    /// <param name="module">
    /// The module announced to the server in the ConnectionSetup labels. Nacos 3.2.4
    /// only tracks a connection in the naming <c>ConnectionBasedClientManager</c> when
    /// the label is <c>naming</c> — an instance registration over a connection labelled
    /// <c>config</c> is rejected with "Client [id] connection already disconnect".
    /// Config traffic keeps the <c>config</c> label, like the Java SDK.
    /// </param>
    public NacosGrpcClient(NacosClientOptions options, ILogger? logger = null, string module = "config")
    {
        _options = options;
        _logger = logger;
        _module = module;
        _clientId = Guid.NewGuid().ToString("N");
        _cts = new CancellationTokenSource();
        _lastActiveTime = DateTime.UtcNow;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Gets whether the client is connected.
    /// </summary>
    public bool IsConnected => _connected;

    /// <summary>
    /// Gets the connection ID assigned by server.
    /// </summary>
    public string? ConnectionId => _connectionId;

    /// <summary>
    /// Gets the client ID.
    /// </summary>
    public string ClientId => _clientId;

    /// <summary>
    /// Connects to the Nacos server.
    /// </summary>
    public virtual async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connected) return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connected) return;
            await ConnectInternalAsync(cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        var servers = _options.GetServerAddressList();
        Exception? lastException = null;

        foreach (var server in servers)
        {
            try
            {
                var grpcAddress = _options.GetGrpcAddress(server);
                var scheme = _options.EnableTls ? "https" : "http";
                var address = $"{scheme}://{grpcAddress}";

                _logger?.LogInformation("Connecting to Nacos gRPC server at {Address}", address);

                // Create channel with options
                _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        // The bi-stream carries the ConnectionSetup that registers
                        // this client, so every unary request must travel over the
                        // very same HTTP/2 connection. A second connection would be
                        // unregistered and answer "Invalid connection Id".
                        EnableMultipleHttp2Connections = false,
                        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                        ConnectTimeout = TimeSpan.FromMilliseconds(ConnectionTimeoutMs)
                    }
                });

                _biStreamClient = new BiRequestStream.BiRequestStreamClient(_channel);

                // Server check to get connection ID
                await ServerCheckAsync(cancellationToken);

                // Start bi-directional stream
                await StartBiStreamAsync(cancellationToken);

                // Start keep alive task
                _keepAliveTask = KeepAliveLoopAsync(_cts.Token);

                _currentServer = server;
                _connected = true;
                _lastActiveTime = DateTime.UtcNow;
                
                _logger?.LogInformation("Connected to Nacos gRPC server at {Address}, ConnectionId: {ConnectionId}", 
                    address, _connectionId);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "Failed to connect to Nacos gRPC server at {Server}", server);
                await CleanupConnectionAsync();
            }
        }

        throw new NacosException(NacosException.ServerError, 
            $"Failed to connect to any Nacos gRPC server: {lastException?.Message}", lastException!);
    }

    /// <summary>
    /// Sends a request and waits for response.
    /// </summary>
    public virtual async Task<TResponse?> RequestAsync<TResponse>(string type, object request,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        await EnsureConnectedAsync(cancellationToken);

        return await SendStreamRequestWithResponseAsync<TResponse>(
            type, request, TimeSpan.FromMilliseconds(_options.DefaultTimeout), cancellationToken);
    }

    /// <summary>
    /// Sends a request and does not deserialize the response. Despite the historical
    /// name, Nacos 3.x serves every client request over the unary <c>Request/request</c>
    /// method — the bi-stream carries ConnectionSetup and server push only.
    /// </summary>
    public virtual async Task SendStreamRequestAsync(string type, object request,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        await SendUnaryRequestAsync(type, request,
            TimeSpan.FromMilliseconds(_options.DefaultTimeout), cancellationToken);

        _logger?.LogDebug("Sent request of type {Type}", type);
    }

    /// <summary>
    /// Sends a request over the unary <c>Request/request</c> method and deserializes
    /// the response. The historical name is kept because the transport clients use it
    /// for the requests whose responses the caller inspects.
    /// </summary>
    public virtual async Task<TResponse?> SendStreamRequestWithResponseAsync<TResponse>(string type, object request,
        TimeSpan timeout, CancellationToken cancellationToken = default) where TResponse : class
    {
        await EnsureConnectedAsync(cancellationToken);

        var responseJson = await SendUnaryRequestAsync(type, request, timeout, cancellationToken);
        return string.IsNullOrEmpty(responseJson)
            ? null
            : JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions);
    }

    /// <summary>
    /// Sends a payload over the unary <c>Request/request</c> method and returns the
    /// raw JSON of the response body.
    /// </summary>
    private async Task<string?> SendUnaryRequestAsync(string type, object request, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        // Set the request ID if the request carries one; Nacos echoes it in the response.
        switch (request)
        {
            case ConfigRpcRequest configRpcRequest:
                configRpcRequest.RequestId = Guid.NewGuid().ToString("N");
                break;
            case NamingRpcRequest namingRpcRequest:
                namingRpcRequest.RequestId = Guid.NewGuid().ToString("N");
                break;
        }

        var payload = CreatePayload(type, request);
        var deadline = DateTime.UtcNow.Add(timeout);

        try
        {
            // Dispose the call so the HTTP/2 response/stream objects are released
            // per request instead of waiting for a GC (same as the generated stubs).
            using var call = _channel!.CreateCallInvoker().AsyncUnaryCall(RequestMethod, null,
                new CallOptions(deadline: deadline, cancellationToken: cancellationToken), payload);

            var response = await call.ResponseAsync;
            _lastActiveTime = DateTime.UtcNow;

            return response.Body?.Value.ToStringUtf8();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _connected = false;
            _logger?.LogWarning(ex, "gRPC connection lost, will reconnect");
            throw new NacosException(NacosException.ServerError, "Connection lost", ex);
        }
    }

    /// <summary>
    /// Registers a handler for push messages.
    /// </summary>
    public virtual void RegisterPushHandler(string handlerId, Action<string, string> handler)
    {
        _pushHandlers.TryAdd(handlerId, handler);
    }

    /// <summary>
    /// Unregisters a push handler.
    /// </summary>
    public virtual void UnregisterPushHandler(string handlerId)
    {
        _pushHandlers.TryRemove(handlerId, out _);
    }

    /// <summary>
    /// Registers a handler for push messages (legacy method).
    /// </summary>
    public void RegisterPushHandler(Action<string, string> handler)
    {
        RegisterPushHandler(handler.GetHashCode().ToString(), handler);
    }

    private async Task ServerCheckAsync(CancellationToken cancellationToken)
    {
        var request = new ServerCheckRequest();
        var payload = CreatePayload(ServerCheckRequest.TYPE, request);

        using var call = _channel!.CreateCallInvoker().AsyncUnaryCall(
            RequestMethod,
            null,
            new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(ConnectionTimeoutMs), cancellationToken: cancellationToken),
            payload);

        var response = await call.ResponseAsync;

        if (response.Body != null && !response.Body.Value.IsEmpty)
        {
            var json = response.Body.Value.ToStringUtf8();
            var checkResponse = JsonSerializer.Deserialize<ServerCheckResponse>(json, _jsonOptions);
            _connectionId = checkResponse?.ConnectionId;
        }
    }

    private async Task StartBiStreamAsync(CancellationToken cancellationToken)
    {
        var setupAck = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _setupAck = setupAck;

        _biStream = _biStreamClient!.requestBiStream(cancellationToken: _cts.Token);

        // Receive before writing the setup so the server's SetupAckRequest cannot
        // be missed.
        _receiveTask = ReceiveStreamMessagesAsync(_cts.Token);

        // Send initial setup through stream. An explicit ability table makes the
        // server answer with SetupAckRequest once the connection is registered;
        // without it the server registers silently and a request racing the setup
        // is rejected ("Invalid connection Id ... is unregistered").
        var setupRequest = new ConnectionSetupRequest
        {
            ClientVersion = "RedNb.Nacos/2.0.0",
            Tenant = _options.Namespace,
            Labels = new Dictionary<string, string>
            {
                { "source", "sdk" },
                { "module", _module }
            },
            AbilityTable = new Dictionary<string, bool>()
        };

        var payload = CreatePayload(ConnectionSetupRequest.TYPE, setupRequest);
        await _biStream.RequestStream.WriteAsync(payload, cancellationToken);

        // Wait for the registration acknowledgement before reporting the client as
        // connected: requests sent earlier are dropped by the server as coming
        // from an unknown connection.
        var completed = await Task.WhenAny(setupAck.Task, Task.Delay(ConnectionTimeoutMs, cancellationToken));
        if (completed != setupAck.Task)
        {
            _logger?.LogWarning(
                "Nacos did not acknowledge the gRPC connection setup within {Timeout}ms", ConnectionTimeoutMs);
        }
    }

    private async Task ReceiveStreamMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var payload in _biStream!.ResponseStream.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var type = payload.Metadata?.Type ?? "Unknown";
                    var body = payload.Body?.Value.ToStringUtf8() ?? "{}";

                    _lastActiveTime = DateTime.UtcNow;
                    _logger?.LogDebug("Received push message of type {Type}", type);

                    // Everything the server writes to the bi-stream is a push:
                    // request responses travel over the unary Request/request call.
                    await HandleServerPushAsync(type, body, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing push message");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Stream receive cancelled");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger?.LogDebug("Stream cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in bi-stream receive loop");
            _connected = false;
        }
    }

    private async Task HandleServerPushAsync(string type, string body, CancellationToken cancellationToken)
    {
        // The connection setup acknowledgement is the readiness signal awaited by
        // StartBiStreamAsync. It is sent without expecting an ack (sendRequestNoAck).
        if (type == SetupAckRequestType)
        {
            _logger?.LogDebug("Server acknowledged the gRPC connection setup");
            _setupAck?.TrySetResult(true);
            return;
        }

        // Handle client detection (health check from server)
        if (type == ClientDetectionRequest.TYPE)
        {
            var detectionResponse = new ClientDetectionResponse { Success = true };

            if (TryGetRequestId(body, out var detectionRequestId))
            {
                detectionResponse.RequestId = detectionRequestId;
            }

            await SendPushAckAsync(ClientDetectionResponse.TYPE, detectionResponse, cancellationToken);
            return;
        }

        // Notify all registered handlers
        foreach (var handler in _pushHandlers.Values)
        {
            try
            {
                handler(type, body);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in push handler for type {Type}", type);
            }
        }

        // Send ack for requests
        if (type.EndsWith("Request"))
        {
            var responseType = type.Replace("Request", "Response");
            await SendPushAckAsync(responseType, BuildPushAck(body), cancellationToken);
        }
    }

    /// <summary>
    /// The payload type of the push Nacos answers a ConnectionSetup with when the
    /// setup carries an ability table (server: <c>SetupAckRequest</c>, sent through
    /// <c>sendRequestNoAck</c>).
    /// </summary>
    private const string SetupAckRequestType = "SetupAckRequest";

    /// <summary>
    /// Builds a push acknowledgement echoing the push's <c>requestId</c>: the server
    /// correlates an ack with its request through that id and logs "Ack receive on a
    /// outdated request" for an ack whose id is missing.
    /// </summary>
    private static object BuildPushAck(string body)
    {
        return TryGetRequestId(body, out var requestId)
            ? new { success = true, requestId }
            : new { success = true };
    }

    private static bool TryGetRequestId(string body, out string? requestId)
    {
        requestId = null;

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("requestId", out var element) &&
                element.ValueKind == JsonValueKind.String)
            {
                requestId = element.GetString();
                return requestId != null;
            }
        }
        catch (JsonException)
        {
            // Not JSON: send the ack without an id.
        }

        return false;
    }

    private async Task SendPushAckAsync(string type, object response, CancellationToken cancellationToken)
    {
        try
        {
            var ackPayload = CreatePayload(type, response);
            await _biStream!.RequestStream.WriteAsync(ackPayload, cancellationToken);
            _logger?.LogDebug("Sent ack for {Type}", type);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send ack for {Type}", type);
        }
    }

    private async Task KeepAliveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(KeepAliveIntervalMs, cancellationToken);

                if (!_connected) continue;

                // Check if we need to send health check
                if ((DateTime.UtcNow - _lastActiveTime).TotalMilliseconds > KeepAliveIntervalMs)
                {
                    await SendHealthCheckAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in keep alive loop");
            }
        }
    }

    private async Task SendHealthCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new HealthCheckRequest();
            var response = await RequestAsync<HealthCheckResponse>(HealthCheckRequest.TYPE, request, cancellationToken);
            
            if (response?.IsSuccess == true)
            {
                _lastActiveTime = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Health check failed");
            _connected = false;
        }
    }

    private Payload CreatePayload(string type, object request)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var body = ByteString.CopyFromUtf8(json);

        return new Payload
        {
            Metadata = new ProtoMetadata
            {
                Type = type,
                ClientIp = GetLocalIp(),
                Headers = 
                { 
                    { "connectionId", _connectionId ?? _clientId },
                    { "clientId", _clientId }
                }
            },
            Body = new Any
            {
                Value = body
            }
        };
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (!_connected)
        {
            await ConnectAsync(cancellationToken);
        }
        
        if (!_connected)
        {
            throw new NacosException(NacosException.ClientDisconnect, "Not connected to Nacos server");
        }
    }

    private async Task CleanupConnectionAsync()
    {
        _connected = false;
        _setupAck = null;

        try
        {
            if (_biStream != null)
            {
                await _biStream.RequestStream.CompleteAsync();
                _biStream.Dispose();
                _biStream = null;
            }
        }
        catch { /* Ignore */ }

        try
        {
            _channel?.Dispose();
            _channel = null;
        }
        catch { /* Ignore */ }

        _biStreamClient = null;
        _connectionId = null;
    }

    private static string GetLocalIp()
    {
        try
        {
            var hostName = System.Net.Dns.GetHostName();
            var addresses = System.Net.Dns.GetHostAddresses(hostName);
            var ipv4 = addresses.FirstOrDefault(a => 
                a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                !System.Net.IPAddress.IsLoopback(a));
            return ipv4?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _cts.CancelAsync();
        
        // Wait for background tasks
        if (_keepAliveTask != null)
        {
            try { await _keepAliveTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* Ignore */ }
        }
        if (_receiveTask != null)
        {
            try { await _receiveTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* Ignore */ }
        }

        await CleanupConnectionAsync();
        
        _cts.Dispose();
        _connectionLock.Dispose();
    }
}
