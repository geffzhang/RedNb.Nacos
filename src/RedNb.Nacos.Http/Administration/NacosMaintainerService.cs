using Microsoft.Extensions.Logging;
using RedNb.Nacos.Administration;
using RedNb.Nacos.Naming;
using RedNb.Nacos;
namespace RedNb.Nacos.Http.Administration;
/// <summary>Migration-only contract; obsolete requests are never sent to the server.</summary>
public class NacosMaintainerService : IMaintainerService
{
    public NacosMaintainerService(NacosClientOptions options, ILogger<NacosMaintainerService>? logger = null)
    { ArgumentNullException.ThrowIfNull(options); }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    /// <inheritdoc />
    public Task<BetaConfigInfo?> GetBetaConfigAsync(string dataId, string group, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<BetaConfigInfo?> GetBetaConfigAsync(string dataId, string group, string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> PublishBetaConfigAsync(string dataId, string group, string content, string betaIps, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> PublishBetaConfigAsync(string dataId, string group, string namespaceId, string content, string betaIps, string? description = null, string? type = null, string? appName = null, string? srcUser = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> StopBetaConfigAsync(string dataId, string group, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> StopBetaConfigAsync(string dataId, string group, string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<GrayConfigInfo?> GetGrayConfigAsync(string dataId, string group, string namespaceId, string grayName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> PublishGrayConfigAsync(string dataId, string group, string namespaceId, string content, GrayConfigRule grayRule, string? description = null, string? type = null, string? srcUser = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> DeleteGrayConfigAsync(string dataId, string group, string namespaceId, string grayName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<IEnumerable<ClientConnectionInfo>> ListClientsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConnectionListResult> ListNamingClientsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConnectionListResult> ListConfigClientsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ClientDetailInfo?> GetClientDetailAsync(string connectionId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<IEnumerable<ClientSubscribedService>> GetClientSubscribersAsync(string connectionId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<IEnumerable<ClientPublishedService>> GetClientPublishedServicesAsync(string connectionId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<IEnumerable<ConfigListenerInfo>> GetClientListenConfigsAsync(string connectionId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> ReloadConnectionCountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<IDictionary<string, int>> GetSdkVersionStatisticsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<IDictionary<string, object>> GetCurrentNodeStatsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> ResetConnectionLimitAsync(string namespaceId, int limitCount, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<ConfigHistoryBasicInfo>> ListConfigHistoryAsync(string dataId, string group, string namespaceId, int pageNo = 1, int pageSize = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigHistoryDetailInfo?> GetConfigHistoryInfoAsync(string dataId, string group, string namespaceId, long nid, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigHistoryDetailInfo?> GetPreviousConfigHistoryInfoAsync(string dataId, string group, string namespaceId, long id, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigDetailInfo?> GetConfigAsync(string dataId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigDetailInfo?> GetConfigAsync(string dataId, string group, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigDetailInfo?> GetConfigAsync(string dataId, string group, string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> PublishConfigAsync(string dataId, string content, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> PublishConfigAsync(string dataId, string group, string content, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> PublishConfigAsync(string dataId, string group, string namespaceId, string content, string? description = null, string? type = null, string? appName = null, string? srcUser = null, string? configTags = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> UpdateConfigMetadataAsync(string dataId, string group, string namespaceId, string? description, string? configTags, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> DeleteConfigAsync(string dataId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> DeleteConfigAsync(string dataId, string group, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> DeleteConfigAsync(string dataId, string group, string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> DeleteConfigsAsync(List<long> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<ConfigBasicInfo>> ListConfigsAsync(string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<ConfigBasicInfo>> ListConfigsAsync(string? dataId, string? group, string namespaceId, string? type = null, string? configTags = null, string? appName = null, int pageNo = 1, int pageSize = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<ConfigBasicInfo>> SearchConfigsAsync(string? dataIdPattern, string? groupPattern, string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<ConfigBasicInfo>> SearchConfigsAsync(string? dataIdPattern, string? groupPattern, string namespaceId, string? configDetail, string? type, string? configTags, string? appName, int pageNo = 1, int pageSize = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<List<ConfigBasicInfo>> GetConfigListByNamespaceAsync(string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigListenerInfo> GetListenersAsync(string dataId, string group, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigListenerInfo> GetListenersAsync(string dataId, string group, string namespaceId, bool aggregation = true, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigListenerInfo> GetAllSubClientConfigByIpAsync(string ip, bool all = false, string? namespaceId = null, bool aggregation = false, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<CloneResult> CloneConfigAsync(string namespaceId, List<ConfigCloneInfo> cloneInfos, SameConfigPolicy policy, string? srcUser = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ConfigImportResult> ImportConfigAsync(string namespaceId, SameConfigPolicy policy, byte[] fileContent, string fileName, string? srcUser = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<byte[]> ExportConfigAsync(ConfigExportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<byte[]> ExportConfigByIdsAsync(string namespaceId, IEnumerable<long> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<byte[]> ExportAllConfigAsync(string namespaceId, string? dataId = null, string? group = null, string? appName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<CloneResult> CloneConfigAsync(string sourceNamespaceId, string targetNamespaceId, IEnumerable<long> ids, SameConfigPolicy policy = SameConfigPolicy.Abort, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<CloneResult> CloneAllConfigAsync(string sourceNamespaceId, string targetNamespaceId, SameConfigPolicy policy = SameConfigPolicy.Abort, string? dataId = null, string? group = null, string? appName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<IEnumerable<NamespaceInfo>> GetNamespacesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<NamespaceInfo?> GetNamespaceAsync(string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> CreateNamespaceAsync(NamespaceRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> UpdateNamespaceAsync(NamespaceRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> DeleteNamespaceAsync(string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<IEnumerable<ClusterMemberInfo>> GetClusterMembersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> GetSelfNodeAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ClusterMemberInfo?> GetClusterLeaderAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> UpdateClusterMemberLookupAsync(string address, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> LeaveClusterAsync(IEnumerable<string> addresses, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ServerStateInfo> GetServerStateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ServerSwitchInfo> GetServerSwitchesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> UpdateServerSwitchAsync(string entry, string value, bool debug = false, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> GetReadinessAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> GetLivenessAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<MetricsInfo> GetMetricsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> GetRaftLeaderAsync(string groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> TransferRaftLeaderAsync(string groupId, string targetAddress, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<bool> ResetRaftClusterAsync(string groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> RegisterInstanceAsync(string serviceName, string ip, int port, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> RegisterInstanceAsync(string groupName, string serviceName, Instance instance, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> DeregisterInstanceAsync(string serviceName, string ip, int port, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> DeregisterInstanceAsync(string groupName, string serviceName, Instance instance, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> UpdateInstanceAsync(string serviceName, Instance instance, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> UpdateInstanceAsync(string groupName, string serviceName, Instance instance, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> PartialUpdateInstanceAsync(string groupName, string serviceName, Instance instance, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<InstanceMetadataBatchResult> BatchUpdateInstanceMetadataAsync(string groupName, string serviceName, List<Instance> instances, Dictionary<string, string> newMetadata, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<InstanceMetadataBatchResult> BatchDeleteInstanceMetadataAsync(string groupName, string serviceName, List<Instance> instances, List<string> metadataKeys, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<List<Instance>> ListInstancesAsync(string serviceName, string? clusterName = null, bool healthyOnly = false, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<List<Instance>> ListInstancesAsync(string groupName, string serviceName, string? clusterName = null, bool healthyOnly = false, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Instance?> GetInstanceDetailAsync(string serviceName, string ip, int port, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Instance?> GetInstanceDetailAsync(string groupName, string serviceName, string ip, int port, string? clusterName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public string GetServerStatus() => "UNSUPPORTED";
    /// <inheritdoc />
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    /// <inheritdoc />
    public Task<MetricsInfo> GetMetricsAsync(bool onlyStatus = false, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> SetLogLevelAsync(string logName, string logLevel, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> UpdateInstanceHealthStatusAsync(string groupName, string serviceName, string ip, int port, bool healthy, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Dictionary<string, HealthCheckerInfo>> GetHealthCheckersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> UpdateClusterAsync(string groupName, string serviceName, ClusterInfo cluster, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> CreateServiceAsync(string serviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> CreateServiceAsync(string groupName, string serviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> CreateServiceAsync(string namespaceId, string groupName, string serviceName, bool ephemeral = true, float protectThreshold = 0f, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> CreateServiceAsync(ServiceDefinition service, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> UpdateServiceAsync(string serviceName, Dictionary<string, string>? newMetadata = null, float? newProtectThreshold = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> UpdateServiceAsync(ServiceDefinition service, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> RemoveServiceAsync(string serviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<string> RemoveServiceAsync(string groupName, string serviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ServiceDetailInfo?> GetServiceDetailAsync(string serviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<ServiceDetailInfo?> GetServiceDetailAsync(string groupName, string serviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<ServiceView>> ListServicesAsync(string namespaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<ServiceView>> ListServicesAsync(string namespaceId, string? groupNameParam = null, string? serviceNameParam = null, bool ignoreEmptyService = false, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<ServiceDetailInfo>> ListServicesWithDetailAsync(string namespaceId, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<SubscriberInfo>> GetSubscribersAsync(string serviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<Page<SubscriberInfo>> GetSubscribersAsync(string groupName, string serviceName, int pageNo = 1, int pageSize = 10, bool aggregation = false, CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
    /// <inheritdoc />
    public Task<List<string>> ListSelectorTypesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Legacy maintenance API is not supported. Use IAdministrationService, IConfigService or INamingService with Nacos 3.2.4.");
}
