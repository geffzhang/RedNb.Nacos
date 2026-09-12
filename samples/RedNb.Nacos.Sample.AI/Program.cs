using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.GrpcClient;

namespace RedNb.Nacos.Sample.AI;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables(prefix: "REDNB_NACOS_")
            .Build();

        var serverAddresses = config["Nacos:ServerAddresses"] ?? "localhost:8848";
        var grpcPortOffset = int.TryParse(config["Nacos:GrpcPortOffset"], out var p) ? p : 1000;
        var username = config["Nacos:Username"] ?? "nacos";
        var password = config["Nacos:Password"] ?? "nacos";

        var options = new NacosClientOptions
        {
            ServerAddresses = serverAddresses,
            Username = username,
            Password = password,
            Namespace = config["Nacos:Namespace"] ?? string.Empty,
            EnableGrpc = true,
            GrpcPortOffset = grpcPortOffset,
            DefaultTimeout = 5000
        };

        using var services = new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(ParseLogLevel(config["Logging:LogLevel:Default"]));
            })
            .BuildServiceProvider();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("RedNb.Nacos.Sample.AI");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        NacosFactory? httpFactory = null;
        NacosGrpcFactory? grpcFactory = null;
        IAiService? httpAi = null;
        IAiService? grpcAi = null;
        var results = new List<(string Section, SampleResult Result)>();

        try
        {
            httpFactory = new NacosFactory(loggerFactory);
            grpcFactory = new NacosGrpcFactory(services);

            logger.LogInformation("Connecting to Nacos at {Server} (HTTP) and gRPC port offset {Offset}",
                serverAddresses, grpcPortOffset);
            logger.LogInformation("ChatProvider: {Provider}", config["ChatProvider"] ?? "echo");
            httpAi = httpFactory.CreateAiService(options);
            grpcAi = grpcFactory.CreateAiService(options);

            logger.LogInformation("Connected. Running AI samples.");

            results.Add(("Mcp", await McpSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
            results.Add(("A2a", await A2aSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
            results.Add(("Prompt", await PromptSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
            results.Add(("Skill", await SkillSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
            results.Add(("AgentSpec", await AgentSpecSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
            results.Add(("PromptChat", await Integration.PromptChatIntegrationSample.RunAsync(httpAi, grpcAi, logger, cts.Token)));
            results.Add(("McpChat", await Integration.McpChatIntegrationSample.RunAsync(httpAi, grpcAi, logger, cts.Token)));
            results.Add(("AgentsAI", await Integration.AgentsAISamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
        }
        catch (NacosException nex)
        {
            logger.LogError(nex,
                "Nacos connection failed (code={Code}): {Message}. " +
                "Is Nacos 3.x running on {Server}? auth ok?",
                nex.ErrorCode, nex.Message, serverAddresses);
            return 2;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Cancelled by user.");
        }
        finally
        {
            if (httpAi is IAsyncDisposable d1) await d1.DisposeAsync();
            if (grpcAi is IAsyncDisposable d2) await d2.DisposeAsync();
        }

        PrintSummary(results, logger);
        return 0;
    }

    private static void PrintSummary(
        List<(string Section, SampleResult Result)> results, ILogger logger)
    {
        logger.LogInformation("--- Summary ---");
        foreach (var (section, result) in results)
        {
            var msg = result.Message is null ? string.Empty : $" — {result.Message}";
            logger.LogInformation("{Section}: {Outcome}{Msg}", section, result.Outcome, msg);
        }
    }

    private static LogLevel ParseLogLevel(string? value) => value?.ToLowerInvariant() switch
    {
        "trace" => LogLevel.Trace,
        "debug" => LogLevel.Debug,
        "information" or "info" => LogLevel.Information,
        "warning" or "warn" => LogLevel.Warning,
        "error" => LogLevel.Error,
        _ => LogLevel.Information
    };
}
