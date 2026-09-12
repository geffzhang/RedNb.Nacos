using FluentAssertions;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai.Listener;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using RedNb.Nacos.GrpcClient.Ai;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Ai;

/// <summary>
/// The Nacos 3.2.4 server registers only 8 AI gRPC handlers. Every other op
/// must fail loudly with ServerNotImplemented (501) BEFORE dispatching —
/// never silently swallow the server's unknown-type response.
/// </summary>
public class NacosGrpcAiServiceUnsupportedTests
{
    private static NacosClientOptions Options => new() { ServerAddresses = "localhost:8848" };

    private static McpServerBasicInfo ValidMcpServer(string name) => new()
    {
        Name = name,
        VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
        Protocol = "stdio"
    };

    [Fact]
    public async Task UnsupportedOps_Throw501_BeforeDispatching()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var operations = new (string Name, Func<Task> Invoke)[]
        {
            ("SubscribeMcpServer", () => service.SubscribeMcpServerAsync("m1", new TestMcpListener())),
            ("DeleteMcpServer", () => service.DeleteMcpServerAsync("m1")),
            ("ListMcpServers", () => service.ListMcpServersAsync()),
            ("RefreshMcpTool", () => service.RefreshMcpToolAsync("m1", "t1")),
            ("GetMcpTool", () => service.GetMcpToolAsync("m1", "t1")),
            ("DeleteMcpTool", () => service.DeleteMcpToolAsync("m1", "t1")),
            ("UpdateMcpTool", () => service.UpdateMcpToolAsync("m1", new McpToolSpec { Name = "t1" })),
            ("SubscribeAgentCard", () => service.SubscribeAgentCardAsync("a1", new TestAgentCardListener())),
            ("DeleteAgent", () => service.DeleteAgentAsync("a1")),
            ("ListAgentCards", () => service.ListAgentCardsAsync()),
        };

        foreach (var op in operations)
        {
            var ex = await Assert.ThrowsAsync<NacosException>(op.Invoke);
            Assert.Equal(NacosException.ServerNotImplemented, ex.ErrorCode);
            fake.Captured.Should().BeEmpty($"{op.Name} must throw before dispatching");
        }
    }

    [Fact]
    public async Task ImportOps_WithNullRequest_KeepInvalidParam()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var ex1 = await Assert.ThrowsAsync<NacosException>(() => service.ValidateImportAsync(null!, CancellationToken.None));
        Assert.Equal(NacosException.InvalidParam, ex1.ErrorCode);

        var ex2 = await Assert.ThrowsAsync<NacosException>(() => service.ImportMcpServersAsync(null!, CancellationToken.None));
        Assert.Equal(NacosException.InvalidParam, ex2.ErrorCode);

        fake.Captured.Should().BeEmpty();
    }

    private sealed class TestMcpListener : AbstractNacosMcpServerListener
    {
        public override void OnEvent(NacosMcpServerEvent evt) { }
    }

    private sealed class TestAgentCardListener : AbstractNacosAgentCardListener
    {
        public override void OnEvent(NacosAgentCardEvent evt) { }
    }
}
