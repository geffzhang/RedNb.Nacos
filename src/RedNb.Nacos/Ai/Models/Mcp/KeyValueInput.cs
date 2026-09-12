using System.Text.Json.Serialization;

namespace RedNb.Nacos.Ai.Models.Mcp;

/// <summary>
/// Key-value input for MCP endpoints.
/// </summary>
public class KeyValueInput
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
