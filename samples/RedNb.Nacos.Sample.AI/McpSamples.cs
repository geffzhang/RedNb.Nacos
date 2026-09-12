using Microsoft.Extensions.Logging;
using RedNb.Nacos;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Sample.AI.Listeners;

namespace RedNb.Nacos.Sample.AI;

/// <summary>
/// MCP section of the sample: releases an MCP server (Streamable HTTP protocol
/// metadata) over the HTTP console channel, reads it back, subscribes over the
/// HTTP long-poll channel, and then registers/deregisters a backend endpoint over
/// gRPC — endpoint operations are served by the gRPC channel only on Nacos 3.2.4.
/// The endpoint deregister and the server delete run in a <c>finally</c>, so a
/// failed or interrupted run cleans up after itself too.
/// </summary>
public static class McpSamples
{
    /// <summary>Address the backend endpoint is registered under (loopback: the server is local).</summary>
    private const string EndpointAddress = "127.0.0.1";

    /// <summary>Port the backend endpoint is registered under.</summary>
    private const int EndpointPort = 9100;

    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi,
        ILogger logger,
        CancellationToken ct)
    {
        var mcpName = $"weather-mcp-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var serviceName = $"{mcpName}-svc";
        var released = false;
        var endpointRegistered = false;

        try
        {
            // 1. Release via HTTP (works for reads + releases on Nacos 3.2.4).
            //    The server validates VersionDetail.Version (not the deprecated
            //    Version), and a non-local protocol additionally requires a
            //    RemoteServerConfig service reference plus a matching REF endpoint
            //    specification.
            var spec = new McpServerBasicInfo
            {
                Name = mcpName,
                // The protocol string is Streamable HTTP registry metadata only —
                // nothing here dials the server (the MCP client round trip lives in
                // the integration sections) — and the server's release validation
                // checks name + versionDetail.version, not a protocol whitelist.
                Protocol = AiConstants.Mcp.ProtocolStreamable,
                Description = "Sample weather MCP server released by RedNb.Nacos.Sample.AI",
                VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
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
            var toolSpec = new McpToolSpecification
            {
                Tools = new List<McpTool>
                {
                    new() { Name = "get_weather", Description = "Returns the weather for a city" }
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

            logger.LogInformation("[MCP] releasing {Name}", mcpName);
            var id = await httpAi.ReleaseMcpServerAsync(spec, toolSpec, endpointSpec, ct);
            released = true;
            logger.LogInformation("[MCP] released id={Id}", id);

            // 2. List and get. The console list route requires an explicit search
            //    mode ("accurate" matches mcpName exactly).
            var page = await httpAi.ListMcpServersAsync(
                mcpName: mcpName, search: "accurate", pageNo: 1, pageSize: 10, ct);
            logger.LogInformation("[MCP] list count={Count}", page.TotalCount);

            var detail = await httpAi.GetMcpServerAsync(mcpName, ct);
            if (detail is null)
            {
                return new SampleResult(SampleOutcome.Failed, "GetMcpServer returned null after release");
            }

            logger.LogInformation("[MCP] get protocol={Protocol} version={Version}",
                detail.Protocol, detail.VersionDetail?.Version);

            // 3. Subscribe via HTTP long-poll (works on Nacos 3.2.4)
            var listener = new DemoMcpListener(logger);
            var subscribed = await httpAi.SubscribeMcpServerAsync(mcpName, listener, ct);
            if (subscribed is null)
            {
                return new SampleResult(SampleOutcome.Failed, "SubscribeMcpServer returned null");
            }

            await httpAi.UnsubscribeMcpServerAsync(mcpName, listener, ct);

            // 4. Register an endpoint via gRPC (HTTP throws ServerError on Nacos 3.2.4).
            //    Deregistering it and deleting the server are the cleanup steps in the
            //    finally below, so a failure or an interrupt after the release does not
            //    orphan the server.
            logger.LogInformation("[MCP] registering endpoint via gRPC");
            // gRPC: RegisterMcpServerEndpointAsync has no HTTP console route on Nacos 3.2.4.
            await grpcAi.RegisterMcpServerEndpointAsync(
                mcpName, EndpointAddress, EndpointPort, "1.0.0", ct);
            endpointRegistered = true;

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (NacosException nex) when (
            nex.ErrorCode == NacosException.ServerError &&
            nex.Message.Contains("MCP tool", StringComparison.OrdinalIgnoreCase))
        {
            // MCP tool CRUD is not exposed on Nacos 3.2.4 by either channel — skip rather than fail
            logger.LogWarning("[MCP] skipping — tool CRUD not exposed on Nacos 3.2.4");
            return new SampleResult(SampleOutcome.Skipped,
                "MCP tool CRUD not exposed on Nacos 3.2.4 (ServerError)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MCP] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
        finally
        {
            // Cleanup runs on the success and the failure path alike, so a failed or
            // interrupted run cannot orphan the released server. It gets its own budget (a
            // cancelled caller token must not abort it) and every step is guarded on its own
            // so one failure cannot skip the rest; a cleanup failure is logged rather than
            // rethrown so it cannot mask the outcome the try/catch above produced.
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var cleanupCt = cleanupCts.Token;

            if (endpointRegistered)
            {
                try
                {
                    // gRPC: DeregisterMcpServerEndpointAsync has no HTTP console route on Nacos 3.2.4.
                    await grpcAi.DeregisterMcpServerEndpointAsync(
                        mcpName, EndpointAddress, EndpointPort, cancellationToken: cleanupCt)
                        .WaitAsync(cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[MCP] cleanup: deregistering {Address}:{Port} failed",
                        EndpointAddress, EndpointPort);
                }
            }

            if (released)
            {
                try
                {
                    await httpAi.DeleteMcpServerAsync(mcpName, cancellationToken: cleanupCt)
                        .WaitAsync(cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx, "[MCP] cleanup: deleting {Name} failed", mcpName);
                }
            }
        }
    }
}
