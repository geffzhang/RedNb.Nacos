using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;
using RedNb.Nacos.Grpc.Serialization;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Serialization;

public class NacosGrpcJsonContextTests
{
    [Fact]
    public void ConfigPublishRequest_SerializesCamelCaseAndOmitsNulls()
    {
        var options = NacosGrpcJsonOptions.Create();
        var request = new ConfigPublishRequest
        {
            RequestId = "r1",
            DataId = "d",
            Group = "g",
            Content = "c"
        };

        var json = JsonSerializer.Serialize(request, options);

        json.Should().Contain("\"requestId\":\"r1\"").And.Contain("\"dataId\":\"d\"");
        json.Should().NotContain("\"RequestId\"");
        json.Should().NotContain("\"tenant\"");
    }

    [Fact]
    public void ConfigChangeNotifyRequest_RoundtripsCaseInsensitively()
    {
        var options = NacosGrpcJsonOptions.Create();
        var back = JsonSerializer.Deserialize<ConfigChangeNotifyRequest>(
            """{"RequestId":"r","dataId":"d","group":"g"}""", options)!;

        back.RequestId.Should().Be("r");
        back.DataId.Should().Be("d");
    }

    [Fact]
    public void NamingServiceInfo_RoundtripsThroughContext()
    {
        var options = NacosGrpcJsonOptions.Create();
        var original = new NamingServiceInfo
        {
            Name = "n",
            GroupName = "g",
            Hosts = new List<NamingInstance> { new() { Ip = "1.2.3.4", Port = 8848 } }
        };

        var back = JsonSerializer.Deserialize<NamingServiceInfo>(
            JsonSerializer.Serialize(original, options), options)!;

        back.Name.Should().Be("n");
        back.Hosts![0].Ip.Should().Be("1.2.3.4");
    }

    [Fact]
    public void UnregisteredType_ThrowsNotSupported()
    {
        var act = () => JsonSerializer.Serialize(new { x = 1 }, NacosGrpcJsonOptions.Create());
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UnregisteredType_GetTypeInfoThrowsNotSupported()
    {
        // JIT parity with the AOT smoke: metadata resolution itself refuses
        // unregistered types instead of falling back to reflection.
        var act = () => NacosGrpcJsonOptions.Create().GetTypeInfo(typeof(UnregisteredType));
        act.Should().Throw<NotSupportedException>();
    }

    private class UnregisteredType
    {
        public int X { get; set; }
    }
}
