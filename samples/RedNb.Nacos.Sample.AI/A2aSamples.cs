using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Model.A2a;
using RedNb.Nacos.Sample.AI.Listeners;

namespace RedNb.Nacos.Sample.AI;

/// <summary>
/// A2A section of the sample: releases an agent card and reads it back over the
/// HTTP console channel, then batch-registers and deregisters its endpoints over
/// gRPC — endpoint operations are served by the gRPC channel only on Nacos 3.2.4.
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

            // 2. Batch register endpoints via gRPC (HTTP throws ServerError).
            //    Every endpoint is validated server-side, so Version is required
            //    and must match the released card version.
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

            logger.LogInformation("[A2A] batch registering {Count} endpoints via gRPC", endpoints.Length);
            // gRPC: RegisterAgentEndpointsAsync has no HTTP console route on Nacos 3.2.4.
            await grpcAi.RegisterAgentEndpointsAsync(agentName, endpoints, ct);

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

            // 5. Deregister endpoints (gRPC) and delete (HTTP)
            foreach (var ep in endpoints)
            {
                // gRPC: DeregisterAgentEndpointAsync has no HTTP console route on Nacos 3.2.4.
                await grpcAi.DeregisterAgentEndpointAsync(agentName, ep, ct);
            }

            await httpAi.DeleteAgentAsync(agentName, cancellationToken: ct);

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[A2A] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }
}
