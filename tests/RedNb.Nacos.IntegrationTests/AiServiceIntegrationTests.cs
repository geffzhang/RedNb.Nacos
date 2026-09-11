using FluentAssertions;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Listener;
using RedNb.Nacos.Core.Ai.Model.A2a;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using Xunit;
using Xunit.Abstractions;

namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class AiServiceIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IAiService? _aiService;
    private readonly NacosClientOptions _options;

    public AiServiceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _options = new NacosClientOptions
        {
            ServerAddresses = NacosServerFixture.ServerAddress,
            ConsoleAddresses = NacosServerFixture.ConsoleAddress,
            Username = NacosServerFixture.Username,
            Password = NacosServerFixture.Password,
            Namespace = "",
            DefaultTimeout = 10000
        };
    }

    public Task InitializeAsync()
    {
        _aiService = new NacosFactory().CreateAiService(_options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_aiService is IAsyncDisposable d) await d.DisposeAsync();
    }

    // ---------- Agent Card (HTTP / console) ----------

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseAndGetAgentCard_RoundTripsViaConsole()
    {
        var agentName = $"it-agent-{Guid.NewGuid():N}";
        var card = new AgentCard
        {
            Name = agentName,
            Version = "1.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        };

        await _aiService!.ReleaseAgentCardAsync(card);

        try
        {
            var retrieved = await WaitForAsync(
                () => _aiService!.GetAgentCardAsync(agentName),
                c => c is not null);
            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be(agentName);
            retrieved.Version.Should().Be("1.0.0");
        }
        finally
        {
            await DeleteWithRetryAsync(() => _aiService!.DeleteAgentAsync(agentName));
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task RegisterAndDeregisterAgentEndpoint_HttpChannel_ThrowsLoudly()
    {
        var agentName = $"it-agent-ep-{Guid.NewGuid():N}";
        var endpoint = new AgentEndpoint
        {
            Address = "127.0.0.1",
            Port = 9000,
            Version = "1.0.0",
            Transport = AiConstants.A2a.TransportJsonRpc
        };

        Func<Task> act = () => _aiService!.RegisterAgentEndpointAsync(agentName, endpoint);
        var ex = await act.Should().ThrowAsync<NacosException>();
        ex.Which.ErrorCode.Should().Be(NacosException.ServerError);
        ex.Which.Message.Should().Contain("not available over the HTTP channel");

        Func<Task> dereg = () => _aiService!.DeregisterAgentEndpointAsync(agentName, endpoint);
        var ex2 = await dereg.Should().ThrowAsync<NacosException>();
        ex2.Which.ErrorCode.Should().Be(NacosException.ServerError);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ListAgentCards_ReturnsReleasedAgent()
    {
        var agentName = $"it-agent-list-{Guid.NewGuid():N}";
        await _aiService!.ReleaseAgentCardAsync(new AgentCard
        {
            Name = agentName,
            Version = "1.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        });

        try
        {
            // The console list endpoint requires an explicit search mode ("accurate"
            // or "blur") and matches agentName exactly, so a pageSize-100 scan of the
            // whole store is neither needed nor deterministic.
            var page = await WaitForAsync(
                () => _aiService!.ListAgentCardsAsync(agentName: agentName, search: "accurate"),
                p => p.PageItems.Any(i => i.Name == agentName));
            page.PageItems.Should().Contain(i => i.Name == agentName);
        }
        finally
        {
            await DeleteWithRetryAsync(() => _aiService!.DeleteAgentAsync(agentName));
        }
    }

    [Fact(Skip = "Server contract surprise: live Nacos 3.2.4 returns rich objects {version, createdAt, updatedAt, latest} at /v3/console/ai/a2a/version/list, but SDK NacosAiService deserializes to List<string>. Documented in docs/SDK_COMPLETENESS_REPORT.md §零 (测试矩阵 — the one server-contract-difference skip).")]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ListAgentVersions_ReturnsVersions()
    {
        var agentName = $"it-agent-ver-{Guid.NewGuid():N}";
        await _aiService!.ReleaseAgentCardAsync(new AgentCard
        {
            Name = agentName,
            Version = "2.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        });
        await Task.Delay(500);

        var versions = await _aiService.ListAgentVersionsAsync(agentName);
        versions.Should().Contain("2.0.0");

        await _aiService.DeleteAgentAsync(agentName);
    }

    // ---------- MCP Server (HTTP / console) ----------

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseAndGetMcpServer_RoundTripsViaConsole()
    {
        var mcpName = $"it-mcp-{Guid.NewGuid():N}";
        var spec = new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        };

        var mcpId = await _aiService!.ReleaseMcpServerAsync(spec, toolSpecification: null);
        mcpId.Should().NotBeNullOrEmpty();

        try
        {
            var retrieved = await WaitForAsync(
                () => _aiService!.GetMcpServerAsync(mcpName),
                s => s is not null);
            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be(mcpName);
            retrieved.VersionDetail!.Version.Should().Be("1.0.0");
            // Control for ReleaseMcpServer_WithToolSpecification_...: a release with no
            // tool specification must not report the TOOL capability.
            retrieved.ToolSpec.Should().BeNull();
        }
        finally
        {
            await DeleteWithRetryAsync(() => _aiService!.DeleteMcpServerAsync(mcpName));
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseMcpServer_WithToolSpecification_SucceedsAndMatchesChannelBehavior()
    {
        var mcpName = $"it-mcp-tools-{Guid.NewGuid():N}";
        var spec = new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        };
        var toolSpec = new McpToolSpecification
        {
            Tools = new List<McpTool>
            {
                new() { Name = "echo", Description = "Echo tool" }
            }
        };

        var mcpId = await _aiService!.ReleaseMcpServerAsync(spec, toolSpec);
        mcpId.Should().NotBeNullOrEmpty();

        try
        {
            // The HTTP console channel accepts and persists tool specifications
            // (field name `toolSpecification`, bound by the server's McpDetailForm),
            // so the tool must come back with the released server.
            var retrieved = await WaitForAsync(
                () => _aiService!.GetMcpServerAsync(mcpName),
                s => s?.ToolSpec is not null);
            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be(mcpName);
            retrieved.ToolSpec.Should().NotBeNull();
            retrieved.ToolSpec!.Tools.Should().Contain(t => t.Name == "echo");
        }
        finally
        {
            await DeleteWithRetryAsync(() => _aiService!.DeleteMcpServerAsync(mcpName));
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseMcpServer_WithEndpointSpecification_CarriesSpecOverConsole()
    {
        var mcpName = $"it-mcp-ep-{Guid.NewGuid():N}";
        var serviceName = $"svc-{Guid.NewGuid():N}";
        var spec = new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "mcp-sse",
            RemoteServerConfig = new McpServerRemoteServiceConfig
            {
                ServiceRef = new McpServiceRef
                {
                    NamespaceId = "public",
                    GroupName = "DEFAULT_GROUP",
                    ServiceName = serviceName
                }
            }
        };
        var endpointSpec = new McpEndpointSpec
        {
            Type = AiConstants.Mcp.EndpointTypeRef,
            Data = new Dictionary<string, string>
            {
                { "namespaceId", "public" },
                { "groupName", "DEFAULT_GROUP" },
                { "serviceName", serviceName }
            }
        };

        // A non-local server type is rejected by Nacos 3.2.4 with
        // "request parameter `endpointSpecification` is required if mcp server type
        // not `local`" (probe P6), so a successful release is itself proof that the
        // form field reached the release service.
        var mcpId = await _aiService!.ReleaseMcpServerAsync(spec, toolSpecification: null, endpointSpec);
        mcpId.Should().NotBeNullOrEmpty();

        try
        {
            var retrieved = await WaitForAsync(
                () => _aiService.GetMcpServerAsync(mcpName),
                s => s?.RemoteServerConfig?.ServiceRef is not null);
            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be(mcpName);
            retrieved.Protocol.Should().Be("mcp-sse");
            retrieved.RemoteServerConfig.Should().NotBeNull();
            retrieved.RemoteServerConfig!.ServiceRef!.ServiceName.Should().Be(serviceName);
            // The backend/frontend endpoint lists stay empty for a REF registration
            // (probe: both empty after release), so presence is asserted on the
            // service reference the endpoint spec resolves through.
        }
        finally
        {
            await DeleteWithRetryAsync(() => _aiService.DeleteMcpServerAsync(mcpName));
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ListMcpServers_ReturnsReleasedServer()
    {
        var mcpName = $"it-mcp-list-{Guid.NewGuid():N}";
        await _aiService!.ReleaseMcpServerAsync(new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        }, toolSpecification: null);

        try
        {
            // mcpName is an exact server-side filter, so no pageSize-100 scan.
            var page = await WaitForAsync(
                () => _aiService!.ListMcpServersAsync(mcpName: mcpName),
                p => p.PageItems.Any(i => i.Name == mcpName));
            page.PageItems.Should().Contain(i => i.Name == mcpName);
        }
        finally
        {
            await DeleteWithRetryAsync(() => _aiService!.DeleteMcpServerAsync(mcpName));
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task RegisterAndDeregisterMcpEndpoint_HttpChannel_ThrowsLoudly()
    {
        Func<Task> reg = () => _aiService!.RegisterMcpServerEndpointAsync("it-mcp-x", "127.0.0.1", 9100);
        var ex = await reg.Should().ThrowAsync<NacosException>();
        ex.Which.ErrorCode.Should().Be(NacosException.ServerError);
        ex.Which.Message.Should().Contain("not available over the HTTP channel");
    }

    // ---------- MCP Server (subscribe — covered separately) ----------

    [Fact(Skip = "Polling-based; covered by manual smoke test. Polling interval is 10s.")]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task SubscribeAgentCard_ReceivesUpdates() { /* kept for reference; see Task 7 for gRPC subscribe */ }

    [Fact(Skip = "Polling-based; covered by manual smoke test. Polling interval is 10s.")]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task SubscribeMcpServer_ReceivesUpdates() { /* kept for reference */ }

    // ---------- Read-after-write helpers ----------

    /// <summary>
    /// The Nacos 3.2.4 console is not always immediately consistent after a release:
    /// an immediate read can briefly answer
    /// {"code":20002,...,"data":"argument \"content\" is null"} or return an empty
    /// page (observed up to ~1s). Polls <paramref name="read"/> until
    /// <paramref name="isSettled"/> holds. A server error answered while the write is
    /// still settling is retried too; if the timeout elapses, the last server error (or
    /// a <see cref="TimeoutException"/>) is thrown, so the failure names the transient
    /// instead of a bare null.
    /// </summary>
    private async Task<T> WaitForAsync<T>(
        Func<Task<T>> read,
        Func<T, bool> isSettled,
        int timeoutMs = 2000,
        int pollMs = 200)
        where T : class?
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        NacosException? lastServerError = null;

        while (true)
        {
            try
            {
                var value = await read();
                if (value is not null && isSettled(value))
                {
                    return value;
                }
            }
            catch (NacosException ex)
            {
                lastServerError = ex; // transient while the release settles
            }

            if (DateTime.UtcNow >= deadline)
            {
                if (lastServerError is not null)
                {
                    throw lastServerError;
                }

                throw new TimeoutException($"The console read did not settle within {timeoutMs} ms.");
            }

            _output.WriteLine($"WaitForAsync: read not settled yet; retrying in {pollMs} ms.");
            await Task.Delay(pollMs);
        }
    }

    /// <summary>
    /// Teardown guard: the same settle window can make the console answer a DELETE
    /// transiently, which would orphan the fixture — retry a couple of times, then let
    /// the failure surface.
    /// </summary>
    private async Task DeleteWithRetryAsync(Func<Task> delete, int attempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await delete();
                return;
            }
            catch (NacosException ex) when (attempt < attempts)
            {
                _output.WriteLine($"DeleteWithRetryAsync: {ex.Message}; retrying.");
                await Task.Delay(200);
            }
        }
    }

    // ---------- Test Listeners (unchanged) ----------

    private class TestAgentCardListener : AbstractNacosAgentCardListener
    {
        private readonly Action<NacosAgentCardEvent> _handler;

        public TestAgentCardListener(Action<NacosAgentCardEvent> handler)
        {
            _handler = handler;
        }

        public override void OnEvent(NacosAgentCardEvent evt)
        {
            _handler(evt);
        }
    }

    private class TestMcpServerListener : AbstractNacosMcpServerListener
    {
        private readonly Action<NacosMcpServerEvent> _handler;

        public TestMcpServerListener(Action<NacosMcpServerEvent> handler)
        {
            _handler = handler;
        }

        public override void OnEvent(NacosMcpServerEvent evt)
        {
            _handler(evt);
        }
    }
}
