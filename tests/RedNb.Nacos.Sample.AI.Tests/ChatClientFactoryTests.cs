using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedNb.Nacos.Sample.AI.Chat;
using Xunit;

namespace RedNb.Nacos.Sample.AI.Tests;

public class ChatClientFactoryTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(values);
        return builder.Build();
    }

    [Fact]
    public void CreateFromConfig_DefaultProvider_ReturnsEcho()
    {
        var config = BuildConfig(new Dictionary<string, string?>()); // empty
        IChatClient client = ChatClientFactory.CreateFromConfig(config, NullLogger.Instance);
        Assert.IsType<EchoChatClient>(client);
    }

    [Fact]
    public void CreateFromConfig_EchoExplicitly_ReturnsEcho()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ChatProvider"] = "echo"
        });
        IChatClient client = ChatClientFactory.CreateFromConfig(config, NullLogger.Instance);
        Assert.IsType<EchoChatClient>(client);
    }

    [Fact]
    public void CreateFromConfig_UnknownProvider_FallsBackToEchoAndLogsWarning()
    {
        var logger = new TestLogger();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ChatProvider"] = "made-up-provider"
        });
        IChatClient client = ChatClientFactory.CreateFromConfig(config, logger);
        Assert.IsType<EchoChatClient>(client);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void CreateFromConfig_OpenAiMissingApiKey_FallsBackToEcho()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ChatProvider"] = "openai"
            // no OpenAI:ApiKey
        });
        IChatClient client = ChatClientFactory.CreateFromConfig(config, NullLogger.Instance);
        Assert.IsType<EchoChatClient>(client);
    }

    private sealed class TestLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
