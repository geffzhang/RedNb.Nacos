using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Grpc.Ai;
using RedNb.Nacos.Grpc.Lock;
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

    [Fact]
    public void PushAckResponse_SerializesAsWireAck()
    {
        // The bi-stream ack every "…Request" push must produce. Server-side
        // fuzzy watch waits for this ack before it finishes the initial sync,
        // so the type has to be registered, not anonymous.
        var json = JsonSerializer.Serialize(
            new PushAckResponse { RequestId = "464" }, NacosGrpcJsonOptions.Create());

        json.Should().Be("""{"success":true,"requestId":"464"}""");
    }

    [Fact]
    public void PushAckResponse_OmitsMissingRequestId()
    {
        var json = JsonSerializer.Serialize(new PushAckResponse(), NacosGrpcJsonOptions.Create());

        json.Should().Be("""{"success":true}""");
    }

    [Fact]
    public void LockOperationRequest_UsesJavaClientWireNames()
    {
        var json = JsonSerializer.Serialize(
            new LockOperationRequest
            {
                LockOperationEnum = "ACQUIRE",
                LockInstance = new LockOperationInstance
                {
                    Key = "public@@k",
                    ExpiredTime = 10000,
                    LockType = "NACOS_LOCK"
                }
            },
            NacosGrpcJsonOptions.Create());

        json.Should().Be(
            """{"lockOperationEnum":"ACQUIRE","lockInstance":{"key":"public@@k","expiredTime":10000,"lockType":"NACOS_LOCK"}}""");
        json.Should().NotContain("\"expireTime\"");
    }

    [Fact]
    public void LockOperationInstance_KeepsExtensionParams()
    {
        var instance = new LockOperationInstance { Key = "k", LockType = "NACOS_LOCK" };
        instance.Params = new Dictionary<string, object> { ["attempt"] = 2, ["region"] = "cn" };

        var json = JsonSerializer.Serialize(instance, NacosGrpcJsonOptions.Create());

        json.Should().Be("""{"key":"k","expiredTime":0,"lockType":"NACOS_LOCK","params":{"attempt":2,"region":"cn"}}""");
    }

    [Fact]
    public void LockOperationResponse_DeserializesCamelCase()
    {
        var response = JsonSerializer.Deserialize<LockOperationResponse>(
            """{"resultCode":200,"errorCode":0,"message":null,"result":true}""",
            NacosGrpcJsonOptions.Create())!;

        response.ResultCode.Should().Be(200);
        response.Result.Should().BeTrue();
        response.Message.Should().BeNull();
    }
}
