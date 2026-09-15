using System.Text.Json.Serialization.Metadata;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Http.Transport;
using RedNb.Nacos;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Models;
using RedNb.Nacos.Ai.Models.Prompt;
using PromptModel = RedNb.Nacos.Ai.Models.Prompt.Prompt;
using RedNb.Nacos.Http.Serialization;
using RedNb.Nacos.Utils;

namespace RedNb.Nacos.Http.Ai;

/// <summary>
/// Nacos AI Prompt service implementation using HTTP.
/// Runtime queries use the client API (<c>/v3/client/ai/prompt</c>) with md5-based
/// conditional requests; management operations use the admin API (<c>/v3/admin/ai/prompt</c>).
/// </summary>
public class NacosPromptService : IPromptService, IAsyncDisposable
{
    private readonly NacosHttpClient _httpClient;
    private readonly NacosClientOptions _options;
    private readonly JsonSerializerOptions _serializationOptions;
    private JsonTypeInfo<T> Info<T>(JsonTypeInfo<T> contract)
        => (JsonTypeInfo<T>)_serializationOptions.GetTypeInfo(typeof(T));

    private readonly ILogger? _logger;
    private readonly string _namespaceId;

    private readonly ConcurrentDictionary<string, HashSet<AbstractNacosPromptListener>> _listeners = new();
    private readonly ConcurrentDictionary<string, PromptModel> _cache = new();
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

    private const string ClientBasePath = "v3/client/ai/prompt";
    private const string AdminBasePath = "v3/admin/ai/prompt";
    private const int PollingIntervalMs = 10000;

    /// <summary>
    /// Creates a prompt service sharing an existing HTTP client.
    /// </summary>
    /// <param name="httpClient">Shared Nacos HTTP client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Optional logger.</param>
    public NacosPromptService(NacosHttpClient httpClient, NacosClientOptions options, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _options = options;
        _serializationOptions = NacosHttpJsonOptions.CreateAi(options.JsonTypeInfoResolver);
        _logger = logger;
        _namespaceId = options.Namespace ?? string.Empty;


    }

    #region Prompt Query

    /// <inheritdoc />
    public Task<PromptModel?> GetPromptAsync(string promptKey, CancellationToken cancellationToken = default)
    {
        return GetPromptAsync(promptKey, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PromptModel?> GetPromptAsync(string promptKey, string? version, CancellationToken cancellationToken = default)
    {
        return FetchPromptAsync(promptKey, version, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PromptModel?> GetPromptByLabelAsync(string promptKey, string label, CancellationToken cancellationToken = default)
    {
        return FetchPromptAsync(promptKey, null, label, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PageResult<PromptMetaSummary>> SearchPromptsAsync(
        string? query = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "query", query },
            { "pageNo", pageNo.ToString() },
            { "pageSize", pageSize.ToString() }
        };
        var headers = BuildNamespaceHeaders();

        var response = await _httpClient.GetWithHeadersAsync($"{ClientBasePath}/search", parameters, headers, _options.DefaultTimeout, cancellationToken);
        var result = JsonSerializer.Deserialize(response ?? "{}", Info(NacosHttpAiJsonContext.Default.PromptApiResultPagedDataPromptMetaSummary));
        return ToPageResult(result?.Data, pageNo, pageSize);
    }

    #endregion

    #region Prompt Subscription

    /// <inheritdoc />
    public Task<PromptModel?> SubscribePromptAsync(string promptKey, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default)
    {
        return SubscribePromptAsync(promptKey, null, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PromptModel?> SubscribePromptAsync(string promptKey, string? version, string? label, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default)
    {
        EnsurePolling();
        ValidatePromptKey(promptKey);
        if (listener == null)
        {
            throw new NacosException(NacosException.InvalidParam, "listener is required");
        }

        var key = BuildKey(promptKey, version, label);
        lock (_listenerLock)
        {
            if (!_listeners.TryGetValue(key, out var listeners))
            {
                listeners = new HashSet<AbstractNacosPromptListener>();
                _listeners[key] = listeners;
            }
            listeners.Add(listener);
        }

        if (!_cache.TryGetValue(key, out var cached))
        {
            cached = await FetchPromptAsync(promptKey, version, label, null, cancellationToken);
            if (cached != null)
            {
                _cache[key] = cached;
            }
        }

        if (cached != null)
        {
            listener.OnEvent(new NacosPromptEvent(cached));
        }

        return cached;
    }

    /// <inheritdoc />
    public Task UnsubscribePromptAsync(string promptKey, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default)
    {
        return UnsubscribePromptAsync(promptKey, null, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsubscribePromptAsync(string promptKey, string? version, string? label, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptKey) || listener == null)
        {
            return Task.CompletedTask;
        }

        var key = BuildKey(promptKey, version, label);
        lock (_listenerLock)
        {
            if (_listeners.TryGetValue(key, out var listeners))
            {
                listeners.Remove(listener);
                if (listeners.Count == 0)
                {
                    _listeners.TryRemove(key, out _);
                    _cache.TryRemove(key, out _);
                }
            }
        }

        return Task.CompletedTask;
    }

    #endregion

    #region Prompt Management

    /// <inheritdoc />
    public async Task<PageResult<PromptMetaSummary>> ListPromptsAsync(
        string? promptKey = null, string? search = null, string? bizTags = null,
        int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "search", search },
            { "bizTags", bizTags },
            { "pageNo", pageNo.ToString() },
            { "pageSize", pageSize.ToString() }
        };
        var headers = BuildNamespaceHeaders();

        var response = await _httpClient.GetWithHeadersAsync($"{AdminBasePath}/list", parameters, headers, _options.DefaultTimeout, cancellationToken);
        var result = JsonSerializer.Deserialize(response ?? "{}", Info(NacosHttpAiJsonContext.Default.PromptApiResultPagedDataPromptMetaSummary));
        return ToPageResult(result?.Data, pageNo, pageSize);
    }

    /// <inheritdoc />
    public async Task<PromptMetaInfo?> GetPromptMetaAsync(string promptKey, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            var response = await _httpClient.GetWithHeadersAsync($"{AdminBasePath}/metadata", parameters, headers, _options.DefaultTimeout, cancellationToken);
            var result = JsonSerializer.Deserialize(response ?? "{}", Info(NacosHttpAiJsonContext.Default.PromptApiResultPromptMetaInfo));
            return result?.Data;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<PromptVersionSummary>> ListPromptVersionsAsync(string promptKey, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey }
        };
        var headers = BuildNamespaceHeaders();

        var response = await _httpClient.GetWithHeadersAsync($"{AdminBasePath}/versions", parameters, headers, _options.DefaultTimeout, cancellationToken);
        var result = JsonSerializer.Deserialize(response ?? "{}", Info(NacosHttpAiJsonContext.Default.PromptApiResultListPromptVersionSummary));
        return result?.Data ?? new List<PromptVersionSummary>();
    }

    /// <inheritdoc />
    public async Task<PromptVersionInfo?> GetPromptVersionDetailAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "version", version }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            var response = await _httpClient.GetWithHeadersAsync($"{AdminBasePath}/version", parameters, headers, _options.DefaultTimeout, cancellationToken);
            var result = JsonSerializer.Deserialize(response ?? "{}", Info(NacosHttpAiJsonContext.Default.PromptApiResultPromptVersionInfo));
            return result?.Data;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task CreatePromptDraftAsync(
        string promptKey, string targetVersion, string template, string? basedOnVersion = null,
        IEnumerable<PromptVariable>? variables = null, string? commitMsg = null, string? description = null,
        IEnumerable<string>? bizTags = null, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "basedOnVersion", basedOnVersion },
            { "targetVersion", targetVersion },
            { "template", template },
            { "commitMsg", commitMsg },
            { "description", description },
            { "variables", variables == null ? null : JsonSerializer.Serialize(variables.ToList(), Info(NacosHttpAiJsonContext.Default.ListPromptVariable)) },
            { "bizTags", bizTags == null ? null : JsonSerializer.Serialize(bizTags.ToList(), Info(NacosHttpAiJsonContext.Default.ListString)) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PostWithHeadersAsync($"{AdminBasePath}/draft", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdatePromptDraftAsync(
        string promptKey, string version, string template, IEnumerable<PromptVariable>? variables = null,
        string? commitMsg = null, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "version", version },
            { "template", template },
            { "commitMsg", commitMsg },
            { "variables", variables == null ? null : JsonSerializer.Serialize(variables.ToList(), Info(NacosHttpAiJsonContext.Default.ListPromptVariable)) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/draft", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeletePromptDraftAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "version", version }
        };
        var headers = BuildNamespaceHeaders();

        await _httpClient.DeleteWithHeadersAsync($"{AdminBasePath}/draft", parameters, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public Task SubmitPromptReviewAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        return PostPromptActionAsync("submit", promptKey, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishPromptAsync(string promptKey, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
    {
        return PostPromptActionAsync("publish", promptKey, version,
            ("updateLatestLabel", updateLatestLabel.ToString().ToLowerInvariant()), cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishPromptAsync(
        string promptKey, string version, string template, string? commitMsg = null, string? description = null,
        IEnumerable<string>? bizTags = null, IEnumerable<PromptVariable>? variables = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "version", version },
            { "template", template },
            { "commitMsg", commitMsg },
            { "description", description },
            { "bizTags", bizTags == null ? null : JsonSerializer.Serialize(bizTags.ToList(), Info(NacosHttpAiJsonContext.Default.ListString)) },
            { "variables", variables == null ? null : JsonSerializer.Serialize(variables.ToList(), Info(NacosHttpAiJsonContext.Default.ListPromptVariable)) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PostWithHeadersAsync($"{AdminBasePath}/publish", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public Task ForcePublishPromptAsync(string promptKey, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
    {
        return PostPromptActionAsync("force-publish", promptKey, version,
            ("updateLatestLabel", updateLatestLabel.ToString().ToLowerInvariant()), cancellationToken);
    }

    /// <inheritdoc />
    public Task RedraftPromptAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        return PostPromptActionAsync("redraft", promptKey, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task OnlinePromptAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        return PostPromptActionAsync("online", promptKey, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task OfflinePromptAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        return PostPromptActionAsync("offline", promptKey, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdatePromptLabelsAsync(string promptKey, IDictionary<string, string> labels, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "labels", JsonSerializer.Serialize(labels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), Info(NacosHttpAiJsonContext.Default.DictionaryStringString)) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/labels", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdatePromptDescriptionAsync(string promptKey, string description, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "description", description }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/description", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdatePromptBizTagsAsync(string promptKey, IEnumerable<string> bizTags, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "bizTags", JsonSerializer.Serialize(bizTags.ToList(), Info(NacosHttpAiJsonContext.Default.ListString)) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/biz-tags", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeletePromptAsync(string promptKey, string? version = null, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
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
        _disposed = true;
    }

    #endregion

    #region Private Methods

    private async Task<PromptModel?> FetchPromptAsync(
        string promptKey, string? version, string? label, string? md5, CancellationToken cancellationToken)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "version", version },
            { "label", label },
            { "md5", md5 }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            // TODO: confirm response shape — design spec hypothesizes raw Prompt JSON
            // (live Nacos 3.x verification needed; defer to CI). Currently parsed as
            // ApiResult<Prompt> envelope; switch to JsonSerializer.Deserialize<Prompt>(...)
            // if/when the live server confirms the unwrapped shape.
            var response = await _httpClient.GetWithHeadersAsync(ClientBasePath, parameters, headers, _options.DefaultTimeout, cancellationToken);
            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            var result = JsonSerializer.Deserialize(response, Info(NacosHttpAiJsonContext.Default.PromptApiResultPrompt));
            return result?.Data;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }
    }

    private async Task PostPromptActionAsync(
        string action, string promptKey, string version,
        (string Name, string? Value) extra = default, CancellationToken cancellationToken = default)
    {
        ValidatePromptKey(promptKey);

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "version", version }
        };

        if (!string.IsNullOrEmpty(extra.Name))
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
                        await PollPromptAsync(key, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error polling prompt subscription {Key}", key);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in prompt subscription polling");
            }
        }
    }

    private async Task PollPromptAsync(string key, CancellationToken cancellationToken)
    {
        var (promptKey, version, label) = ParseKey(key);
        var cached = _cache.TryGetValue(key, out var existing) ? existing : null;

        var parameters = new Dictionary<string, string?>
        {
            { "promptKey", promptKey },
            { "version", version },
            { "label", label },
            { "md5", cached?.Md5 }
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

        var result = JsonSerializer.Deserialize(raw.BodyString, Info(NacosHttpAiJsonContext.Default.PromptApiResultPrompt));
        var current = result?.Data;
        if (current == null)
        {
            return;
        }

        _cache[key] = current;
        NotifyListeners(key, current);
    }

    private void NotifyListeners(string key, PromptModel prompt)
    {
        List<AbstractNacosPromptListener> listeners;
        lock (_listenerLock)
        {
            listeners = _listeners.TryGetValue(key, out var set) ? set.ToList() : new List<AbstractNacosPromptListener>();
        }

        var evt = new NacosPromptEvent(prompt);
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

    internal static string BuildKey(string promptKey, string? version, string? label)
    {
        return $"{promptKey}@@{version ?? string.Empty}@@{label ?? string.Empty}";
    }

    private static (string PromptKey, string? Version, string? Label) ParseKey(string key)
    {
        var parts = key.Split("@@");
        return (
            parts[0],
            parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : null,
            parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) ? parts[2] : null);
    }

    private static void ValidatePromptKey(string promptKey)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
        {
            throw new NacosException(NacosException.InvalidParam, "promptKey is required");
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

    private static PageResult<T> ToPageResult<T>(PagedData<T>? data, int pageNo, int pageSize)
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

    /// <summary>
    /// API result wrapper for Nacos v3 API responses.
    /// </summary>
    internal class ApiResult<T>
    {
        public int Code { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    /// <summary>
    /// Paged data from API response.
    /// </summary>
    internal class PagedData<T>
    {
        public int TotalCount { get; set; }
        public List<T>? PageItems { get; set; }
    }
}
