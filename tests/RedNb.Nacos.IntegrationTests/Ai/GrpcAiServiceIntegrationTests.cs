using FluentAssertions;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Model.A2a;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using RedNb.Nacos.GrpcClient;
using RedNb.Nacos.GrpcClient.Ai;
using Xunit;
using Xunit.Abstractions;

namespace RedNb.Nacos.IntegrationTests.Ai;

[Collection("NacosIntegration")]
public class GrpcAiServiceIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IAiService? _grpcAi;
    private IAiService? _httpAi;

    public GrpcAiServiceIntegrationTests(ITestOutputHelper output) { _output = output; }

    public async Task InitializeAsync()
    {
        var options = new NacosClientOptions
        {
            ServerAddresses = NacosServerFixture.ServerAddress,
            ConsoleAddresses = NacosServerFixture.ConsoleAddress,
            Username = NacosServerFixture.Username,
            Password = NacosServerFixture.Password,
            EnableGrpc = true,
            DefaultTimeout = 10000
        };
        _grpcAi = new NacosGrpcFactory().CreateAiService(options);
        if (_grpcAi is NacosGrpcAiService gs)
            await gs.InitializeAsync();

        // Shared HTTP-side service for cleanup. The Nacos 3.2.4 server does not
        // register gRPC handlers for McpServerDeleteRequest / AgentDeleteRequest
        // (only Query/Release/Endpoint/QueryPrompt/Agent are dispatched), so
        // teardown goes through the console-port HTTP admin API instead.
        _httpAi = new NacosFactory().CreateAiService(new NacosClientOptions
        {
            ServerAddresses = NacosServerFixture.ServerAddress,
            ConsoleAddresses = NacosServerFixture.ConsoleAddress,
            Username = NacosServerFixture.Username,
            Password = NacosServerFixture.Password,
            DefaultTimeout = 10000
        });
    }

    public async Task DisposeAsync()
    {
        if (_grpcAi is IAsyncDisposable d) await d.DisposeAsync();
        if (_httpAi is IAsyncDisposable h) await h.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseAndGetMcpServer_ViaGrpc()
    {
        var mcpName = $"grpc-mcp-{Guid.NewGuid():N}";
        var spec = new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        };
        await _grpcAi!.ReleaseMcpServerAsync(spec, toolSpecification: null);
        await Task.Delay(500);
        var got = await _grpcAi.GetMcpServerAsync(mcpName);
        got.Should().NotBeNull();
        got!.Name.Should().Be(mcpName);
        await _httpAi!.DeleteMcpServerAsync(mcpName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task RegisterAndDeregisterMcpEndpoint_ViaGrpc()
    {
        var mcpName = $"grpc-mcp-ep-{Guid.NewGuid():N}";
        await _grpcAi!.ReleaseMcpServerAsync(new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        }, toolSpecification: null);
        await Task.Delay(500);

        await _grpcAi.RegisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100, "1.0.0");
        await Task.Delay(500);
        var got = await _grpcAi.GetMcpServerAsync(mcpName);
        // Backend endpoint attached -> either retrieved or null on race; tolerate
        got.Should().NotBeNull();

        await _grpcAi.DeregisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100);
        await _httpAi!.DeleteMcpServerAsync(mcpName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseAndGetAgentCard_ViaGrpc()
    {
        var agentName = $"grpc-agent-{Guid.NewGuid():N}";
        var card = new AgentCard
        {
            Name = agentName,
            Version = "1.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        };
        await _grpcAi!.ReleaseAgentCardAsync(card);
        await Task.Delay(500);
        var got = await _grpcAi.GetAgentCardAsync(agentName);
        got.Should().NotBeNull();
        got!.Name.Should().Be(agentName);
        await _httpAi!.DeleteAgentAsync(agentName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task CrossChannel_ReleaseViaGrpc_VisibleViaHttpList()
    {
        var mcpName = $"cross-mcp-{Guid.NewGuid():N}";
        await _grpcAi!.ReleaseMcpServerAsync(new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        }, toolSpecification: null);
        await Task.Delay(1000);

        try
        {
            var page = await _httpAi!.ListMcpServersAsync(pageSize: 100);
            page.PageItems.Should().Contain(i => i.Name == mcpName);
        }
        finally
        {
            await _httpAi!.DeleteMcpServerAsync(mcpName);
        }
    }
}
