using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.Mcp;

namespace RedNb.Nacos.Grpc.Ai;

// 原 NacosGrpcAiService 的 private 嵌套 wire 类,提升为 internal 顶层类
// 以便源生成上下文声明(源生成上下文无法引用 private 类型)。

internal class McpServerQueryRequest
{
    public string? NamespaceId { get; set; }
    public string McpName { get; set; } = string.Empty;
    public string? Version { get; set; }
}

internal class McpServerQueryResponse
{
    public McpServerDetailInfo? McpServerDetailInfo { get; set; }
}

internal class McpServerReleaseRequest
{
    public string? NamespaceId { get; set; }
    public string McpName { get; set; } = string.Empty;
    public McpServerBasicInfo? ServerSpecification { get; set; }
    public McpToolSpecification? ToolSpecification { get; set; }
    public McpEndpointSpec? EndpointSpecification { get; set; }
}

internal class McpServerReleaseResponse
{
    public string? McpId { get; set; }
}

internal class McpEndpointRequest
{
    public string? NamespaceId { get; set; }
    public string McpName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Version { get; set; }
    public string Type { get; set; } = "register";
}

internal class McpServerNotification
{
    public string McpName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public McpServerDetailInfo? McpServerDetailInfo { get; set; }
}

internal class AgentCardQueryRequest
{
    public string? NamespaceId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? RegistrationType { get; set; }
}

internal class AgentCardQueryResponse
{
    public AgentCardDetailInfo? AgentCardDetailInfo { get; set; }
}

internal class AgentCardReleaseRequest
{
    public string? NamespaceId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public AgentCard? AgentCard { get; set; }
    public string RegistrationType { get; set; } = AiConstants.A2a.A2aEndpointTypeService;
    public bool SetAsLatest { get; set; }
}

internal class AgentEndpointRequest
{
    public string? NamespaceId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public AgentEndpoint? Endpoint { get; set; }
    public string Type { get; set; } = "register";
}

/// <summary>
/// Batched version of <see cref="AgentEndpointRequest"/> — server side this is
/// handled by <c>BatchAgentEndpointRequestHandler</c> (Nacos 3.2.4). The wire
/// contract mirrors the Java type <c>BatchAgentEndpointRequest</c>:
/// <c>namespaceId</c> + <c>agentName</c> from <c>AbstractAgentRequest</c>, plus a
/// list of <c>endpoints</c>. No <c>type</c> discriminator — register is implied.
/// </summary>
internal class BatchAgentEndpointRequest
{
    public string? NamespaceId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public List<AgentEndpoint> Endpoints { get; set; } = new();
}

/// <summary>
/// Server's <c>AgentEndpointResponse</c> — inherits <c>resultCode</c> +
/// <c>message</c> from <c>com.alibaba.nacos.api.remote.response.Response</c> and
/// adds a <c>type</c> discriminator (unused by the SDK). The single-endpoint
/// path's <c>OperationResponse</c> (Success/Message) does not match the server
/// payload and is left in place to minimise blast radius; the batch path
/// inspects <c>success</c> / <c>resultCode</c> directly.
///
/// Nacos 3.x success contract (wire-captured against 3.2.4): the server's
/// <c>Response</c> carries a Jackson-serialized <c>success</c> field (the
/// computed <c>isSuccess()</c>, <c>true</c> on success) and <c>resultCode</c>
/// is <c>ResponseCode.SUCCESS.getCode()</c> = <c>200</c>, not <c>0</c> — a
/// successful register looks like
/// <c>{"resultCode":200,"errorCode":0,"type":"batchRegisterEndpoint","success":true}</c>.
/// Failures carry <c>success:false</c> plus a <c>message</c>. The batch
/// handler has no per-endpoint result list, so a partial success cannot be
/// reported to the caller.
/// </summary>
internal class BatchAgentEndpointResponse
{
    public int ResultCode { get; set; }
    public bool? Success { get; set; }
    public string? Message { get; set; }
    public string? Type { get; set; }
}

internal class AgentCardNotification
{
    public string AgentName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public AgentCardDetailInfo? AgentCardDetailInfo { get; set; }
}

internal class OperationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
