using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Http.Transport;
using RedNb.Nacos;
using RedNb.Nacos.Config;
using RedNb.Nacos.Config.Filter;
using RedNb.Nacos.Config.FuzzyWatch;
using RedNb.Nacos.Monitor;
using RedNb.Nacos.Utils;

namespace RedNb.Nacos.Http.Config;

/// <summary>
/// Nacos config service implementation using HTTP.
/// </summary>
public class NacosConfigService : IConfigService
{
    private readonly NacosClientOptions _options;
    private readonly NacosHttpClient _httpClient;
    private readonly ILogger<NacosConfigService>? _logger;
    private readonly ConfigListenerManager _listenerManager;
    private readonly LocalConfigCache _localCache;
    private readonly ConfigFilterChainManager _filterChainManager;
    private readonly FuzzyWatchManager _fuzzyWatchManager;
    private readonly MetricsMonitor _metricsMonitor;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;
    private bool _isHealthy = true;

    // Nacos v3 splits the config API by caller: reads use the client API (no auth
    // required), writes use the admin API (token auto-attached by NacosHttpClient).
    // See https://nacos.io/en/docs/v3/open-api
    private const string ConfigClientApiPath = "v3/client/cs/config";
    private const string ConfigAdminApiPath = "v3/admin/cs/config";

    public NacosConfigService(NacosClientOptions options, ILogger<NacosConfigService>? logger = null)
    {
        _options = options;
        _logger = logger;
        _httpClient = new NacosHttpClient(options, logger);
        _listenerManager = new ConfigListenerManager();
        _localCache = new LocalConfigCache(options);
        _filterChainManager = new ConfigFilterChainManager();
        _fuzzyWatchManager = new FuzzyWatchManager(logger);
        _metricsMonitor = MetricsMonitor.Default;
        _cts = new CancellationTokenSource();

        // Update connection status
        _metricsMonitor.SetConnectionStatus(true);
    }

    public async Task<string?> GetConfigAsync(string dataId, string group, long timeoutMs,
        CancellationToken cancellationToken = default)
    {
        group = GetGroupOrDefault(group);
        ValidateParams(dataId, group);

        try
        {
            var parameters = new Dictionary<string, string?>
            {
                { "dataId", dataId },
                { "groupName", group },
                { "namespaceId", GetTenant() }
            };

            var response = await _httpClient.GetAsync(ConfigClientApiPath, parameters, timeoutMs, cancellationToken);
            var content = ExtractConfigContent(response);

            if (content != null)
            {
                _localCache.SaveSnapshot(dataId, group, content);
                _isHealthy = true;
                _metricsMonitor.SetConnectionStatus(true);
                _metricsMonitor.RecordConfigRequestSuccess();

                // Apply filter chain for decryption
                content = await ApplyGetFilterAsync(dataId, group, content, cancellationToken);
            }

            return content;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            _metricsMonitor.RecordConfigRequestSuccess(); // 404 is still a successful response
            return null;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NoRight)
        {
            // Access denied is not a transient failure, so it must not be masked by a
            // stale snapshot: serving the cached value after a refusal would report a
            // permission problem as a successful read. This mirrors the Java client,
            // which rethrows NO_RIGHT from getConfigInner for the same reason.
            _isHealthy = false;
            _metricsMonitor.RecordConfigRequestFailed();
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get config from server, trying local cache");
            _isHealthy = false;
            _metricsMonitor.SetConnectionStatus(false);
            _metricsMonitor.RecordConfigRequestFailed();

            // Try to get from local cache
            var cached = _localCache.GetSnapshot(dataId, group);
            if (cached != null)
            {
                _logger?.LogInformation("Using cached config for {DataId}@{Group}", dataId, group);
                // Apply filter chain for decryption
                cached = await ApplyGetFilterAsync(dataId, group, cached, cancellationToken);
                return cached;
            }

            throw;
        }
    }

    public async Task<string?> GetConfigAndSignListenerAsync(string dataId, string group, long timeoutMs,
        IConfigChangeListener listener, CancellationToken cancellationToken = default)
    {
        var content = await GetConfigAsync(dataId, group, timeoutMs, cancellationToken);
        await AddListenerAsync(dataId, group, listener, cancellationToken);
        return content;
    }

    public async Task AddListenerAsync(string dataId, string group, IConfigChangeListener listener,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("HTTP config listening is unavailable in Nacos 3.2.4. Use AddNacosConfig or NacosGrpcConfigService.");
    }

    public void RemoveListener(string dataId, string group, IConfigChangeListener listener)
    {
        group = GetGroupOrDefault(group);
        _listenerManager.RemoveListener(dataId, group, GetTenant(), listener);
        _metricsMonitor.SetListenConfigCount(_listenerManager.GetListeningConfigs().Count);
        _logger?.LogDebug("Removed listener for {DataId}@{Group}", dataId, group);
    }

    public async Task<bool> PublishConfigAsync(string dataId, string group, string content,
        CancellationToken cancellationToken = default)
    {
        return await PublishConfigAsync(dataId, group, content, ConfigType.Default, cancellationToken);
    }

    public async Task<bool> PublishConfigAsync(string dataId, string group, string content, string type,
        CancellationToken cancellationToken = default)
    {
        group = GetGroupOrDefault(group);
        ValidateParams(dataId, group, content);

        // Apply filter chain for encryption
        var (processedContent, encryptedDataKey) = await ApplyPublishFilterAsync(dataId, group, content, cancellationToken);

        var parameters = new Dictionary<string, string?>
        {
            { "dataId", dataId },
            { "groupName", group },
            { "namespaceId", GetTenant() },
            { "content", processedContent },
            { "type", type }
        };

        if (!string.IsNullOrEmpty(encryptedDataKey))
        {
            parameters["encryptedDataKey"] = encryptedDataKey;
        }

        var body = NacosUtils.BuildQueryString(parameters);

        try
        {
            var response = await _httpClient.PostAsync(ConfigAdminApiPath, null, body,
                _options.DefaultTimeout, cancellationToken);

            var result = IsSuccessEnvelope(response);

            if (result)
            {
                _metricsMonitor.RecordConfigRequestSuccess();
                _logger?.LogInformation("Published config {DataId}@{Group}", dataId, group);
            }
            else
            {
                _metricsMonitor.RecordConfigRequestFailed();
            }

            return result;
        }
        catch
        {
            _metricsMonitor.RecordConfigRequestFailed();
            throw;
        }
    }

    /// <summary>
    /// Not supported over HTTP: the v3 admin endpoint ignores <c>casMd5</c>, so a
    /// stale MD5 would silently overwrite newer content. Use the gRPC config service
    /// (<c>NacosGrpcConfigService</c>), which carries the CAS MD5 on the wire.
    /// </summary>
    [Obsolete("CAS is not supported over the v3 admin HTTP endpoint; use the gRPC config service. " +
        "This overload throws NotSupportedException when casMd5 is non-empty.")]
    public Task<bool> PublishConfigCasAsync(string dataId, string group, string content, string casMd5,
        CancellationToken cancellationToken = default)
    {
        return PublishConfigCasAsync(dataId, group, content, casMd5, ConfigType.Default, cancellationToken);
    }

    /// <inheritdoc cref="PublishConfigCasAsync(string, string, string, string, CancellationToken)"/>
    [Obsolete("CAS is not supported over the v3 admin HTTP endpoint; use the gRPC config service. " +
        "This overload throws NotSupportedException when casMd5 is non-empty.")]
    public async Task<bool> PublishConfigCasAsync(string dataId, string group, string content, string casMd5,
        string type, CancellationToken cancellationToken = default)
    {
        // Nacos 3.2.4 ignores `casMd5` on the admin publish endpoint (verified live:
        // a wrong MD5 still returns code 0 and overwrites the content), so a CAS
        // publish cannot be honored here. Fail loudly instead of silently
        // downgrading to an unconditional overwrite; an empty MD5 degrades to a
        // plain publish, which is what the server would do anyway.
        if (!string.IsNullOrEmpty(casMd5))
        {
            throw new NotSupportedException(
                "CAS publish is not supported over the v3 admin HTTP endpoint (casMd5 is ignored by " +
                "the server); use the gRPC config service instead.");
        }

        return await PublishConfigAsync(dataId, group, content, type, cancellationToken);
    }

    public async Task<bool> RemoveConfigAsync(string dataId, string group,
        CancellationToken cancellationToken = default)
    {
        group = GetGroupOrDefault(group);
        ValidateParams(dataId, group);

        var parameters = new Dictionary<string, string?>
        {
            { "dataId", dataId },
            { "groupName", group },
            { "namespaceId", GetTenant() }
        };

        try
        {
            var response = await _httpClient.DeleteAsync(ConfigAdminApiPath, parameters,
                _options.DefaultTimeout, cancellationToken);

            var result = IsSuccessEnvelope(response);

            if (result)
            {
                _localCache.RemoveSnapshot(dataId, group);
                _metricsMonitor.RecordConfigRequestSuccess();
                _logger?.LogInformation("Removed config {DataId}@{Group}", dataId, group);
            }
            else
            {
                _metricsMonitor.RecordConfigRequestFailed();
            }

            return result;
        }
        catch
        {
            _metricsMonitor.RecordConfigRequestFailed();
            throw;
        }
    }

    public string GetServerStatus()
    {
        return _isHealthy ? "UP" : "DOWN";
    }

    public void AddConfigFilter(IConfigFilter configFilter)
    {
        _filterChainManager.AddFilter(configFilter);
        _logger?.LogDebug("Added config filter: {FilterName}", configFilter.FilterName);
    }

    #region Fuzzy Watch

    public Task FuzzyWatchAsync(string groupNamePattern, IConfigFuzzyWatchEventWatcher watcher,
        CancellationToken cancellationToken = default)
    {
        return FuzzyWatchAsync("*", groupNamePattern, watcher, cancellationToken);
    }

    public Task FuzzyWatchAsync(string dataIdPattern, string groupNamePattern, IConfigFuzzyWatchEventWatcher watcher,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Fuzzy Watch requires the gRPC service in Nacos 3.2.4.");
    }

    public Task<ISet<string>> FuzzyWatchWithGroupKeysAsync(string groupNamePattern,
        IConfigFuzzyWatchEventWatcher watcher, CancellationToken cancellationToken = default)
    {
        return FuzzyWatchWithGroupKeysAsync("*", groupNamePattern, watcher, cancellationToken);
    }

    public async Task<ISet<string>> FuzzyWatchWithGroupKeysAsync(string dataIdPattern, string groupNamePattern,
        IConfigFuzzyWatchEventWatcher watcher, CancellationToken cancellationToken = default)
    {
        await FuzzyWatchAsync(dataIdPattern, groupNamePattern, watcher, cancellationToken);

        // Return current matching keys
        var matchingKeys = _fuzzyWatchManager.GetMatchingKeys(dataIdPattern, groupNamePattern, GetTenant() ?? "");
        return matchingKeys;
    }

    public Task CancelFuzzyWatchAsync(string groupNamePattern, IConfigFuzzyWatchEventWatcher watcher,
        CancellationToken cancellationToken = default)
    {
        return CancelFuzzyWatchAsync("*", groupNamePattern, watcher, cancellationToken);
    }

    public Task CancelFuzzyWatchAsync(string dataIdPattern, string groupNamePattern,
        IConfigFuzzyWatchEventWatcher watcher, CancellationToken cancellationToken = default)
    {
        _fuzzyWatchManager.RemoveWatcher(dataIdPattern, groupNamePattern, GetTenant() ?? "", watcher);
        _logger?.LogDebug("Cancelled fuzzy watch for dataId={DataIdPattern}, group={GroupPattern}",
            dataIdPattern, groupNamePattern);
        return Task.CompletedTask;
    }

    #endregion

    public async Task ShutdownAsync()
    {
        await DisposeAsync();
    }

    /// <summary>
    /// Unwraps the v3 config read envelope and returns the config content.
    /// Code <see cref="NacosConstants.ConfigNotFoundCode"/> (config does not exist)
    /// and a missing data payload yield <c>null</c>; any other non-zero code is a
    /// server-side failure and throws, so callers never mistake a refusal for a
    /// missing config.
    /// </summary>
    private static string? ExtractConfigContent(string? response)
    {
        if (string.IsNullOrEmpty(response) || !NacosEnvelope.TryParse(response, out var root))
        {
            return null;
        }

        var code = NacosEnvelope.GetCode(root);

        if (code == NacosConstants.ConfigNotFoundCode)
        {
            return null;
        }

        NacosEnvelope.ThrowIfFailed(root, "Get config");

        if (root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        return null;
    }

    /// <summary>
    /// Checks whether a v3 write envelope reports success (code 0 and, when
    /// present, a truthy <c>data</c> flag).
    /// </summary>
    private static bool IsSuccessEnvelope(string? response)
    {
        if (!NacosEnvelope.TryParse(response, out var root))
        {
            return false;
        }

        if (NacosEnvelope.GetCode(root) != NacosConstants.SuccessCode)
        {
            return false;
        }

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return true;
    }

    private string GetGroupOrDefault(string? group)
    {
        return string.IsNullOrWhiteSpace(group) ? NacosConstants.DefaultGroup : group.Trim();
    }

    private string? GetTenant()
    {
        return string.IsNullOrWhiteSpace(_options.Namespace) ? null : _options.Namespace;
    }

    private static void ValidateParams(string dataId, string group, string? content = null)
    {
        if (string.IsNullOrWhiteSpace(dataId))
        {
            throw new NacosException(NacosException.InvalidParam, "dataId is required");
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            throw new NacosException(NacosException.InvalidParam, "group is required");
        }

        if (content != null && string.IsNullOrEmpty(content))
        {
            throw new NacosException(NacosException.InvalidParam, "content cannot be empty");
        }
    }

    #region Filter Chain Methods

    private async Task<string?> ApplyGetFilterAsync(string dataId, string group, string? content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(content) || !_filterChainManager.HasFilters)
        {
            return content;
        }

        try
        {
            var request = new ConfigRequest();
            request.SetParameter(ConfigRequestKeys.DataId, dataId);
            request.SetParameter(ConfigRequestKeys.Group, group);
            request.SetParameter(ConfigRequestKeys.Tenant, GetTenant());
            request.SetParameter(ConfigRequestKeys.Content, content);

            var response = new ConfigResponse();

            await _filterChainManager.DoFilterAsync(request, response, cancellationToken);

            // Get decrypted content from response
            var processedContent = response.GetParameter<string>(ConfigResponseKeys.Content);
            return processedContent ?? content;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error applying get filter for {DataId}@{Group}", dataId, group);
            return content;
        }
    }

    private async Task<(string Content, string? EncryptedDataKey)> ApplyPublishFilterAsync(
        string dataId, string group, string content, CancellationToken cancellationToken)
    {
        if (!_filterChainManager.HasFilters)
        {
            return (content, null);
        }

        try
        {
            var request = new ConfigRequest();
            request.SetParameter(ConfigRequestKeys.DataId, dataId);
            request.SetParameter(ConfigRequestKeys.Group, group);
            request.SetParameter(ConfigRequestKeys.Tenant, GetTenant());
            request.SetParameter(ConfigRequestKeys.Content, content);

            var response = new ConfigResponse();

            await _filterChainManager.DoFilterAsync(request, response, cancellationToken);

            // Get encrypted content and data key from response
            var processedContent = response.GetParameter<string>(ConfigResponseKeys.Content) ?? content;
            var encryptedDataKey = response.GetParameter<string>(ConfigResponseKeys.EncryptedDataKey);

            return (processedContent, encryptedDataKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error applying publish filter for {DataId}@{Group}", dataId, group);
            return (content, null);
        }
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _cts.CancelAsync();
        _cts.Dispose();
        _httpClient.Dispose();
        _metricsMonitor.SetConnectionStatus(false);
        _metricsMonitor.SetListenConfigCount(0);
        _disposed = true;
    }
}
