using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedNb.Nacos.Serialization;
using Xunit;

namespace RedNb.Nacos.Tests.Serialization;

public class UserCollectionTests
{
    [Fact]
    public void StrictSdkDictionaryRetainsNestedUnregisteredPrimitiveArrays()
    {
        var options = NacosJsonOptions.Create(allowReflectionFallback: false);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
            new ObjectValueConverter().Write(writer, new Dictionary<string, object> { ["items"] = new[] { 1, 2 } }, options);
        Assert.Equal("{\"items\":[1,2]}", Encoding.UTF8.GetString(buffer.ToArray()));
    }

    [Fact]
    public void UnregisteredApplicationCollectionFailsInStrictMode()
    {
        var options = NacosJsonOptions.Create(allowReflectionFallback: false);
        using var buffer = new MemoryStream();
        using var writer = new Utf8JsonWriter(buffer);
        var error = Assert.Throws<NotSupportedException>(() => new ObjectValueConverter().Write(writer, new CustomCollection { 1 }, options));
        Assert.Contains(nameof(CustomCollection), error.Message);
        Assert.Contains("JsonTypeInfoResolver", error.Message);
    }

    [Fact]
    public void RegisteredCollectionConverterIsNotBypassedByEnumerableFallback()
    {
        var options = NacosJsonOptions.Create(user: CollectionContext.Default, allowReflectionFallback: false);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
            new ObjectValueConverter().Write(writer, new CustomCollection { 1, 2 }, options);
        Assert.Equal("{\"count\":2}", Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
[JsonConverter(typeof(CustomCollectionConverter))]
public sealed class CustomCollection : List<int> { }
public sealed class CustomCollectionConverter : JsonConverter<CustomCollection>
{
    public override CustomCollection? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
    public override void Write(Utf8JsonWriter writer, CustomCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WriteNumber("count", value.Count); writer.WriteEndObject();
    }
}
[JsonSerializable(typeof(CustomCollection))]
internal partial class CollectionContext : JsonSerializerContext { }
