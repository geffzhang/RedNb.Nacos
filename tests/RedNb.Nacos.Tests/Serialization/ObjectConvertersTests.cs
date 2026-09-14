using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Serialization;
using Xunit;

namespace RedNb.Nacos.Tests.Serialization;

public class ObjectConvertersTests
{
    private sealed class Holder
    {
        [System.Text.Json.Serialization.JsonPropertyName("params")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ObjectDictionaryConverter))]
        public Dictionary<string, object>? Params { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("anything")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ObjectValueConverter))]
        public object? Anything { get; set; }
    }

    [Fact]
    public void ObjectDictionaryConverter_RoundtripsMixedValues()
    {
        var json = """{"params":{"s":"x","i":5,"d":1.5,"b":true,"n":null,"arr":[1,"a"],"obj":{"k":"v"}}}""";

        var holder = JsonSerializer.Deserialize<Holder>(json)!;

        ((JsonElement)holder.Params!["s"]).GetString().Should().Be("x");
        ((JsonElement)holder.Params!["i"]).GetInt32().Should().Be(5);
        ((JsonElement)holder.Params!["d"]).GetDouble().Should().Be(1.5);
        ((JsonElement)holder.Params!["b"]).GetBoolean().Should().Be(true);
        ((JsonElement)holder.Params!["n"]).ValueKind.Should().Be(JsonValueKind.Null);
        ((JsonElement)holder.Params!["arr"]).GetRawText().Should().Be("""[1,"a"]""");
        ((JsonElement)holder.Params!["obj"]).GetRawText().Should().Be("""{"k":"v"}""");

        var back = JsonSerializer.Serialize(holder);
        var reparsed = JsonDocument.Parse(back).RootElement;
        reparsed.GetProperty("params").GetProperty("s").GetString().Should().Be("x");
        reparsed.GetProperty("params").GetProperty("i").GetInt32().Should().Be(5);
        reparsed.GetProperty("params").GetProperty("arr")[1].GetString().Should().Be("a");
    }

    [Fact]
    public void ObjectValueConverter_RoundtripsAnything()
    {
        var holder = JsonSerializer.Deserialize<Holder>("""{"anything":{"deep":[1,2,{"z":null}]}}""")!;
        holder.Anything.Should().BeOfType<JsonElement>();

        var back = JsonDocument.Parse(JsonSerializer.Serialize(holder)).RootElement;
        back.GetProperty("anything").GetProperty("deep")[2].GetProperty("z").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void ObjectDictionaryConverter_WritesUserSetPrimitives()
    {
        var holder = new Holder { Params = new Dictionary<string, object> { ["n"] = 42, ["s"] = "txt", ["f"] = true } };

        var back = JsonDocument.Parse(JsonSerializer.Serialize(holder)).RootElement.GetProperty("params");
        back.GetProperty("n").GetInt32().Should().Be(42);
        back.GetProperty("s").GetString().Should().Be("txt");
        back.GetProperty("f").GetBoolean().Should().Be(true);
    }
}
