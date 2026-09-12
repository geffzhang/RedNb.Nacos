using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Http.Transport;
using RedNb.Nacos;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Naming.FuzzyWatch;
using RedNb.Nacos.Naming.Selector;
using RedNb.Nacos.Failover;
using RedNb.Nacos.Http.Naming;
using RedNb.Nacos.Monitor;
using RedNb.Nacos.Utils;

namespace RedNb.Nacos.Http.Naming;

/// <summary>
/// Nacos naming service implementation using HTTP.
/// </summary>
public class NacosNamingService : INamingService
{
    private readonly NacosClientOptions _options;
    private readonly NacosHttpClient _httpClient;
    private readonly ILogger<NacosNamingService>? _logger;
    private readonly ServiceInfoHolder _serviceInfoHolder;
    private readonly InstancesChangeNotifier _changeNotifier;
    private readonly BeatReactor _beatReactor;
    private readonly NamingFuzzyWatchManager _fuzzyWatchManager;
    private readonly NamingFailoverReactor? _failoverReactor;
    private readonly MetricsMonitor _metricsMonitor;
    private readonly CancellationTokenSource _cts;
    private readonly Dictionary<string, Action<IInstancesChangeEvent>> _selectorListeners = new();
    private bool _disposed;
    private readonly object _updateLock = new();
    private Task? _updateTask;
    private bool _isHealthy = true;

    // HTTP v3 path constants — see src/RedNb.Nacos.Http/Naming/NamingApiPaths.cs
    private static readonly string InstanceApiPath = NamingApiPaths.Instance;
    private static readonly string InstanceListApiPath = NamingApiPaths.InstanceList;
    private static readonly string ServiceApiPath = NamingApiPaths.ServiceList;

    /// <summary>
    /// Cache duration of a ServiceInfo built from a v3 instance-list response.
    /// </summary>
    private const long QueryCacheMillis = 3000;

    public NacosNamingService(NacosClientOptions options, ILogger<NacosNamingService>? logger = null)
        : this(options, null, logger)
    {
    }

    public NacosNamingService(
        NacosClientOptions options,
        IFailoverDataSource<ServiceInfo>? failoverDataSource,
        ILogger<NacosNamingService>? logger = null)
    {
        _options = options;
        _logger = logger;
        _httpClient = new NacosHttpClient(options, logger);
        _serviceInfoHolder = new ServiceInfoHolder(options);
        _changeNotifier = new InstancesChangeNotifier();
        _beatReactor = new BeatReactor(this, options, logger);
        _fuzzyWatchManager = new NamingFuzzyWatchManager(logger);
        _metricsMonitor = MetricsMonitor.Default;
        _cts = new CancellationTokenSource();

        // Initialize failover reactor if data source is provided
        if (failoverDataSource != null && logger != null)
        {
            _failoverReactor = new NamingFailoverReactor(
                logger,
                failoverDataSource,
                () => GetServiceInfoMap(),
                "http");
            _failoverReactor.Init();
        }

        // Update connection status
        _metricsMonitor.SetConnectionStatus(true);

        // Start service info update task
    }

    /// <summary>
    /// Gets the internal service info map for failover reactor.
    /// </summary>
    private System.Collections.Concurrent.ConcurrentDictionary<string, ServiceInfo> GetServiceInfoMap()
    {
        var map = new System.Collections.Concurrent.ConcurrentDictionary<string, ServiceInfo>();
        foreach (var info in _serviceInfoHolder.GetAllServiceInfos())
        {
            map[info.Key] = info;
        }
        return map;
    }

    #region Registration

    public Task RegisterInstanceAsync(string serviceName, string ip, int port,
        CancellationToken cancellationToken = default)
    {
        return RegisterInstanceAsync(serviceName, NacosConstants.DefaultGroup, ip, port,
            NacosConstants.DefaultClusterName, cancellationToken);
    }

    public Task RegisterInstanceAsync(string serviceName, string groupName, string ip, int port,
        CancellationToken cancellationToken = default)
    {
        return RegisterInstanceAsync(serviceName, groupName, ip, port,
            NacosConstants.DefaultClusterName, cancellationToken);
    }

    public Task RegisterInstanceAsync(string serviceName, string ip, int port, string clusterName,
        CancellationToken cancellationToken = default)
    {
        return RegisterInstanceAsync(serviceName, NacosConstants.DefaultGroup, ip, port, clusterName, cancellationToken);
    }

    public Task RegisterInstanceAsync(string serviceName, string groupName, string ip, int port,
        string clusterName, CancellationToken cancellationToken = default)
    {
        var instance = new Instance
        {
            Ip = ip,
            Port = port,
            ClusterName = clusterName,
            Weight = 1.0,
            Healthy = true,
            Enabled = true,
            Ephemeral = true
        };
        return RegisterInstanceAsync(serviceName, groupName, instance, cancellationToken);
    }

    public Task RegisterInstanceAsync(string serviceName, Instance instance,
        CancellationToken cancellationToken = default)
    {
        return RegisterInstanceAsync(serviceName, NacosConstants.DefaultGroup, instance, cancellationToken);
    }

    public async Task RegisterInstanceAsync(string serviceName, string groupName, Instance instance,
        CancellationToken cancellationToken = default)
    {
        instance.Validate();
        groupName = GetGroupOrDefault(groupName);

        var parameters = BuildRegisterParameters(serviceName, groupName, instance);

        var response = await _httpClient.PostWithHeadersAsync(InstanceApiPath, parameters, null, null,
            _options.DefaultTimeout, cancellationToken);

        // The v3 contract reports failures in the envelope, not in the HTTP status:
        // a rejected registration must not look like a successful one.
        NacosEnvelope.ThrowIfFailed(response, $"Register instance to {serviceName}@{groupName}");

        // Invalidate cached ServiceInfo so subsequent GetAllInstancesAsync(refreshes
        // from the server instead of returning a stale empty snapshot.
        _serviceInfoHolder.RemoveServiceInfo(serviceName, groupName, "");

        // Start heartbeat for ephemeral instances
        if (instance.Ephemeral)
        {
            _beatReactor.AddBeatInfo(serviceName, groupName, instance);
        }

        _logger?.LogInformation("Registered instance {Ip}:{Port} to service {Service}@{Group}",
            instance.Ip, instance.Port, serviceName, groupName);
    }

    public async Task BatchRegisterInstanceAsync(string serviceName, string groupName,
        List<Instance> instances, CancellationToken cancellationToken = default)
    {
        foreach (var instance in instances)
        {
            await RegisterInstanceAsync(serviceName, groupName, instance, cancellationToken);
        }
    }

    public async Task BatchDeregisterInstanceAsync(string serviceName, string groupName,
        List<Instance> instances, CancellationToken cancellationToken = default)
    {
        foreach (var instance in instances)
        {
            await DeregisterInstanceAsync(serviceName, groupName, instance, cancellationToken);
        }
    }

    #endregion

    #region Deregistration

    public Task DeregisterInstanceAsync(string serviceName, string ip, int port,
        CancellationToken cancellationToken = default)
    {
        return DeregisterInstanceAsync(serviceName, NacosConstants.DefaultGroup, ip, port, cancellationToken);
    }

    public Task DeregisterInstanceAsync(string serviceName, string groupName, string ip, int port,
        CancellationToken cancellationToken = default)
    {
        return DeregisterInstanceAsync(serviceName, groupName, ip, port,
            NacosConstants.DefaultClusterName, cancellationToken);
    }

    public Task DeregisterInstanceAsync(string serviceName, string ip, int port, string clusterName,
        CancellationToken cancellationToken = default)
    {
        return DeregisterInstanceAsync(serviceName, NacosConstants.DefaultGroup, ip, port, clusterName, cancellationToken);
    }

    public Task DeregisterInstanceAsync(string serviceName, string groupName, string ip, int port,
        string clusterName, CancellationToken cancellationToken = default)
    {
        var instance = new Instance
        {
            Ip = ip,
            Port = port,
            ClusterName = clusterName
        };
        return DeregisterInstanceAsync(serviceName, groupName, instance, cancellationToken);
    }

    public Task DeregisterInstanceAsync(string serviceName, Instance instance,
        CancellationToken cancellationToken = default)
    {
        return DeregisterInstanceAsync(serviceName, NacosConstants.DefaultGroup, instance, cancellationToken);
    }

    public async Task DeregisterInstanceAsync(string serviceName, string groupName, Instance instance,
        CancellationToken cancellationToken = default)
    {
        groupName = GetGroupOrDefault(groupName);

        var parameters = new Dictionary<string, string?>
        {
            { "serviceName", serviceName },
            { "groupName", groupName },
            { "namespaceId", GetNamespace() },
            { "ip", instance.Ip },
            { "port", instance.Port.ToString() },
            { "clusterName", instance.ClusterName },
            { "ephemeral", instance.Ephemeral.ToString().ToLower() }
        };

        var response = await _httpClient.DeleteWithHeadersAsync(InstanceApiPath, parameters, null,
            _options.DefaultTimeout, cancellationToken);

        // Only a deregistration the server accepted may stop the heartbeat: a
        // rejected one (v3 reports it in the envelope) leaves the instance alive.
        NacosEnvelope.ThrowIfFailed(response, $"Deregister instance from {serviceName}@{groupName}");

        // Stop heartbeat
        _beatReactor.RemoveBeatInfo(serviceName, groupName, instance);

        // Invalidate cached ServiceInfo after a successful deregistration.
        _serviceInfoHolder.RemoveServiceInfo(serviceName, groupName, "");

        _logger?.LogInformation("Deregistered instance {Ip}:{Port} from service {Service}@{Group}",
            instance.Ip, instance.Port, serviceName, groupName);
    }

    #endregion

    #region Query

    public Task<List<Instance>> GetAllInstancesAsync(string serviceName,
        CancellationToken cancellationToken = default)
    {
        return GetAllInstancesAsync(serviceName, NacosConstants.DefaultGroup, new List<string>(), true, cancellationToken);
    }

    public Task<List<Instance>> GetAllInstancesAsync(string serviceName, string groupName,
        CancellationToken cancellationToken = default)
    {
        return GetAllInstancesAsync(serviceName, groupName, new List<string>(), true, cancellationToken);
    }

    public Task<List<Instance>> GetAllInstancesAsync(string serviceName, bool subscribe,
        CancellationToken cancellationToken = default)
    {
        return GetAllInstancesAsync(serviceName, NacosConstants.DefaultGroup, new List<string>(), subscribe, cancellationToken);
    }

    public Task<List<Instance>> GetAllInstancesAsync(string serviceName, string groupName, bool subscribe,
        CancellationToken cancellationToken = default)
    {
        return GetAllInstancesAsync(serviceName, groupName, new List<string>(), subscribe, cancellationToken);
    }

    public Task<List<Instance>> GetAllInstancesAsync(string serviceName, List<string> clusters,
        CancellationToken cancellationToken = default)
    {
        return GetAllInstancesAsync(serviceName, NacosConstants.DefaultGroup, clusters, true, cancellationToken);
    }

    public Task<List<Instance>> GetAllInstancesAsync(string serviceName, string groupName, List<string> clusters,
        CancellationToken cancellationToken = default)
    {
        return GetAllInstancesAsync(serviceName, groupName, clusters, true, cancellationToken);
    }

    public Task<List<Instance>> GetAllInstancesAsync(string serviceName, List<string> clusters, bool subscribe,
        CancellationToken cancellationToken = default)
    {
        return GetAllInstancesAsync(serviceName, NacosConstants.DefaultGroup, clusters, subscribe, cancellationToken);
    }

    public async Task<List<Instance>> GetAllInstancesAsync(string serviceName, string groupName,
        List<string> clusters, bool subscribe, CancellationToken cancellationToken = default)
    {
        groupName = GetGroupOrDefault(groupName);
        var clusterString = NacosUtils.GetClusterString(clusters);

        ServiceInfo? serviceInfo;

        // Check failover first
        if (_failoverReactor != null && _failoverReactor.IsFailoverSwitch(ServiceInfo.GetKey(
            $"{groupName}{NacosConstants.ServiceInfoSplitter}{serviceName}", clusterString)))
        {
            serviceInfo = _failoverReactor.GetService(ServiceInfo.GetKey(
                $"{groupName}{NacosConstants.ServiceInfoSplitter}{serviceName}", clusterString));
            _metricsMonitor.RecordFailoverUsed();
            _logger?.LogDebug("Using failover data for service {Service}@{Group}", serviceName, groupName);
            return serviceInfo?.Hosts ?? new List<Instance>();
        }

        if (subscribe)
        {
            // Try to get from cache first
            serviceInfo = _serviceInfoHolder.GetServiceInfo(serviceName, groupName, clusterString);
            if (serviceInfo == null)
            {
                serviceInfo = await QueryServiceAsync(serviceName, groupName, clusterString, cancellationToken);
                if (serviceInfo != null)
                {
                    _serviceInfoHolder.ProcessServiceInfo(serviceInfo);
                    _metricsMonitor.SetServiceInfoMapSize(_serviceInfoHolder.GetAllServiceInfos().Count());
                }
            }
        }
        else
        {
            serviceInfo = await QueryServiceAsync(serviceName, groupName, clusterString, cancellationToken);
        }

        return serviceInfo?.Hosts ?? new List<Instance>();
    }

    public async Task<List<Instance>> SelectInstancesAsync(string serviceName, bool healthy,
        CancellationToken cancellationToken = default)
    {
        return await SelectInstancesAsync(serviceName, NacosConstants.DefaultGroup, new List<string>(), healthy, true, cancellationToken);
    }

    public async Task<List<Instance>> SelectInstancesAsync(string serviceName, string groupName, bool healthy,
        CancellationToken cancellationToken = default)
    {
        return await SelectInstancesAsync(serviceName, groupName, new List<string>(), healthy, true, cancellationToken);
    }

    public async Task<List<Instance>> SelectInstancesAsync(string serviceName, bool healthy, bool subscribe,
        CancellationToken cancellationToken = default)
    {
        return await SelectInstancesAsync(serviceName, NacosConstants.DefaultGroup, new List<string>(), healthy, subscribe, cancellationToken);
    }

    public async Task<List<Instance>> SelectInstancesAsync(string serviceName, string groupName, bool healthy,
        bool subscribe, CancellationToken cancellationToken = default)
    {
        return await SelectInstancesAsync(serviceName, groupName, new List<string>(), healthy, subscribe, cancellationToken);
    }

    public async Task<List<Instance>> SelectInstancesAsync(string serviceName, List<string> clusters, bool healthy,
        CancellationToken cancellationToken = default)
    {
        return await SelectInstancesAsync(serviceName, NacosConstants.DefaultGroup, clusters, healthy, true, cancellationToken);
    }

    public async Task<List<Instance>> SelectInstancesAsync(string serviceName, string groupName,
        List<string> clusters, bool healthy, CancellationToken cancellationToken = default)
    {
        return await SelectInstancesAsync(serviceName, groupName, clusters, healthy, true, cancellationToken);
    }

    public async Task<List<Instance>> SelectInstancesAsync(string serviceName, List<string> clusters,
        bool healthy, bool subscribe, CancellationToken cancellationToken = default)
    {
        return await SelectInstancesAsync(serviceName, NacosConstants.DefaultGroup, clusters, healthy, subscribe, cancellationToken);
    }

    public async Task<List<Instance>> SelectInstancesAsync(string serviceName, string groupName,
        List<string> clusters, bool healthy, bool subscribe, CancellationToken cancellationToken = default)
    {
        var instances = await GetAllInstancesAsync(serviceName, groupName, clusters, subscribe, cancellationToken);
        return instances.Where(i => i.Healthy == healthy && i.Enabled && i.Weight > 0).ToList();
    }

    public async Task<Instance?> SelectOneHealthyInstanceAsync(string serviceName,
        CancellationToken cancellationToken = default)
    {
        return await SelectOneHealthyInstanceAsync(serviceName, NacosConstants.DefaultGroup, new List<string>(), true, cancellationToken);
    }

    public async Task<Instance?> SelectOneHealthyInstanceAsync(string serviceName, string groupName,
        CancellationToken cancellationToken = default)
    {
        return await SelectOneHealthyInstanceAsync(serviceName, groupName, new List<string>(), true, cancellationToken);
    }

    public async Task<Instance?> SelectOneHealthyInstanceAsync(string serviceName, bool subscribe,
        CancellationToken cancellationToken = default)
    {
        return await SelectOneHealthyInstanceAsync(serviceName, NacosConstants.DefaultGroup, new List<string>(), subscribe, cancellationToken);
    }

    public async Task<Instance?> SelectOneHealthyInstanceAsync(string serviceName, string groupName,
        bool subscribe, CancellationToken cancellationToken = default)
    {
        return await SelectOneHealthyInstanceAsync(serviceName, groupName, new List<string>(), subscribe, cancellationToken);
    }

    public async Task<Instance?> SelectOneHealthyInstanceAsync(string serviceName, List<string> clusters,
        CancellationToken cancellationToken = default)
    {
        return await SelectOneHealthyInstanceAsync(serviceName, NacosConstants.DefaultGroup, clusters, true, cancellationToken);
    }

    public async Task<Instance?> SelectOneHealthyInstanceAsync(string serviceName, string groupName,
        List<string> clusters, CancellationToken cancellationToken = default)
    {
        return await SelectOneHealthyInstanceAsync(serviceName, groupName, clusters, true, cancellationToken);
    }

    public async Task<Instance?> SelectOneHealthyInstanceAsync(string serviceName, List<string> clusters,
        bool subscribe, CancellationToken cancellationToken = default)
    {
        return await SelectOneHealthyInstanceAsync(serviceName, NacosConstants.DefaultGroup, clusters, subscribe, cancellationToken);
    }

    public async Task<Instance?> SelectOneHealthyInstanceAsync(string serviceName, string groupName,
        List<string> clusters, bool subscribe, CancellationToken cancellationToken = default)
    {
        var instances = await SelectInstancesAsync(serviceName, groupName, clusters, true, subscribe, cancellationToken);
        return SelectByRandomWeight(instances);
    }

    #endregion

    #region Subscription

    public Task SubscribeAsync(string serviceName, Action<IInstancesChangeEvent> listener,
        CancellationToken cancellationToken = default)
    {
        return SubscribeAsync(serviceName, NacosConstants.DefaultGroup, new List<string>(), listener, cancellationToken);
    }

    public Task SubscribeAsync(string serviceName, string groupName, Action<IInstancesChangeEvent> listener,
        CancellationToken cancellationToken = default)
    {
        return SubscribeAsync(serviceName, groupName, new List<string>(), listener, cancellationToken);
    }

    public Task SubscribeAsync(string serviceName, List<string> clusters, Action<IInstancesChangeEvent> listener,
        CancellationToken cancellationToken = default)
    {
        return SubscribeAsync(serviceName, NacosConstants.DefaultGroup, clusters, listener, cancellationToken);
    }

    public async Task SubscribeAsync(string serviceName, string groupName, List<string> clusters,
        Action<IInstancesChangeEvent> listener, CancellationToken cancellationToken = default)
    {
        lock (_updateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _updateTask ??= Task.Run(() => StartServiceInfoUpdateTaskAsync(_cts.Token));
        }
        groupName = GetGroupOrDefault(groupName);
        var clusterString = NacosUtils.GetClusterString(clusters);

        _changeNotifier.RegisterListener(serviceName, groupName, clusterString, listener);

        // Ensure service is being polled
        var serviceInfo = await QueryServiceAsync(serviceName, groupName, clusterString, cancellationToken);
        if (serviceInfo != null)
        {
            _serviceInfoHolder.ProcessServiceInfo(serviceInfo);
        }

        _logger?.LogDebug("Subscribed to service {Service}@{Group}", serviceName, groupName);
    }

    public Task SubscribeAsync(string serviceName, INamingSelector selector,
        Action<IInstancesChangeEvent> listener, CancellationToken cancellationToken = default)
    {
        return SubscribeAsync(serviceName, NacosConstants.DefaultGroup, selector, listener, cancellationToken);
    }

    public async Task SubscribeAsync(string serviceName, string groupName, INamingSelector selector,
        Action<IInstancesChangeEvent> listener, CancellationToken cancellationToken = default)
    {
        groupName = GetGroupOrDefault(groupName);
        var selectorKey = selector?.Expression ?? "";

        // Create a filtered listener that applies the selector
        Action<IInstancesChangeEvent> filteredListener = (evt) =>
        {
            if (selector != null)
            {
                var context = new NamingContext
                {
                    ServiceName = serviceName,
                    GroupName = groupName,
                    Instances = evt.Instances,
                    HealthyOnly = false
                };
                var result = selector.Select(context);
                evt = new InstancesChangeEvent
                {
                    ServiceName = evt.ServiceName,
                    GroupName = evt.GroupName,
                    Clusters = evt.Clusters,
                    Instances = result.Instances,
                    AddedInstances = evt.AddedInstances?.Where(i => result.Instances.Contains(i)).ToList(),
                    RemovedInstances = evt.RemovedInstances,
                    ModifiedInstances = evt.ModifiedInstances?.Where(i => result.Instances.Contains(i)).ToList()
                };
            }
            listener(evt);
        };

        _changeNotifier.RegisterListener(serviceName, groupName, selectorKey, filteredListener);
        _selectorListeners[$"{serviceName}@@{groupName}@@{listener.GetHashCode()}"] = filteredListener;

        // Ensure service is being polled
        var serviceInfo = await QueryServiceAsync(serviceName, groupName, "", cancellationToken);
        if (serviceInfo != null)
        {
            _serviceInfoHolder.ProcessServiceInfo(serviceInfo);
        }

        _logger?.LogDebug("Subscribed to service {Service}@{Group} with selector {Selector}",
            serviceName, groupName, selector?.Expression);
    }

    public Task UnsubscribeAsync(string serviceName, Action<IInstancesChangeEvent> listener,
        CancellationToken cancellationToken = default)
    {
        return UnsubscribeAsync(serviceName, NacosConstants.DefaultGroup, new List<string>(), listener, cancellationToken);
    }

    public Task UnsubscribeAsync(string serviceName, string groupName, Action<IInstancesChangeEvent> listener,
        CancellationToken cancellationToken = default)
    {
        return UnsubscribeAsync(serviceName, groupName, new List<string>(), listener, cancellationToken);
    }

    public Task UnsubscribeAsync(string serviceName, List<string> clusters, Action<IInstancesChangeEvent> listener,
        CancellationToken cancellationToken = default)
    {
        return UnsubscribeAsync(serviceName, NacosConstants.DefaultGroup, clusters, listener, cancellationToken);
    }

    public Task UnsubscribeAsync(string serviceName, string groupName, List<string> clusters,
        Action<IInstancesChangeEvent> listener, CancellationToken cancellationToken = default)
    {
        groupName = GetGroupOrDefault(groupName);
        var clusterString = NacosUtils.GetClusterString(clusters);

        _changeNotifier.DeregisterListener(serviceName, groupName, clusterString, listener);

        _logger?.LogDebug("Unsubscribed from service {Service}@{Group}", serviceName, groupName);

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string serviceName, INamingSelector selector,
        Action<IInstancesChangeEvent> listener, CancellationToken cancellationToken = default)
    {
        return UnsubscribeAsync(serviceName, NacosConstants.DefaultGroup, selector, listener, cancellationToken);
    }

    public Task UnsubscribeAsync(string serviceName, string groupName, INamingSelector selector,
        Action<IInstancesChangeEvent> listener, CancellationToken cancellationToken = default)
    {
        groupName = GetGroupOrDefault(groupName);
        var selectorKey = selector?.Expression ?? "";
        var listenerKey = $"{serviceName}@@{groupName}@@{listener.GetHashCode()}";

        if (_selectorListeners.TryGetValue(listenerKey, out var filteredListener))
        {
            _changeNotifier.DeregisterListener(serviceName, groupName, selectorKey, filteredListener);
            _selectorListeners.Remove(listenerKey);
        }

        _logger?.LogDebug("Unsubscribed from service {Service}@{Group} with selector", serviceName, groupName);

        return Task.CompletedTask;
    }

    #endregion

    #region Service List

    public Task<ListView<string>> GetServicesOfServerAsync(int pageNo, int pageSize,
        CancellationToken cancellationToken = default)
    {
        return GetServicesOfServerAsync(pageNo, pageSize, NacosConstants.DefaultGroup, cancellationToken);
    }

    public async Task<ListView<string>> GetServicesOfServerAsync(int pageNo, int pageSize, string groupName,
        CancellationToken cancellationToken = default)
    {
        groupName = GetGroupOrDefault(groupName);

        var parameters = new Dictionary<string, string?>
        {
            { "pageNo", pageNo.ToString() },
            { "pageSize", pageSize.ToString() },
            { "namespaceId", GetNamespace() },
            { "groupName", groupName }
        };

        var response = await _httpClient.GetWithHeadersAsync(ServiceApiPath, parameters, null,
            _options.DefaultTimeout, cancellationToken);

        return ParseServiceList(response);
    }

    public Task<ListView<string>> GetServicesOfServerAsync(int pageNo, int pageSize, INamingSelector selector,
        CancellationToken cancellationToken = default)
    {
        return GetServicesOfServerAsync(pageNo, pageSize, NacosConstants.DefaultGroup, selector, cancellationToken);
    }

    public async Task<ListView<string>> GetServicesOfServerAsync(int pageNo, int pageSize, string groupName,
        INamingSelector selector, CancellationToken cancellationToken = default)
    {
        groupName = GetGroupOrDefault(groupName);

        var parameters = new Dictionary<string, string?>
        {
            { "pageNo", pageNo.ToString() },
            { "pageSize", pageSize.ToString() },
            { "namespaceId", GetNamespace() },
            { "groupName", groupName }
        };

        // Add selector parameters if provided
        if (selector != null)
        {
            parameters["selector"] = JsonSerializer.Serialize(new
            {
                type = selector.Type,
                expression = selector.Expression
            });
        }

        var response = await _httpClient.GetWithHeadersAsync(ServiceApiPath, parameters, null,
            _options.DefaultTimeout, cancellationToken);

        return ParseServiceList(response);
    }

    public Task<List<ServiceInfo>> GetSubscribeServicesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_changeNotifier.GetSubscribedServices()
            .Select(key =>
            {
                var (serviceName, groupName, clusters) = ParseServiceKey(key);
                return _serviceInfoHolder.GetServiceInfo(serviceName, groupName, clusters);
            })
            .Where(s => s != null)
            .Cast<ServiceInfo>()
            .ToList());
    }

    #endregion

    #region Server Status

    public string GetServerStatus()
    {
        return _isHealthy ? "UP" : "DOWN";
    }

    public async Task ShutdownAsync()
    {
        await DisposeAsync();
    }

    #endregion

    #region Fuzzy Watch (Nacos 3.0)

    public Task FuzzyWatchAsync(string serviceNamePattern, INamingFuzzyWatchEventWatcher watcher,
        CancellationToken cancellationToken = default)
    {
        return FuzzyWatchAsync(serviceNamePattern, "*", watcher, cancellationToken);
    }

    public Task FuzzyWatchAsync(string serviceNamePattern, string groupNamePattern,
        INamingFuzzyWatchEventWatcher watcher, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Fuzzy Watch requires the gRPC service in Nacos 3.2.4.");
    }

    public Task<ISet<string>> FuzzyWatchWithGroupKeysAsync(string serviceNamePattern,
        INamingFuzzyWatchEventWatcher watcher, CancellationToken cancellationToken = default)
    {
        return FuzzyWatchWithGroupKeysAsync(serviceNamePattern, "*", watcher, cancellationToken);
    }

    public async Task<ISet<string>> FuzzyWatchWithGroupKeysAsync(string serviceNamePattern,
        string groupNamePattern, INamingFuzzyWatchEventWatcher watcher, CancellationToken cancellationToken = default)
    {
        await FuzzyWatchAsync(serviceNamePattern, groupNamePattern, watcher, cancellationToken);

        // Return current matching keys
        var matchingKeys = _fuzzyWatchManager.GetMatchingKeys(serviceNamePattern, groupNamePattern, GetNamespace() ?? "");
        return matchingKeys;
    }

    public Task CancelFuzzyWatchAsync(string serviceNamePattern, INamingFuzzyWatchEventWatcher watcher,
        CancellationToken cancellationToken = default)
    {
        return CancelFuzzyWatchAsync(serviceNamePattern, "*", watcher, cancellationToken);
    }

    public Task CancelFuzzyWatchAsync(string serviceNamePattern, string groupNamePattern,
        INamingFuzzyWatchEventWatcher watcher, CancellationToken cancellationToken = default)
    {
        _fuzzyWatchManager.RemoveWatcher(serviceNamePattern, groupNamePattern, GetNamespace() ?? "", watcher);
        _logger?.LogDebug("Cancelled fuzzy watch for service={ServicePattern}, group={GroupPattern}",
            serviceNamePattern, groupNamePattern);
        return Task.CompletedTask;
    }

    #endregion

    #region Internal Methods

    internal async Task<bool> SendBeatAsync(string serviceName, string groupName, Instance instance,
        CancellationToken cancellationToken)
    {
        try
        {
            // v3 heartbeats reuse the instance endpoint: the instance fields plus beat=true.
            var parameters = BuildRegisterParameters(serviceName, groupName, instance);
            parameters["beat"] = "true";

            var body = NacosUtils.BuildQueryString(parameters);

            var response = await _httpClient.PostWithHeadersAsync(InstanceApiPath, null, body, null,
                _options.DefaultTimeout, cancellationToken);

            // A heartbeat the server rejected (wrong instance, denied, ...) is
            // reported in the v3 envelope and must not count as a success.
            if (NacosEnvelope.TryParse(response, out var root) &&
                NacosEnvelope.GetCode(root) != NacosConstants.SuccessCode)
            {
                _logger?.LogWarning("Heartbeat rejected for {Service}@{Group}: {Message}", serviceName,
                    groupName, NacosEnvelope.GetMessage(root) ?? "unknown error");
                return false;
            }

            _isHealthy = true;
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send heartbeat for {Service}@{Group}", serviceName, groupName);
            return false;
        }
    }

    private async Task<ServiceInfo?> QueryServiceAsync(string serviceName, string groupName,
        string clusters, CancellationToken cancellationToken)
    {
        string? response;

        // Only the transport call sits inside the catch: a failure here degrades to
        // "no fresh data" (cache/failover still apply).
        try
        {
            var parameters = new Dictionary<string, string?>
            {
                { "serviceName", serviceName },
                { "groupName", groupName },
                { "namespaceId", GetNamespace() },
                // NOTE: the v3 client API names this parameter `clusterName` (singular,
                // comma-separated values). Sending `clusters` is silently ignored by
                // Nacos 3.2.4 and returns every cluster — verified against the live server.
                { "clusterName", clusters },
                { "healthyOnly", "false" }
            };

            response = await _httpClient.GetWithHeadersAsync(InstanceListApiPath, parameters, null,
                _options.DefaultTimeout, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to query service {Service}@{Group}", serviceName, groupName);
            _isHealthy = false;
            _metricsMonitor.SetConnectionStatus(false);
            _metricsMonitor.RecordNamingRequestFailed();

            // Update failover status
            if (_failoverReactor != null)
            {
                _metricsMonitor.SetFailoverEnabled(_failoverReactor.IsFailoverSwitch());
            }

            return null;
        }

        if (string.IsNullOrEmpty(response))
        {
            _metricsMonitor.RecordNamingRequestFailed();
            return null;
        }

        _isHealthy = true;
        _metricsMonitor.SetConnectionStatus(true);
        _metricsMonitor.RecordNamingRequestSuccess();

        // Parsing sits OUTSIDE the catch window: a non-zero envelope code (e.g. access
        // denied) is a server-side refusal, not a transport failure, and must propagate
        // to the caller instead of being reported as "the service has no instances".
        // The v3 client API returns a flat JSON array of instances in `data`;
        // rebuild the ServiceInfo the rest of the client works with.
        var hosts = ParseInstanceList(response);
        return new ServiceInfo
        {
            Name = serviceName,
            GroupName = groupName,
            Clusters = clusters,
            CacheMillis = QueryCacheMillis,
            LastRefTime = NacosUtils.GetCurrentTimeMillis(),
            Hosts = hosts
        };
    }

    private async Task StartServiceInfoUpdateTaskAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting service info update task");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(10000, cancellationToken); // Update every 10 seconds

                var subscribedServices = _changeNotifier.GetSubscribedServices();
                foreach (var serviceKey in subscribedServices)
                {
                    try
                    {
                        var (serviceName, groupName, clusters) = ParseServiceKey(serviceKey);
                        var oldInfo = _serviceInfoHolder.GetSnapshot(serviceName, groupName, clusters);
                        var newInfo = await QueryServiceAsync(serviceName, groupName, clusters, cancellationToken);

                        if (newInfo != null)
                        {
                            var hasChanged = _serviceInfoHolder.ProcessServiceInfo(newInfo);
                            if (hasChanged)
                            {
                                NotifyListeners(serviceName, groupName, clusters, oldInfo, newInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to update service info for {Key}", serviceKey);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in service info update task");
            }
        }

        _logger?.LogInformation("Service info update task stopped");
    }

    private void NotifyListeners(string serviceName, string groupName, string clusters,
        ServiceInfo? oldInfo, ServiceInfo newInfo)
    {
        var changeEvent = new InstancesChangeEvent
        {
            ServiceName = serviceName,
            GroupName = groupName,
            Clusters = clusters,
            Instances = newInfo.Hosts
        };

        // Calculate diff
        if (oldInfo != null)
        {
            var oldIps = oldInfo.Hosts.Select(h => h.ToInetAddr()).ToHashSet();
            var newIps = newInfo.Hosts.Select(h => h.ToInetAddr()).ToHashSet();

            changeEvent.AddedInstances = newInfo.Hosts.Where(h => !oldIps.Contains(h.ToInetAddr())).ToList();
            changeEvent.RemovedInstances = oldInfo.Hosts.Where(h => !newIps.Contains(h.ToInetAddr())).ToList();
        }
        else
        {
            changeEvent.AddedInstances = newInfo.Hosts;
        }

        // Record service change push metric
        _metricsMonitor.RecordServiceChangePush();

        _changeNotifier.NotifyListeners(serviceName, groupName, clusters, changeEvent);
    }

    private static Instance? SelectByRandomWeight(List<Instance> instances)
    {
        if (instances.Count == 0) return null;
        if (instances.Count == 1) return instances[0];

        var totalWeight = instances.Sum(i => i.Weight);
        if (totalWeight <= 0)
        {
            return instances[Random.Shared.Next(instances.Count)];
        }

        var randomWeight = Random.Shared.NextDouble() * totalWeight;
        var currentWeight = 0.0;

        foreach (var instance in instances)
        {
            currentWeight += instance.Weight;
            if (currentWeight >= randomWeight)
            {
                return instance;
            }
        }

        return instances[^1];
    }

    private Dictionary<string, string?> BuildRegisterParameters(string serviceName, string groupName, Instance instance)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "serviceName", serviceName },
            { "groupName", groupName },
            { "namespaceId", GetNamespace() },
            { "ip", instance.Ip },
            { "port", instance.Port.ToString() },
            { "weight", instance.Weight.ToString() },
            { "enabled", instance.Enabled.ToString().ToLower() },
            { "healthy", instance.Healthy.ToString().ToLower() },
            { "ephemeral", instance.Ephemeral.ToString().ToLower() },
            { "clusterName", instance.ClusterName }
        };

        if (instance.Metadata.Count > 0)
        {
            parameters["metadata"] = JsonSerializer.Serialize(instance.Metadata);
        }

        return parameters;
    }

    private string GetGroupOrDefault(string? group)
    {
        return string.IsNullOrWhiteSpace(group) ? NacosConstants.DefaultGroup : group.Trim();
    }

    private string? GetNamespace()
    {
        return string.IsNullOrWhiteSpace(_options.Namespace) ? null : _options.Namespace;
    }

    private static string GetServiceKey(string serviceName, string groupName, string clusters)
    {
        return $"{groupName}@@{serviceName}@@{clusters}";
    }

    private static (string ServiceName, string GroupName, string Clusters) ParseServiceKey(string key)
    {
        var parts = key.Split("@@");
        return (parts.Length > 1 ? parts[1] : parts[0],
                parts.Length > 0 ? parts[0] : NacosConstants.DefaultGroup,
                parts.Length > 2 ? parts[2] : string.Empty);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _cts.CancelAsync();
        if (_updateTask != null) await _updateTask;
        await _beatReactor.DisposeAsync();
        _cts.Dispose();
        _failoverReactor?.Dispose();
        _httpClient.Dispose();
        _metricsMonitor.SetConnectionStatus(false);
        _disposed = true;
    }

    /// <summary>
    /// Parses the flat instance array returned in the <c>data</c> field of a v3
    /// instance-list response. A malformed or absent payload yields an empty list;
    /// a non-zero envelope code (e.g. access denied) throws, so a refused query is
    /// never reported as "the service has no instances".
    /// </summary>
    private static List<Instance> ParseInstanceList(string response)
    {
        try
        {
            if (!NacosEnvelope.TryParse(response, out var root))
            {
                return new List<Instance>();
            }

            NacosEnvelope.ThrowIfFailed(root, "List instances");

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return new List<Instance>();
            }

            return data.Deserialize<List<Instance>>() ?? new List<Instance>();
        }
        catch (JsonException)
        {
            return new List<Instance>();
        }
    }

    /// <summary>
    /// Parses a v3 admin service-list envelope into the paged view model. A non-zero
    /// envelope code throws; individual malformed items are skipped.
    /// </summary>
    private static ListView<string> ParseServiceList(string? response)
    {
        if (!NacosEnvelope.TryParse(response, out var root))
        {
            return new ListView<string>();
        }

        NacosEnvelope.ThrowIfFailed(root, "List services");

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return new ListView<string>();
        }

        // Guard every accessor: TryGetProperty succeeds for a JSON null, and the
        // throwing accessors would escape as InvalidOperationException.
        var count = data.TryGetProperty("totalCount", out var totalCount) &&
                    totalCount.ValueKind == JsonValueKind.Number &&
                    totalCount.TryGetInt32(out var total)
            ? total
            : 0;

        var names = new List<string>();

        if (data.TryGetProperty("pageItems", out var pageItems) && pageItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pageItems.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String &&
                    name.GetString() is { } serviceName)
                {
                    names.Add(serviceName);
                }
            }
        }

        return new ListView<string>(count, names);
    }
}
