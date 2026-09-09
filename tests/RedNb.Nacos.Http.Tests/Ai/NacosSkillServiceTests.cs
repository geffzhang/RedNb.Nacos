using FluentAssertions;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai.Model.Skills;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Ai;

/// <summary>
/// Tests for the Skill operations of the AI service using WireMock.
/// </summary>
public class NacosSkillServiceTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly NacosClientOptions _options;
    private readonly NacosFactory _factory;

    public NacosSkillServiceTests()
    {
        _server = WireMockServer.Start();
        _options = new NacosClientOptions
        {
            ServerAddresses = $"localhost:{_server.Port}"
        };
        _factory = new NacosFactory();
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task DownloadSkillZipAsync_ReturnsZipBytesAndHeaders()
    {
        // Arrange
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3 };

        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/skills")
                .WithParam("name", "doc-writer")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("X-Nacos-Skill-Md5", "skillmd5")
                .WithHeader("X-Nacos-Skill-Resolved-Version", "1.0.0")
                .WithBody(zipBytes));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var package = await aiService.DownloadSkillZipAsync("doc-writer");

        // Assert
        package.Should().NotBeNull();
        package!.ZipContent.Should().Equal(zipBytes);
        package.Md5.Should().Be("skillmd5");
        package.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task DownloadSkillZipAsync_NotModified_ReturnsNull()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/skills")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(304));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var package = await aiService.DownloadSkillZipByVersionAsync("doc-writer", "1.0.0");

        // Assert
        package.Should().BeNull();
    }

    [Fact]
    public async Task DownloadSkillZipAsync_NotFound_ReturnsNull()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/skills")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("skill not found"));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var package = await aiService.DownloadSkillZipAsync("missing");

        // Assert
        package.Should().BeNull();
    }

    [Fact]
    public async Task SubscribeSkillAsync_NotifiesListenerWithPackage()
    {
        // Arrange
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/skills")
                .WithParam("name", "doc-writer")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("X-Nacos-Skill-Md5", "m1")
                .WithHeader("X-Nacos-Skill-Resolved-Version", "2.0.0")
                .WithBody(zipBytes));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        var listener = new TestSkillListener();

        // Act
        var package = await aiService.SubscribeSkillAsync("doc-writer", listener);

        // Assert
        package.Should().NotBeNull();
        listener.Events.Should().ContainSingle();
        listener.Events[0].SkillName.Should().Be("doc-writer");
        listener.Events[0].Md5.Should().Be("m1");
        listener.Events[0].Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task SearchSkillsAsync_ReturnsPage()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ai/skills/search")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""
                {
                    "code": 0,
                    "data": {
                        "totalCount": 2,
                        "pageItems": [
                            { "name": "doc-writer", "scope": "public" },
                            { "name": "code-reviewer", "scope": "public" }
                        ]
                    }
                }
                """));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        var page = await aiService.SearchSkillsAsync("writer");

        // Assert
        page.TotalCount.Should().Be(2);
        page.PageItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task UploadSkillZipAsync_SendsMultipartRequest()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/admin/ai/skills/upload")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{ \"code\": 0, \"data\": true }"));

        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        // Act
        await aiService.UploadSkillZipAsync(new byte[] { 1, 2, 3 }, "doc-writer.zip", overwrite: true);

        // Assert
        var requests = _server.FindLogEntries(
            Request.Create().WithPath("/nacos/v3/admin/ai/skills/upload").UsingPost());
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task DownloadSkillZipAsync_EmptyName_ThrowsInvalidParam()
    {
        var aiService = _factory.CreateAiService(_options);
        await using var _ = aiService;

        var act = () => aiService.DownloadSkillZipAsync("");

        await act.Should().ThrowAsync<NacosException>()
            .Where(e => e.ErrorCode == NacosException.InvalidParam);
    }

    private class TestSkillListener : AbstractNacosSkillListener
    {
        public List<NacosSkillEvent> Events { get; } = new();

        public override void OnEvent(NacosSkillEvent @event)
        {
            Events.Add(@event);
        }
    }
}
