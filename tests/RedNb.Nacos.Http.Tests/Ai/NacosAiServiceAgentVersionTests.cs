using FluentAssertions;
using RedNb.Nacos.Http.Ai;
using RedNb.Nacos;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Ai;

/// <summary>
/// Tests for agent-version listing on the console AI channel using WireMock.
/// </summary>
public class NacosAiServiceAgentVersionTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly NacosAiService _aiService;

    public NacosAiServiceAgentVersionTests()
    {
        _server = WireMockServer.Start();
        var options = new NacosClientOptions
        {
            ServerAddresses = "",
            ConsoleAddresses = $"localhost:{_server.Port}"
        };
        _aiService = new NacosAiService(options);
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task ListAgentVersionInfosAsync_DeserializesRichObjects()
    {
        _server
            .Given(Request.Create()
                .WithPath("/v3/console/ai/a2a/version/list")
                .WithParam("agentName", "probe-agent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"code":0,"message":"success","data":[{"version":"1.0.0","createdAt":"2026-09-11T11:44:31Z","updatedAt":"2026-09-11T11:44:31Z","latest":true},{"version":"0.9.0","createdAt":"2026-09-10T10:00:00Z","updatedAt":"2026-09-10T10:00:00Z","latest":false}]}"""));

        var infos = await _aiService.ListAgentVersionInfosAsync("probe-agent");

        infos.Should().HaveCount(2);
        infos[0].Version.Should().Be("1.0.0");
        infos[0].Latest.Should().BeTrue();
        infos[0].CreatedAt.Should().Be(DateTimeOffset.Parse("2026-09-11T11:44:31Z"));
        infos[1].Version.Should().Be("0.9.0");
        infos[1].Latest.Should().BeFalse();
    }

    [Fact]
    public async Task ListAgentVersionsAsync_ProjectsVersionStrings()
    {
        _server
            .Given(Request.Create()
                .WithPath("/v3/console/ai/a2a/version/list")
                .WithParam("agentName", "probe-agent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"code":0,"message":"success","data":[{"version":"1.0.0","createdAt":"2026-09-11T11:44:31Z","updatedAt":"2026-09-11T11:44:31Z","latest":true}]}"""));

        var versions = await _aiService.ListAgentVersionsAsync("probe-agent");

        versions.Should().Equal("1.0.0");
    }
}
