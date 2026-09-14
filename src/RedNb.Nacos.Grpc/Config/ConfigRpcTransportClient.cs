using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedNb.Nacos;
using RedNb.Nacos.Grpc.Serialization;

namespace RedNb.Nacos.Grpc.Config;

/// <summary>
/// Config-specific gRPC transport client.
/// Handles configuration service communication over gRPC.
/// </summary>
internal class ConfigRpcTransportClient : IAsyncDisposable
{
    private readonly NacosGrpcClient _grpcClient;
    private readonly NacosClientOptions _options;
    private readonly ILogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;
    private readonly FuzzyInitializationTracker _fuzzyInitialization = new();

    /// <summary>
    /// Event fired when a config change notification is received.
    /// </summary>
    public event Action<ConfigChangeNotifyRequest>? OnConfigChanged;

    /// <summary>
    /// Event fired when a fuzzy watch change notification is received.
    /// </summary>
    public event Action<ConfigFuzzyWatchChangeNotifyRequest>? OnFuzzyWatchChanged;

    public ConfigRpcTransportClient(NacosGrpcClient grpcClient, NacosClientOptions options, ILogger? logger = null)
    {
        _grpcClient = grpcClient;
        _options = options;
        _logger = logger;
        _jsonOptions = NacosGrpcJsonOptions.Create();

        // Register push handler
        _grpcClient.RegisterPushHandler("config", HandlePushMessage);
    }

    /// <summary>
    /// Gets whether the client is connected.
    /// </summary>
    public bool IsConnected => _grpcClient.IsConnected;

    /// <summary>
    /// Queries configuration from server.
    /// </summary>
    public async Task<ConfigQueryResponse?> QueryConfigAsync(string dataId, string group, string? tenant,
        string? tag = null, CancellationToken cancellationToken = default)
    {
        var request = new ConfigQueryRequest
        {
            DataId = dataId,
            Group = group,
            Tenant = tenant,
            Tag = tag
        };

        return await _grpcClient.RequestAsync<ConfigQueryResponse>(
            ConfigQueryRequest.TYPE, request, cancellationToken);
    }

    /// <summary>
    /// Publishes configuration to server.
    /// </summary>
    public async Task<bool> PublishConfigAsync(string dataId, string group, string? tenant,
        string content, string? type = null, string? casMd5 = null, string? encryptedDataKey = null,
        Dictionary<string, string>? additionalParams = null, CancellationToken cancellationToken = default)
    {
        var request = new ConfigPublishRequest
        {
            DataId = dataId,
            Group = group,
            Tenant = tenant,
            Content = content,
            Type = type,
            CasMd5 = casMd5,
            EncryptedDataKey = encryptedDataKey,
            AdditionMap = additionalParams
        };

        var response = await _grpcClient.RequestAsync<ConfigPublishResponse>(
            ConfigPublishRequest.TYPE, request, cancellationToken);

        return response?.IsSuccess ?? false;
    }

    /// <summary>
    /// Removes configuration from server.
    /// </summary>
    public async Task<bool> RemoveConfigAsync(string dataId, string group, string? tenant,
        string? tag = null, CancellationToken cancellationToken = default)
    {
        var request = new ConfigRemoveRequest
        {
            DataId = dataId,
            Group = group,
            Tenant = tenant,
            Tag = tag
        };

        var response = await _grpcClient.RequestAsync<ConfigRemoveResponse>(
            ConfigRemoveRequest.TYPE, request, cancellationToken);

        return response?.IsSuccess ?? false;
    }

    /// <summary>
    /// Sends batch listen request for configurations.
    /// </summary>
    public async Task<ConfigBatchListenResponse?> BatchListenAsync(
        List<ConfigListenContext> listenContexts, bool listen = true,
        CancellationToken cancellationToken = default)
    {
        var request = new ConfigBatchListenRequest
        {
            Listen = listen,
            ConfigListenContexts = listenContexts
        };

        // Listen operations need the response body (long-poll timeout applies)
        return await _grpcClient.SendRequestWithResponseAsync<ConfigBatchListenResponse>(
            ConfigBatchListenRequest.TYPE, request,
            TimeSpan.FromMilliseconds(_options.LongPollTimeout),
            cancellationToken);
    }

    /// <summary>
    /// Sends batch listen request via stream (fire and forget).
    /// </summary>
    public async Task SendBatchListenAsync(List<ConfigListenContext> listenContexts, bool listen = true,
        CancellationToken cancellationToken = default)
    {
        var request = new ConfigBatchListenRequest
        {
            Listen = listen,
            ConfigListenContexts = listenContexts
        };

        await _grpcClient.SendRequestAsync(ConfigBatchListenRequest.TYPE, request, cancellationToken);
    }

    /// <summary>
    /// Sends fuzzy watch request.
    /// </summary>
    public async Task<ConfigFuzzyWatchResponse?> FuzzyWatchAsync(
        List<ConfigFuzzyListenContext> contexts, bool watch = true,
        CancellationToken cancellationToken = default)
    {
        var request = new ConfigFuzzyWatchRequest
        {
            Watch = watch,
            Contexts = contexts
        };

        var context = contexts.Single();
        request.GroupKeyPattern = $"{(string.IsNullOrEmpty(_options.Namespace) ? "public" : _options.Namespace)}>>{context.GroupPattern}>>{context.DataIdPattern}";

        var initial = watch ? _fuzzyInitialization.Begin(request.GroupKeyPattern) : null;
        try
        {
            var response = await _grpcClient.SendRequestWithResponseAsync<ConfigFuzzyWatchResponse>(
                ConfigFuzzyWatchRequest.TYPE, request, TimeSpan.FromMilliseconds(_options.DefaultTimeout), cancellationToken);
            if (response?.IsSuccess == true && initial != null)
                response.MatchedGroupKeys = (await initial.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken)).ToList();
            return response;
        }
        finally { _fuzzyInitialization.Remove(request.GroupKeyPattern); }
    }

    /// <summary>
    /// Sends fuzzy watch request via stream (fire and forget).
    /// </summary>
    public async Task SendFuzzyWatchAsync(List<ConfigFuzzyListenContext> contexts, bool watch = true,
        CancellationToken cancellationToken = default)
    {
        foreach (var context in contexts)
        {
            var response = await FuzzyWatchAsync([context], watch, cancellationToken);
            if (response?.IsSuccess != true)
                throw new NacosException(response?.ErrorCode ?? NacosException.ServerError, response?.Message ?? "Fuzzy watch rejected");
        }
    }

    private void HandlePushMessage(string type, string body)
    {
        try
        {
            switch (type)
            {
                case ConfigChangeNotifyRequest.TYPE:
                    HandleConfigChangeNotify(body);
                    break;

                case ConfigFuzzyWatchChangeNotifyRequest.TYPE:
                    HandleFuzzyWatchChangeNotify(body);
                    break;
                case "ConfigFuzzyWatchSyncRequest":
                    HandleFuzzyWatchChangeNotify(body);
                    break;

                default:
                    _logger?.LogDebug("Received unknown config push type: {Type}", type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling config push message of type {Type}", type);
        }
    }

    private void HandleConfigChangeNotify(string body)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ConfigChangeNotifyRequest>(body, _jsonOptions);
            if (request != null)
            {
                _logger?.LogDebug("Received config change notify: {DataId}@{Group}",
                    request.DataId, request.Group);
                OnConfigChanged?.Invoke(request);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deserializing config change notify");
        }
    }

    private void HandleFuzzyWatchChangeNotify(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            _fuzzyInitialization.Process(root, "groupKey");
            if (root.TryGetProperty("contexts", out var contexts))
            {
                foreach (var item in contexts.EnumerateArray()) EmitFuzzy(item, root);
                return;
            }
            if (root.TryGetProperty("groupKey", out _)) { EmitFuzzy(root, root); return; }
            var request = JsonSerializer.Deserialize<ConfigFuzzyWatchChangeNotifyRequest>(body, _jsonOptions);
            if (request != null)
            {
                _logger?.LogDebug("Received fuzzy watch change notify: {DataId}@{Group}, Type={ChangeType}",
                    request.DataId, request.Group, request.ChangedType);
                OnFuzzyWatchChanged?.Invoke(request);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deserializing fuzzy watch change notify");
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _grpcClient.UnregisterPushHandler("config");
        OnConfigChanged = null;
        OnFuzzyWatchChanged = null;

        return ValueTask.CompletedTask;
    }

    private void EmitFuzzy(JsonElement item, JsonElement root)
    {
        if (!item.TryGetProperty("groupKey", out var key)) return;
        var parts = key.GetString()!.Split('+');
        if (parts.Length < 2) return;
        OnFuzzyWatchChanged?.Invoke(new ConfigFuzzyWatchChangeNotifyRequest
        {
            DataId = parts[0],
            Group = parts[1],
            Tenant = parts.Length > 2 ? parts[2] : "",
            ChangedType = item.TryGetProperty("changeType", out var change) ? change.GetString()! : "ADD_CONFIG",
            SyncType = root.TryGetProperty("syncType", out var sync) ? sync.GetString()! : "FUZZY_WATCH_RESOURCE_CHANGED"
        });
    }
}
