using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Grpc.Ai;
using Xunit;

namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class AiFacadeTests
{
    [Fact]
    public async Task OneClientCreatesOverHttpRegistersOverGrpcAndDeletesOverHttp()
    {
        var options = ConfigReliabilityTests.Options();
        options.Namespace = "public";
        await using var client = new NacosAiClient(options);
        var name = "audit-facade-" + Guid.NewGuid().ToString("N");
        await client.ReleaseAgentCardAsync(new AgentCard
        {
            Name = name,
            Version = "1.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        });
        try
        {
            await ConfigReliabilityTests.Eventually(async () => await client.GetAgentCardAsync(name) != null);
            await client.RegisterAgentEndpointsAsync(name, [
                new AgentEndpoint { Version = "1.0.0", Address = "127.0.0.1", Port = 19401, Transport = AiConstants.A2a.TransportJsonRpc },
                new AgentEndpoint { Version = "1.0.0", Address = "127.0.0.1", Port = 19402, Transport = AiConstants.A2a.TransportJsonRpc }
            ]);
            Assert.Contains(await client.ListAgentVersionsAsync(name), version => version == "1.0.0");
            await client.DeregisterAgentEndpointAsync(name, "1.0.0", "127.0.0.1", 19401);
            await client.DeregisterAgentEndpointAsync(name, "1.0.0", "127.0.0.1", 19402);
        }
        finally { await client.DeleteAgentAsync(name); }
        await ConfigReliabilityTests.Eventually(async () => await client.GetAgentCardAsync(name) == null);
    }
}
