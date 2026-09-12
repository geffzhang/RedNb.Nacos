using System.Text.Json.Serialization;

namespace RedNb.Nacos.Ai.Models.A2a;

/// <summary>
/// Metadata for one released version of an agent card, as returned by the
/// console AI version-list endpoint.
/// </summary>
public class AgentVersionInfo
{
    /// <summary>
    /// Gets or sets the version string.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the creation time (ISO 8601).
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update time (ISO 8601).
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this version is the latest one.
    /// </summary>
    [JsonPropertyName("latest")]
    public bool Latest { get; set; }
}
