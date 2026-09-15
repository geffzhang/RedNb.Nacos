using FluentAssertions;
using RedNb.Nacos.Http;
using RedNb.Nacos;
using RedNb.Nacos.Ai.Models.AgentSpec;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Ai;

/// <summary>
/// Tests for the AgentSpec operations of the AI service using WireMock.
/// </summary>
public class NacosAgentSpecServiceTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly NacosClientOptions _options;
    private readonly NacosFactory _factory;

    public NacosAgentSpecServiceTests()
    {
        _server = TestHttpServer.Start();
        _options = new NacosClientOptions
        {
            ServerAddresses = $"localhost:{_server.Port}",
            Namespace = TestNamespace
        };
        _factory = new NacosFactory();
    }

    private const string TestNamespace = "test-ns";

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task GetAgentSpecAsync_Success_ReturnsAgentSpec()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/agentspecs")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .WithParam("name", "travel-agent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""
                {
                    "code": 0,
                    "data": {
                        "namespaceId": "public",
                        "name": "travel-agent",
                        "description": "Plans trips",
                        "content": "kind: AgentSpec",
                        "resource": { "prompt": { "name": "prompt", "type": "prompt", "content": "travel/planner" } }
                    }
                }
                """));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var agentSpec = await aiService.GetAgentSpecAsync("travel-agent");

        // Assert
        agentSpec.Should().NotBeNull();
        agentSpec!.Name.Should().Be("travel-agent");
        agentSpec.Content.Should().Be("kind: AgentSpec");
        agentSpec.Resource.Should().ContainKey("prompt");
    }

    [Fact]
    public async Task GetAgentSpecAsync_NotFound_ReturnsNull()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/agentspecs")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("agentspec not found"));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var agentSpec = await aiService.GetAgentSpecAsync("missing");

        // Assert
        agentSpec.Should().BeNull();
    }

    [Fact]
    public async Task SubscribeAgentSpecAsync_NotifiesListenerWithCurrentValue()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/agentspecs")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .WithParam("name", "travel-agent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""
                { "code": 0, "data": { "name": "travel-agent", "content": "kind: AgentSpec" } }
                """));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        var listener = new TestAgentSpecListener();

        // Act
        var current = await aiService.SubscribeAgentSpecAsync("travel-agent", listener);

        // Assert
        current.Should().NotBeNull();
        listener.Events.Should().ContainSingle();
        listener.Events[0].Name.Should().Be("travel-agent");
    }

    [Fact]
    public async Task ListAgentSpecsAsync_ReturnsPage()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/admin/ai/agentspecs/list")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""
                {
                    "code": 0,
                    "data": {
                        "totalCount": 1,
                        "pageItems": [ { "name": "travel-agent", "onlineCnt": 1 } ]
                    }
                }
                """));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var page = await aiService.ListAgentSpecsAsync();

        // Assert
        page.TotalCount.Should().Be(1);
        page.PageItems.Should().ContainSingle(a => a.Name == "travel-agent");
    }

    [Fact]
    public async Task OnlineAgentSpecAsync_PostsToAdminApi()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/admin/ai/agentspecs/online")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{ \"code\": 0, \"data\": true }"));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        await aiService.OnlineAgentSpecAsync("travel-agent", "1.0.0", scope: "public");

        // Assert
        var requests = _server.FindLogEntries(
            Request.Create().WithPath("/nacos/v3/admin/ai/agentspecs/online").UsingPost());
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAgentSpecAsync_EmptyName_ThrowsInvalidParam()
    {
        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        var act = () => aiService.GetAgentSpecAsync("");

        await act.Should().ThrowAsync<NacosException>()
            .Where(e => e.ErrorCode == NacosException.InvalidParam);
    }

    private class TestAgentSpecListener : AbstractNacosAgentSpecListener
    {
        public List<NacosAgentSpecEvent> Events { get; } = new();

        public override void OnEvent(NacosAgentSpecEvent @event)
        {
            Events.Add(@event);
        }
    }
}
