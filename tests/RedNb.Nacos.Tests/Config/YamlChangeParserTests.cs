using FluentAssertions;
using RedNb.Nacos.Config.Parser;
using Xunit;

namespace RedNb.Nacos.Tests.Config;

public class YamlChangeParserTests
{
    [Theory]
    [InlineData("  retries: 5\n  <<: *base\n")]
    [InlineData("  <<: *base\n  retries: 5\n")]
    public void ExplicitKeysAlwaysOverrideMergedValues(string mapping)
    {
        var values = ParseAsNew("defaults: &base\n  retries: 3\nservice:\n" + mapping);
        Assert.Equal("5", values["service.retries"]);
    }

    [Fact]
    public void MergeSequenceUsesEarlierMappingFirst()
    {
        var values = ParseAsNew("a: &a {value: first, a: one}\nb: &b {value: second, b: two}\nservice:\n  <<: [*a, *b]\n");
        Assert.Equal("first", values["service.value"]);
        Assert.Equal("one", values["service.a"]);
        Assert.Equal("two", values["service.b"]);
        Assert.DoesNotContain(values.Keys, key => key.Contains("[0]"));
    }

    [Fact]
    public void QuotedMergeKeyIsAnOrdinaryKey()
    {
        var values = ParseAsNew("service:\n  \"<<\": {value: literal}\n");
        Assert.Equal("literal", values["service.<<.value"]);
    }

    [Theory]
    [InlineData("loop: &loop {self: *loop}")]
    [InlineData("loop: &loop {<<: *loop}")]
    public void CyclicAliasesReturnNoPartialConfiguration(string yaml)
        => Assert.Empty(ParseAsNew(yaml));

    [Fact]
    public void ExplicitNullAndEmptyContainersOverrideMergedSubtrees()
    {
        var values = ParseAsNew("base: &base {nested: {x: old}, list: [old]}\nservice:\n  <<: *base\n  nested: null\n  list: []\n");
        Assert.Equal("", values["service.nested"]);
        Assert.DoesNotContain("service.nested.x", values.Keys);
        Assert.DoesNotContain("service.list[0]", values.Keys);
    }
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
