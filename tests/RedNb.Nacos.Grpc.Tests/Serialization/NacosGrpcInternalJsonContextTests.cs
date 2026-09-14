using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Grpc.Ai;
using RedNb.Nacos.Grpc.Serialization;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Serialization;

public class NacosGrpcInternalJsonContextTests
{
    [Fact]
    public void McpServerNotification_RoundtripsThroughCombinedResolver()
    {
        var options = NacosGrpcJsonOptions.Create();
        var notification = new McpServerNotification { McpName = "m", Version = "1" };

        var json = JsonSerializer.Serialize(notification, options);
        var back = JsonSerializer.Deserialize<McpServerNotification>(json, options)!;

        back.McpName.Should().Be("m");
        back.Version.Should().Be("1");
    }

    [Fact]
    public void AgentCardReleaseRequest_SerializesCamelCase()
    {
        var options = NacosGrpcJsonOptions.Create();
        var request = new AgentCardReleaseRequest
        {
            AgentName = "a",
            AgentCard = new AgentCard { Url = "http://a" }
        };

        var json = JsonSerializer.Serialize(request, options);
        json.Should().Contain("\"agentName\":\"a\"").And.Contain("\"agentCard\"");
    }

    [Fact]
    public void AgentEndpoint_RoundtripsDeepCopyShape()
    {
        var options = NacosGrpcJsonOptions.Create();
        var endpoints = new List<AgentEndpoint>
        {
            new() { Address = "1.2.3.4", Port = 8848, Version = "v" }
        };

        var json = JsonSerializer.Serialize(endpoints, options);
        var copy = JsonSerializer.Deserialize<List<AgentEndpoint>>(json, options)!;

        copy.Should().NotBeSameAs(endpoints);
        copy[0].Address.Should().Be("1.2.3.4");
        copy[0].Port.Should().Be(8848);
    }
}
