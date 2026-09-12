using Microsoft.Extensions.AI;

namespace RedNb.Nacos.Sample.AI.Chat;

/// <summary>
/// Default IChatClient implementation for the AI sample. Echoes the last
/// user message back and annotates the response with the count of tools it
/// was handed. Never throws — it is the safety net for the no-API-key case.
/// </summary>
public sealed class EchoChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = new("echo");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var userText = messages?
            .LastOrDefault(m => m.Role == ChatRole.User)?.Text
            ?? string.Empty;

        var toolCount = options?.Tools?.Count ?? 0;
        var suffix = toolCount > 0
            ? $" (echo, {toolCount} tool(s) wired from Nacos)"
            : string.Empty;

        var reply = new ChatMessage(
            ChatRole.Assistant,
            $"[echo]{suffix} {userText}".TrimStart());

        return Task.FromResult(new ChatResponse(reply));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { /* no-op */ }
}
