using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Http.Ai;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Serialization;

public class UserContextHttpTests
{
    [Fact]
    public async Task ClientOptionsResolverIsUsedForNestedApplicationPayload()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/v3/console/ai/a2a").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));
        server.Given(Request.Create().WithPath("/v3/console/ai/a2a").UsingPost())
            .RespondWith(Response.Create().WithBody("{\"code\":0,\"data\":\"ok\"}"));
        var resolver = new RenameUserPayloadResolver();
        await using var client = new NacosAiService(new NacosClientOptions
        {
            ServerAddresses = $"localhost:{server.Port}", ConsoleAddresses = $"localhost:{server.Port}",
            JsonTypeInfoResolver = resolver, DefaultTimeout = 10000
        });
        await client.ReleaseAgentCardAsync(new AgentCard
        {
            Name = "context-test", Version = "1.0.0", ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc", Url = "http://localhost:1234",
            Capabilities = new AgentCapabilities
            {
                Extensions = [new AgentExtension { Uri = "urn:test", Params = new() { ["application"] = new HttpUserPayload { Value = 23 } } }]
            }
        });
        var entry = Assert.Single(server.LogEntries, e => e.RequestMessage?.Method == "POST");
        var body = Assert.IsType<string>(entry.RequestMessage!.Body);
        var card = body.Split('&').Single(p => p.StartsWith("agentCard="))["agentCard=".Length..];
        using var json = JsonDocument.Parse(Uri.UnescapeDataString(card.Replace("+", " ")));
        Assert.Equal(23, json.RootElement.GetProperty("capabilities").GetProperty("extensions")[0]
            .GetProperty("params").GetProperty("application").GetProperty("from_context").GetInt32());
        Assert.True(resolver.Called);
    }
}

public sealed class HttpUserPayload { public int Value { get; set; } }
[JsonSerializable(typeof(HttpUserPayload))]
internal partial class HttpUserContext : JsonSerializerContext { }
internal sealed class RenameUserPayloadResolver : IJsonTypeInfoResolver
{
    public bool Called { get; private set; }
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var info = ((IJsonTypeInfoResolver)HttpUserContext.Default).GetTypeInfo(type, options);
        if (type == typeof(HttpUserPayload) && info != null)
        {
            Called = true; info.Properties[0].Name = "from_context";
        }
        return info;
    }
}
