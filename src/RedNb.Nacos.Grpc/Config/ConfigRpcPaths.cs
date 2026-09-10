namespace RedNb.Nacos.Grpc.Config;

/// <summary>
/// gRPC Metadata.type strings used by Config bi-stream requests and responses.
/// Matches the values the Nacos 3.x Java gRPC client dispatches on.
/// </summary>
internal static class ConfigRpcPaths
{
    public const string ConfigQueryRequest = "ConfigQueryRequest";
    public const string ConfigQueryResponse = "ConfigQueryResponse";
    public const string ConfigPublishRequest = "ConfigPublishRequest";
    public const string ConfigPublishResponse = "ConfigPublishResponse";
    public const string ConfigRemoveRequest = "ConfigRemoveRequest";
    public const string ConfigRemoveResponse = "ConfigRemoveResponse";
    public const string ConfigBatchListenRequest = "ConfigBatchListenRequest";
    public const string ConfigBatchListenResponse = "ConfigBatchListenResponse";
    public const string ConfigChangeNotifyRequest = "ConfigChangeNotifyRequest";
    public const string ConfigFuzzyWatchRequest = "ConfigFuzzyWatchRequest";
    public const string ConfigFuzzyWatchResponse = "ConfigFuzzyWatchResponse";
    public const string ConfigFuzzyWatchChangeNotifyRequest = "ConfigFuzzyWatchChangeNotifyRequest";
    public const string ConnectionSetupRequest = "ConnectionSetupRequest";
    public const string ConnectionSetupResponse = "ConnectionSetupResponse";
    public const string HealthCheckRequest = "HealthCheckRequest";
}
