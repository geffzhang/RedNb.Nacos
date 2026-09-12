using RedNb.Nacos.Ai.Listener;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Mcp.Import;
using RedNb.Nacos.Ai.Models.Mcp.Validation;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Ai.Models.Skill;
using RedNb.Nacos.Ai.Models;
using RedNb.Nacos.Ai;
using RedNb.Nacos;
using Model = RedNb.Nacos.Ai.Models;

namespace RedNb.Nacos.Grpc.Ai;

/// <summary>Routes AI operations to the Nacos 3.2.4 transport that implements them.</summary>
public sealed partial class NacosAiClient : IAiService
{
    private readonly Lazy<NacosGrpcAiService> _grpc;
    private readonly Lazy<RedNb.Nacos.Http.Ai.NacosAiService> _http;
    private RedNb.Nacos.Http.Ai.NacosAiService Http
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _http.Value; }
    }
    /// <summary>Creates a client without opening connections or starting subscriptions.</summary>
    public NacosAiClient(NacosClientOptions options)
    {
        options.Validate();
        _grpc = new(() => new NacosGrpcAiService(options));
        _http = new(() => new RedNb.Nacos.Http.Ai.NacosAiService(options));
    }
    /// <inheritdoc />
    public Task<McpServerDetailInfo?> GetMcpServerAsync(string mcpName, CancellationToken cancellationToken = default) => Http.GetMcpServerAsync(mcpName, cancellationToken);
    /// <inheritdoc />
    public Task<McpServerDetailInfo?> GetMcpServerAsync(string mcpName, string? version, CancellationToken cancellationToken = default) => Http.GetMcpServerAsync(mcpName, version, cancellationToken);
    /// <inheritdoc />
    public Task<string> ReleaseMcpServerAsync(McpServerBasicInfo serverSpecification, McpToolSpecification? toolSpecification, CancellationToken cancellationToken = default) => Http.ReleaseMcpServerAsync(serverSpecification, toolSpecification, cancellationToken);
    /// <inheritdoc />
    public Task<string> ReleaseMcpServerAsync(McpServerBasicInfo serverSpecification, McpToolSpecification? toolSpecification, McpEndpointSpec? endpointSpecification, CancellationToken cancellationToken = default) => Http.ReleaseMcpServerAsync(serverSpecification, toolSpecification, endpointSpecification, cancellationToken);
    /// <inheritdoc />
    public Task RegisterMcpServerEndpointAsync(string mcpName, string address, int port, CancellationToken cancellationToken = default) => RegisterMcpServerEndpointAsync(mcpName, address, port, null, cancellationToken);
    /// <inheritdoc />
    public Task RegisterMcpServerEndpointAsync(string mcpName, string address, int port, string? version, CancellationToken cancellationToken = default) => RegisterMcpAsync(mcpName, address, port, version, cancellationToken);
    /// <inheritdoc />
    public Task DeregisterMcpServerEndpointAsync(string mcpName, string address, int port, CancellationToken cancellationToken = default) => DeregisterMcpAsync(mcpName, address, port, cancellationToken);
    /// <inheritdoc />
    public Task<McpServerDetailInfo?> SubscribeMcpServerAsync(string mcpName, AbstractNacosMcpServerListener listener, CancellationToken cancellationToken = default) => Http.SubscribeMcpServerAsync(mcpName, listener, cancellationToken);
    /// <inheritdoc />
    public Task<McpServerDetailInfo?> SubscribeMcpServerAsync(string mcpName, string? version, AbstractNacosMcpServerListener listener, CancellationToken cancellationToken = default) => Http.SubscribeMcpServerAsync(mcpName, version, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribeMcpServerAsync(string mcpName, AbstractNacosMcpServerListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribeMcpServerAsync(mcpName, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribeMcpServerAsync(string mcpName, string? version, AbstractNacosMcpServerListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribeMcpServerAsync(mcpName, version, listener, cancellationToken);
    /// <inheritdoc />
    public Task DeleteMcpServerAsync(string mcpName, string? version = null, CancellationToken cancellationToken = default) => Http.DeleteMcpServerAsync(mcpName, version, cancellationToken);
    /// <inheritdoc />
    public Task<PageResult<McpServerBasicInfo>> ListMcpServersAsync(string? mcpName = null, string? search = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => Http.ListMcpServersAsync(mcpName, search, pageNo, pageSize, cancellationToken);
    /// <inheritdoc />
    public Task<McpServerImportValidationResult> ValidateImportAsync(McpServerImportRequest request, CancellationToken cancellationToken = default) => Http.ValidateImportAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<McpServerImportResponse> ImportMcpServersAsync(McpServerImportRequest request, CancellationToken cancellationToken = default) => Http.ImportMcpServersAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<McpToolSpec?> RefreshMcpToolAsync(string mcpName, string toolName, string? version = null, CancellationToken cancellationToken = default) => Http.RefreshMcpToolAsync(mcpName, toolName, version, cancellationToken);
    /// <inheritdoc />
    public Task<McpToolSpec?> GetMcpToolAsync(string mcpName, string toolName, string? version = null, CancellationToken cancellationToken = default) => Http.GetMcpToolAsync(mcpName, toolName, version, cancellationToken);
    /// <inheritdoc />
    public Task DeleteMcpToolAsync(string mcpName, string toolName, string? version = null, CancellationToken cancellationToken = default) => Http.DeleteMcpToolAsync(mcpName, toolName, version, cancellationToken);
    /// <inheritdoc />
    public Task UpdateMcpToolAsync(string mcpName, McpToolSpec toolSpec, string? version = null, CancellationToken cancellationToken = default) => Http.UpdateMcpToolAsync(mcpName, toolSpec, version, cancellationToken);
    /// <inheritdoc />
    public async Task ShutdownAsync(CancellationToken cancellationToken = default) => await DisposeAsync();
    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> GetAgentCardAsync(string agentName, CancellationToken cancellationToken = default) => Http.GetAgentCardAsync(agentName, cancellationToken);
    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> GetAgentCardAsync(string agentName, string? version, CancellationToken cancellationToken = default) => Http.GetAgentCardAsync(agentName, version, cancellationToken);
    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> GetAgentCardAsync(string agentName, string? version, string? registrationType, CancellationToken cancellationToken = default) => Http.GetAgentCardAsync(agentName, version, registrationType, cancellationToken);
    /// <inheritdoc />
    public Task ReleaseAgentCardAsync(AgentCard agentCard, CancellationToken cancellationToken = default) => Http.ReleaseAgentCardAsync(agentCard, cancellationToken);
    /// <inheritdoc />
    public Task ReleaseAgentCardAsync(AgentCard agentCard, string registrationType, CancellationToken cancellationToken = default) => Http.ReleaseAgentCardAsync(agentCard, registrationType, cancellationToken);
    /// <inheritdoc />
    public Task ReleaseAgentCardAsync(AgentCard agentCard, string registrationType, bool setAsLatest, CancellationToken cancellationToken = default) => Http.ReleaseAgentCardAsync(agentCard, registrationType, setAsLatest, cancellationToken);
    /// <inheritdoc />
    public Task RegisterAgentEndpointAsync(string agentName, string version, string address, int port, CancellationToken cancellationToken = default) => RegisterAgentEndpointAsync(agentName, version, address, port, AiConstants.A2a.TransportJsonRpc, cancellationToken);
    /// <inheritdoc />
    public Task RegisterAgentEndpointAsync(string agentName, string version, string address, int port, string transport, CancellationToken cancellationToken = default) => RegisterAgentEndpointAsync(agentName, new AgentEndpoint { Version = version, Address = address, Port = port, Transport = transport }, cancellationToken);
    /// <inheritdoc />
    public Task RegisterAgentEndpointAsync(string agentName, AgentEndpoint endpoint, CancellationToken cancellationToken = default) => RegisterAgentAsync(agentName, [endpoint], false, cancellationToken);
    /// <inheritdoc />
    public Task RegisterAgentEndpointsAsync(string agentName, IEnumerable<AgentEndpoint> endpoints, CancellationToken cancellationToken = default) => RegisterAgentAsync(agentName, endpoints.ToList(), true, cancellationToken);
    /// <inheritdoc />
    public Task DeregisterAgentEndpointAsync(string agentName, string version, string address, int port, CancellationToken cancellationToken = default) => DeregisterAgentEndpointAsync(agentName, new AgentEndpoint { Version = version, Address = address, Port = port }, cancellationToken);
    /// <inheritdoc />
    public Task DeregisterAgentEndpointAsync(string agentName, AgentEndpoint endpoint, CancellationToken cancellationToken = default) => DeregisterAgentAsync(agentName, endpoint, cancellationToken);
    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> SubscribeAgentCardAsync(string agentName, AbstractNacosAgentCardListener listener, CancellationToken cancellationToken = default) => Http.SubscribeAgentCardAsync(agentName, listener, cancellationToken);
    /// <inheritdoc />
    public Task<AgentCardDetailInfo?> SubscribeAgentCardAsync(string agentName, string? version, AbstractNacosAgentCardListener listener, CancellationToken cancellationToken = default) => Http.SubscribeAgentCardAsync(agentName, version, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribeAgentCardAsync(string agentName, AbstractNacosAgentCardListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribeAgentCardAsync(agentName, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribeAgentCardAsync(string agentName, string? version, AbstractNacosAgentCardListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribeAgentCardAsync(agentName, version, listener, cancellationToken);
    /// <inheritdoc />
    public Task DeleteAgentAsync(string agentName, string? version = null, CancellationToken cancellationToken = default) => Http.DeleteAgentAsync(agentName, version, cancellationToken);
    /// <inheritdoc />
    public Task<PageResult<AgentCardBasicInfo>> ListAgentCardsAsync(string? agentName = null, string? search = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => Http.ListAgentCardsAsync(agentName, search, pageNo, pageSize, cancellationToken);
    /// <inheritdoc />
    public Task<List<string>> ListAgentVersionsAsync(string agentName, CancellationToken cancellationToken = default) => Http.ListAgentVersionsAsync(agentName, cancellationToken);
    /// <inheritdoc />
    public Task<List<AgentVersionInfo>> ListAgentVersionInfosAsync(string agentName, CancellationToken cancellationToken = default) => Http.ListAgentVersionInfosAsync(agentName, cancellationToken);
    /// <inheritdoc />
    public Task<Model.Prompt.Prompt?> GetPromptAsync(string promptKey, CancellationToken cancellationToken = default) => Http.GetPromptAsync(promptKey, cancellationToken);
    /// <inheritdoc />
    public Task<Model.Prompt.Prompt?> GetPromptAsync(string promptKey, string? version, CancellationToken cancellationToken = default) => Http.GetPromptAsync(promptKey, version, cancellationToken);
    /// <inheritdoc />
    public Task<Model.Prompt.Prompt?> GetPromptByLabelAsync(string promptKey, string label, CancellationToken cancellationToken = default) => Http.GetPromptByLabelAsync(promptKey, label, cancellationToken);
    /// <inheritdoc />
    public Task<PageResult<PromptMetaSummary>> SearchPromptsAsync(string? query = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => Http.SearchPromptsAsync(query, pageNo, pageSize, cancellationToken);
    /// <inheritdoc />
    public Task<Model.Prompt.Prompt?> SubscribePromptAsync(string promptKey, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default) => Http.SubscribePromptAsync(promptKey, listener, cancellationToken);
    /// <inheritdoc />
    public Task<Model.Prompt.Prompt?> SubscribePromptAsync(string promptKey, string? version, string? label, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default) => Http.SubscribePromptAsync(promptKey, version, label, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribePromptAsync(string promptKey, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribePromptAsync(promptKey, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribePromptAsync(string promptKey, string? version, string? label, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribePromptAsync(promptKey, version, label, listener, cancellationToken);
    /// <inheritdoc />
    public Task<PageResult<PromptMetaSummary>> ListPromptsAsync(string? promptKey = null, string? search = null, string? bizTags = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => Http.ListPromptsAsync(promptKey, search, bizTags, pageNo, pageSize, cancellationToken);
    /// <inheritdoc />
    public Task<PromptMetaInfo?> GetPromptMetaAsync(string promptKey, CancellationToken cancellationToken = default) => Http.GetPromptMetaAsync(promptKey, cancellationToken);
    /// <inheritdoc />
    public Task<List<PromptVersionSummary>> ListPromptVersionsAsync(string promptKey, CancellationToken cancellationToken = default) => Http.ListPromptVersionsAsync(promptKey, cancellationToken);
    /// <inheritdoc />
    public Task<PromptVersionInfo?> GetPromptVersionDetailAsync(string promptKey, string version, CancellationToken cancellationToken = default) => Http.GetPromptVersionDetailAsync(promptKey, version, cancellationToken);
    /// <inheritdoc />
    public Task CreatePromptDraftAsync(string promptKey, string targetVersion, string template, string? basedOnVersion = null, IEnumerable<PromptVariable>? variables = null, string? commitMsg = null, string? description = null, IEnumerable<string>? bizTags = null, CancellationToken cancellationToken = default) => Http.CreatePromptDraftAsync(promptKey, targetVersion, template, basedOnVersion, variables, commitMsg, description, bizTags, cancellationToken);
    /// <inheritdoc />
    public Task UpdatePromptDraftAsync(string promptKey, string version, string template, IEnumerable<PromptVariable>? variables = null, string? commitMsg = null, CancellationToken cancellationToken = default) => Http.UpdatePromptDraftAsync(promptKey, version, template, variables, commitMsg, cancellationToken);
    /// <inheritdoc />
    public Task DeletePromptDraftAsync(string promptKey, string version, CancellationToken cancellationToken = default) => Http.DeletePromptDraftAsync(promptKey, version, cancellationToken);
    /// <inheritdoc />
    public Task SubmitPromptReviewAsync(string promptKey, string version, CancellationToken cancellationToken = default) => Http.SubmitPromptReviewAsync(promptKey, version, cancellationToken);
    /// <inheritdoc />
    public Task PublishPromptAsync(string promptKey, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default) => Http.PublishPromptAsync(promptKey, version, updateLatestLabel, cancellationToken);
    /// <inheritdoc />
    public Task PublishPromptAsync(string promptKey, string version, string template, string? commitMsg = null, string? description = null, IEnumerable<string>? bizTags = null, IEnumerable<PromptVariable>? variables = null, CancellationToken cancellationToken = default) => Http.PublishPromptAsync(promptKey, version, template, commitMsg, description, bizTags, variables, cancellationToken);
    /// <inheritdoc />
    public Task ForcePublishPromptAsync(string promptKey, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default) => Http.ForcePublishPromptAsync(promptKey, version, updateLatestLabel, cancellationToken);
    /// <inheritdoc />
    public Task RedraftPromptAsync(string promptKey, string version, CancellationToken cancellationToken = default) => Http.RedraftPromptAsync(promptKey, version, cancellationToken);
    /// <inheritdoc />
    public Task OnlinePromptAsync(string promptKey, string version, CancellationToken cancellationToken = default) => Http.OnlinePromptAsync(promptKey, version, cancellationToken);
    /// <inheritdoc />
    public Task OfflinePromptAsync(string promptKey, string version, CancellationToken cancellationToken = default) => Http.OfflinePromptAsync(promptKey, version, cancellationToken);
    /// <inheritdoc />
    public Task UpdatePromptLabelsAsync(string promptKey, IDictionary<string, string> labels, CancellationToken cancellationToken = default) => Http.UpdatePromptLabelsAsync(promptKey, labels, cancellationToken);
    /// <inheritdoc />
    public Task UpdatePromptDescriptionAsync(string promptKey, string description, CancellationToken cancellationToken = default) => Http.UpdatePromptDescriptionAsync(promptKey, description, cancellationToken);
    /// <inheritdoc />
    public Task UpdatePromptBizTagsAsync(string promptKey, IEnumerable<string> bizTags, CancellationToken cancellationToken = default) => Http.UpdatePromptBizTagsAsync(promptKey, bizTags, cancellationToken);
    /// <inheritdoc />
    public Task DeletePromptAsync(string promptKey, string? version = null, CancellationToken cancellationToken = default) => Http.DeletePromptAsync(promptKey, version, cancellationToken);
    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipAsync(string skillName, CancellationToken cancellationToken = default) => Http.DownloadSkillZipAsync(skillName, cancellationToken);
    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipByVersionAsync(string skillName, string version, CancellationToken cancellationToken = default) => Http.DownloadSkillZipByVersionAsync(skillName, version, cancellationToken);
    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipByLabelAsync(string skillName, string label, CancellationToken cancellationToken = default) => Http.DownloadSkillZipByLabelAsync(skillName, label, cancellationToken);
    /// <inheritdoc />
    public Task<PageResult<SkillSummary>> SearchSkillsAsync(string? query = null, IEnumerable<string>? tagsAll = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => Http.SearchSkillsAsync(query, tagsAll, pageNo, pageSize, cancellationToken);
    /// <inheritdoc />
    public Task<SkillPackage?> SubscribeSkillAsync(string skillName, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default) => Http.SubscribeSkillAsync(skillName, listener, cancellationToken);
    /// <inheritdoc />
    public Task<SkillPackage?> SubscribeSkillAsync(string skillName, string? version, string? label, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default) => Http.SubscribeSkillAsync(skillName, version, label, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribeSkillAsync(string skillName, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribeSkillAsync(skillName, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribeSkillAsync(string skillName, string? version, string? label, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribeSkillAsync(skillName, version, label, listener, cancellationToken);
    /// <inheritdoc />
    public Task<PageResult<SkillSummary>> ListSkillsAsync(string? skillName = null, string? search = null, string? orderBy = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => Http.ListSkillsAsync(skillName, search, orderBy, pageNo, pageSize, cancellationToken);
    /// <inheritdoc />
    public Task<SkillMeta?> GetSkillMetaAsync(string skillName, CancellationToken cancellationToken = default) => Http.GetSkillMetaAsync(skillName, cancellationToken);
    /// <inheritdoc />
    public Task<Skill?> GetSkillDetailAsync(string skillName, string? version = null, CancellationToken cancellationToken = default) => Http.GetSkillDetailAsync(skillName, version, cancellationToken);
    /// <inheritdoc />
    public Task UploadSkillZipAsync(byte[] zipContent, string fileName, bool overwrite = false, string? targetVersion = null, string? commitMsg = null, string? uploadAction = null, bool autoPublishIfNew = false, CancellationToken cancellationToken = default) => Http.UploadSkillZipAsync(zipContent, fileName, overwrite, targetVersion, commitMsg, uploadAction, autoPublishIfNew, cancellationToken);
    /// <inheritdoc />
    public Task CreateSkillDraftAsync(Skill skill, string? basedOnVersion = null, string? targetVersion = null, string? commitMsg = null, CancellationToken cancellationToken = default) => Http.CreateSkillDraftAsync(skill, basedOnVersion, targetVersion, commitMsg, cancellationToken);
    /// <inheritdoc />
    public Task UpdateSkillDraftAsync(Skill skill, string version, bool setAsLatest = false, string? commitMsg = null, CancellationToken cancellationToken = default) => Http.UpdateSkillDraftAsync(skill, version, setAsLatest, commitMsg, cancellationToken);
    /// <inheritdoc />
    public Task DeleteSkillDraftAsync(string skillName, string version, CancellationToken cancellationToken = default) => Http.DeleteSkillDraftAsync(skillName, version, cancellationToken);
    /// <inheritdoc />
    public Task SubmitSkillReviewAsync(string skillName, string version, CancellationToken cancellationToken = default) => Http.SubmitSkillReviewAsync(skillName, version, cancellationToken);
    /// <inheritdoc />
    public Task PublishSkillAsync(string skillName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default) => Http.PublishSkillAsync(skillName, version, updateLatestLabel, cancellationToken);
    /// <inheritdoc />
    public Task ForcePublishSkillAsync(string skillName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default) => Http.ForcePublishSkillAsync(skillName, version, updateLatestLabel, cancellationToken);
    /// <inheritdoc />
    public Task RedraftSkillAsync(string skillName, string version, CancellationToken cancellationToken = default) => Http.RedraftSkillAsync(skillName, version, cancellationToken);
    /// <inheritdoc />
    public Task OnlineSkillAsync(string skillName, string version, string? scope = null, CancellationToken cancellationToken = default) => Http.OnlineSkillAsync(skillName, version, scope, cancellationToken);
    /// <inheritdoc />
    public Task OfflineSkillAsync(string skillName, string version, CancellationToken cancellationToken = default) => Http.OfflineSkillAsync(skillName, version, cancellationToken);
    /// <inheritdoc />
    public Task UpdateSkillScopeAsync(string skillName, string scope, CancellationToken cancellationToken = default) => Http.UpdateSkillScopeAsync(skillName, scope, cancellationToken);
    /// <inheritdoc />
    public Task UpdateSkillLabelsAsync(string skillName, IDictionary<string, string> labels, CancellationToken cancellationToken = default) => Http.UpdateSkillLabelsAsync(skillName, labels, cancellationToken);
    /// <inheritdoc />
    public Task UpdateSkillBizTagsAsync(string skillName, IEnumerable<string> bizTags, CancellationToken cancellationToken = default) => Http.UpdateSkillBizTagsAsync(skillName, bizTags, cancellationToken);
    /// <inheritdoc />
    public Task DeleteSkillAsync(string skillName, string? version = null, CancellationToken cancellationToken = default) => Http.DeleteSkillAsync(skillName, version, cancellationToken);
    /// <inheritdoc />
    public Task<Model.AgentSpec.AgentSpec?> GetAgentSpecAsync(string agentSpecName, CancellationToken cancellationToken = default) => Http.GetAgentSpecAsync(agentSpecName, cancellationToken);
    /// <inheritdoc />
    public Task<Model.AgentSpec.AgentSpec?> GetAgentSpecAsync(string agentSpecName, string? version, CancellationToken cancellationToken = default) => Http.GetAgentSpecAsync(agentSpecName, version, cancellationToken);
    /// <inheritdoc />
    public Task<Model.AgentSpec.AgentSpec?> GetAgentSpecByLabelAsync(string agentSpecName, string label, CancellationToken cancellationToken = default) => Http.GetAgentSpecByLabelAsync(agentSpecName, label, cancellationToken);
    /// <inheritdoc />
    public Task<PageResult<AgentSpecSummary>> SearchAgentSpecsAsync(string? keyword = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => Http.SearchAgentSpecsAsync(keyword, pageNo, pageSize, cancellationToken);
    /// <inheritdoc />
    public Task<Model.AgentSpec.AgentSpec?> SubscribeAgentSpecAsync(string agentSpecName, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default) => Http.SubscribeAgentSpecAsync(agentSpecName, listener, cancellationToken);
    /// <inheritdoc />
    public Task<Model.AgentSpec.AgentSpec?> SubscribeAgentSpecAsync(string agentSpecName, string? version, string? label, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default) => Http.SubscribeAgentSpecAsync(agentSpecName, version, label, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribeAgentSpecAsync(string agentSpecName, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribeAgentSpecAsync(agentSpecName, listener, cancellationToken);
    /// <inheritdoc />
    public Task UnsubscribeAgentSpecAsync(string agentSpecName, string? version, string? label, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default) => Http.UnsubscribeAgentSpecAsync(agentSpecName, version, label, listener, cancellationToken);
    /// <inheritdoc />
    public Task<PageResult<AgentSpecSummary>> ListAgentSpecsAsync(string? agentSpecName = null, string? search = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default) => Http.ListAgentSpecsAsync(agentSpecName, search, pageNo, pageSize, cancellationToken);
    /// <inheritdoc />
    public Task<AgentSpecMeta?> GetAgentSpecMetaAsync(string agentSpecName, CancellationToken cancellationToken = default) => Http.GetAgentSpecMetaAsync(agentSpecName, cancellationToken);
    /// <inheritdoc />
    public Task<Model.AgentSpec.AgentSpec?> GetAgentSpecDetailAsync(string agentSpecName, string? version = null, CancellationToken cancellationToken = default) => Http.GetAgentSpecDetailAsync(agentSpecName, version, cancellationToken);
    /// <inheritdoc />
    public Task UploadAgentSpecAsync(byte[] content, string fileName, bool overwrite = false, CancellationToken cancellationToken = default) => Http.UploadAgentSpecAsync(content, fileName, overwrite, cancellationToken);
    /// <inheritdoc />
    public Task CreateAgentSpecDraftAsync(Model.AgentSpec.AgentSpec agentSpec, string? basedOnVersion = null, string? targetVersion = null, CancellationToken cancellationToken = default) => Http.CreateAgentSpecDraftAsync(agentSpec, basedOnVersion, targetVersion, cancellationToken);
    /// <inheritdoc />
    public Task UpdateAgentSpecDraftAsync(Model.AgentSpec.AgentSpec agentSpec, string version, bool setAsLatest = false, CancellationToken cancellationToken = default) => Http.UpdateAgentSpecDraftAsync(agentSpec, version, setAsLatest, cancellationToken);
    /// <inheritdoc />
    public Task DeleteAgentSpecDraftAsync(string agentSpecName, string version, CancellationToken cancellationToken = default) => Http.DeleteAgentSpecDraftAsync(agentSpecName, version, cancellationToken);
    /// <inheritdoc />
    public Task SubmitAgentSpecReviewAsync(string agentSpecName, string version, CancellationToken cancellationToken = default) => Http.SubmitAgentSpecReviewAsync(agentSpecName, version, cancellationToken);
    /// <inheritdoc />
    public Task PublishAgentSpecAsync(string agentSpecName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default) => Http.PublishAgentSpecAsync(agentSpecName, version, updateLatestLabel, cancellationToken);
    /// <inheritdoc />
    public Task ForcePublishAgentSpecAsync(string agentSpecName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default) => Http.ForcePublishAgentSpecAsync(agentSpecName, version, updateLatestLabel, cancellationToken);
    /// <inheritdoc />
    public Task RedraftAgentSpecAsync(string agentSpecName, string version, CancellationToken cancellationToken = default) => Http.RedraftAgentSpecAsync(agentSpecName, version, cancellationToken);
    /// <inheritdoc />
    public Task OnlineAgentSpecAsync(string agentSpecName, string version, string? scope = null, CancellationToken cancellationToken = default) => Http.OnlineAgentSpecAsync(agentSpecName, version, scope, cancellationToken);
    /// <inheritdoc />
    public Task OfflineAgentSpecAsync(string agentSpecName, string version, CancellationToken cancellationToken = default) => Http.OfflineAgentSpecAsync(agentSpecName, version, cancellationToken);
    /// <inheritdoc />
    public Task UpdateAgentSpecScopeAsync(string agentSpecName, string scope, CancellationToken cancellationToken = default) => Http.UpdateAgentSpecScopeAsync(agentSpecName, scope, cancellationToken);
    /// <inheritdoc />
    public Task UpdateAgentSpecLabelsAsync(string agentSpecName, IDictionary<string, string> labels, CancellationToken cancellationToken = default) => Http.UpdateAgentSpecLabelsAsync(agentSpecName, labels, cancellationToken);
    /// <inheritdoc />
    public Task UpdateAgentSpecBizTagsAsync(string agentSpecName, IEnumerable<string> bizTags, CancellationToken cancellationToken = default) => Http.UpdateAgentSpecBizTagsAsync(agentSpecName, bizTags, cancellationToken);
    /// <inheritdoc />
    public Task DeleteAgentSpecAsync(string agentSpecName, string? version = null, CancellationToken cancellationToken = default) => Http.DeleteAgentSpecAsync(agentSpecName, version, cancellationToken);
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _recoveryCts.CancelAsync();
        if (_recoveryTask != null) await _recoveryTask;
        _recoveryCts.Dispose();
        if (_grpc.IsValueCreated) await _grpc.Value.DisposeAsync();
        if (_http.IsValueCreated) await _http.Value.DisposeAsync();
    }
}
