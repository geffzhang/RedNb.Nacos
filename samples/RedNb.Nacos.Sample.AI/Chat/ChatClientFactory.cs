using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RedNb.Nacos.Sample.AI.Chat;

/// <summary>
/// Resolves an <see cref="IChatClient"/> based on the "ChatProvider" key in
/// configuration. Default provider is <see cref="EchoChatClient"/>. OpenAI and
/// Azure branches are only compiled when the matching symbol is defined, so
/// the default build does not pull in their provider packages.
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

            var openAi = new OpenAI.OpenAIClient(apiKey);
            return openAi.AsChatClient(model);
        }
#endif

#if AZURE_PROVIDER
        if (string.Equals(provider, "azure", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = config["AzureOpenAI:Endpoint"];
            var apiKey = config["AzureOpenAI:ApiKey"];
            var deployment = config["AzureOpenAI:DeploymentName"];
            if (string.IsNullOrWhiteSpace(endpoint) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(deployment))
            {
                logger?.LogWarning(
                    "ChatProvider=azure but AzureOpenAI:* config is incomplete — falling back to EchoChatClient.");
                return new EchoChatClient();
            }

            var azure = new Azure.AI.Inference.AzureOpenAIClient(
                new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
            return azure.AsChatClient(deployment);
        }
#endif

        logger?.LogWarning(
            "Unknown ChatProvider '{Provider}' — falling back to EchoChatClient.", provider);
        return new EchoChatClient();
    }
}
