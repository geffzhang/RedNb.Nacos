using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Http.Transport;
using RedNb.Nacos;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Models;
using RedNb.Nacos.Ai.Models.AgentSpec;
using AgentSpecModel = RedNb.Nacos.Ai.Models.AgentSpec.AgentSpec;
using RedNb.Nacos.Http.Serialization;
using RedNb.Nacos.Utils;

namespace RedNb.Nacos.Http.Ai;

/// <summary>
/// Nacos AI AgentSpec service implementation using HTTP.
/// Runtime queries use the client API (<c>/v3/client/ai/agentspecs</c>) with md5-based
/// conditional requests; management operations use the admin API (<c>/v3/admin/ai/agentspecs</c>).
/// </summary>
public class NacosAgentSpecService : IAgentSpecService, IAsyncDisposable
{
    private readonly NacosHttpClient _httpClient;
    private readonly NacosClientOptions _options;
    private readonly ILogger? _logger;
    private readonly string _namespaceId;

    private readonly ConcurrentDictionary<string, HashSet<AbstractNacosAgentSpecListener>> _listeners = new();
    private readonly ConcurrentDictionary<string, AgentSpecModel> _cache = new();
    private readonly ConcurrentDictionary<string, string?> _md5Cache = new();
    private readonly object _listenerLock = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private readonly object _pollLock = new();
    private Task? _pollTask;
    private void EnsurePolling()
    {
        lock (_pollLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pollTask ??= Task.Run(() => StartPollingAsync(_cts.Token));
        }
    }

    private const string ClientBasePath = "v3/client/ai/agentspecs";
    private const string AdminBasePath = "v3/admin/ai/agentspecs";
    private const int PollingIntervalMs = 10000;

    private static readonly JsonSerializerOptions JsonOptions = NacosHttpJsonOptions.CreateAi();

    /// <summary>
    /// Creates an AgentSpec service sharing an existing HTTP client.
    /// </summary>
    /// <param name="httpClient">Shared Nacos HTTP client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Optional logger.</param>
    public NacosAgentSpecService(NacosHttpClient httpClient, NacosClientOptions options, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _namespaceId = options.Namespace ?? string.Empty;


    }

    #region AgentSpec Query

    /// <inheritdoc />
    public Task<AgentSpecModel?> GetAgentSpecAsync(string agentSpecName, CancellationToken cancellationToken = default)
    {
        return GetAgentSpecAsync(agentSpecName, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AgentSpecModel?> GetAgentSpecAsync(string agentSpecName, string? version, CancellationToken cancellationToken = default)
    {
        return FetchAgentSpecAsync(agentSpecName, version, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AgentSpecModel?> GetAgentSpecByLabelAsync(string agentSpecName, string label, CancellationToken cancellationToken = default)
    {
        return FetchAgentSpecAsync(agentSpecName, null, label, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PageResult<AgentSpecSummary>> SearchAgentSpecsAsync(
        string? keyword = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "keyword", keyword },
            { "pageNo", pageNo.ToString() },
            { "pageSize", pageSize.ToString() }
        };
        var headers = BuildNamespaceHeaders();

        var response = await _httpClient.GetWithHeadersAsync($"{ClientBasePath}/search", parameters, headers, _options.DefaultTimeout, cancellationToken);
        var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<NacosPromptService.PagedData<AgentSpecSummary>>>(response ?? "{}", JsonOptions);
        return ToPageResult(result?.Data, pageNo, pageSize);
    }

    #endregion

    #region AgentSpec Subscription

    /// <inheritdoc />
    public Task<AgentSpecModel?> SubscribeAgentSpecAsync(string agentSpecName, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default)
    {
        return SubscribeAgentSpecAsync(agentSpecName, null, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentSpecModel?> SubscribeAgentSpecAsync(string agentSpecName, string? version, string? label, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default)
    {
        EnsurePolling();
        ValidateAgentSpecName(agentSpecName);
        if (listener == null)
        {
            throw new NacosException(NacosException.InvalidParam, "listener is required");
        }

        var key = BuildKey(agentSpecName, version, label);
        lock (_listenerLock)
        {
            if (!_listeners.TryGetValue(key, out var listeners))
            {
                listeners = new HashSet<AbstractNacosAgentSpecListener>();
                _listeners[key] = listeners;
            }
            listeners.Add(listener);
        }

        if (!_cache.TryGetValue(key, out var cached))
        {
            cached = await FetchAgentSpecAsync(agentSpecName, version, label, null, cancellationToken);
            if (cached != null)
            {
                _cache[key] = cached;
            }
        }

        if (cached != null)
        {
            listener.OnEvent(new NacosAgentSpecEvent(cached, version));
        }

        return cached;
    }

    /// <inheritdoc />
    public Task UnsubscribeAgentSpecAsync(string agentSpecName, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default)
    {
        return UnsubscribeAgentSpecAsync(agentSpecName, null, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsubscribeAgentSpecAsync(string agentSpecName, string? version, string? label, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentSpecName) || listener == null)
        {
            return Task.CompletedTask;
        }

        var key = BuildKey(agentSpecName, version, label);
        lock (_listenerLock)
        {
            if (_listeners.TryGetValue(key, out var listeners))
            {
                listeners.Remove(listener);
                if (listeners.Count == 0)
                {
                    _listeners.TryRemove(key, out _);
                    _cache.TryRemove(key, out _);
                    _md5Cache.TryRemove(key, out _);
                }
            }
        }

        return Task.CompletedTask;
    }

    #endregion

    #region AgentSpec Management

    /// <inheritdoc />
    public async Task<PageResult<AgentSpecSummary>> ListAgentSpecsAsync(
        string? agentSpecName = null, string? search = null,
        int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName },
            { "search", search },
            { "pageNo", pageNo.ToString() },
            { "pageSize", pageSize.ToString() }
        };
        var headers = BuildNamespaceHeaders();

        var response = await _httpClient.GetWithHeadersAsync($"{AdminBasePath}/list", parameters, headers, _options.DefaultTimeout, cancellationToken);
        var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<NacosPromptService.PagedData<AgentSpecSummary>>>(response ?? "{}", JsonOptions);
        return ToPageResult(result?.Data, pageNo, pageSize);
    }

    /// <inheritdoc />
    public async Task<AgentSpecMeta?> GetAgentSpecMetaAsync(string agentSpecName, CancellationToken cancellationToken = default)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            var response = await _httpClient.GetWithHeadersAsync($"{AdminBasePath}/version", parameters, headers, _options.DefaultTimeout, cancellationToken);
            var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<AgentSpecMeta>>(response ?? "{}", JsonOptions);
            return result?.Data;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AgentSpecModel?> GetAgentSpecDetailAsync(string agentSpecName, string? version = null, CancellationToken cancellationToken = default)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName },
            { "version", version }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            var response = await _httpClient.GetWithHeadersAsync(AdminBasePath, parameters, headers, _options.DefaultTimeout, cancellationToken);
            var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<AgentSpecModel>>(response ?? "{}", JsonOptions);
            return result?.Data;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task UploadAgentSpecAsync(
        byte[] content, string fileName, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (content == null || content.Length == 0)
        {
            throw new NacosException(NacosException.InvalidParam, "content is required");
        }

        using var multipart = new MultipartFormDataContent();
        // namespaceId is sent via the X-Nacos-Namespace-Id HTTP header (v3 spec), not as a form part.
        multipart.Add(new StringContent(overwrite.ToString().ToLowerInvariant()), "overwrite");

        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(fileContent, "file", fileName);

        var headers = BuildNamespaceHeaders();
        await _httpClient.PostMultipartWithHeadersAsync($"{AdminBasePath}/upload", multipart, null, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateAgentSpecDraftAsync(
        AgentSpecModel agentSpec, string? basedOnVersion = null, string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAgentSpec(agentSpec);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpec.Name },
            { "agentSpecCard", JsonSerializer.Serialize(agentSpec, JsonOptions) },
            { "basedOnVersion", basedOnVersion },
            { "targetVersion", targetVersion }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PostWithHeadersAsync($"{AdminBasePath}/draft", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAgentSpecDraftAsync(
        AgentSpecModel agentSpec, string version, bool setAsLatest = false,
        CancellationToken cancellationToken = default)
    {
        ValidateAgentSpec(agentSpec);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpec.Name },
            { "version", version },
            { "agentSpecCard", JsonSerializer.Serialize(agentSpec, JsonOptions) },
            { "setAsLatest", setAsLatest.ToString().ToLowerInvariant() }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/draft", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAgentSpecDraftAsync(string agentSpecName, string version, CancellationToken cancellationToken = default)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName },
            { "version", version }
        };
        var headers = BuildNamespaceHeaders();

        await _httpClient.DeleteWithHeadersAsync($"{AdminBasePath}/draft", parameters, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public Task SubmitAgentSpecReviewAsync(string agentSpecName, string version, CancellationToken cancellationToken = default)
    {
        return PostAgentSpecActionAsync("submit", agentSpecName, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishAgentSpecAsync(string agentSpecName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
    {
        return PostAgentSpecActionAsync("publish", agentSpecName, version,
            ("updateLatestLabel", updateLatestLabel.ToString().ToLowerInvariant()), cancellationToken);
    }

    /// <inheritdoc />
    public Task ForcePublishAgentSpecAsync(string agentSpecName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
    {
        return PostAgentSpecActionAsync("force-publish", agentSpecName, version,
            ("updateLatestLabel", updateLatestLabel.ToString().ToLowerInvariant()), cancellationToken);
    }

    /// <inheritdoc />
    public Task RedraftAgentSpecAsync(string agentSpecName, string version, CancellationToken cancellationToken = default)
    {
        return PostAgentSpecActionAsync("redraft", agentSpecName, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task OnlineAgentSpecAsync(string agentSpecName, string version, string? scope = null, CancellationToken cancellationToken = default)
    {
        return PostAgentSpecActionAsync("online", agentSpecName, version, ("scope", scope), cancellationToken);
    }

    /// <inheritdoc />
    public Task OfflineAgentSpecAsync(string agentSpecName, string version, CancellationToken cancellationToken = default)
    {
        return PostAgentSpecActionAsync("offline", agentSpecName, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAgentSpecScopeAsync(string agentSpecName, string scope, CancellationToken cancellationToken = default)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName },
            { "scope", scope }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/scope", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAgentSpecLabelsAsync(string agentSpecName, IDictionary<string, string> labels, CancellationToken cancellationToken = default)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName },
            { "labels", JsonSerializer.Serialize(labels, JsonOptions) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/labels", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAgentSpecBizTagsAsync(string agentSpecName, IEnumerable<string> bizTags, CancellationToken cancellationToken = default)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName },
            { "bizTags", JsonSerializer.Serialize(bizTags, JsonOptions) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/biz-tags", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAgentSpecAsync(string agentSpecName, string? version = null, CancellationToken cancellationToken = default)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName },
            { "version", version }
        };
        var headers = BuildNamespaceHeaders();

        await _httpClient.DeleteWithHeadersAsync(AdminBasePath, parameters, headers, _options.DefaultTimeout, cancellationToken);
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        await _cts.CancelAsync();
        if (_pollTask != null) await _pollTask;
        _cts.Dispose();
        lock (_listenerLock)
        {
            _listeners.Clear();
        }
        _cache.Clear();
        _md5Cache.Clear();
        _disposed = true;
    }

    #endregion

    #region Private Methods

    private async Task<AgentSpecModel?> FetchAgentSpecAsync(
        string agentSpecName, string? version, string? label, string? md5, CancellationToken cancellationToken)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "name", agentSpecName },
            { "version", version },
            { "label", label },
            { "md5", md5 }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            var response = await _httpClient.GetWithHeadersAsync(ClientBasePath, parameters, headers, _options.DefaultTimeout, cancellationToken);
            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<AgentSpecModel>>(response, JsonOptions);
            return result?.Data;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }
    }

    private async Task PostAgentSpecActionAsync(
        string action, string agentSpecName, string version,
        (string Name, string? Value) extra = default, CancellationToken cancellationToken = default)
    {
        ValidateAgentSpecName(agentSpecName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentSpecName", agentSpecName },
            { "version", version }
        };

        if (!string.IsNullOrEmpty(extra.Name) && extra.Value != null)
        {
            parameters[extra.Name] = extra.Value;
        }

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PostWithHeadersAsync($"{AdminBasePath}/{action}", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    private async Task StartPollingAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollingIntervalMs, cancellationToken);

                List<string> keys;
                lock (_listenerLock)
                {
                    keys = _listeners.Keys.ToList();
                }

                foreach (var key in keys)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        await PollAgentSpecAsync(key, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error polling AgentSpec subscription {Key}", key);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in AgentSpec subscription polling");
            }
        }
    }

    private async Task PollAgentSpecAsync(string key, CancellationToken cancellationToken)
    {
        var (agentSpecName, version, label) = ParseKey(key);
        _md5Cache.TryGetValue(key, out var cachedMd5);

        var parameters = new Dictionary<string, string?>
        {
            { "name", agentSpecName },
            { "version", version },
            { "label", label },
            { "md5", cachedMd5 }
        };
        var headers = BuildNamespaceHeaders();

        NacosRawResponse raw;
        try
        {
            raw = await _httpClient.GetRawAsync(ClientBasePath, parameters, headers, _options.DefaultTimeout, cancellationToken);
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return;
        }

        if (raw.IsNotModified)
        {
            return;
        }

        var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<AgentSpecModel>>(raw.BodyString, JsonOptions);
        var current = result?.Data;
        if (current == null)
        {
            return;
        }

        _cache[key] = current;
        _md5Cache[key] = raw.GetHeader(AiConstants.AgentSpec.HeaderAgentSpecMd5);
        var resolvedVersion = raw.GetHeader(AiConstants.AgentSpec.HeaderAgentSpecResolvedVersion);
        NotifyListeners(key, current, resolvedVersion);
    }

    private void NotifyListeners(string key, AgentSpecModel agentSpec, string? resolvedVersion)
    {
        List<AbstractNacosAgentSpecListener> listeners;
        lock (_listenerLock)
        {
            listeners = _listeners.TryGetValue(key, out var set) ? set.ToList() : new List<AbstractNacosAgentSpecListener>();
        }

        var evt = new NacosAgentSpecEvent(agentSpec, resolvedVersion);
        foreach (var listener in listeners)
        {
            try
            {
                listener.OnEvent(evt);
            }
            catch
            {
                // Ignore listener exceptions
            }
        }
    }

    internal static string BuildKey(string agentSpecName, string? version, string? label)
    {
        return $"{agentSpecName}@@{version ?? string.Empty}@@{label ?? string.Empty}";
    }

    private static (string AgentSpecName, string? Version, string? Label) ParseKey(string key)
    {
        var parts = key.Split("@@");
        return (
            parts[0],
            parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : null,
            parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) ? parts[2] : null);
    }

    private static void ValidateAgentSpecName(string agentSpecName)
    {
        if (string.IsNullOrWhiteSpace(agentSpecName))
        {
            throw new NacosException(NacosException.InvalidParam, "agentSpecName is required");
        }
    }

    private static void ValidateAgentSpec(AgentSpecModel agentSpec)
    {
        if (agentSpec == null || string.IsNullOrWhiteSpace(agentSpec.Name))
        {
            throw new NacosException(NacosException.InvalidParam, "agentSpec.Name is required");
        }
    }

    /// <summary>
    /// Builds a header dictionary carrying <c>namespaceId</c> as the v3
    /// <c>X-Nacos-Namespace-Id</c> HTTP header (omitted when empty).
    /// </summary>
    private Dictionary<string, string> BuildNamespaceHeaders()
    {
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(_namespaceId))
        {
            headers[NacosConstants.NamespaceHeader] = _namespaceId;
        }
        return headers;
    }

    private static PageResult<T> ToPageResult<T>(NacosPromptService.PagedData<T>? data, int pageNo, int pageSize)
    {
        if (data == null)
        {
            return PageResult<T>.Empty(pageNo, pageSize);
        }

        return new PageResult<T>
        {
            TotalCount = data.TotalCount,
            PageNumber = pageNo,
            PageSize = pageSize,
            PageItems = data.PageItems ?? new List<T>()
        };
    }

    #endregion
}
