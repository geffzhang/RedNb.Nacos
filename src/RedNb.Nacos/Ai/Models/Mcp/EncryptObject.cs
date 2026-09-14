using System.Text.Json.Serialization;
using RedNb.Nacos.Serialization;

namespace RedNb.Nacos.Ai.Models.Mcp;

/// <summary>
/// Encrypted object for MCP tool specification.
/// </summary>
public class EncryptObject
{
    /// <summary>
    /// Gets or sets the encryption algorithm.
    /// </summary>
    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; set; }

    /// <summary>
    /// Gets or sets the encrypted data.
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    [JsonConverter(typeof(ObjectDictionaryConverter))]
    public Dictionary<string, object>? Metadata { get; set; }
}
