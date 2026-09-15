using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Mcp.Import;
using RedNb.Nacos.Ai.Models.Mcp.Validation;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Ai.Models.Skill;
using RedNb.Nacos.Http.Ai;
using RedNb.Nacos.Http.Transport;

namespace RedNb.Nacos.Http.Serialization;

/// <summary>
/// Source-generated metadata for the SDK-internal API envelope types
/// (<see cref="NacosPromptService.ApiResult{T}"/>/<c>PagedData&lt;T&gt;</c>) and
/// the login response.
/// </summary>
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<AgentSpecSummary>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<AgentSpecMeta>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<AgentSpec>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<McpServerDetailInfo>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<string>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<McpServerBasicInfo>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<McpServerImportValidationResult>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<McpServerImportResponse>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<AgentCardDetailInfo>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<AgentCardBasicInfo>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<List<AgentVersionInfo>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<PromptMetaSummary>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<PromptMetaInfo>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<List<PromptVersionSummary>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<PromptVersionInfo>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<Prompt>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<SkillSummary>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<SkillMeta>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<Skill>))]
[JsonSerializable(typeof(NacosAiService.ApiResult<NacosAiService.PagedData<McpServerBasicInfo>>), TypeInfoPropertyName = "AiApiResultPagedDataMcpServerBasicInfo")]
[JsonSerializable(typeof(NacosAiService.ApiResult<McpServerDetailInfo>), TypeInfoPropertyName = "AiApiResultMcpServerDetailInfo")]
[JsonSerializable(typeof(NacosAiService.ApiResult<string>), TypeInfoPropertyName = "AiApiResultString")]
[JsonSerializable(typeof(NacosAiService.ApiResult<McpServerImportValidationResult>), TypeInfoPropertyName = "AiApiResultMcpServerImportValidationResult")]
[JsonSerializable(typeof(NacosAiService.ApiResult<McpServerImportResponse>), TypeInfoPropertyName = "AiApiResultMcpServerImportResponse")]
[JsonSerializable(typeof(NacosAiService.ApiResult<AgentCardDetailInfo>), TypeInfoPropertyName = "AiApiResultAgentCardDetailInfo")]
[JsonSerializable(typeof(NacosAiService.ApiResult<NacosAiService.PagedData<AgentCardBasicInfo>>), TypeInfoPropertyName = "AiApiResultPagedDataAgentCardBasicInfo")]
[JsonSerializable(typeof(NacosAiService.ApiResult<List<AgentVersionInfo>>), TypeInfoPropertyName = "AiApiResultListAgentVersionInfo")]
[JsonSerializable(typeof(NacosAiService.PagedData<McpServerBasicInfo>), TypeInfoPropertyName = "AiPagedDataMcpServerBasicInfo")]
[JsonSerializable(typeof(NacosAiService.PagedData<AgentCardBasicInfo>), TypeInfoPropertyName = "AiPagedDataAgentCardBasicInfo")]
[JsonSerializable(typeof(LoginResponse))]
internal sealed partial class NacosHttpInternalJsonContext : JsonSerializerContext
{
}
