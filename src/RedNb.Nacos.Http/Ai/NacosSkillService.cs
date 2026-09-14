using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Http.Transport;
using RedNb.Nacos;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Models;
using RedNb.Nacos.Ai.Models.Skill;
using RedNb.Nacos.Http.Serialization;
using RedNb.Nacos.Utils;

namespace RedNb.Nacos.Http.Ai;

/// <summary>
/// Nacos AI Skill service implementation using HTTP.
/// Skill packages are downloaded through the client API (<c>/v3/client/ai/skills</c>) as ZIP
/// bytes with md5-based conditional requests; management operations use the admin API
/// (<c>/v3/admin/ai/skills</c>).
/// </summary>
public class NacosSkillService : ISkillService, IAsyncDisposable
{
    private readonly NacosHttpClient _httpClient;
    private readonly NacosClientOptions _options;
    private readonly ILogger? _logger;
    private readonly string _namespaceId;

    private readonly ConcurrentDictionary<string, HashSet<AbstractNacosSkillListener>> _listeners = new();
    private readonly ConcurrentDictionary<string, SkillPackage> _cache = new();
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

    private const string ClientBasePath = "v3/client/ai/skills";
    private const string AdminBasePath = "v3/admin/ai/skills";
    private const int PollingIntervalMs = 10000;

    private static readonly JsonSerializerOptions JsonOptions = NacosHttpJsonOptions.CreateAi();

    /// <summary>
    /// Creates a skill service sharing an existing HTTP client.
    /// </summary>
    /// <param name="httpClient">Shared Nacos HTTP client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Optional logger.</param>
    public NacosSkillService(NacosHttpClient httpClient, NacosClientOptions options, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _namespaceId = options.Namespace ?? string.Empty;


    }

    #region Skill Download

    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipAsync(string skillName, CancellationToken cancellationToken = default)
    {
        return DownloadSkillZipInternalAsync(skillName, null, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipByVersionAsync(string skillName, string version, CancellationToken cancellationToken = default)
    {
        return DownloadSkillZipInternalAsync(skillName, version, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipByLabelAsync(string skillName, string label, CancellationToken cancellationToken = default)
    {
        return DownloadSkillZipInternalAsync(skillName, null, label, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PageResult<SkillSummary>> SearchSkillsAsync(
        string? query = null, IEnumerable<string>? tagsAll = null,
        int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "query", query },
            { "tagsAll", tagsAll == null ? null : string.Join(",", tagsAll) },
            { "pageNo", pageNo.ToString() },
            { "pageSize", pageSize.ToString() }
        };
        var headers = BuildNamespaceHeaders();

        var response = await _httpClient.GetWithHeadersAsync($"{ClientBasePath}/search", parameters, headers, _options.DefaultTimeout, cancellationToken);
        var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<NacosPromptService.PagedData<SkillSummary>>>(response ?? "{}", JsonOptions);
        return ToPageResult(result?.Data, pageNo, pageSize);
    }

    #endregion

    #region Skill Subscription

    /// <inheritdoc />
    public Task<SkillPackage?> SubscribeSkillAsync(string skillName, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default)
    {
        return SubscribeSkillAsync(skillName, null, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SkillPackage?> SubscribeSkillAsync(string skillName, string? version, string? label, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default)
    {
        EnsurePolling();
        ValidateSkillName(skillName);
        if (listener == null)
        {
            throw new NacosException(NacosException.InvalidParam, "listener is required");
        }

        var key = BuildKey(skillName, version, label);
        lock (_listenerLock)
        {
            if (!_listeners.TryGetValue(key, out var listeners))
            {
                listeners = new HashSet<AbstractNacosSkillListener>();
                _listeners[key] = listeners;
            }
            listeners.Add(listener);
        }

        if (!_cache.TryGetValue(key, out var cached))
        {
            cached = await DownloadSkillZipInternalAsync(skillName, version, label, null, cancellationToken);
            if (cached != null)
            {
                _cache[key] = cached;
            }
        }

        if (cached != null)
        {
            listener.OnEvent(new NacosSkillEvent(skillName, cached.ZipContent, cached.Md5, cached.Version));
        }

        return cached;
    }

    /// <inheritdoc />
    public Task UnsubscribeSkillAsync(string skillName, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default)
    {
        return UnsubscribeSkillAsync(skillName, null, null, listener, cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsubscribeSkillAsync(string skillName, string? version, string? label, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(skillName) || listener == null)
        {
            return Task.CompletedTask;
        }

        var key = BuildKey(skillName, version, label);
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

    #region Skill Management

    /// <inheritdoc />
    public async Task<PageResult<SkillSummary>> ListSkillsAsync(
        string? skillName = null, string? search = null, string? orderBy = null,
        int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName },
            { "search", search },
            { "orderBy", orderBy },
            { "pageNo", pageNo.ToString() },
            { "pageSize", pageSize.ToString() }
        };
        var headers = BuildNamespaceHeaders();

        var response = await _httpClient.GetWithHeadersAsync($"{AdminBasePath}/list", parameters, headers, _options.DefaultTimeout, cancellationToken);
        var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<NacosPromptService.PagedData<SkillSummary>>>(response ?? "{}", JsonOptions);
        return ToPageResult(result?.Data, pageNo, pageSize);
    }

    /// <inheritdoc />
    public async Task<SkillMeta?> GetSkillMetaAsync(string skillName, CancellationToken cancellationToken = default)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            var response = await _httpClient.GetWithHeadersAsync($"{AdminBasePath}/version", parameters, headers, _options.DefaultTimeout, cancellationToken);
            var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<SkillMeta>>(response ?? "{}", JsonOptions);
            return result?.Data;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Skill?> GetSkillDetailAsync(string skillName, string? version = null, CancellationToken cancellationToken = default)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName },
            { "version", version }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            var response = await _httpClient.GetWithHeadersAsync(AdminBasePath, parameters, headers, _options.DefaultTimeout, cancellationToken);
            var result = JsonSerializer.Deserialize<NacosPromptService.ApiResult<Skill>>(response ?? "{}", JsonOptions);
            return result?.Data;
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task UploadSkillZipAsync(
        byte[] zipContent, string fileName, bool overwrite = false, string? targetVersion = null,
        string? commitMsg = null, string? uploadAction = null, bool autoPublishIfNew = false,
        CancellationToken cancellationToken = default)
    {
        if (zipContent == null || zipContent.Length == 0)
        {
            throw new NacosException(NacosException.InvalidParam, "zipContent is required");
        }

        using var content = new MultipartFormDataContent();
        // namespaceId is sent via the X-Nacos-Namespace-Id HTTP header (v3 spec), not as a form part.
        content.Add(new StringContent(overwrite.ToString().ToLowerInvariant()), "overwrite");
        content.Add(new StringContent(autoPublishIfNew.ToString().ToLowerInvariant()), "autoPublishIfNew");

        if (!string.IsNullOrEmpty(targetVersion))
        {
            content.Add(new StringContent(targetVersion), "targetVersion");
        }
        if (!string.IsNullOrEmpty(commitMsg))
        {
            content.Add(new StringContent(commitMsg), "commitMsg");
        }
        if (!string.IsNullOrEmpty(uploadAction))
        {
            content.Add(new StringContent(uploadAction), "uploadAction");
        }

        var fileContent = new ByteArrayContent(zipContent);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", fileName);

        var headers = BuildNamespaceHeaders();
        await _httpClient.PostMultipartWithHeadersAsync($"{AdminBasePath}/upload", content, null, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateSkillDraftAsync(
        Skill skill, string? basedOnVersion = null, string? targetVersion = null,
        string? commitMsg = null, CancellationToken cancellationToken = default)
    {
        ValidateSkill(skill);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skill.Name },
            { "skillCard", JsonSerializer.Serialize(skill, JsonOptions) },
            { "basedOnVersion", basedOnVersion },
            { "targetVersion", targetVersion },
            { "commitMsg", commitMsg }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PostWithHeadersAsync($"{AdminBasePath}/draft", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateSkillDraftAsync(
        Skill skill, string version, bool setAsLatest = false, string? commitMsg = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSkill(skill);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skill.Name },
            { "version", version },
            { "skillCard", JsonSerializer.Serialize(skill, JsonOptions) },
            { "setAsLatest", setAsLatest.ToString().ToLowerInvariant() },
            { "commitMsg", commitMsg }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/draft", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteSkillDraftAsync(string skillName, string version, CancellationToken cancellationToken = default)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName },
            { "version", version }
        };
        var headers = BuildNamespaceHeaders();

        await _httpClient.DeleteWithHeadersAsync($"{AdminBasePath}/draft", parameters, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public Task SubmitSkillReviewAsync(string skillName, string version, CancellationToken cancellationToken = default)
    {
        return PostSkillActionAsync("submit", skillName, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishSkillAsync(string skillName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
    {
        return PostSkillActionAsync("publish", skillName, version,
            ("updateLatestLabel", updateLatestLabel.ToString().ToLowerInvariant()), cancellationToken);
    }

    /// <inheritdoc />
    public Task ForcePublishSkillAsync(string skillName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
    {
        return PostSkillActionAsync("force-publish", skillName, version,
            ("updateLatestLabel", updateLatestLabel.ToString().ToLowerInvariant()), cancellationToken);
    }

    /// <inheritdoc />
    public Task RedraftSkillAsync(string skillName, string version, CancellationToken cancellationToken = default)
    {
        return PostSkillActionAsync("redraft", skillName, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task OnlineSkillAsync(string skillName, string version, string? scope = null, CancellationToken cancellationToken = default)
    {
        return PostSkillActionAsync("online", skillName, version, ("scope", scope), cancellationToken);
    }

    /// <inheritdoc />
    public Task OfflineSkillAsync(string skillName, string version, CancellationToken cancellationToken = default)
    {
        return PostSkillActionAsync("offline", skillName, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateSkillScopeAsync(string skillName, string scope, CancellationToken cancellationToken = default)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName },
            { "scope", scope }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/scope", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateSkillLabelsAsync(string skillName, IDictionary<string, string> labels, CancellationToken cancellationToken = default)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName },
            { "labels", JsonSerializer.Serialize(labels, JsonOptions) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/labels", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateSkillBizTagsAsync(string skillName, IEnumerable<string> bizTags, CancellationToken cancellationToken = default)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName },
            { "bizTags", JsonSerializer.Serialize(bizTags, JsonOptions) }
        };

        var body = NacosUtils.BuildQueryString(parameters);
        var headers = BuildNamespaceHeaders();
        await _httpClient.PutWithHeadersAsync($"{AdminBasePath}/biz-tags", null, body, headers, _options.DefaultTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteSkillAsync(string skillName, string? version = null, CancellationToken cancellationToken = default)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName },
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

    private async Task<SkillPackage?> DownloadSkillZipInternalAsync(
        string skillName, string? version, string? label, string? md5, CancellationToken cancellationToken)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "name", skillName },
            { "version", version },
            { "label", label },
            { "md5", md5 }
        };
        var headers = BuildNamespaceHeaders();

        NacosRawResponse raw;
        try
        {
            raw = await _httpClient.GetRawAsync(ClientBasePath, parameters, headers, _options.DefaultTimeout, cancellationToken);
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            return null;
        }

        if (raw.IsNotModified || raw.Body.Length == 0)
        {
            return null;
        }

        return new SkillPackage
        {
            SkillName = skillName,
            ZipContent = raw.Body,
            Md5 = raw.GetHeader(AiConstants.Skill.HeaderSkillMd5),
            Version = raw.GetHeader(AiConstants.Skill.HeaderSkillResolvedVersion)
        };
    }

    private async Task PostSkillActionAsync(
        string action, string skillName, string version,
        (string Name, string? Value) extra = default, CancellationToken cancellationToken = default)
    {
        ValidateSkillName(skillName);

        var parameters = new Dictionary<string, string?>
        {
            { "skillName", skillName },
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
                        await PollSkillAsync(key, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error polling skill subscription {Key}", key);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in skill subscription polling");
            }
        }
    }

    private async Task PollSkillAsync(string key, CancellationToken cancellationToken)
    {
        var (skillName, version, label) = ParseKey(key);
        var cached = _cache.TryGetValue(key, out var existing) ? existing : null;

        // Conditional request: only md5 is sent, so the server returns 304 when unchanged
        var current = await DownloadSkillZipInternalAsync(skillName, version, label, cached?.Md5, cancellationToken);
        if (current == null)
        {
            return;
        }

        _cache[key] = current;
        NotifyListeners(key, current);
    }

    private void NotifyListeners(string key, SkillPackage package)
    {
        List<AbstractNacosSkillListener> listeners;
        lock (_listenerLock)
        {
            listeners = _listeners.TryGetValue(key, out var set) ? set.ToList() : new List<AbstractNacosSkillListener>();
        }

        var evt = new NacosSkillEvent(package.SkillName ?? string.Empty, package.ZipContent, package.Md5, package.Version);
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

    internal static string BuildKey(string skillName, string? version, string? label)
    {
        return $"{skillName}@@{version ?? string.Empty}@@{label ?? string.Empty}";
    }

    private static (string SkillName, string? Version, string? Label) ParseKey(string key)
    {
        var parts = key.Split("@@");
        return (
            parts[0],
            parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : null,
            parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) ? parts[2] : null);
    }

    private static void ValidateSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            throw new NacosException(NacosException.InvalidParam, "skillName is required");
        }
    }

    private static void ValidateSkill(Skill skill)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.Name))
        {
            throw new NacosException(NacosException.InvalidParam, "skill.Name is required");
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
