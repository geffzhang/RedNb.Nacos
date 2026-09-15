using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedNb.Nacos.Serialization;

/// <summary>Retains the string API while accepting numeric server timestamps.</summary>
public sealed class TimestampStringConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString();
        if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected timestamp string or number.");
        using var value = JsonDocument.ParseValue(ref reader);
        return value.RootElement.GetRawText();
    }
    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
