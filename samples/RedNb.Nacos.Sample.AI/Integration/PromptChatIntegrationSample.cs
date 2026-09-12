using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Model.Prompt;
using RedNb.Nacos.Sample.AI.Chat;

namespace RedNb.Nacos.Sample.AI.Integration;

/// <summary>
/// Prompt-to-chat integration section of the sample: publishes a throwaway prompt to the Nacos
/// prompt registry, renders the template that comes back into a system message, and hands that
/// message plus a user turn to an <see cref="IChatClient"/> — closing the loop from a
/// registry-stored prompt template to a chat request.
/// The run is self-contained (it publishes its own timestamped key rather than depending on a key
/// another section created and deleted), and it takes the prompt offline and deletes it in a
/// <c>finally</c> so the success and the failure path both clean up.
/// The prompt registry is HTTP-only on Nacos 3.2.4 (the gRPC AI service delegates every prompt
/// call to the same HTTP sub-service), so the gRPC handle is unused here.
/// </summary>
public static class PromptChatIntegrationSample
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi, // unused: prompt registry APIs are HTTP-only on Nacos 3.2.4
        ILogger logger,
        CancellationToken ct)
    {
        var promptKey = $"code-review-chat-{DateTime.UtcNow:yyyyMMddHHmmss}";
        const string version = "1.0.0";
        var created = false;
        var published = false;

        try
        {
            // 1. Publish a throwaway prompt: draft -> reviewing -> online. The template declares
            //    both placeholders it is rendered with below, so the render is observable.
            logger.LogInformation("[PromptChat] draft {Key}@{Version}", promptKey, version);
            // HTTP: draft pipeline entry (no gRPC prompt route on Nacos 3.2.4).
            await httpAi.CreatePromptDraftAsync(
                promptKey,
                version,
                "You are a code reviewer. Review this {{language}} code: {{code}}",
                variables: new[]
                {
                    new PromptVariable { Name = "language", DefaultValue = "C#" },
                    new PromptVariable { Name = "code", DefaultValue = "var x = 1 + 1;" }
                },
                cancellationToken: ct);
            created = true;

            // HTTP: submit for review (editing -> reviewing).
            await httpAi.SubmitPromptReviewAsync(promptKey, version, ct);

            // HTTP: publish the reviewing version; the server refuses a version that is neither
            //       reviewing nor online, so this must follow the submit above.
            await httpAi.PublishPromptAsync(
                promptKey, version, updateLatestLabel: true, cancellationToken: ct);
            published = true;
            logger.LogInformation("[PromptChat] published {Key}@{Version}", promptKey, version);

            // 2. Read the resolved prompt back through the client query route (it only serves
            //    online versions, so this doubles as the check that publish took effect) and
            //    render the template into the system message.
            // HTTP: client query (v3/client/ai/prompt).
            var prompt = await httpAi.GetPromptAsync(promptKey, version, ct);
            if (prompt is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    "GetPrompt returned null after publish");
            }

            var sysMsg = prompt.Render(new Dictionary<string, string>
            {
                ["language"] = "C#",
                ["code"] = "var x = 1 + 1;" // tiny snippet
            });
            logger.LogInformation("[PromptChat] rendered system prompt ({Length} chars): {Snippet}",
                sysMsg.Length,
                sysMsg.Length > 120 ? sysMsg[..120] + "..." : sysMsg);

            if (!sysMsg.Contains("C#", StringComparison.Ordinal))
            {
                return new SampleResult(SampleOutcome.Failed,
                    "Render did not substitute the {{language}} variable");
            }

            // 3. Hand the rendered prompt to the chat client as the system message. The default
            //    provider is the echo client, so the round trip is observable without an API key.
            IChatClient chat = new EchoChatClient();
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, sysMsg),
                new(ChatRole.User, "Review this code")
            };

            var response = await chat.GetResponseAsync(messages, cancellationToken: ct);
            var assistantText = response.Messages.LastOrDefault()?.Text ?? string.Empty;
            logger.LogInformation("[PromptChat] echo reply: {Reply}",
                assistantText.Length > 120 ? assistantText[..120] + "..." : assistantText);

            // The echo client marks every reply with an "[echo]" annotation; the
            // " (echo, N tool(s) wired from Nacos)" suffix is appended only when tools are
            // wired, and this round trip wires none.
            if (!assistantText.Contains("[echo]", StringComparison.Ordinal))
            {
                return new SampleResult(SampleOutcome.Failed,
                    "EchoChatClient did not annotate its reply with the [echo] marker");
            }

            // The user turn coming back is the proof that the messages reached the client.
            if (!assistantText.Contains("Review this code", StringComparison.Ordinal))
            {
                return new SampleResult(SampleOutcome.Failed,
                    "EchoChatClient did not echo the user message");
            }

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PromptChat] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
        finally
        {
            // Cleanup runs on the success and the failure path alike, so the throwaway prompt
            // never outlives the run. Each call is guarded by the step that made its
            // precondition true (taking a version offline only makes sense once it is online),
            // and a cleanup failure is logged rather than rethrown so it cannot mask the outcome
            // the try/catch above produced.
            try
            {
                if (published)
                {
                    // HTTP: online -> offline.
                    await httpAi.OfflinePromptAsync(promptKey, version, ct);
                }

                if (created)
                {
                    // HTTP: delete this version (a null version would delete all of them).
                    await httpAi.DeletePromptAsync(promptKey, version, cancellationToken: ct);
                }
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(cleanupEx,
                    "[PromptChat] cleanup of {Key}@{Version} failed", promptKey, version);
            }
        }
    }
}
