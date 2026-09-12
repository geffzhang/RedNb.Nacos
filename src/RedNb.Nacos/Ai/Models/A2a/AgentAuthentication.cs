using System.Text.Json.Serialization;

namespace RedNb.Nacos.Ai.Models.A2a;

/// <summary>
/// Agent authentication information.
/// </summary>
public class AgentAuthentication
{
    /// <summary>
    /// Gets or sets the authentication schemes.
    /// </summary>
    [JsonPropertyName("schemes")]
    public List<string>? Schemes { get; set; }

    /// <summary>
    /// Gets or sets the credentials.
    /// </summary>
    [JsonPropertyName("credentials")]
    public string? Credentials { get; set; }
}
