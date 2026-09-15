using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Mcp.Import;
using RedNb.Nacos.Ai.Models.Mcp.Validation;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Ai.Models.Skill;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Naming.Models;

namespace RedNb.Nacos.Http.Serialization;

/// <summary>
/// Source-generated metadata for HTTP service models. Default options, matching the
/// reflection-based <c>JsonOptions = new()</c> of 2.0.0 (PascalCase output,
/// case-sensitive reads).
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AgentSpec))]
[JsonSerializable(typeof(AgentSpecMeta))]
[JsonSerializable(typeof(AgentSpecSummary))]
[JsonSerializable(typeof(AgentCard))]
[JsonSerializable(typeof(AgentCardBasicInfo))]
[JsonSerializable(typeof(AgentCardDetailInfo))]
[JsonSerializable(typeof(AgentVersionInfo))]
[JsonSerializable(typeof(List<AgentVersionInfo>))]
[JsonSerializable(typeof(McpServerBasicInfo))]
[JsonSerializable(typeof(McpServerDetailInfo))]
[JsonSerializable(typeof(McpToolSpecification))]
[JsonSerializable(typeof(McpEndpointSpec))]
[JsonSerializable(typeof(McpServerImportValidationResult))]
[JsonSerializable(typeof(McpServerImportResponse))]
[JsonSerializable(typeof(McpServerRemoteServiceConfig))]
[JsonSerializable(typeof(Prompt))]
[JsonSerializable(typeof(PromptMetaSummary))]
[JsonSerializable(typeof(PromptMetaInfo))]
[JsonSerializable(typeof(PromptVariable))]
[JsonSerializable(typeof(List<PromptVariable>))]
[JsonSerializable(typeof(PromptVersionSummary))]
[JsonSerializable(typeof(PromptVersionInfo))]
[JsonSerializable(typeof(List<PromptVersionSummary>))]
[JsonSerializable(typeof(Skill))]
[JsonSerializable(typeof(SkillMeta))]
[JsonSerializable(typeof(SkillSummary))]
[JsonSerializable(typeof(ServiceInfo))]
[JsonSerializable(typeof(NamingSelector))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
public sealed partial class NacosHttpJsonContext : JsonSerializerContext
{
}
