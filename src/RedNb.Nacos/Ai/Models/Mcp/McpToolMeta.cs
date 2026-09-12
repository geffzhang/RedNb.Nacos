using System.Text.Json.Serialization;

namespace RedNb.Nacos.Ai.Models.Mcp;

/// <summary>
/// MCP tool metadata.
/// </summary>
public class McpToolMeta
{
    /// <summary>
    /// Gets or sets whether the tool is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    /// <summary>
    /// Gets or sets custom metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
