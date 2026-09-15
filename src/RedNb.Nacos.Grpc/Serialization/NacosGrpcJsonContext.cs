using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;
using RedNb.Nacos.Naming.Models;

namespace RedNb.Nacos.Grpc.Serialization;

/// <summary>
/// Source-generated serialization metadata for the gRPC wire payloads. The options
/// mirror the reflection-based <c>_jsonOptions</c> of 2.0.0: camelCase property
/// names, nulls omitted, case-insensitive reads.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConfigQueryRequest))]
[JsonSerializable(typeof(ConfigQueryResponse))]
[JsonSerializable(typeof(ConfigPublishRequest))]
[JsonSerializable(typeof(ConfigPublishResponse))]
[JsonSerializable(typeof(ConfigRemoveRequest))]
[JsonSerializable(typeof(ConfigRemoveResponse))]
[JsonSerializable(typeof(ConfigListenContext))]
[JsonSerializable(typeof(ConfigBatchListenRequest))]
[JsonSerializable(typeof(ConfigBatchListenResponse))]
[JsonSerializable(typeof(ConfigChangeNotifyRequest))]
[JsonSerializable(typeof(ConfigChangeNotifyResponse))]
[JsonSerializable(typeof(ConfigFuzzyListenContext))]
[JsonSerializable(typeof(ConfigFuzzyWatchRequest))]
[JsonSerializable(typeof(ConfigFuzzyWatchResponse))]
[JsonSerializable(typeof(ConfigFuzzyWatchChangeNotifyRequest))]
[JsonSerializable(typeof(ConfigFuzzyWatchChangeNotifyResponse))]
[JsonSerializable(typeof(ConnectionSetupRequest))]
[JsonSerializable(typeof(HealthCheckRequest))]
[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(ClientDetectionRequest))]
[JsonSerializable(typeof(ClientDetectionResponse))]
[JsonSerializable(typeof(ServerCheckRequest))]
[JsonSerializable(typeof(ServerCheckResponse))]
[JsonSerializable(typeof(InstanceRequest))]
[JsonSerializable(typeof(InstanceResponse))]
[JsonSerializable(typeof(BatchInstanceRequest))]
[JsonSerializable(typeof(BatchInstanceResponse))]
[JsonSerializable(typeof(ServiceQueryRequest))]
[JsonSerializable(typeof(ServiceQueryResponse))]
[JsonSerializable(typeof(SubscribeServiceRequest))]
[JsonSerializable(typeof(SubscribeServiceResponse))]
[JsonSerializable(typeof(ServiceListRequest))]
[JsonSerializable(typeof(ServiceListResponse))]
[JsonSerializable(typeof(NotifySubscriberRequest))]
[JsonSerializable(typeof(NotifySubscriberResponse))]
[JsonSerializable(typeof(NamingFuzzyWatchRequest))]
[JsonSerializable(typeof(NamingFuzzyWatchResponse))]
[JsonSerializable(typeof(NamingFuzzyWatchChangeItem))]
[JsonSerializable(typeof(NamingFuzzyWatchCancelRequest))]
[JsonSerializable(typeof(NamingFuzzyWatchCancelResponse))]
[JsonSerializable(typeof(NamingFuzzyWatchNotifyRequest))]
[JsonSerializable(typeof(NamingFuzzyWatchNotifyResponse))]
[JsonSerializable(typeof(PersistentInstanceRequest))]
[JsonSerializable(typeof(NamingInstance))]
[JsonSerializable(typeof(NamingServiceInfo))]
[JsonSerializable(typeof(AgentEndpoint))]
[JsonSerializable(typeof(List<AgentEndpoint>))]
[JsonSerializable(typeof(NamingSelector))]
public sealed partial class NacosGrpcJsonContext : JsonSerializerContext
{
}
