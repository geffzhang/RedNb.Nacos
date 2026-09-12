using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RedNb.Nacos.Sample.AI.Chat;

/// <summary>
/// Resolves an <see cref="IChatClient"/> based on the "ChatProvider" key in
/// configuration. Default provider is <see cref="EchoChatClient"/>. The OpenAI
/// branch is only compiled when OPENAI_PROVIDER is defined, so the default
/// build does not pull in its provider package.
/// </summary>
public static class ChatClientFactory
{
    public static IChatClient CreateFromConfig(IConfiguration config, ILogger? logger)
    {
        var provider = config["ChatProvider"];
        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(provider, "echo", StringComparison.OrdinalIgnoreCase))
        {
            return new EchoChatClient();
        }

#if OPENAI_PROVIDER
        if (string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = config["OpenAI:ApiKey"];
            var model = config["OpenAI:Model"] ?? "gpt-4o-mini";
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger?.LogWarning(
                    "ChatProvider=openai but OpenAI:ApiKey is missing — falling back to EchoChatClient.");
                return new EchoChatClient();
            }

            var openAi = new OpenAI.Chat.ChatClient(model, apiKey);
            return openAi.AsIChatClient();
        }
#endif

        logger?.LogWarning(
            "Unknown ChatProvider '{Provider}' — falling back to EchoChatClient.", provider);
        return new EchoChatClient();
    }
}
