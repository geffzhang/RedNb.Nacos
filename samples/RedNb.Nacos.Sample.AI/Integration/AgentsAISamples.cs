using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using RedNb.Nacos.Sample.AI.Chat;
using A2ACard = A2A.AgentCard;
using A2AClientOptions = A2A.A2AClientOptions;
using NacosAgentCapabilities = RedNb.Nacos.Core.Ai.Model.A2a.AgentCapabilities;
using NacosAgentCard = RedNb.Nacos.Core.Ai.Model.A2a.AgentCard;
using NacosAgentCardDetailInfo = RedNb.Nacos.Core.Ai.Model.A2a.AgentCardDetailInfo;
using NacosAgentSkill = RedNb.Nacos.Core.Ai.Model.A2a.AgentSkill;

namespace RedNb.Nacos.Sample.AI.Integration;

/// <summary>
/// Microsoft.Agents.AI section of the sample: the three Nacos-backed building blocks an agent needs,
/// each with the registry as the source of truth for the endpoint or the card.
/// <list type="number">
/// <item><description>
/// <b>MCP</b> — hosts an MCP server in this process, releases it to the Nacos MCP registry and
/// registers the live endpoint (HTTP for the release, gRPC for the endpoint on Nacos 3.2.4), reads
/// the endpoint back <em>from Nacos</em>, dials it with the ModelContextProtocol client, and hands
/// the discovered tools to a <see cref="ChatClientAgent"/>. Self-contained: the server is hosted
/// here, so an empty tool list is a failure, not a skip.
/// </description></item>
/// <item><description>
/// <b>Skill</b> — an inline, code-defined skill (<see cref="AgentInlineSkill"/>) fed to the agent
/// through <see cref="AgentSkillsProvider"/>. Fully offline: no Nacos round trip and no model
/// credentials are involved.
/// </description></item>
/// <item><description>
/// <b>A2A</b> — releases a throwaway agent card to the Nacos A2A registry, reads the card JSON
/// back (the discovery moment), maps the registry's 0.3.7 card fields onto the A2A 1.0 protocol
/// card type, and builds an <see cref="AIAgent"/> plus a protocol client from it. The card is
/// parsed and surfaced, not called: a live A2A round trip needs a hosted A2A agent, and the
/// hosting packages (<c>Microsoft.Agents.AI.Hosting.A2A*</c>) are outside this sample's pinned
/// package set, so this demo stops at "the registry's card drives the agent/client construction".
/// </description></item>
/// </list>
/// Every sub-demo runs in its own <c>try/catch</c> and logs its own outcome: a crash or a failed
/// check is collected as a failure and the remaining sub-demos still run, so one broken demo cannot
/// mask the others. No sub-demo reports <see cref="SampleOutcome.Skipped"/>: each one hosts or
/// releases what it needs, so a missing registration is a failure rather than a skip — and the
/// section reports <see cref="SampleOutcome.Ok"/> only when no sub-demo failed.
/// </summary>
public static class AgentsAISamples
{
    /// <summary>Name of the tool hosted by the in-process MCP server and released to Nacos.</summary>
    private const string ToolName = "get_weather";

    /// <summary>Description of that tool, carried by both the host and the Nacos tool spec.</summary>
    private const string ToolDescription = "Returns the weather for a city";

    /// <summary>Address the MCP backend endpoint is registered under (loopback: the server is local).</summary>
    private const string McpEndpointAddress = "127.0.0.1";

    /// <summary>MCP server version released to Nacos and registered alongside the endpoint.</summary>
    private const string McpServerVersion = "1.0.0";

    /// <summary>Version of the throwaway A2A card released to Nacos.</summary>
    private const string A2aCardVersion = "1.0.0";

    /// <summary>Protocol version of the throwaway A2A card released to Nacos.</summary>
    private const string A2aCardProtocolVersion = "0.3.7";

    /// <summary>URL the throwaway A2A card advertises (never dialed — nothing listens there).</summary>
    private const string A2aCardUrl = "http://127.0.0.1:9201";

    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi,
        ILogger logger,
        CancellationToken ct)
    {
        var failures = new List<string>();

        await TryAsync("MCP", () => RunMcpAgentAsync(httpAi, grpcAi, logger, ct), failures, logger);
        await TryAsync("Skill", () => RunSkillAgentAsync(logger, ct), failures, logger);
        await TryAsync("A2A", () => RunA2aAgentAsync(httpAi, logger, ct), failures, logger);

        return failures.Count == 0
            ? new SampleResult(SampleOutcome.Ok)
            : new SampleResult(SampleOutcome.Failed, string.Join("; ", failures));

        static async Task TryAsync(
            string name, Func<Task<SampleResult>> demo,
            List<string> failures, ILogger logger)
        {
            try
            {
                var r = await demo();
                logger.LogInformation("[AgentsAI/{Name}] {Outcome}: {Message}",
                    name, r.Outcome, r.Message ?? "ok");
                if (r.Outcome == SampleOutcome.Failed) failures.Add($"{name}: {r.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AgentsAI/{Name}] crashed", name);
                failures.Add($"{name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sub-demo 1: the MCP registry path. Hosts an MCP server, releases it to Nacos, registers the
    /// live endpoint, resolves the endpoint from what Nacos returns, connects the MCP client to
    /// that endpoint, and turns the discovered tools into the functions of a
    /// <see cref="ChatClientAgent"/>. The echo backend is the only chat client wired here, so the
    /// demo runs without model credentials — the Nacos/MCP half is real.
    /// The registry calls are HTTP except the endpoint operations, which Nacos 3.2.4 serves over
    /// the gRPC channel only, so both handles are used.
    /// </summary>
    private static async Task<SampleResult> RunMcpAgentAsync(
        IAiService httpAi, IAiService grpcAi, ILogger logger, CancellationToken ct)
    {
        var mcpName = $"agentsai-mcp-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var serviceName = $"{mcpName}-svc";

        WebApplication? mcpHost = null;
        var boundPort = 0;
        var serverReleased = false;
        var endpointRegistered = false;

        try
        {
            // 1. Host a real MCP server in this process, on an ephemeral port (the real one is read
            //    back from the host below, never hard-coded).
            var builder = WebApplication.CreateBuilder();
            //    The MCP SDK logs protocol traffic at Information level; keep this section's output
            //    readable while still surfacing warnings and errors raised inside the server.
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.WebHost.UseUrls($"http://{McpEndpointAddress}:0");

            IEnumerable<McpServerTool> tools = new[]
            {
                McpServerTool.Create(GetWeather, new McpServerToolCreateOptions
                {
                    Name = ToolName,
                    Description = ToolDescription
                })
            };

            builder.Services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools(tools);

            mcpHost = builder.Build();
            mcpHost.MapMcp();
            await mcpHost.StartAsync(ct);

            boundPort = ResolveBoundPort(mcpHost);
            logger.LogInformation("[AgentsAI/MCP] hosted MCP server on {Url}", mcpHost.Urls.First());

            // 2. Release that server to the Nacos MCP registry over HTTP: a non-local protocol needs
            //    a RemoteServerConfig service reference plus a matching REF endpoint specification.
            var spec = new McpServerBasicInfo
            {
                Name = mcpName,
                // The wire dialect is Streamable HTTP (the client dials with
                // TransportMode.StreamableHttp); the protocol string is registry metadata only.
                Protocol = AiConstants.Mcp.ProtocolStreamable,
                Description = "Weather MCP server hosted in-process by RedNb.Nacos.Sample.AI",
                VersionDetail = new ServerVersionDetail { Version = McpServerVersion },
                RemoteServerConfig = new McpServerRemoteServiceConfig
                {
                    ServiceRef = new McpServiceRef
                    {
                        NamespaceId = AiConstants.Mcp.DefaultNamespace,
                        GroupName = NacosConstants.DefaultGroup,
                        ServiceName = serviceName
                    }
                }
            };
            var toolSpec = new McpToolSpecification
            {
                Tools = new List<McpTool>
                {
                    new() { Name = ToolName, Description = ToolDescription }
                }
            };
            var endpointSpec = new McpEndpointSpec
            {
                Type = AiConstants.Mcp.EndpointTypeRef,
                Data = new Dictionary<string, string>
                {
                    { "namespaceId", AiConstants.Mcp.DefaultNamespace },
                    { "groupName", NacosConstants.DefaultGroup },
                    { "serviceName", serviceName }
                }
            };

            logger.LogInformation("[AgentsAI/MCP] releasing {Name} to Nacos", mcpName);
            // HTTP: release the server, its tool spec and its REF endpoint spec (console channel).
            await httpAi.ReleaseMcpServerAsync(spec, toolSpec, endpointSpec, cancellationToken: ct);
            serverReleased = true;

            // 3. Register the live backend endpoint over gRPC: on Nacos 3.2.4 the console listener
            //    has no HTTP route for endpoint operations.
            logger.LogInformation("[AgentsAI/MCP] registering endpoint {Address}:{Port} via gRPC",
                McpEndpointAddress, boundPort);
            // gRPC: endpoint registration has no HTTP console route on Nacos 3.2.4.
            await grpcAi.RegisterMcpServerEndpointAsync(
                mcpName, McpEndpointAddress, boundPort, version: McpServerVersion, cancellationToken: ct);
            endpointRegistered = true;

            // 4. Discovery: read the server back out of the registry and take the endpoint from what
            //    Nacos returns — deliberately not from the local boundPort variable.
            // HTTP: read the server detail back (console channel).
            var detail = await httpAi.GetMcpServerAsync(mcpName, cancellationToken: ct);
            if (detail is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"GetMcpServer('{mcpName}') returned null after release");
            }

            var endpoint = FirstUsableEndpoint(detail.BackendEndpoints);
            var endpointSource = "backendEndpoints";
            if (endpoint is null)
            {
                endpoint = FirstUsableEndpoint(detail.FrontendEndpoints);
                endpointSource = "frontendEndpoints";
            }

            if (endpoint is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    "registry returned no endpoint after registration");
            }

            var endpointUri = BuildEndpointUri(endpoint);
            logger.LogInformation(
                "[AgentsAI/MCP] registry discovery: {Source} -> {Address}:{Port}, dialing {Uri}",
                endpointSource, endpoint.Address, endpoint.Port, endpointUri);

            // 5. Dial the discovered endpoint and take the tool list off the wire. The endpoint we
            //    registered is plain HTTP, so the transport is too.
            await using var mcpClient = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                {
                    Endpoint = endpointUri,
                    TransportMode = HttpTransportMode.StreamableHttp
                }),
                cancellationToken: ct);

            var mcpTools = await mcpClient.ListToolsAsync(cancellationToken: ct);
            logger.LogInformation("[AgentsAI/MCP] handshake discovered {Count} tool(s): {Names}",
                mcpTools.Count,
                string.Join(", ", mcpTools.Select(t => t.Name)));

            if (mcpTools.Count == 0)
            {
                // This section hosts the server itself, so an empty list is a broken registry or
                // wire path — not a missing prerequisite.
                return new SampleResult(SampleOutcome.Failed,
                    "MCP handshake succeeded but the server exposed no tools");
            }

            // 6. Wire the discovered tools into a ChatClientAgent. MCP 2.2.0 ships McpClientTool as
            //    an AIFunction subclass, so the discovered tools are already functions — no
            //    AsAIFunction()/AIFunctionFactory.Create wrapping needed.
            var aiFunctions = mcpTools.Cast<AIFunction>().ToList();

            IChatClient chat = new EchoChatClient()
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            // ChatOptions.Tools is IList<AITool>; the functions are handed over unchanged.
            AIAgent agent = new ChatClientAgent(chat, new ChatClientAgentOptions
            {
                Name = "NacosMcpAgent",
                ChatOptions = new ChatOptions { Tools = aiFunctions.Cast<AITool>().ToList() }
            });

            AgentResponse response = await agent.RunAsync(
                $"What's the weather in Beijing? ({mcpTools.Count} MCP tool(s) discovered via Nacos)",
                cancellationToken: ct);

            var reply = response.Text;
            logger.LogInformation("[AgentsAI/MCP] agent reply: {Reply}",
                reply.Length > 120 ? reply[..120] + "..." : reply);

            // The echo client always prefixes "[echo]"; the " (echo, N tool(s) wired from Nacos)"
            // suffix appears only when tools reached the chat request, and here N >= 1 by
            // construction. Verified on the reply because the suffix is the proof the discovered
            // functions actually flowed through the agent into the chat client.
            if (!reply.Contains("[echo]", StringComparison.Ordinal))
            {
                return new SampleResult(SampleOutcome.Failed,
                    "EchoChatClient did not annotate its reply with the [echo] marker");
            }

            if (!reply.Contains($"{aiFunctions.Count} tool", StringComparison.Ordinal))
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"the agent did not pass the {aiFunctions.Count} discovered tool(s) to the chat client");
            }

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AgentsAI/MCP] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
        finally
        {
            // Cleanup runs on the success and the failure path alike, with its own budget (a
            // cancelled caller token must not abort it); every step is guarded on its own so one
            // failure cannot skip the rest, and a cleanup failure is logged rather than rethrown so
            // it cannot mask the outcome the try/catch above produced.
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var cleanupCt = cleanupCts.Token;

            if (endpointRegistered)
            {
                try
                {
                    // gRPC: deregister the endpoint (HTTP has no console route on Nacos 3.2.4).
                    await grpcAi.DeregisterMcpServerEndpointAsync(
                        mcpName, McpEndpointAddress, boundPort, cancellationToken: cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[AgentsAI/MCP] cleanup: deregistering {Address}:{Port} failed",
                        McpEndpointAddress, boundPort);
                }
            }

            if (serverReleased)
            {
                try
                {
                    // HTTP: delete the server (which also removes its endpoint service).
                    await httpAi.DeleteMcpServerAsync(mcpName, cancellationToken: cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[AgentsAI/MCP] cleanup: deleting {Name} failed", mcpName);
                }
            }

            if (mcpHost is not null)
            {
                try
                {
                    await mcpHost.StopAsync(cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[AgentsAI/MCP] cleanup: stopping the in-process MCP host failed");
                }

                try
                {
                    // The same budget as the other cleanup steps: a dispose that hangs (a stuck
                    // transport, an unresponsive server) must not stall the section.
                    await mcpHost.DisposeAsync().AsTask().WaitAsync(cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[AgentsAI/MCP] cleanup: disposing the in-process MCP host failed");
                }
            }
        }
    }

    /// <summary>
    /// Sub-demo 2: an inline, code-defined skill. The skill is built in code (no SKILL.md on disk,
    /// no `skill://` discovery) and handed to the agent through the skills provider, which renders
    /// the skill list into the model context. The echo backend keeps the demo offline — the reply
    /// proves the skill-enabled agent pipeline ran end to end with no server and no credentials.
    /// </summary>
    private static async Task<SampleResult> RunSkillAgentAsync(ILogger logger, CancellationToken ct)
    {
        // An inline SEP-2640-style skill. AgentSkill is abstract in 1.21.0, so the concrete
        // AgentInlineSkill carries name/description/instructions in one call.
        var skill = new AgentInlineSkill(
            name: "unit-converter",
            description: "Convert between common units using a multiplication factor.",
            instructions: "miles -> kilometers x1.60934; pounds -> kilograms x0.453592.");

        // The provider feeds the skill to capable models as context; UseSkills lives in the core
        // Microsoft.Agents.AI package (file-based discovery would need the file skills source).
        using var skillsProvider = new AgentSkillsProviderBuilder()
            .UseSkills([skill])
            .Build();

        IChatClient chat = new EchoChatClient();
        AIAgent agent = new ChatClientAgent(chat, new ChatClientAgentOptions
        {
            Name = "NacosSkillAgent",
            AIContextProviders = [skillsProvider]
        });

        AgentResponse response = await agent.RunAsync(
            "How many kilometers is a marathon?",
            cancellationToken: ct);

        var reply = response.Text;
        logger.LogInformation("[AgentsAI/Skill] agent reply: {Text}",
            reply.Length > 120 ? reply[..120] + "..." : reply);

        if (!reply.Contains("[echo]", StringComparison.Ordinal))
        {
            return new SampleResult(SampleOutcome.Failed,
                "EchoChatClient did not annotate its reply with the [echo] marker");
        }

        // The user turn coming back proves the skill-enabled agent carried the turn to the client.
        if (!reply.Contains("marathon", StringComparison.Ordinal))
        {
            return new SampleResult(SampleOutcome.Failed,
                "EchoChatClient did not echo the user message");
        }

        // The provider advertises the skill to the model as functions, so the chat request carries
        // them and the echo reply gains the "tool(s) wired from Nacos" suffix. Asserting the suffix
        // (EchoChatClient's own stable format, not a skills-package detail) keeps this demo from
        // reporting Ok if a provider regression silently contributes nothing to the request.
        if (!reply.Contains("tool(s) wired from Nacos", StringComparison.Ordinal))
        {
            return new SampleResult(SampleOutcome.Failed,
                "the skills provider did not contribute any function to the chat request");
        }

        return new SampleResult(SampleOutcome.Ok);
    }

    /// <summary>
    /// Sub-demo 3: the A2A registry path. Releases a throwaway agent card to the Nacos A2A registry
    /// over HTTP, reads the card JSON back out of the registry, maps it onto the A2A 1.0 protocol
    /// card type, and builds both an <see cref="AIAgent"/> and a protocol client from that card.
    /// The registry read is the discovery moment — nothing is built from the local card variable.
    /// A live A2A round trip (<c>RunAsync</c>) is deliberately out of scope: it needs a hosted A2A
    /// agent, and the hosting packages (<c>Microsoft.Agents.AI.Hosting.A2A*</c>) are not in this
    /// sample's pinned package set, so there is no server at the card's URL to talk to.
    /// </summary>
    private static async Task<SampleResult> RunA2aAgentAsync(
        IAiService httpAi, ILogger logger, CancellationToken ct)
    {
        var agentName = $"agentsai-card-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var released = false;

        try
        {
            // 1. Release a throwaway card. The server validates Name, Version and ProtocolVersion;
            //    the SDK defaults the registration type to SERVICE, and no endpoints are needed
            //    because this demo never dials the card.
            var card = new NacosAgentCard
            {
                Name = agentName,
                Description = "Throwaway A2A card released by the Microsoft.Agents.AI sample section",
                Version = A2aCardVersion,
                ProtocolVersion = A2aCardProtocolVersion,
                // The card's URL is the interface the A2A 1.0 card type below turns into
                // supportedInterfaces; nothing listens there, which is fine for this demo.
                Url = A2aCardUrl,
                PreferredTransport = AiConstants.A2a.TransportJsonRpc,
                Capabilities = new NacosAgentCapabilities { Streaming = true },
                DefaultInputModes = new List<string> { "text" },
                DefaultOutputModes = new List<string> { "text" },
                Skills = new List<NacosAgentSkill>
                {
                    new()
                    {
                        Id = "travel-planner",
                        Name = "Travel planner",
                        Description = "Plans a route from A to B (demo skill metadata).",
                        Tags = new List<string> { "travel", "routes" },
                        Examples = new List<string> { "Plan a route from A to B" },
                        InputModes = new List<string> { "text" },
                        OutputModes = new List<string> { "text" }
                    }
                }
            };

            logger.LogInformation("[AgentsAI/A2A] releasing {Name}@{Version}", agentName, A2aCardVersion);
            // HTTP: the agent-card route is served by the console channel on Nacos 3.2.4.
            await httpAi.ReleaseAgentCardAsync(card, ct);
            released = true;

            // 2. Discovery: read the card back out of the registry — the local `card` above is not
            //    used again. The registry returns the card JSON it stored.
            // HTTP: read the agent card back (console channel).
            var detail = await httpAi.GetAgentCardAsync(agentName, ct);
            if (detail is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"GetAgentCard('{agentName}') returned null after release");
            }

            logger.LogInformation("[AgentsAI/A2A] registry card: name={Name} version={Version} " +
                "registrationType={RegistrationType} latest={Latest}",
                detail.Name, detail.Version, detail.RegistrationType, detail.LatestVersion);

            // 3. Parse the registry's card JSON into the A2A 1.0 protocol card type the agent and
            //    client APIs consume.
            var remoteCard = ParseRegistryCard(detail, logger);
            if (string.IsNullOrWhiteSpace(remoteCard.Name) ||
                string.IsNullOrWhiteSpace(remoteCard.Version))
            {
                return new SampleResult(SampleOutcome.Failed,
                    "the registry card JSON did not carry a name/version");
            }

            if (remoteCard.SupportedInterfaces is not { Count: > 0 })
            {
                return new SampleResult(SampleOutcome.Failed,
                    "the registry card exposed no interface to build an A2A client from");
            }

            logger.LogInformation(
                "[AgentsAI/A2A] parsed card: name={Name} version={Version} description={Description} " +
                "interfaces={Interfaces} skills={Skills}",
                remoteCard.Name, remoteCard.Version, remoteCard.Description,
                string.Join(", ", remoteCard.SupportedInterfaces.Select(
                    i => $"{i.ProtocolBinding}@{i.Url} (protocol {i.ProtocolVersion})")),
                remoteCard.Skills?.Count ?? 0);

            // 4. Build the A2A agent and the protocol client from the card the registry returned.
            //    The static call is deliberate: the A2A namespace collides with the Nacos A2A model
            //    namespace this file already uses, so the extension is invoked explicitly.
            using var httpClient = new HttpClient();
            var clientOptions = new A2AClientOptions();

            AIAgent remote = A2A.A2AAgentCardExtensions.AsAIAgent(
                remoteCard, httpClient, clientOptions, loggerFactory: null);
            logger.LogInformation("[AgentsAI/A2A] agent built from the registry card: {Type} '{Name}'",
                remote.GetType().FullName, remote.Name);

            // A live round trip would be remote.RunAsync(...). It is out of scope here: the card's
            // URL has no hosted A2A agent behind it (the Microsoft.Agents.AI.Hosting.A2A* packages
            // are not in this sample's pinned set), so the demo stops at a working client factory.
            var a2aClient = A2A.A2AClientFactory.Create(remoteCard, httpClient, clientOptions);
            if (a2aClient is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    "A2AClientFactory.Create returned null for the registry card");
            }

            logger.LogInformation("[AgentsAI/A2A] protocol client from the registry card: {Type}",
                a2aClient.GetType().FullName);

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AgentsAI/A2A] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
        finally
        {
            // Cleanup runs on the success and the failure path alike, with its own budget; a
            // cleanup failure is logged rather than rethrown so it cannot mask the outcome above.
            if (released)
            {
                using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    // HTTP: delete the agent card (and any version of it) released above.
                    await httpAi.DeleteAgentAsync(agentName, cancellationToken: cleanupCts.Token);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[AgentsAI/A2A] cleanup: deleting {Name} failed", agentName);
                }
            }
        }
    }

    /// <summary>
    /// The single tool the hosted MCP server exposes. Canned data is the demo payload — the point
    /// is that the tool travels through the registry, not what the weather is.
    /// </summary>
    private static string GetWeather(string? city = null)
    {
        var where = string.IsNullOrWhiteSpace(city) ? "Beijing" : city!;
        return $"{where}: 21C, clear skies (canned demo data)";
    }

    /// <summary>
    /// Reads the port Kestrel actually bound. The host was asked for port 0, so the requested port
    /// is not the real one — only the server's address feature knows it.
    /// </summary>
    private static int ResolveBoundPort(WebApplication host)
    {
        var addresses = host.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;

        var port = FirstPort(addresses);
        if (port == 0)
        {
            // Fall back to the host's own view if the address feature is unavailable.
            port = FirstPort(host.Urls);
        }

        if (port == 0)
        {
            throw new InvalidOperationException("the in-process MCP host reported no bound port");
        }

        return port;
    }

    private static int FirstPort(IEnumerable<string>? urls)
        => urls?.Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri.Port : 0)
               .FirstOrDefault(p => p > 0)
           ?? 0;

    /// <summary>
    /// Picks the first endpoint that carries a dialable address. A registered instance always has
    /// one; an endpoint Nacos synthesised for a front-end reference might not.
    /// </summary>
    private static McpEndpointInfo? FirstUsableEndpoint(List<McpEndpointInfo>? endpoints)
        => endpoints?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Address) && e.Port > 0);

    /// <summary>
    /// Builds the base address the MCP client dials from the registry's endpoint shape: only http
    /// and https are dialable, and the registered instance carries no path — a front-end endpoint
    /// may, so it is honoured when present.
    /// </summary>
    private static Uri BuildEndpointUri(McpEndpointInfo endpoint)
    {
        var scheme = string.Equals(endpoint.Protocol, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? Uri.UriSchemeHttps
            : Uri.UriSchemeHttp;

        var baseUri = new Uri($"{scheme}://{endpoint.Address}:{endpoint.Port}");
        return string.IsNullOrWhiteSpace(endpoint.Path) ? baseUri : new Uri(baseUri, endpoint.Path);
    }

    /// <summary>
    /// Turns the card JSON the Nacos registry returned into an A2A 1.0 <see cref="A2ACard"/>.
    /// The registry stores the 0.3.7 card shape the release sent (<c>url</c> +
    /// <c>preferredTransport</c>, plus the advertised skills and modes); the 1.0 protocol type
    /// requires <c>supportedInterfaces</c> (it is a JSON-required property), so the 0.3.7
    /// URL + transport pair is mapped onto the 1.0 interface list when the registry card does not
    /// carry interfaces of its own. Every other field comes straight from the registry JSON.
    /// </summary>
    private static A2ACard ParseRegistryCard(NacosAgentCardDetailInfo detail, ILogger logger)
    {
        // The typed read-back reproduces the card JSON Nacos stored — field names included.
        var node = JsonNode.Parse(JsonSerializer.Serialize(detail))!.AsObject();

        if (node["supportedInterfaces"] is not JsonArray { Count: > 0 })
        {
            var cardUrl = node["url"]?.GetValue<string>();
            var transport = node["preferredTransport"]?.GetValue<string>();
            var protocolVersion = node["protocolVersion"]?.GetValue<string>();

            // url, protocolBinding and protocolVersion are all JSON-required on the 1.0 interface,
            // so the mapped entry is written only with non-null values: the card's url gates the
            // entry, and a blank transport/protocolVersion falls back to the values this demo
            // releases (preferredTransport = JSONRPC, protocolVersion = 0.3.7).
            var binding = string.IsNullOrWhiteSpace(transport)
                ? AiConstants.A2a.TransportJsonRpc
                : transport;
            var version = string.IsNullOrWhiteSpace(protocolVersion)
                ? A2aCardProtocolVersion
                : protocolVersion;

            var interfaces = new JsonArray();
            if (!string.IsNullOrWhiteSpace(cardUrl))
            {
                interfaces.Add(new JsonObject
                {
                    ["url"] = cardUrl,
                    ["protocolBinding"] = binding,
                    ["protocolVersion"] = version
                });
            }

            node["supportedInterfaces"] = interfaces;
            if (interfaces.Count > 0)
            {
                // Log the binding actually written into the mapped interface (the fallback-aware
                // value), so a fired fallback can never be logged as an empty transport.
                logger.LogInformation(
                    "[AgentsAI/A2A] registry card carried the 0.3.7 shape (url={Url}, transport={Transport}) " +
                    "— mapped onto the A2A 1.0 supportedInterfaces list",
                    cardUrl, binding);
            }
            else
            {
                logger.LogWarning(
                    "[AgentsAI/A2A] registry card carried no url/transport and no interfaces");
            }
        }

        var card = JsonSerializer.Deserialize<A2ACard>(
            node.ToJsonString(), A2A.A2AJsonUtilities.DefaultOptions)
            ?? throw new JsonException("the registry card JSON did not deserialize into an A2A agent card");

        return card;
    }
}
