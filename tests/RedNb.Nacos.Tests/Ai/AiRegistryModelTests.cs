using System.Text.Json;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Ai.Models.Skill;
using Xunit;

namespace RedNb.Nacos.Tests.Ai;

/// <summary>
/// Tests for Prompt, Skill and AgentSpec models.
/// </summary>
public class AiRegistryModelTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #region Prompt Model Tests

    [Fact]
    public void Prompt_SerializesCorrectly()
    {
        var prompt = new RedNb.Nacos.Ai.Models.Prompt.Prompt
        {
            PromptKey = "code-review",
            Version = "1.0.0",
            Template = "Review the {{language}} code",
            Md5 = "abc123",
            Variables = new List<PromptVariable>
            {
                new() { Name = "language", DefaultValue = "C#", Description = "Programming language" }
            }
        };

        var json = JsonSerializer.Serialize(prompt, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<RedNb.Nacos.Ai.Models.Prompt.Prompt>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("code-review", deserialized.PromptKey);
        Assert.Equal("1.0.0", deserialized.Version);
        Assert.Equal("Review the {{language}} code", deserialized.Template);
        Assert.Equal("abc123", deserialized.Md5);
        Assert.Single(deserialized.Variables!);
        Assert.Equal("language", deserialized.Variables![0].Name);
        Assert.Equal("C#", deserialized.Variables[0].DefaultValue);
    }

    [Fact]
    public void Prompt_DeserializesCamelCaseServerPayload()
    {
        var json = """
        {
            "promptKey": "assistant",
            "version": "2.1.0",
            "template": "You are {{role}}",
            "md5": "deadbeef",
            "variables": [ { "name": "role", "defaultValue": "helper" } ]
        }
        """;

        var prompt = JsonSerializer.Deserialize<RedNb.Nacos.Ai.Models.Prompt.Prompt>(json, _jsonOptions);

        Assert.NotNull(prompt);
        Assert.Equal("assistant", prompt.PromptKey);
        Assert.Equal("2.1.0", prompt.Version);
        Assert.Equal("deadbeef", prompt.Md5);
        Assert.Single(prompt.Variables!);
    }

    [Fact]
    public void Prompt_Render_ReplacesVariables()
    {
        var prompt = new RedNb.Nacos.Ai.Models.Prompt.Prompt
        {
            Template = "Hello {{name}}, welcome to {{place}}",
            Variables = new List<PromptVariable>
            {
                new() { Name = "place", DefaultValue = "Nacos" }
            }
        };

        var rendered = prompt.Render(new Dictionary<string, string> { ["name"] = "agent" });

        Assert.Equal("Hello agent, welcome to Nacos", rendered);
    }

    [Fact]
    public void PromptMetaSummary_SerializesCorrectly()
    {
        var meta = new PromptMetaSummary
        {
            PromptKey = "assistant",
            Description = "Assistant prompt",
            LatestVersion = "2.1.0",
            EditingVersion = "2.2.0-beta",
            OnlineCnt = 1,
            Labels = new Dictionary<string, string> { ["latest"] = "2.1.0", ["stable"] = "2.0.0" },
            DownloadCount = 42
        };

        var json = JsonSerializer.Serialize(meta, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PromptMetaSummary>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("assistant", deserialized.PromptKey);
        Assert.Equal("2.1.0", deserialized.LatestVersion);
        Assert.Equal(1, deserialized.OnlineCnt);
        Assert.Equal("2.0.0", deserialized.Labels!["stable"]);
        Assert.Equal(42, deserialized.DownloadCount);
    }

    [Fact]
    public void NacosPromptEvent_ExposesPromptProperties()
    {
        var prompt = new RedNb.Nacos.Ai.Models.Prompt.Prompt { PromptKey = "p1", Version = "1.0.0" };
        var evt = new NacosPromptEvent(prompt);

        Assert.Equal("p1", evt.PromptKey);
        Assert.Equal("1.0.0", evt.Version);
        Assert.Same(prompt, evt.Prompt);
    }

    #endregion

    #region Skill Model Tests

    [Fact]
    public void Skill_SerializesCorrectly()
    {
        var skill = new Skill
        {
            NamespaceId = "public",
            Name = "doc-writer",
            Description = "Writes docs",
            SkillMd = "# Doc Writer",
            Resource = new Dictionary<string, SkillResource>
            {
                ["template"] = new() { Name = "template", Type = "file", Content = "base64..." }
            }
        };

        var json = JsonSerializer.Serialize(skill, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Skill>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("doc-writer", deserialized.Name);
        Assert.Equal("# Doc Writer", deserialized.SkillMd);
        Assert.Equal("file", deserialized.Resource!["template"].Type);
    }

    [Fact]
    public void SkillSummary_SerializesLifecycleFields()
    {
        var summary = new SkillSummary
        {
            Name = "doc-writer",
            Scope = "public",
            EditingVersion = "1.1.0",
            OnlineCnt = 2,
            Labels = new Dictionary<string, string> { ["latest"] = "1.0.0" },
            Writable = true
        };

        var json = JsonSerializer.Serialize(summary, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SkillSummary>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("1.1.0", deserialized.EditingVersion);
        Assert.Equal(2, deserialized.OnlineCnt);
        Assert.True(deserialized.Writable);
    }

    [Fact]
    public void NacosSkillEvent_CarriesZipAndMd5()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var evt = new NacosSkillEvent("skill-a", bytes, "md5value", "1.0.0");

        Assert.Equal("skill-a", evt.SkillName);
        Assert.Equal(bytes, evt.ZipContent);
        Assert.Equal("md5value", evt.Md5);
        Assert.Equal("1.0.0", evt.Version);
    }

    #endregion

    #region AgentSpec Model Tests

    [Fact]
    public void AgentSpec_SerializesCorrectly()
    {
        var agentSpec = new RedNb.Nacos.Ai.Models.AgentSpec.AgentSpec
        {
            NamespaceId = "public",
            Name = "travel-agent",
            Description = "Plans trips",
            BizTags = new List<string> { "travel" },
            Content = "kind: AgentSpec",
            Resource = new Dictionary<string, AgentSpecResource>
            {
                ["prompt"] = new() { Name = "prompt", Type = "prompt", Content = "travel/planner" }
            }
        };

        var json = JsonSerializer.Serialize(agentSpec, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<RedNb.Nacos.Ai.Models.AgentSpec.AgentSpec>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("travel-agent", deserialized.Name);
        Assert.Equal("kind: AgentSpec", deserialized.Content);
        Assert.Equal("travel/planner", deserialized.Resource!["prompt"].Content);
    }

    [Fact]
    public void AgentSpecMeta_IncludesVersions()
    {
        var meta = new AgentSpecMeta
        {
            Name = "travel-agent",
            Versions = new List<AgentSpecVersionSummary>
            {
                new() { Version = "1.0.0", Status = "online", Author = "admin" },
                new() { Version = "1.1.0", Status = "draft" }
            }
        };

        var json = JsonSerializer.Serialize(meta, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AgentSpecMeta>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Versions!.Count);
        Assert.Equal("online", deserialized.Versions[0].Status);
        Assert.Equal("draft", deserialized.Versions[1].Status);
    }

    [Fact]
    public void NacosAgentSpecEvent_ExposesResolvedVersion()
    {
        var agentSpec = new RedNb.Nacos.Ai.Models.AgentSpec.AgentSpec { Name = "a1" };
        var evt = new NacosAgentSpecEvent(agentSpec, "3.0.0");

        Assert.Equal("a1", evt.Name);
        Assert.Equal("3.0.0", evt.Version);
        Assert.Same(agentSpec, evt.AgentSpec);
    }

    #endregion
}
