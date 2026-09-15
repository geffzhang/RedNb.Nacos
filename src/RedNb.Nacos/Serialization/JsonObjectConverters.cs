using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;
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
        => JsonObjectConverters.WriteValue(writer, value, options);
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
            JsonObjectConverters.WriteValue(writer, pair.Value, options);
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
            JsonObjectConverters.WriteValue(writer, pair.Value, options);
        }

        writer.WriteEndObject();
    }
}

/// <summary>Shared value writer for the AOT-safe object converters.</summary>
internal static class JsonObjectConverters
{
    public static void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        => WriteValue(writer, value, options, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options, HashSet<object> active)
    {
        if (writer.CurrentDepth >= (options.MaxDepth == 0 ? 64 : options.MaxDepth))
            throw new JsonException("Maximum JSON depth exceeded.");
        // Preserve registered collection converters and the standard dictionary
        // contract (e.g. ExpandoObject). Only structural BCL containers may use
        // the metadata-free fallback needed for object-valued native arrays.
        if (value is IEnumerable && value is not string && value is not byte[])
        {
            System.Text.Json.Serialization.Metadata.JsonTypeInfo? metadata = null;
            try { metadata = options.GetTypeInfo(value.GetType()); }
            catch (NotSupportedException) when (IsStructuralContainer(value)) { }
            if (metadata != null && (metadata.Kind == System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.None || !IsStructuralContainer(value)))
            {
                JsonSerializer.Serialize(writer, value, metadata);
                return;
            }
        }
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
            case byte v: writer.WriteNumberValue(v); break;
            case sbyte v: writer.WriteNumberValue(v); break;
            case short v: writer.WriteNumberValue(v); break;
            case ushort v: writer.WriteNumberValue(v); break;
            case int v: writer.WriteNumberValue(v); break;
            case uint v: writer.WriteNumberValue(v); break;
            case long v: writer.WriteNumberValue(v); break;
            case ulong v: writer.WriteNumberValue(v); break;
            case float v: writer.WriteNumberValue(v); break;
            case double v: writer.WriteNumberValue(v); break;
            case decimal v: writer.WriteNumberValue(v); break;
            case byte[] bytes: writer.WriteBase64StringValue(bytes); break;
            case char character: writer.WriteStringValue(character.ToString()); break;
            case DateTime date: writer.WriteStringValue(date); break;
            case DateTimeOffset date: writer.WriteStringValue(date); break;
            case Guid guid: writer.WriteStringValue(guid); break;
            case DateOnly date: JsonSerializer.Serialize(writer, date, BuiltInValueContext.Default.DateOnly); break;
            case TimeOnly time: JsonSerializer.Serialize(writer, time, BuiltInValueContext.Default.TimeOnly); break;
            case IDictionary dictionary when dictionary.Keys.Cast<object>().All(key => key is string):
                if (!active.Add(value)) throw new JsonException("A JSON reference cycle was detected.");
                try
                {
                writer.WriteStartObject();
                foreach (DictionaryEntry pair in dictionary)
                {
                    writer.WritePropertyName((string)pair.Key);
                    WriteValue(writer, pair.Value, options, active);
                }
                writer.WriteEndObject();
                }
                finally { active.Remove(value); }
                break;
            case IDictionary:
                JsonSerializer.Serialize(writer, value, options.GetTypeInfo(value.GetType()));
                break;
            case IEnumerable<KeyValuePair<string, object>> pairs when value is IDictionary<string, object> or IReadOnlyDictionary<string, object>:
                if (!active.Add(value)) throw new JsonException("A JSON reference cycle was detected.");
                try
                {
                    writer.WriteStartObject();
                    foreach (var pair in pairs)
                    {
                        writer.WritePropertyName(pair.Key);
                        WriteValue(writer, pair.Value, options, active);
                    }
                    writer.WriteEndObject();
                }
                finally { active.Remove(value); }
                break;
            case IEnumerable items:
                if (!active.Add(value)) throw new JsonException("A JSON reference cycle was detected.");
                try
                {
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    WriteValue(writer, item, options, active);
                }
                writer.WriteEndArray();
                }
                finally { active.Remove(value); }
                break;
            default:
                if (value.GetType() == typeof(object)) { writer.WriteStartObject(); writer.WriteEndObject(); break; }
                var metadata = options.GetTypeInfo(value.GetType());
                JsonSerializer.Serialize(writer, value, metadata);
                break;
        }
    }

    private static bool IsStructuralContainer(object value)
        => value is IDictionary or IDictionary<string, object> or IReadOnlyDictionary<string, object>
           || value.GetType().IsArray
           || value.GetType().Assembly.GetName().Name is "System.Private.CoreLib" or "System.Collections"
               or "System.Collections.Concurrent" or "System.Collections.Immutable" or "System.Linq";
}

[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(TimeOnly))]
internal partial class BuiltInValueContext : JsonSerializerContext { }
