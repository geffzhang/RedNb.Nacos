using System.Text.Json.Serialization;

namespace RedNb.Nacos.Administration;

/// <summary>Supported Nacos v3 namespace administration, separate from runtime discovery.</summary>
public interface IAdministrationService : IAsyncDisposable
{
    Task<IReadOnlyList<NacosNamespace>> ListNamespacesAsync(CancellationToken cancellationToken = default);
    Task CreateNamespaceAsync(string namespaceId, string displayName, string? description = null, CancellationToken cancellationToken = default);
    Task UpdateNamespaceAsync(string namespaceId, string displayName, string? description = null, CancellationToken cancellationToken = default);
    Task DeleteNamespaceAsync(string namespaceId, CancellationToken cancellationToken = default);
}

/// <summary>A namespace returned by the Nacos v3 administration endpoint.</summary>
public sealed class NacosNamespace
{
    [JsonPropertyName("namespace")]
    public string Id { get; set; } = "";
    [JsonPropertyName("namespaceShowName")]
    public string DisplayName { get; set; } = "";
    [JsonPropertyName("namespaceDesc")]
    public string? Description { get; set; }
}
