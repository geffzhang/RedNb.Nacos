using Microsoft.Extensions.Logging;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Sample.AI.Listeners;

namespace RedNb.Nacos.Sample.AI;

/// <summary>
/// A2A section of the sample: releases an agent card and reads it back over the
/// HTTP console channel, then batch-registers and deregisters its endpoints over
/// gRPC — endpoint operations are served by the gRPC channel only on Nacos 3.2.4.
/// The endpoint deregisters and the card delete run in a <c>finally</c>, so a
/// failed or interrupted run cleans up after itself too.
/// </summary>
public static class A2aSamples
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi,
        ILogger logger,
        CancellationToken ct)
    {
        var agentName = $"travel-agent-{DateTime.UtcNow:yyyyMMddHHmmss}";
        const string version = "1.0.0";
        var released = false;
        var endpointsRegistered = false;

        // The endpoints are registered inside the try below; the array is declared here
        // so the cleanup in the finally can deregister exactly what was registered.
        var endpoints = new[]
        {
            new AgentEndpoint
            {
                Address = "127.0.0.1",
                Port = 9201,
                Version = version,
                Transport = AiConstants.A2a.TransportHttpJson
            },
            new AgentEndpoint
            {
                Address = "127.0.0.1",
                Port = 9202,
                Version = version,
                Transport = AiConstants.A2a.TransportJsonRpc
            }
        };

        try
        {
            // 1. Release Agent Card via HTTP. The server validates Name, Version
            //    and ProtocolVersion; the SDK defaults the registration type to
            //    SERVICE, so the endpoints below are registered against the card.
            var card = new AgentCard
            {
                Name = agentName,
                Version = version,
                ProtocolVersion = "0.3.7",
                PreferredTransport = "jsonrpc",
                Url = "http://127.0.0.1:9201"
            };

            logger.LogInformation("[A2A] releasing {Name}", agentName);
            await httpAi.ReleaseAgentCardAsync(card, ct);
            released = true;

            // 2. Batch register endpoints via gRPC (HTTP throws ServerError).
            //    Every endpoint is validated server-side, so Version is required
            //    and must match the released card version.
            logger.LogInformation("[A2A] batch registering {Count} endpoints via gRPC", endpoints.Length);
            // gRPC: RegisterAgentEndpointsAsync has no HTTP console route on Nacos 3.2.4.
            await grpcAi.RegisterAgentEndpointsAsync(agentName, endpoints, ct);
            endpointsRegistered = true;

            // 3. List and get. The console list route requires an explicit search
            //    mode ("accurate" matches agentName exactly).
            var page = await httpAi.ListAgentCardsAsync(
                agentName: agentName, search: "accurate", pageNo: 1, pageSize: 10, ct);
            logger.LogInformation("[A2A] list count={Count}", page.TotalCount);

            var detail = await httpAi.GetAgentCardAsync(agentName, ct);
            if (detail is null)
            {
                return new SampleResult(SampleOutcome.Failed, "GetAgentCard returned null after release");
            }

            logger.LogInformation("[A2A] get version={Version} registrationType={RegistrationType}",
                detail.Version, detail.RegistrationType);

            // 4. Subscribe + unsubscribe
            var listener = new DemoAgentCardListener(logger);
            var subscribed = await httpAi.SubscribeAgentCardAsync(agentName, listener, ct);
            if (subscribed is null)
            {
                return new SampleResult(SampleOutcome.Failed, "SubscribeAgentCard returned null");
            }

            await httpAi.UnsubscribeAgentCardAsync(agentName, listener, ct);

            // The endpoint deregisters (gRPC) and the card delete (HTTP) are the cleanup
            // steps in the finally below, so a failure or an interrupt after the release
            // does not orphan the card.
            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[A2A] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
        finally
        {
            // Cleanup runs on the success and the failure path alike, so a failed or
            // interrupted run cannot orphan the released card. It gets its own budget (a
            // cancelled caller token must not abort it) and every step is guarded on its own
            // so one failure cannot skip the rest; a cleanup failure is logged rather than
            // rethrown so it cannot mask the outcome the try/catch above produced.
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var cleanupCt = cleanupCts.Token;

            if (endpointsRegistered)
            {
                foreach (var ep in endpoints)
                {
                    try
                    {
                        // gRPC: DeregisterAgentEndpointAsync has no HTTP console route on Nacos 3.2.4.
                        await grpcAi.DeregisterAgentEndpointAsync(agentName, ep, cleanupCt)
                            .WaitAsync(cleanupCt);
                    }
                    catch (Exception cleanupEx)
                    {
                        logger.LogWarning(cleanupEx,
                            "[A2A] cleanup: deregistering {Address}:{Port} failed",
                            ep.Address, ep.Port);
                    }
                }
            }

            if (released)
            {
                try
                {
                    await httpAi.DeleteAgentAsync(agentName, cancellationToken: cleanupCt)
                        .WaitAsync(cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx, "[A2A] cleanup: deleting {Name} failed", agentName);
                }
            }
        }
    }
}
