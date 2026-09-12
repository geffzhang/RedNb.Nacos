using Microsoft.Extensions.Logging;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Ai.Models.Prompt;

namespace RedNb.Nacos.Sample.AI;

/// <summary>
/// Prompt section of the sample: drives the full prompt lifecycle over the HTTP
/// admin channel — create draft, submit for review, publish, bring online, read
/// the resolved template back and render it, list, then take it offline and
/// delete it. The prompt registry is HTTP-only on Nacos 3.2.4 (the gRPC AI
/// service delegates every prompt call to the same HTTP sub-service), so the
/// gRPC handle is unused here.
/// The offline/delete cleanup runs in a <c>finally</c>, so a failed or
/// interrupted run cleans up after itself too.
/// </summary>
public static class PromptSamples
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi, // unused: prompt registry APIs are HTTP-only on Nacos 3.2.4
        ILogger logger,
        CancellationToken ct)
    {
        var promptKey = $"code-review-{DateTime.UtcNow:yyyyMMddHHmmss}";
        const string version = "1.0.0";
        var created = false;
        var published = false;

        try
        {
            // 1. Create a draft version. A brand-new promptKey needs a template
            //    (the server rejects an empty one), and declaring the {{language}}
            //    variable gives the template a default value for rendering.
            logger.LogInformation("[Prompt] draft {Key}@{Version}", promptKey, version);
            // HTTP: draft pipeline entry (no gRPC prompt route on Nacos 3.2.4).
            await httpAi.CreatePromptDraftAsync(
                promptKey,
                version,
                "Review {{language}} code",
                variables: new[]
                {
                    new PromptVariable { Name = "language", DefaultValue = "C#" }
                },
                cancellationToken: ct);
            created = true;

            // 2. Submit the draft for review (editing -> reviewing).
            // HTTP: submit for review.
            await httpAi.SubmitPromptReviewAsync(promptKey, version, ct);
            logger.LogInformation("[Prompt] submitted review");

            // 3. Publish the reviewing version (reviewing -> online). The server
            //    refuses to publish a version that is neither reviewing nor online,
            //    so this must follow the submit above.
            //    Vanilla Nacos 3.2.4 ships the default AI pipeline plugin
            //    (nacos-default-ai-pipeline-plugin-3.2.4.jar), so the normal publish
            //    requires an approval step the stock server exposes no API for; the
            //    force-publish route [since=3.2.1] is the documented unattended
            //    bypass and has the same reviewing -> online effect.
            // HTTP: force-publish and move the "latest" label onto this version.
            await httpAi.ForcePublishPromptAsync(
                promptKey, version, updateLatestLabel: true, cancellationToken: ct);
            published = true;
            logger.LogInformation("[Prompt] published");

            // HTTP: explicit online switch — a no-op once publish made it online.
            await httpAi.OnlinePromptAsync(promptKey, version, ct);
            logger.LogInformation("[Prompt] online");

            // 4. Read the resolved prompt back through the client query route and
            //    render the template with a variable value.
            // HTTP: client query (v3/client/ai/prompt).
            var prompt = await httpAi.GetPromptAsync(promptKey, version, ct);
            if (prompt is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    "GetPrompt returned null after publish");
            }

            var rendered = prompt.Render(
                new Dictionary<string, string> { ["language"] = "C#" });
            logger.LogInformation("[Prompt] rendered: {Snippet}",
                rendered.Length > 80 ? rendered[..80] + "..." : rendered);

            // 5. List. The admin list route takes an explicit search mode
            //    ("accurate" matches promptKey exactly).
            // HTTP: admin list (v3/admin/ai/prompt/list).
            var page = await httpAi.ListPromptsAsync(
                promptKey: promptKey, search: "accurate", pageNo: 1, pageSize: 10,
                cancellationToken: ct);
            logger.LogInformation("[Prompt] list count={Count}", page.TotalCount);

            // 6. Cleanup (take the version offline, then delete it) is the finally below,
            //    so a failure or an interrupt after the draft was created does not orphan it.
            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Prompt] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
        finally
        {
            // Cleanup runs on the success and the failure path alike, so a failed or
            // interrupted run cannot orphan the draft. It gets its own budget (a cancelled
            // caller token must not abort it) and every step is guarded on its own so one
            // failure cannot skip the rest; a cleanup failure is logged rather than rethrown
            // so it cannot mask the outcome the try/catch above produced. Each step is
            // guarded by the step that made its precondition true (taking a version offline
            // only makes sense once it is online).
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var cleanupCt = cleanupCts.Token;

            if (published)
            {
                try
                {
                    // HTTP: online -> offline.
                    await httpAi.OfflinePromptAsync(promptKey, version, cancellationToken: cleanupCt)
                        .WaitAsync(cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[Prompt] cleanup: taking {Key}@{Version} offline failed", promptKey, version);
                }
            }

            if (created)
            {
                try
                {
                    // HTTP: delete this version (a null version would delete all of them).
                    await httpAi.DeletePromptAsync(promptKey, version, cancellationToken: cleanupCt)
                        .WaitAsync(cleanupCt);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx,
                        "[Prompt] cleanup: deleting {Key}@{Version} failed", promptKey, version);
                }
            }
        }
    }
}
