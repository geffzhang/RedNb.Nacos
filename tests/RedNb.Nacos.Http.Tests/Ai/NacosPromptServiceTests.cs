using FluentAssertions;
using RedNb.Nacos.Client;
using RedNb.Nacos.Client.Ai;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai.Model.Prompt;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Ai;

/// <summary>
/// Tests for the Prompt operations of the AI service using WireMock.
/// </summary>
public class NacosPromptServiceTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly NacosClientOptions _options;
    private readonly NacosFactory _factory;

    public NacosPromptServiceTests()
    {
        _server = WireMockServer.Start();
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
    public async Task GetPromptAsync_Success_ReturnsPrompt()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/prompt")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .WithParam("promptKey", "code-review")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""
                {
                    "code": 0,
                    "message": "success",
                    "data": {
                        "promptKey": "code-review",
                        "version": "1.2.0",
                        "template": "Review {{language}} code",
                        "md5": "abc123",
                        "variables": [ { "name": "language", "defaultValue": "C#" } ]
                    }
                }
                """));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var prompt = await aiService.GetPromptAsync("code-review");

        // Assert
        prompt.Should().NotBeNull();
        prompt!.PromptKey.Should().Be("code-review");
        prompt.Version.Should().Be("1.2.0");
        prompt.Template.Should().Be("Review {{language}} code");
        prompt.Md5.Should().Be("abc123");
        prompt.Variables.Should().ContainSingle(v => v.Name == "language");
    }

    [Fact]
    public async Task GetPromptAsync_NotFound_ReturnsNull()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/prompt")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("prompt not found"));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var prompt = await aiService.GetPromptAsync("missing");

        // Assert
        prompt.Should().BeNull();
    }

    [Fact]
    public async Task GetPromptByLabelAsync_PassesLabelParameter()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/prompt")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .WithParam("promptKey", "assistant")
                .WithParam("label", "stable")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""
                { "code": 0, "data": { "promptKey": "assistant", "version": "2.0.0", "template": "hi", "md5": "m1" } }
                """));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var prompt = await aiService.GetPromptByLabelAsync("assistant", "stable");

        // Assert
        prompt.Should().NotBeNull();
        prompt!.Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task SubscribePromptAsync_NotifiesListenerWithCurrentValue()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/prompt")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .WithParam("promptKey", "assistant")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""
                { "code": 0, "data": { "promptKey": "assistant", "version": "1.0.0", "template": "t", "md5": "m" } }
                """));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        var listener = new TestPromptListener();

        // Act
        var current = await aiService.SubscribePromptAsync("assistant", listener);

        // Assert
        current.Should().NotBeNull();
        listener.Events.Should().ContainSingle();
        listener.Events[0].PromptKey.Should().Be("assistant");
        listener.Events[0].Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task ListPromptsAsync_ReturnsPage()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/admin/ai/prompt/list")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""
                {
                    "code": 0,
                    "data": {
                        "totalCount": 1,
                        "pageItems": [ { "promptKey": "assistant", "latestVersion": "2.0.0", "onlineCnt": 1 } ]
                    }
                }
                """));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var page = await aiService.ListPromptsAsync();

        // Assert
        page.TotalCount.Should().Be(1);
        page.PageItems.Should().ContainSingle(p => p.PromptKey == "assistant");
    }

    [Fact]
    public async Task PublishPromptAsync_PostsToAdminApi()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/admin/ai/prompt/publish")
                .WithHeader("X-Nacos-Namespace-Id", TestNamespace)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{ \"code\": 0, \"data\": true }"));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        await aiService.PublishPromptAsync("assistant", "1.0.0", updateLatestLabel: true);

        // Assert
        var requests = _server.FindLogEntries(
            Request.Create().WithPath("/nacos/v3/admin/ai/prompt/publish").UsingPost());
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetPromptAsync_EmptyKey_ThrowsInvalidParam()
    {
        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        var act = () => aiService.GetPromptAsync("");

        await act.Should().ThrowAsync<NacosException>()
            .Where(e => e.ErrorCode == NacosException.InvalidParam);
    }

    private class TestPromptListener : AbstractNacosPromptListener
    {
        public List<NacosPromptEvent> Events { get; } = new();

        public override void OnEvent(NacosPromptEvent @event)
        {
            Events.Add(@event);
        }
    }
}
