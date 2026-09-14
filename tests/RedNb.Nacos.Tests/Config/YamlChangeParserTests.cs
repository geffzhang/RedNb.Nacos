using FluentAssertions;
using RedNb.Nacos.Config.Parser;
using Xunit;

namespace RedNb.Nacos.Tests.Config;

public class YamlChangeParserTests
{
    private readonly YamlChangeParser _parser = new();

    private Dictionary<string, string> ParseAsNew(string content)
        => _parser.Parse(null, content, "yaml")
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.NewValue!);

    [Fact]
    public void Parse_SimpleScalars_FlattensToStrings()
    {
        var result = ParseAsNew("name: app\nport: 8080\nratio: 1.5\n");

        result.Should().Contain("name", "app");
        result.Should().Contain("port", "8080");
        result.Should().Contain("ratio", "1.5");
    }

    [Fact]
    public void Parse_NestedMap_FlattensWithDots()
    {
        var result = ParseAsNew("server:\n  host: localhost\n  port: 8848\n");

        result.Should().Contain("server.host", "localhost");
        result.Should().Contain("server.port", "8848");
    }

    [Fact]
    public void Parse_List_FlattensWithIndexes()
    {
        var result = ParseAsNew("servers:\n  - a\n  - b\n");

        result.Should().Contain("servers[0]", "a");
        result.Should().Contain("servers[1]", "b");
    }

    [Fact]
    public void Parse_Scalars_RemainStrings()
    {
        // Verified against 2.0.0's reflection-based Deserializer<object>: unquoted
        // scalars stay strings; no type conversion happens for object deserialization.
        var result = ParseAsNew("enabled: true\nquoted: \"true\"\nport: 8080\n");

        result.Should().Contain("enabled", "true");
        result.Should().Contain("quoted", "true");
        result.Should().Contain("port", "8080");
    }

    [Fact]
    public void Parse_NullScalar_BecomesEmptyString()
    {
        var result = ParseAsNew("key: null\nother:\n");

        result.Should().Contain("key", "");
        result.Should().Contain("other", "");
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmpty()
    {
        ParseAsNew("").Should().BeEmpty();
        ParseAsNew("   ").Should().BeEmpty();
    }
}
