using System.Text.Json.Serialization;
using RedNb.Nacos.Grpc.Ai;

namespace RedNb.Nacos.Grpc.Serialization;

/// <summary>
/// Source-generated metadata for gRPC AI wire models that stay internal to the SDK.
/// Same options as <see cref="NacosGrpcJsonContext"/>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(McpServerQueryRequest))]
[JsonSerializable(typeof(McpServerQueryResponse))]
[JsonSerializable(typeof(McpServerReleaseRequest))]
[JsonSerializable(typeof(McpServerReleaseResponse))]
[JsonSerializable(typeof(McpEndpointRequest))]
[JsonSerializable(typeof(McpServerNotification))]
[JsonSerializable(typeof(AgentCardQueryRequest))]
[JsonSerializable(typeof(AgentCardQueryResponse))]
[JsonSerializable(typeof(AgentCardReleaseRequest))]
[JsonSerializable(typeof(AgentEndpointRequest))]
[JsonSerializable(typeof(BatchAgentEndpointRequest))]
[JsonSerializable(typeof(BatchAgentEndpointResponse))]
[JsonSerializable(typeof(AgentCardNotification))]
[JsonSerializable(typeof(OperationResponse))]
internal sealed partial class NacosGrpcInternalJsonContext : JsonSerializerContext
{
}
