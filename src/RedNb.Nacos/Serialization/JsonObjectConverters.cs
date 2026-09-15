using System.Text.Json;
using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;

namespace RedNb.Nacos.Serialization;

/// <summary>
/// AOT-safe converter for <c>object</c> members. Mirrors reflection-based STJ:
/// unknown JSON values are kept as <see cref="JsonElement"/>; user-set primitives
/// are written as-is. Never uses reflection.
/// </summary>
public sealed class ObjectValueConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        => JsonObjectConverters.WriteValue(writer, value);
}

/// <summary>
/// AOT-safe converter for <c>Dictionary&lt;string, object&gt;</c> members.
/// </summary>
public sealed class ObjectDictionaryConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var result = new Dictionary<string, object>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.Clone();
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value)
        {
            writer.WritePropertyName(pair.Key);
            JsonObjectConverters.WriteValue(writer, pair.Value);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// AOT-safe converter for <see cref="SecurityScheme"/> (a
/// <see cref="Dictionary{TKey,TValue}"/> of arbitrary values).
/// </summary>
public sealed class SecuritySchemeConverter : JsonConverter<SecurityScheme>
{
    public override SecurityScheme Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var values = new Dictionary<string, object>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        return new SecurityScheme(values);
    }

    public override void Write(Utf8JsonWriter writer, SecurityScheme value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value)
        {
            writer.WritePropertyName(pair.Key);
            JsonObjectConverters.WriteValue(writer, pair.Value);
        }

        writer.WriteEndObject();
    }
}

/// <summary>Shared value writer for the AOT-safe object converters.</summary>
internal static class JsonObjectConverters
{
    public static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                writer.WriteNumberValue(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case float or double or decimal:
                writer.WriteNumberValue(Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case Dictionary<string, object> dictionary:
                writer.WriteStartObject();
                foreach (var pair in dictionary)
                {
                    writer.WritePropertyName(pair.Key);
                    WriteValue(writer, pair.Value);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable<object> items:
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new JsonException($"Unsupported value type '{value.GetType().FullName}' in AOT-safe converter.");
        }
    }
}
