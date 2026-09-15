using System.Text.Json.Serialization;

namespace RedNb.Nacos.Naming.Models;

/// <summary>
/// Wire shape of the naming selector parameter (<c>{"type": ..., "expression": ...}</c>).
/// Replaces the anonymous types previously serialized by the Http and Grpc naming services.
/// </summary>
public sealed class NamingSelector
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("expression")]
    public string? Expression { get; set; }
}
