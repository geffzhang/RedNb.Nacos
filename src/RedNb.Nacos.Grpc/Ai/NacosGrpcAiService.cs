using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedNb.Nacos;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Listener;
using RedNb.Nacos.Ai.Models;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Mcp.Import;
using RedNb.Nacos.Ai.Models.Mcp.Validation;
using RedNb.Nacos.Grpc.Serialization;

namespace RedNb.Nacos.Grpc.Ai;

/// <summary>
/// Nacos AI service implementation using gRPC.
/// Provides both A2A (Agent-to-Agent) and MCP (Model Context Protocol) capabilities.
/// </summary>
public partial class NacosGrpcAiService : IAiService
{
    private readonly NacosClientOptions _options;
    private readonly NacosGrpcClient _grpcClient;
    private readonly ILogger<NacosGrpcAiService>? _logger;
    private readonly string _namespaceId;
    private readonly RedNb.Nacos.Http.Transport.NacosHttpClient _registryHttpClient;
    private readonly RedNb.Nacos.Http.Ai.NacosPromptService _promptService;
    private readonly RedNb.Nacos.Http.Ai.NacosSkillService _skillService;
    private readonly RedNb.Nacos.Http.Ai.NacosAgentSpecService _agentSpecService;

    private readonly ConcurrentDictionary<string, McpServerDetailInfo?> _mcpCache = new();
    private readonly ConcurrentDictionary<string, AgentCardDetailInfo?> _agentCache = new();
    private readonly ConcurrentDictionary<string, List<AbstractNacosMcpServerListener>> _mcpListeners = new();
    private readonly ConcurrentDictionary<string, List<AbstractNacosAgentCardListener>> _agentListeners = new();

    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = NacosGrpcJsonOptions.Create();

    /// <summary>
    /// Creates a new NacosGrpcAiService.
    /// </summary>
    // The connection must announce the naming module: the server only tracks
    // `naming`-labelled connections, and MCP endpoint ops register ephemeral
    // naming instances bound to the connection (mirror NacosGrpcNamingService).
    public NacosGrpcAiService(NacosClientOptions options, ILogger<NacosGrpcAiService>? logger = null)
        : this(options, new NacosGrpcClient(options, logger, "naming"), logger)
    {
    }

    /// <summary>
    /// Test-friendly overload: injects the gRPC client so tests can capture
    /// requests with a <c>FakeNacosGrpcClient</c> instead of a live channel.
    /// </summary>
    internal NacosGrpcAiService(NacosClientOptions options, NacosGrpcClient grpcClient, ILogger<NacosGrpcAiService>? logger = null)
    {
        _options = options;
        _logger = logger;
        _grpcClient = grpcClient;
        _namespaceId = string.IsNullOrWhiteSpace(options.Namespace) ? NacosConstants.DefaultNamespace : options.Namespace;

        // Prompt / Skill / AgentSpec APIs are HTTP-only on the Nacos server side,
        // so they are served by dedicated HTTP services sharing one client.
        _registryHttpClient = new RedNb.Nacos.Http.Transport.NacosHttpClient(options, logger);
        _promptService = new RedNb.Nacos.Http.Ai.NacosPromptService(_registryHttpClient, options, logger);
        _skillService = new RedNb.Nacos.Http.Ai.NacosSkillService(_registryHttpClient, options, logger);
        _agentSpecService = new RedNb.Nacos.Http.Ai.NacosAgentSpecService(_registryHttpClient, options, logger);

        // Register push handler
        _grpcClient.RegisterPushHandler(HandlePushMessage);
    }

    /// <summary>
    /// Initializes the gRPC connection.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _grpcClient.ConnectAsync(cancellationToken);
        _logger?.LogInformation("NacosGrpcAiService initialized");
    }

    internal string? ConnectionId => _grpcClient.ConnectionId;
    internal bool IsConnected => _grpcClient.IsConnected;

    #region MCP Server Operations

    /// <inheritdoc />
    public Task<McpServerDetailInfo?> GetMcpServerAsync(string mcpName, CancellationToken cancellationToken = default)
    {
        return GetMcpServerAsync(mcpName, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<McpServerDetailInfo?> GetMcpServerAsync(string mcpName, string? version, CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);

        var request = new McpServerQueryRequest
        {
            NamespaceId = _namespaceId,
            McpName = mcpName,
            Version = version
        };

        var response = await _grpcClient.RequestAsync<McpServerQueryResponse>(
            "QueryMcpServerRequest", request, cancellationToken);

        return response?.McpServerDetailInfo;
    }

    /// <inheritdoc />
    public Task<string> ReleaseMcpServerAsync(McpServerBasicInfo serverSpecification, McpToolSpecification? toolSpecification, CancellationToken cancellationToken = default)
    {
        return ReleaseMcpServerAsync(serverSpecification, toolSpecification, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ReleaseMcpServerAsync(McpServerBasicInfo serverSpecification, McpToolSpecification? toolSpecification, McpEndpointSpec? endpointSpecification, CancellationToken cancellationToken = default)
    {
        ValidateMcpServerSpec(serverSpecification);

        var request = new McpServerReleaseRequest
        {
            NamespaceId = _namespaceId,
            McpName = serverSpecification.Name!,
            ServerSpecification = serverSpecification,
            ToolSpecification = toolSpecification,
            EndpointSpecification = endpointSpecification
        };

        var response = await _grpcClient.RequestAsync<McpServerReleaseResponse>(
            "ReleaseMcpServerRequest", request, cancellationToken);

        return response?.McpId ?? string.Empty;
    }

    /// <inheritdoc />
    public Task RegisterMcpServerEndpointAsync(string mcpName, string address, int port, CancellationToken cancellationToken = default)
    {
        return RegisterMcpServerEndpointAsync(mcpName, address, port, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RegisterMcpServerEndpointAsync(string mcpName, string address, int port, string? version, CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);
        ValidateEndpoint(address, port);

        var request = new McpEndpointRequest
        {
            NamespaceId = _namespaceId,
            McpName = mcpName,
            Address = address,
            Port = port,
            Version = version,
            Type = "registerEndpoint"
        };

        var response = await _grpcClient.RequestAsync<OperationResponse>(
            "McpServerEndpointRequest", request, cancellationToken);

        if (response?.Success != true)
        {
            throw new NacosException(NacosException.ServerError, "Failed to register MCP endpoint");
        }

        _logger?.LogInformation("Registered MCP endpoint {Address}:{Port} to {McpName}", address, port, mcpName);
    }

    /// <inheritdoc />
    public async Task DeregisterMcpServerEndpointAsync(string mcpName, string address, int port, CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);
        ValidateEndpoint(address, port);

        var request = new McpEndpointRequest
        {
            NamespaceId = _namespaceId,
            McpName = mcpName,
            Address = address,
            Port = port,
            Type = "deregisterEndpoint"
        };

        var response = await _grpcClient.RequestAsync<OperationResponse>(
            "McpServerEndpointRequest", request, cancellationToken);

        if (response?.Success != true)
        {
            throw new NacosException(NacosException.ServerError, "Failed to deregister MCP endpoint");
        }

        _logger?.LogInformation("Deregistered MCP endpoint {Address}:{Port} from {McpName}", address, port, mcpName);
    }

    /// <inheritdoc />
    public Task<McpServerDetailInfo?> SubscribeMcpServerAsync(string mcpName, AbstractNacosMcpServerListener listener, CancellationToken cancellationToken = default)
    {
        return SubscribeMcpServerAsync(mcpName, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public Task<McpServerDetailInfo?> SubscribeMcpServerAsync(string mcpName, string? version, AbstractNacosMcpServerListener listener, CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);
        if (listener == null) throw new NacosException(NacosException.InvalidParam, "listener is required");

        throw new NacosException(NacosException.ServerNotImplemented,
            "McpServerSubscribeRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    /// <inheritdoc />
    public Task UnsubscribeMcpServerAsync(string mcpName, AbstractNacosMcpServerListener listener, CancellationToken cancellationToken = default)
    {
        return UnsubscribeMcpServerAsync(mcpName, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsubscribeMcpServerAsync(string mcpName, string? version, AbstractNacosMcpServerListener listener, CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);
        if (listener == null) return Task.CompletedTask;

        var key = GetMcpKey(mcpName, version);

        if (_mcpListeners.TryGetValue(key, out var list))
        {
            list.Remove(listener);
            if (list.Count == 0)
            {
                _mcpListeners.TryRemove(key, out _);
                _mcpCache.TryRemove(key, out _);
            }
        }

        _logger?.LogDebug("Unsubscribed from MCP server {McpName}@{Version}", mcpName, version);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteMcpServerAsync(string mcpName, string? version = null, CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);

        throw new NacosException(NacosException.ServerNotImplemented,
            "McpServerDeleteRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    /// <inheritdoc />
    public Task<PageResult<McpServerBasicInfo>> ListMcpServersAsync(
        string? mcpName = null,
        string? search = null,
        int pageNo = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        throw new NacosException(NacosException.ServerNotImplemented,
            "McpServerListRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    #endregion

    #region MCP Server Import/Validation

    /// <inheritdoc />
    public Task<McpServerImportValidationResult> ValidateImportAsync(
        McpServerImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new NacosException(NacosException.InvalidParam, "request is required");

        throw new NacosException(NacosException.ServerNotImplemented,
            "McpServerValidateImportRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    /// <inheritdoc />
    public Task<McpServerImportResponse> ImportMcpServersAsync(
        McpServerImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new NacosException(NacosException.InvalidParam, "request is required");

        throw new NacosException(NacosException.ServerNotImplemented,
            "McpServerImportRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    #endregion

    #region MCP Tool Management

    /// <inheritdoc />
    public Task<McpToolSpec?> RefreshMcpToolAsync(
        string mcpName,
        string toolName,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);
        ValidateToolName(toolName);

        throw new NacosException(NacosException.ServerNotImplemented,
            "MCP tool management has no Nacos 3.2.4 gRPC handler and no HTTP console endpoint — operation unavailable on this server");
    }

    /// <inheritdoc />
    public Task<McpToolSpec?> GetMcpToolAsync(
        string mcpName,
        string toolName,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);
        ValidateToolName(toolName);

        throw new NacosException(NacosException.ServerNotImplemented,
            "MCP tool management has no Nacos 3.2.4 gRPC handler and no HTTP console endpoint — operation unavailable on this server");
    }

    /// <inheritdoc />
    public Task DeleteMcpToolAsync(
        string mcpName,
        string toolName,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);
        ValidateToolName(toolName);

        throw new NacosException(NacosException.ServerNotImplemented,
            "MCP tool management has no Nacos 3.2.4 gRPC handler and no HTTP console endpoint — operation unavailable on this server");
    }

    /// <inheritdoc />
    public Task UpdateMcpToolAsync(
        string mcpName,
        McpToolSpec toolSpec,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ValidateMcpName(mcpName);
        if (toolSpec == null)
            throw new NacosException(NacosException.InvalidParam, "toolSpec is required");
        ValidateToolName(toolSpec.Name);

        throw new NacosException(NacosException.ServerNotImplemented,
            "MCP tool management has no Nacos 3.2.4 gRPC handler and no HTTP console endpoint — operation unavailable on this server");
    }

    #endregion

    #region Agent Card Operations

    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> GetAgentCardAsync(string agentName, CancellationToken cancellationToken = default)
    {
        return GetAgentCardAsync(agentName, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> GetAgentCardAsync(string agentName, string? version, CancellationToken cancellationToken = default)
    {
        return GetAgentCardAsync(agentName, version, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentCardDetailInfo?> GetAgentCardAsync(string agentName, string? version, string? registrationType, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);

        var request = new AgentCardQueryRequest
        {
            NamespaceId = _namespaceId,
            AgentName = agentName,
            Version = version,
            RegistrationType = registrationType
        };

        var response = await _grpcClient.RequestAsync<AgentCardQueryResponse>(
            "QueryAgentCardRequest", request, cancellationToken);

        return response?.AgentCardDetailInfo;
    }

    /// <inheritdoc />
    public Task ReleaseAgentCardAsync(AgentCard agentCard, CancellationToken cancellationToken = default)
    {
        return ReleaseAgentCardAsync(agentCard, AiConstants.A2a.A2aEndpointTypeService, false, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReleaseAgentCardAsync(AgentCard agentCard, string registrationType, CancellationToken cancellationToken = default)
    {
        return ReleaseAgentCardAsync(agentCard, registrationType, false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReleaseAgentCardAsync(AgentCard agentCard, string registrationType, bool setAsLatest, CancellationToken cancellationToken = default)
    {
        ValidateAgentCard(agentCard);

        var request = new AgentCardReleaseRequest
        {
            NamespaceId = _namespaceId,
            AgentName = agentCard.Name!,
            AgentCard = agentCard,
            RegistrationType = registrationType ?? AiConstants.A2a.A2aEndpointTypeService,
            SetAsLatest = setAsLatest
        };

        var response = await _grpcClient.RequestAsync<OperationResponse>(
            "ReleaseAgentCardRequest", request, cancellationToken);

        if (response?.Success != true)
        {
            throw new NacosException(NacosException.ServerError, "Failed to release Agent Card");
        }

        _logger?.LogInformation("Released Agent Card {AgentName}@{Version}", agentCard.Name, agentCard.Version);
    }

    /// <inheritdoc />
    public Task RegisterAgentEndpointAsync(string agentName, string version, string address, int port, CancellationToken cancellationToken = default)
    {
        return RegisterAgentEndpointAsync(agentName, version, address, port, AiConstants.A2a.TransportJsonRpc, cancellationToken);
    }

    /// <inheritdoc />
    public Task RegisterAgentEndpointAsync(string agentName, string version, string address, int port, string transport, CancellationToken cancellationToken = default)
    {
        var endpoint = new AgentEndpoint
        {
            Address = address,
            Port = port,
            Version = version,
            Transport = transport
        };
        return RegisterAgentEndpointAsync(agentName, endpoint, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RegisterAgentEndpointAsync(string agentName, AgentEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        ValidateAgentEndpoint(endpoint);

        var request = new AgentEndpointRequest
        {
            NamespaceId = _namespaceId,
            AgentName = agentName,
            Endpoint = endpoint,
            Type = "registerEndpoint"
        };

        var response = await _grpcClient.RequestAsync<OperationResponse>(
            "AgentEndpointRequest", request, cancellationToken);

        if (response?.Success != true)
        {
            throw new NacosException(NacosException.ServerError, "Failed to register Agent endpoint");
        }

        _logger?.LogInformation("Registered Agent endpoint {Address}:{Port} to {AgentName}",
            endpoint.Address, endpoint.Port, agentName);
    }

    /// <inheritdoc />
    public async Task RegisterAgentEndpointsAsync(string agentName, IEnumerable<AgentEndpoint> endpoints, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        var endpointList = endpoints?.ToList()
            ?? throw new NacosException(NacosException.InvalidParam, "endpoints is required");
        if (endpointList.Count == 0)
        {
            throw new NacosException(NacosException.InvalidParam, "endpoints cannot be empty");
        }

        // Per-endpoint validation: same semantics as the single-endpoint path
        // (version non-blank, address non-blank, port in range). Each endpoint is
        // normalized so callers may pass partial AgentEndpoint instances; the
        // default transport matches what the single-endpoint path uses.
        var normalized = new List<AgentEndpoint>(endpointList.Count);
        foreach (var endpoint in endpointList)
        {
            ValidateAgentEndpoint(endpoint);

            normalized.Add(new AgentEndpoint
            {
                Transport = string.IsNullOrWhiteSpace(endpoint.Transport)
                    ? AiConstants.A2a.TransportJsonRpc
                    : endpoint.Transport,
                Address = endpoint.Address,
                Port = endpoint.Port,
                Path = endpoint.Path,
                SupportTls = endpoint.SupportTls,
                Version = endpoint.Version,
                Protocol = endpoint.Protocol,
                Query = endpoint.Query
            });
        }

        // Single batch op over the AI gRPC connection — server side this is
        // dispatched by BatchAgentEndpointRequestHandler (Nacos 3.2.4, audit §3.1
        // row 5). The handler returns AgentEndpointResponse with a single
        // resultCode (all-or-nothing: there is no per-endpoint result list, so a
        // partial success cannot be surfaced to the caller — if the server later
        // grows that capability this method should switch to inspecting it).
        var request = new BatchAgentEndpointRequest
        {
            NamespaceId = _namespaceId,
            AgentName = agentName,
            Endpoints = normalized
        };

        var response = await _grpcClient.RequestAsync<BatchAgentEndpointResponse>(
            "BatchAgentEndpointRequest", request, cancellationToken);

        if (response is null)
        {
            throw new NacosException(NacosException.ServerError,
                "Failed to register Agent endpoints: no response from server");
        }

        // Nacos 3.x marks success with the Jackson-serialized "success" flag (and
        // resultCode 200 = ResponseCode.SUCCESS) rather than resultCode 0. Trust
        // the flag first so a successful batch register is not rejected; fall back
        // to resultCode != 0 for servers/wire shapes that omit "success".
        if (response.Success is not true && response.ResultCode != 0)
        {
            throw new NacosException(NacosException.ServerError,
                $"Failed to register Agent endpoints: {response.Message ?? "unknown error"}");
        }

        _logger?.LogInformation("Registered {Count} Agent endpoint(s) to {AgentName} via BatchAgentEndpointRequest",
            normalized.Count, agentName);
    }

    /// <inheritdoc />
    public Task DeregisterAgentEndpointAsync(string agentName, string version, string address, int port, CancellationToken cancellationToken = default)
    {
        var endpoint = new AgentEndpoint
        {
            Address = address,
            Port = port,
            Version = version
        };
        return DeregisterAgentEndpointAsync(agentName, endpoint, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeregisterAgentEndpointAsync(string agentName, AgentEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        ValidateAgentEndpoint(endpoint);

        var request = new AgentEndpointRequest
        {
            NamespaceId = _namespaceId,
            AgentName = agentName,
            Endpoint = endpoint,
            Type = "deregisterEndpoint"
        };

        var response = await _grpcClient.RequestAsync<OperationResponse>(
            "AgentEndpointRequest", request, cancellationToken);

        if (response?.Success != true)
        {
            throw new NacosException(NacosException.ServerError, "Failed to deregister Agent endpoint");
        }

        _logger?.LogInformation("Deregistered Agent endpoint {Address}:{Port} from {AgentName}",
            endpoint.Address, endpoint.Port, agentName);
    }

    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> SubscribeAgentCardAsync(string agentName, AbstractNacosAgentCardListener listener, CancellationToken cancellationToken = default)
    {
        return SubscribeAgentCardAsync(agentName, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> SubscribeAgentCardAsync(string agentName, string? version, AbstractNacosAgentCardListener listener, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        if (listener == null) throw new NacosException(NacosException.InvalidParam, "listener is required");

        throw new NacosException(NacosException.ServerNotImplemented,
            "AgentCardSubscribeRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    /// <inheritdoc />
    public Task UnsubscribeAgentCardAsync(string agentName, AbstractNacosAgentCardListener listener, CancellationToken cancellationToken = default)
    {
        return UnsubscribeAgentCardAsync(agentName, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsubscribeAgentCardAsync(string agentName, string? version, AbstractNacosAgentCardListener listener, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        if (listener == null) return Task.CompletedTask;

        var key = GetAgentKey(agentName, version);

        if (_agentListeners.TryGetValue(key, out var list))
        {
            list.Remove(listener);
            if (list.Count == 0)
            {
                _agentListeners.TryRemove(key, out _);
                _agentCache.TryRemove(key, out _);
            }
        }

        _logger?.LogDebug("Unsubscribed from Agent Card {AgentName}@{Version}", agentName, version);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAgentAsync(string agentName, string? version = null, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);

        throw new NacosException(NacosException.ServerNotImplemented,
            "AgentDeleteRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    /// <inheritdoc />
    public Task<PageResult<AgentCardBasicInfo>> ListAgentCardsAsync(
        string? agentName = null,
        string? search = null,
        int pageNo = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        throw new NacosException(NacosException.ServerNotImplemented,
            "AgentListRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    /// <inheritdoc />
    public Task<List<string>> ListAgentVersionsAsync(string agentName, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        throw new NacosException(NacosException.ServerNotImplemented,
            "AgentVersionListRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    /// <inheritdoc />
    public Task<List<AgentVersionInfo>> ListAgentVersionInfosAsync(string agentName, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        throw new NacosException(NacosException.ServerNotImplemented,
            "AgentVersionListRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await DisposeAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _mcpListeners.Clear();
        _agentListeners.Clear();
        _mcpCache.Clear();
        _agentCache.Clear();

        await _promptService.DisposeAsync();
        await _skillService.DisposeAsync();
        await _agentSpecService.DisposeAsync();
        _registryHttpClient.Dispose();
        await _grpcClient.DisposeAsync();
        _disposed = true;
    }

    #endregion

    #region Private Methods

    private void HandlePushMessage(string type, string body)
    {
        try
        {
            if (type.Contains("McpServer") || type.Contains("Mcp"))
            {
                HandleMcpServerPush(body);
            }
            else if (type.Contains("AgentCard") || type.Contains("Agent"))
            {
                HandleAgentCardPush(body);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling AI push message of type {Type}", type);
        }
    }

    private void HandleMcpServerPush(string body)
    {
        var notification = JsonSerializer.Deserialize<McpServerNotification>(body, JsonOptions);
        if (notification == null) return;

        var key = GetMcpKey(notification.McpName, notification.Version);

        if (_mcpListeners.TryGetValue(key, out var listeners) && listeners.Count > 0)
        {
            var detailInfo = notification.McpServerDetailInfo;
            if (detailInfo != null)
            {
                _mcpCache[key] = detailInfo;
                var evt = new NacosMcpServerEvent(detailInfo);

                foreach (var listener in listeners.ToList())
                {
                    try
                    {
                        listener.OnEvent(evt);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error notifying MCP listener for {McpName}", notification.McpName);
                    }
                }
            }
        }
    }

    private void HandleAgentCardPush(string body)
    {
        var notification = JsonSerializer.Deserialize<AgentCardNotification>(body, JsonOptions);
        if (notification == null) return;

        var key = GetAgentKey(notification.AgentName, notification.Version);

        if (_agentListeners.TryGetValue(key, out var listeners) && listeners.Count > 0)
        {
            var detailInfo = notification.AgentCardDetailInfo;
            if (detailInfo != null)
            {
                _agentCache[key] = detailInfo;
                var evt = new NacosAgentCardEvent(detailInfo);

                foreach (var listener in listeners.ToList())
                {
                    try
                    {
                        listener.OnEvent(evt);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error notifying Agent listener for {AgentName}", notification.AgentName);
                    }
                }
            }
        }
    }

    private static string GetMcpKey(string mcpName, string? version) => $"{mcpName}@@{version ?? "latest"}";
    private static string GetAgentKey(string agentName, string? version) => $"{agentName}@@{version ?? "latest"}";

    private static void ValidateMcpName(string mcpName)
    {
        if (string.IsNullOrWhiteSpace(mcpName))
            throw new NacosException(NacosException.InvalidParam, "mcpName is required");
    }

    private static void ValidateAgentName(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new NacosException(NacosException.InvalidParam, "agentName is required");
    }

    private static void ValidateToolName(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new NacosException(NacosException.InvalidParam, "toolName is required");
    }

    private static void ValidateEndpoint(string address, int port)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new NacosException(NacosException.InvalidParam, "address is required");
        if (port <= 0 || port > 65535)
            throw new NacosException(NacosException.InvalidParam, "port must be between 1 and 65535");
    }

    private static void ValidateMcpServerSpec(McpServerBasicInfo? spec)
    {
        if (spec == null)
            throw new NacosException(NacosException.InvalidParam, "serverSpecification is required");
        if (string.IsNullOrWhiteSpace(spec.Name))
            throw new NacosException(NacosException.InvalidParam, "serverSpecification.Name is required");
        if (spec.VersionDetail == null || string.IsNullOrWhiteSpace(spec.VersionDetail.Version))
            throw new NacosException(NacosException.InvalidParam, "serverSpecification.VersionDetail.Version is required");
    }

    private static void ValidateAgentCard(AgentCard? agentCard)
    {
        if (agentCard == null)
            throw new NacosException(NacosException.InvalidParam, "agentCard is required");
        if (string.IsNullOrWhiteSpace(agentCard.Name))
            throw new NacosException(NacosException.InvalidParam, "agentCard.Name is required");
        if (string.IsNullOrWhiteSpace(agentCard.Version))
            throw new NacosException(NacosException.InvalidParam, "agentCard.Version is required");
    }

    private static void ValidateAgentEndpoint(AgentEndpoint? endpoint)
    {
        if (endpoint == null)
            throw new NacosException(NacosException.InvalidParam, "endpoint is required");
        if (string.IsNullOrWhiteSpace(endpoint.Version))
            throw new NacosException(NacosException.InvalidParam, "endpoint.Version is required");
        ValidateEndpoint(endpoint.Address ?? "", endpoint.Port);
    }

    #endregion
}
