using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Config;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Naming.Models;
using RedNb.Nacos.Serialization;
using Xunit;

namespace RedNb.Nacos.Tests.Serialization;

public class NacosJsonContextTests
{
    [Fact]
    public void ServiceInfo_RoundtripsThroughContext()
    {
        var options = NacosJsonOptions.Create();
        var original = new ServiceInfo("svc")
        {
            Hosts = new List<Instance> { new() { Ip = "1.2.3.4", Port = 8848 } }
        };

        var json = JsonSerializer.Serialize(original, options);
        var back = JsonSerializer.Deserialize<ServiceInfo>(json, options)!;

        back.Hosts![0].Ip.Should().Be("1.2.3.4");
        back.Hosts![0].Port.Should().Be(8848);
    }

    [Fact]
    public void ConfigInfo_RoundtripsCaseInsensitively()
    {
        var options = NacosJsonOptions.Create();
        var back = JsonSerializer.Deserialize<ConfigInfo>(
            """{"dataid":"d","group":"g","content":"c"}""", options)!;

        back.DataId.Should().Be("d");
        back.Group.Should().Be("g");
        back.Content.Should().Be("c");
    }

    [Fact]
    public void NamingSelector_SerializesAsLowercaseTypeExpression()
    {
        var options = NacosJsonOptions.Create();
        var json = JsonSerializer.Serialize(new NamingSelector { Type = "label", Expression = "a=1" }, options);

        json.Should().Be("""{"type":"label","expression":"a=1"}""");
    }

    [Fact]
    public void UnregisteredCollection_ThrowsNotSupported()
    {
        // InstancesDiffer serializes its HashSet<Instance> logs via .ToList();
        // the strict resolver must reject the unregistered collection type.
        var set = new HashSet<Instance> { new() { Ip = "1.2.3.4", Port = 8848 } };
        var act = () => JsonSerializer.Serialize(set, NacosJsonOptions.Create());
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UnregisteredType_ThrowsNotSupported()
    {
        var options = NacosJsonOptions.Create();
        var act = () => JsonSerializer.Serialize(new { x = 1 }, options);
        act.Should().Throw<NotSupportedException>();
    }
}
