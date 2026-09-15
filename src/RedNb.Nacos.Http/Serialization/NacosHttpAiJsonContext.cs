using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Mcp.Import;
using RedNb.Nacos.Ai.Models.Mcp.Validation;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Ai.Models.Skill;
using RedNb.Nacos.Http.Ai;

namespace RedNb.Nacos.Http.Serialization;

/// <summary>
/// Source-generated metadata for the AI service wire profile, matching the
/// reflection-based <c>JsonOptions = new() { PropertyNamingPolicy = CamelCase,
/// PropertyNameCaseInsensitive = true }</c> of 2.0.0: camelCase output and
/// case-insensitive reads. The AI models are also serialized with default options
/// through <see cref="NacosHttpJsonContext"/>, so the naming policy is expressed
/// here on a dedicated context instead of shared attributes.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<PromptMetaSummary>>), TypeInfoPropertyName = "PromptApiResultPagedDataPromptMetaSummary")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<PromptMetaInfo>), TypeInfoPropertyName = "PromptApiResultPromptMetaInfo")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<List<PromptVersionSummary>>), TypeInfoPropertyName = "PromptApiResultListPromptVersionSummary")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<PromptVersionSummary>>), TypeInfoPropertyName = "PromptApiResultPagedPromptVersions")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<PromptVersionInfo>), TypeInfoPropertyName = "PromptApiResultPromptVersionInfo")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<Prompt>), TypeInfoPropertyName = "PromptApiResultPrompt")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<SkillSummary>>), TypeInfoPropertyName = "PromptApiResultPagedDataSkillSummary")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<SkillMeta>), TypeInfoPropertyName = "PromptApiResultSkillMeta")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<Skill>), TypeInfoPropertyName = "PromptApiResultSkill")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<AgentSpecSummary>>), TypeInfoPropertyName = "PromptApiResultPagedDataAgentSpecSummary")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<AgentSpecMeta>), TypeInfoPropertyName = "PromptApiResultAgentSpecMeta")]
[JsonSerializable(typeof(NacosPromptService.ApiResult<AgentSpec>), TypeInfoPropertyName = "PromptApiResultAgentSpec")]
[JsonSerializable(typeof(NacosAiService.ApiResult<NacosAiService.PagedData<McpServerBasicInfo>>), TypeInfoPropertyName = "AiApiResultPagedDataMcpServerBasicInfo")]
[JsonSerializable(typeof(NacosAiService.ApiResult<McpServerDetailInfo>), TypeInfoPropertyName = "AiApiResultMcpServerDetailInfo")]
[JsonSerializable(typeof(NacosAiService.ApiResult<string>), TypeInfoPropertyName = "AiApiResultString")]
[JsonSerializable(typeof(NacosAiService.ApiResult<McpServerImportValidationResult>), TypeInfoPropertyName = "AiApiResultMcpServerImportValidationResult")]
[JsonSerializable(typeof(NacosAiService.ApiResult<McpServerImportResponse>), TypeInfoPropertyName = "AiApiResultMcpServerImportResponse")]
[JsonSerializable(typeof(NacosAiService.ApiResult<AgentCardDetailInfo>), TypeInfoPropertyName = "AiApiResultAgentCardDetailInfo")]
[JsonSerializable(typeof(NacosAiService.ApiResult<NacosAiService.PagedData<AgentCardBasicInfo>>), TypeInfoPropertyName = "AiApiResultPagedDataAgentCardBasicInfo")]
[JsonSerializable(typeof(NacosAiService.ApiResult<List<AgentVersionInfo>>), TypeInfoPropertyName = "AiApiResultListAgentVersionInfo")]
[JsonSerializable(typeof(McpServerBasicInfo))]
[JsonSerializable(typeof(McpServerDetailInfo))]
[JsonSerializable(typeof(McpToolSpecification))]
[JsonSerializable(typeof(McpEndpointSpec))]
[JsonSerializable(typeof(AgentCard))]
[JsonSerializable(typeof(AgentCardDetailInfo))]
[JsonSerializable(typeof(PromptVariable))]
[JsonSerializable(typeof(List<PromptVariable>))]
[JsonSerializable(typeof(Skill))]
[JsonSerializable(typeof(AgentSpec))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class NacosHttpAiJsonContext : JsonSerializerContext
{
}
