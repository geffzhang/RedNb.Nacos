using System.Text.Json;
using RedNb.Nacos.Administration;
using RedNb.Nacos.Http.Serialization;
using RedNb.Nacos.Http.Transport;
using RedNb.Nacos.Utils;

namespace RedNb.Nacos.Http.Administration;

/// <summary>Uses authenticated v3 Admin APIs on the server API port.</summary>
public sealed class NacosAdministrationService : IAdministrationService
{
    private const string Path = "/v3/admin/core/namespace";
    private readonly NacosHttpClient _client;
    public NacosAdministrationService(NacosClientOptions options)
    {
        options.Validate();
        _client = new NacosHttpClient(options);
    }
    public async Task<IReadOnlyList<NacosNamespace>> ListNamespacesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.GetAsync(Path + "/list", cancellationToken: cancellationToken);
        var root = Read(response);
        return root.GetProperty("data").Deserialize<List<NacosNamespace>>(NacosHttpJsonOptions.CreateCaseInsensitive()) ?? [];
    }
    public async Task CreateNamespaceAsync(string namespaceId, string displayName, string? description = null, CancellationToken cancellationToken = default)
        => Read(await _client.PostAsync(Path, body: NacosUtils.BuildQueryString(Parameters(namespaceId, displayName, description)), cancellationToken: cancellationToken));
    public async Task UpdateNamespaceAsync(string namespaceId, string displayName, string? description = null, CancellationToken cancellationToken = default)
        => Read(await _client.PutAsync(Path, body: NacosUtils.BuildQueryString(Parameters(namespaceId, displayName, description)), cancellationToken: cancellationToken));
    public async Task DeleteNamespaceAsync(string namespaceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        if (namespaceId == "public") throw new ArgumentException("The public namespace cannot be deleted.", nameof(namespaceId));
        Read(await _client.DeleteAsync(Path, new() { ["namespaceId"] = namespaceId }, cancellationToken: cancellationToken));
    }
    private static Dictionary<string, string?> Parameters(string id, string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new() { ["namespaceId"] = id, ["namespaceName"] = name, ["namespaceDesc"] = description };
    }
    private static JsonElement Read(string? response)
    {
        if (!NacosEnvelope.TryParse(response, out var root)) throw new NacosException(NacosException.ServerError, "Invalid administration response");
        NacosEnvelope.ThrowIfFailed(root, "Namespace administration");
        return root;
    }
    public ValueTask DisposeAsync() { _client.Dispose(); return ValueTask.CompletedTask; }
}
