using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;

namespace RedNb.Nacos.Sample.AI;

/// <summary>
/// AgentSpec section of the sample: builds a minimal AgentSpec package in memory and drives the
/// full AgentSpec lifecycle over the HTTP channel — ZIP upload (multipart), submit for review,
/// publish, bring online, read the released version back by version and by label, then take it
/// offline and delete it.
/// The AgentSpec registry is HTTP-only on Nacos 3.2.4 (the gRPC AI service delegates every
/// AgentSpec call to the same HTTP sub-service), so the gRPC handle is unused here.
/// </summary>
public static class AgentSpecSamples
{
    /// <summary>
    /// Version the server assigns to the first upload of a brand-new AgentSpec:
    /// <c>AgentSpecOperationServiceImpl.DEFAULT_INITIAL_VERSION</c> is "0.0.1", and it is used by
    /// <c>uploadSingleAgentSpecFromZip</c> when no AgentSpec of that name exists and the upload
    /// did not ask to overwrite. Upload takes no target version, and the package manifest has no
    /// version field, so a fresh, timestamped name always lands on this version.
    /// </summary>
    private const string InitialVersion = "0.0.1";

    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi, // unused: agentspec registry APIs are HTTP-only on Nacos 3.2.4
        ILogger logger,
        CancellationToken ct)
    {
        var agentName = $"travel-agent-spec-{DateTime.UtcNow:yyyyMMddHHmmss}";

        try
        {
            // 1. Build a minimal AgentSpec package in memory. The server parses the upload with
            //    AgentSpecZipParser: manifest.json is mandatory and is where the AgentSpec name
            //    comes from, while AGENTS.md is the agent instruction body the server stores as
            //    the version's main content.
            var zipBytes = BuildAgentSpecPackage(agentName);

            logger.LogInformation("[AgentSpec] uploading {Bytes} bytes for {Name}", zipBytes.Length, agentName);
            // HTTP: admin upload (v3/admin/ai/agentspecs/upload) as multipart/form-data carrying
            //       an "overwrite" part and the ZIP under the "file" part. The server names the
            //       AgentSpec from manifest.json -> worker.suggested_name, NOT from the multipart
            //       file name — the server only size-checks the part, never its extension. With
            //       overwrite=false and a name nobody has used, the ZIP lands as draft 0.0.1.
            await httpAi.UploadAgentSpecAsync(
                zipBytes, $"{agentName}.zip", overwrite: false, cancellationToken: ct);

            // 2. Draft -> reviewing -> online. A Nacos publish pipeline runs only when an AI
            //    pipeline plugin is installed (off by default — the server ships no
            //    plugin/ai/pipeline implementation and PublishPipelineExecutor.isPipelineAvailable
            //    is false), so with no plugin the submit publishes the version straight away and
            //    the explicit publish below is accepted rather than rejected; with a pipeline
            //    configured the version waits in reviewing and the publish call surfaces the
            //    server's refusal.
            // HTTP: draft -> reviewing.
            await httpAi.SubmitAgentSpecReviewAsync(agentName, InitialVersion, cancellationToken: ct);
            logger.LogInformation("[AgentSpec] submitted for review");

            // HTTP: reviewing -> published; updateLatestLabel moves the "latest" label along.
            await httpAi.PublishAgentSpecAsync(
                agentName, InitialVersion, updateLatestLabel: true, cancellationToken: ct);

            // HTTP: explicit online switch with the public scope; a no-op once publish has
            //       already made the version online.
            await httpAi.OnlineAgentSpecAsync(
                agentName, InitialVersion, scope: AiConstants.AgentSpec.ScopePublic, cancellationToken: ct);
            logger.LogInformation("[AgentSpec] published + online");

            // 3. Read the released version back through the client route, by version and by
            //    label. The client route only serves online versions, so this doubles as the
            //    check that publish + online actually took effect.
            // HTTP: client read by version (v3/client/ai/agentspecs) with md5-based caching.
            var spec = await httpAi.GetAgentSpecAsync(agentName, InitialVersion, cancellationToken: ct);
            if (spec is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    "GetAgentSpec returned null after publish");
            }

            var resourceNames = spec.Resource?.Values
                .Select(resource => resource.Name)
                .OfType<string>()
                .ToList() ?? [];

            logger.LogInformation(
                "[AgentSpec] read back {Name}@{Version} contentLength={Length} resources=[{Resources}]",
                agentName, InitialVersion, spec.Content?.Length ?? 0, string.Join(", ", resourceNames));

            // HTTP: client read by label (same route, label=latest instead of version).
            var byLabel = await httpAi.GetAgentSpecByLabelAsync(
                agentName, AiConstants.AgentSpec.LabelLatest, cancellationToken: ct);
            if (byLabel is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"GetAgentSpecByLabel returned null for the '{AiConstants.AgentSpec.LabelLatest}' label");
            }

            logger.LogInformation("[AgentSpec] '{Label}' label resolves to {Name}",
                AiConstants.AgentSpec.LabelLatest, byLabel.Name);

            if (!resourceNames.Any(name => name.EndsWith("AGENTS.md", StringComparison.OrdinalIgnoreCase)))
            {
                return new SampleResult(SampleOutcome.Failed,
                    $"Uploaded package lost its AGENTS.md resource (resources: {string.Join(", ", resourceNames)})");
            }

            // 4. Cleanup: take the version offline, then delete the AgentSpec. The DELETE route
            //    binds the name and version, and the version argument is optional — passing it
            //    here removes only the 0.0.1 this run created.
            // HTTP: online -> offline.
            await httpAi.OfflineAgentSpecAsync(agentName, InitialVersion, cancellationToken: ct);
            // HTTP: delete the AgentSpec version.
            await httpAi.DeleteAgentSpecAsync(agentName, InitialVersion, cancellationToken: ct);

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AgentSpec] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }

    /// <summary>
    /// Builds an AgentSpec package in memory: the mandatory <c>manifest.json</c> plus the
    /// <c>AGENTS.md</c> instruction body. Entries are written with UTF-8 and LF line breaks, the
    /// encoding and newline style the server's parser and content digest are written against.
    /// </summary>
    private static byte[] BuildAgentSpecPackage(string agentName)
    {
        // manifest.json is the only entry the server requires: AgentSpecZipParser.parseManifest
        // takes the AgentSpec name from worker.suggested_name (missing or blank is a 400
        // PARAMETER_MISSING), then reads the optional top-level description and the optional
        // tags (falling back to bizTags), where tags may be a JSON array of strings or a single
        // string. Property names are snake_case exactly as the server reads them.
        var manifest = JsonSerializer.Serialize(new
        {
            worker = new { suggested_name = agentName },
            description = "Sample AgentSpec generated by RedNb.Nacos.Sample.AI.",
            tags = new[] { "sample", "agentspec" }
        });

        // AGENTS.md is the agent instruction body. The parser turns every non-manifest entry
        // into an AgentSpecResource, and the operation service picks the entry named AGENTS.md
        // (case-insensitive) as the version's main content.
        var agentsMd = string.Join('\n', new[]
        {
            $"# {agentName}",
            string.Empty,
            "Sample travel-planning agent spec generated by RedNb.Nacos.Sample.AI.",
            string.Empty,
            "## Instructions",
            string.Empty,
            "Answer travel questions using the itinerary tools exposed by this agent.",
            string.Empty
        });

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "manifest.json", manifest);
            WriteEntry(zip, "AGENTS.md", agentsMd);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Writes one UTF-8 text entry into an AgentSpec package.
    /// </summary>
    private static void WriteEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName);
        using var entryStream = entry.Open();
        entryStream.Write(Encoding.UTF8.GetBytes(content));
    }
}
