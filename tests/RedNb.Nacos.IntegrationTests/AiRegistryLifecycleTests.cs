using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RedNb.Nacos.Grpc.Ai;
using RedNb.Nacos.Ai.Models.Prompt;
using Xunit;

namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class AiRegistryLifecycleTests
{
    [Fact]
    public async Task PromptDraftPublishRenderAndDelete()
    {
        await using var client = new NacosAiClient(ConfigReliabilityTests.Options());
        var key = "audit-prompt-" + Guid.NewGuid().ToString("N");
        await client.CreatePromptDraftAsync(key, "1.0.0", "Hello {{name}}", variables: [new PromptVariable { Name = "name", DefaultValue = "world" }]);
        try
        {
            await client.SubmitPromptReviewAsync(key, "1.0.0");
            await client.ForcePublishPromptAsync(key, "1.0.0");
            await ConfigReliabilityTests.Eventually(async () => await client.GetPromptAsync(key, "1.0.0") != null);
            var prompt = await client.GetPromptAsync(key, "1.0.0");
            Assert.Equal("Hello reader", prompt!.Render(new Dictionary<string, string> { ["name"] = "reader" }));
            Assert.NotNull(await client.GetPromptByLabelAsync(key, "latest"));
        }
        finally { try { await client.OfflinePromptAsync(key, "1.0.0"); } finally { await client.DeletePromptAsync(key); } }
    }

    [Fact]
    public async Task SkillUploadPublishDownloadPreservesZip()
    {
        await using var client = new NacosAiClient(ConfigReliabilityTests.Options());
        var key = "audit-skill-" + Guid.NewGuid().ToString("N");
        var markdown = "---\nname: " + key + "\ndescription: SDK integration test\nversion: 1.0.0\n---\n# Test skill\n";
        await client.UploadSkillZipAsync(Zip(new() { ["SKILL.md"] = markdown }), key + ".zip", targetVersion: "1.0.0");
        try
        {
            await client.SubmitSkillReviewAsync(key, "1.0.0");
            await client.ForcePublishSkillAsync(key, "1.0.0");
            await ConfigReliabilityTests.Eventually(async () => await client.DownloadSkillZipByVersionAsync(key, "1.0.0") != null);
            var package = await client.DownloadSkillZipByVersionAsync(key, "1.0.0");
            using var zip = new ZipArchive(new MemoryStream(package!.ZipContent));
            var entry = Assert.Single(zip.Entries, e => e.Name == "SKILL.md");
            using var reader = new StreamReader(entry.Open());
            Assert.Contains("# Test skill", await reader.ReadToEndAsync());
            Assert.False(string.IsNullOrWhiteSpace(package.Md5));
        }
        finally { try { await client.OfflineSkillAsync(key, "1.0.0"); } finally { await client.DeleteSkillAsync(key); } }
    }

    [Fact]
    public async Task AgentSpecUploadPublishAndReadResources()
    {
        await using var client = new NacosAiClient(ConfigReliabilityTests.Options());
        var key = "audit-spec-" + Guid.NewGuid().ToString("N");
        var manifest = JsonSerializer.Serialize(new { worker = new { suggested_name = key }, description = "SDK integration test" });
        await client.UploadAgentSpecAsync(Zip(new() { ["manifest.json"] = manifest, ["AGENTS.md"] = "# Integration test\n" }), key + ".zip");
        try
        {
            await client.SubmitAgentSpecReviewAsync(key, "0.0.1");
            await client.ForcePublishAgentSpecAsync(key, "0.0.1");
            await ConfigReliabilityTests.Eventually(async () => await client.GetAgentSpecAsync(key, "0.0.1") != null);
            var spec = await client.GetAgentSpecByLabelAsync(key, "latest");
            using var content = JsonDocument.Parse(spec!.Content!);
            Assert.Equal(key, content.RootElement.GetProperty("worker").GetProperty("suggested_name").GetString());
            var instructions = Assert.Single(spec.Resource!.Values, resource => resource.Name == "AGENTS.md");
            Assert.Contains("Integration test", instructions.Content);
        }
        finally { try { await client.OfflineAgentSpecAsync(key, "0.0.1"); } finally { await client.DeleteAgentSpecAsync(key); } }
    }

    private static byte[] Zip(Dictionary<string, string> files)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var file in files)
            {
                using var writer = new StreamWriter(zip.CreateEntry(file.Key).Open(), new UTF8Encoding(false));
                writer.Write(file.Value);
            }
        return stream.ToArray();
    }
}
