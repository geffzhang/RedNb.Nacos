using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using RedNb.Nacos.Sample.AI.Chat;

namespace RedNb.Nacos.Sample.AI.Integration;

/// <summary>
/// MCP-to-chat integration section of the sample: hosts a real MCP server in this process, releases
/// it to the Nacos MCP registry (server + tool spec + REF endpoint spec over HTTP, live backend
/// endpoint over gRPC), reads the server back out of the registry, dials the endpoint <em>Nacos
/// returned</em> with the ModelContextProtocol client, and wires the discovered tools into an
/// <see cref="IChatClient"/>.
/// The run is self-contained: it hosts its own server rather than discovering one another section
/// created and deleted (which can never exist at this point in the run), so there is no skip path —
/// every failure is reported with the server's or exception's message.
/// The registry calls are HTTP except for the endpoint operations, which Nacos 3.2.4 serves over the
/// gRPC channel only, so both handles are used.
/// </summary>
public static class McpChatIntegrationSample
{
    /// <summary>Name of the tool: hosted by the in-process MCP server and released to Nacos.</summary>
    private const string ToolName = "get_weather";

    /// <summary>Description of the tool, carried by both the host and the Nacos tool spec.</summary>
    private const string ToolDescription = "Returns the weather for a city";

    /// <summary>Address the backend endpoint is registered under (loopback: the server is local).</summary>
    private const string EndpointAddress = "127.0.0.1";

    /// <summary>Version released to Nacos and registered alongside the endpoint.</summary>
    private const string ServerVersion = "1.0.0";

    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi,
        ILogger logger,
        CancellationToken ct)
    {
        var mcpName = $"weather-chat-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var serviceName = $"{mcpName}-svc";

        WebApplication? mcpHost = null;
        var boundPort = 0;
        var serverReleased = false;
        var endpointRegistered = false;

        try
        {
            // 1. Host a real MCP server in this process. The port is ephemeral (0) — Kestrel picks a
            //    free one and the real value is read back from the host below, never hard-coded.
            var builder = WebApplication.CreateBuilder();
            //    The MCP SDK logs protocol traffic at Information level; keep this section's output
            //    readable while still surfacing warnings and errors raised inside the server.
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.WebHost.UseUrls($"http://{EndpointAddress}:0");

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
            logger.LogInformation("[McpChat] hosted MCP server on {Url}", mcpHost.Urls.First());

            // 2. Release that server to the Nacos MCP registry over HTTP, mirroring the verified
            //    payload shape: a non-local protocol needs a RemoteServerConfig service reference
            //    plus a matching REF endpoint specification.
            var spec = new McpServerBasicInfo
            {
                Name = mcpName,
                Protocol = AiConstants.Mcp.ProtocolSse,
                Description = "Weather MCP server hosted in-process by RedNb.Nacos.Sample.AI",
                VersionDetail = new ServerVersionDetail { Version = ServerVersion },
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

            logger.LogInformation("[McpChat] releasing {Name} to Nacos", mcpName);
            // HTTP: release the server, its tool spec and its REF endpoint spec (console channel).
            await httpAi.ReleaseMcpServerAsync(spec, toolSpec, endpointSpec, cancellationToken: ct);
            serverReleased = true;

            // 3. Register the live backend endpoint over gRPC: on Nacos 3.2.4 the console listener has
            //    no HTTP route for endpoint operations (HTTP throws ServerError).
            logger.LogInformation("[McpChat] registering endpoint {Address}:{Port} via gRPC",
                EndpointAddress, boundPort);
            // gRPC: endpoint registration has no HTTP console route on Nacos 3.2.4.
            await grpcAi.RegisterMcpServerEndpointAsync(
                mcpName, EndpointAddress, boundPort, version: ServerVersion, cancellationToken: ct);
            endpointRegistered = true;

            // 4. Discovery: read the server back out of the registry and resolve the endpoint from
            //    what Nacos returns — deliberately not from the local boundPort variable.
            // HTTP: read the server detail back (console channel).
            var detail = await httpAi.GetMcpServerAsync(mcpName, cancellationToken: ct);
            if (detail is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"GetMcpServer('{mcpName}') returned null after release");
            }

            // The gRPC registration above is a backend service instance, so it surfaces as a backend
            // endpoint; a frontend endpoint (when Nacos reports one) is the other dialable shape.
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
                "[McpChat] registry discovery: {Source} -> {Address}:{Port}, dialing {Uri}",
                endpointSource, endpoint.Address, endpoint.Port, endpointUri);

            // 5. Flow A: dial the discovered endpoint with the MCP client and call the tool over the
            //    wire. The endpoints we register are plain HTTP, so the transport is too.
            logger.LogInformation("[McpChat] connecting with the MCP client");
            await using var mcpClient = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                {
                    Endpoint = endpointUri,
                    TransportMode = HttpTransportMode.StreamableHttp
                }),
                cancellationToken: ct);

            var mcpTools = await mcpClient.ListToolsAsync(cancellationToken: ct);
            logger.LogInformation("[McpChat] handshake discovered {Count} tool(s): {Names}",
                mcpTools.Count,
                string.Join(", ", mcpTools.Select(t => t.Name)));

            if (mcpTools.Count == 0)
            {
                return new SampleResult(SampleOutcome.Failed,
                    "MCP handshake succeeded but the server exposed no tools");
            }

            var weatherTool = mcpTools.FirstOrDefault(
                t => string.Equals(t.Name, ToolName, StringComparison.Ordinal));
            if (weatherTool is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"MCP server does not expose the '{ToolName}' tool released to Nacos " +
                    $"(exposes: {string.Join(", ", mcpTools.Select(t => t.Name))})");
            }

            var call = await mcpClient.CallToolAsync(
                weatherTool.Name,
                arguments: new Dictionary<string, object?> { ["city"] = "Beijing" },
                cancellationToken: ct);

            var toolText = call.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
            if (call.IsError == true)
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"{weatherTool.Name} returned an MCP error: {toolText}");
            }

            if (toolText.Length == 0)
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"{weatherTool.Name} returned no text content");
            }

            logger.LogInformation("[McpChat] {Tool}(city=Beijing) -> {Text}",
                weatherTool.Name, toolText.Length > 120 ? toolText[..120] + "..." : toolText);

            // 6. Flow B: wire the discovered tools into an IChatClient. MCP 2.2.0 ships McpClientTool
            //    as an AIFunction subclass, so the tools are usable as functions as-is — no
            //    AsAIFunction()/AIFunctionFactory.Create wrapping needed — and UseFunctionInvocation
            //    can invoke them over the wire if the client ever asks for a call.
            var aiFunctions = mcpTools.Cast<AIFunction>().ToList();
            IChatClient chat = new EchoChatClient()
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, "What's the weather in Beijing?")
            };
            // ChatOptions.Tools is IList<AITool>; the functions are handed over unchanged.
            var options = new ChatOptions { Tools = aiFunctions.Cast<AITool>().ToList() };

            var response = await chat.GetResponseAsync(messages, options, cancellationToken: ct);
            var reply = response.Messages.LastOrDefault()?.Text ?? string.Empty;
            logger.LogInformation("[McpChat] echo reply: {Reply}",
                reply.Length > 120 ? reply[..120] + "..." : reply);

            // The echo client always prefixes "[echo]"; the " (echo, N tool(s) wired from Nacos)"
            // suffix appears only when tools are wired, and here N >= 1 by construction.
            if (!reply.Contains("[echo]", StringComparison.Ordinal))
            {
                return new SampleResult(SampleOutcome.Failed,
                    "EchoChatClient did not annotate its reply with the [echo] marker");
            }

            if (!reply.Contains($"{aiFunctions.Count} tool", StringComparison.Ordinal))
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"EchoChatClient did not report the {aiFunctions.Count} discovered tool(s) as wired");
            }

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[McpChat] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
        finally
        {
            // Cleanup runs on the success and the failure path alike. It gets its own budget (a
            // cancelled caller token must not abort it) and every step is guarded on its own so one
            // failure cannot skip the rest; a cleanup failure is logged rather than rethrown so it
            // cannot mask the outcome the try/catch above produced.
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var cleanupCt = cleanupCts.Token;

            if (endpointRegistered)
            {
                try
                {
                    // gRPC: deregister the endpoint (HTTP has no console route on Nacos 3.2.4).
                    await grpcAi.DeregisterMcpServerEndpointAsync(
                        mcpName, EndpointAddress, boundPort, cancellationToken: cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[McpChat] cleanup: deregistering {Address}:{Port} failed",
                        EndpointAddress, boundPort);
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
                    logger.LogWarning(cleanupEx, "[McpChat] cleanup: deleting {Name} failed", mcpName);
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
                        "[McpChat] cleanup: stopping the in-process MCP host failed");
                }

                try
                {
                    await mcpHost.DisposeAsync();
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[McpChat] cleanup: disposing the in-process MCP host failed");
                }
            }
        }
    }

    /// <summary>
    /// The single tool the hosted MCP server exposes. Canned data is the demo payload — the point is
    /// that the call travels a real MCP round trip, not what the weather is.
    /// </summary>
    private static string GetWeather(string? city = null)
    {
        var where = string.IsNullOrWhiteSpace(city) ? "Beijing" : city!;
        return $"{where}: 21C, clear skies (canned demo data)";
    }

    /// <summary>
    /// Reads the port Kestrel actually bound. The host was asked for port 0, so the requested port is
    /// not the real one — only the server's address feature knows it.
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
}
