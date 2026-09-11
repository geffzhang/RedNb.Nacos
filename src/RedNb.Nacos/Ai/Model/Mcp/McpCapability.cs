using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedNb.Nacos.Core.Ai.Model.Mcp;

/// <summary>
/// MCP capability information.
/// </summary>
/// <remarks>
/// Nacos 3.2.4 serializes MCP capabilities as bare tokens (<c>"TOOL"</c>,
/// <c>"PROMPT"</c>, <c>"RESOURCE"</c>) — the server models them as the
/// <c>McpCapability</c> enum, and a server released with a tool specification comes
/// back with <c>"capabilities": ["TOOL"]</c>. This type therefore reads (and, when
/// no description is set, writes) that token form as well as the object form
/// <c>{"name": ..., "description": ...}</c>.
/// </remarks>
[JsonConverter(typeof(McpCapabilityJsonConverter))]
public class McpCapability
{
    /// <summary>
    /// Gets or sets the capability name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the capability description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Converts <see cref="McpCapability"/> to and from the two shapes Nacos uses: a
/// bare capability token (<c>"TOOL"</c>) and the object form
/// <c>{"name": "TOOL", "description": "..."}</c>.
/// </summary>
internal sealed class McpCapabilityJsonConverter : JsonConverter<McpCapability>
{
    /// <inheritdoc />
    public override McpCapability Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new McpCapability { Name = reader.GetString() };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Cannot convert {reader.TokenType} to {nameof(McpCapability)}.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return new McpCapability
        {
            Name = root.TryGetProperty("name", out var name) ? name.GetString() : null,
            Description = root.TryGetProperty("description", out var description) ? description.GetString() : null
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, McpCapability value, JsonSerializerOptions options)
    {
        // The server binds capabilities to its McpCapability enum, so the bare token
        // is the only form it can read back; the object form is kept for capabilities
        // that carry a description.
        if (string.IsNullOrEmpty(value.Description))
        {
            writer.WriteStringValue(value.Name);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WriteString("description", value.Description);
        writer.WriteEndObject();
    }
}
