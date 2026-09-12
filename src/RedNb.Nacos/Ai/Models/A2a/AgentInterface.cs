using System.Text.Json.Serialization;

namespace RedNb.Nacos.Ai.Models.A2a;

/// <summary>
/// Agent interface information.
/// </summary>
public class AgentInterface
{
    /// <summary>
    /// Gets or sets the interface URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the transport type (e.g., "JSONRPC", "GRPC", "HTTP+JSON").
    /// </summary>
    [JsonPropertyName("transport")]
    public string? Transport { get; set; }
}
