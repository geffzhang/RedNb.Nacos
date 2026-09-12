using RedNb.Nacos.Ai.Models;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Ai.Models.Skill;
using PromptModel = RedNb.Nacos.Ai.Models.Prompt.Prompt;
using AgentSpecModel = RedNb.Nacos.Ai.Models.AgentSpec.AgentSpec;

namespace RedNb.Nacos.Grpc.Ai;

/// <summary>
/// Prompt, Skill and AgentSpec operations of <see cref="NacosGrpcAiService"/>.
/// These Nacos AI registry APIs are HTTP-only on the server side, so all members
/// delegate to the dedicated HTTP sub-services.
/// </summary>
public partial class NacosGrpcAiService
{
    #region Prompt Query

    /// <inheritdoc />
    public Task<PromptModel?> GetPromptAsync(string promptKey, CancellationToken cancellationToken = default)
        => _promptService.GetPromptAsync(promptKey, cancellationToken);

    /// <inheritdoc />
    public Task<PromptModel?> GetPromptAsync(string promptKey, string? version, CancellationToken cancellationToken = default)
        => _promptService.GetPromptAsync(promptKey, version, cancellationToken);

    /// <inheritdoc />
    public Task<PromptModel?> GetPromptByLabelAsync(string promptKey, string label, CancellationToken cancellationToken = default)
        => _promptService.GetPromptByLabelAsync(promptKey, label, cancellationToken);

    /// <inheritdoc />
    public Task<PageResult<PromptMetaSummary>> SearchPromptsAsync(string? query = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        => _promptService.SearchPromptsAsync(query, pageNo, pageSize, cancellationToken);

    #endregion

    #region Prompt Subscription

    /// <inheritdoc />
    public Task<PromptModel?> SubscribePromptAsync(string promptKey, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default)
        => _promptService.SubscribePromptAsync(promptKey, listener, cancellationToken);

    /// <inheritdoc />
    public Task<PromptModel?> SubscribePromptAsync(string promptKey, string? version, string? label, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default)
        => _promptService.SubscribePromptAsync(promptKey, version, label, listener, cancellationToken);

    /// <inheritdoc />
    public Task UnsubscribePromptAsync(string promptKey, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default)
        => _promptService.UnsubscribePromptAsync(promptKey, listener, cancellationToken);

    /// <inheritdoc />
    public Task UnsubscribePromptAsync(string promptKey, string? version, string? label, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default)
        => _promptService.UnsubscribePromptAsync(promptKey, version, label, listener, cancellationToken);

    #endregion

    #region Prompt Management

    /// <inheritdoc />
    public Task<PageResult<PromptMetaSummary>> ListPromptsAsync(string? promptKey = null, string? search = null, string? bizTags = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        => _promptService.ListPromptsAsync(promptKey, search, bizTags, pageNo, pageSize, cancellationToken);

    /// <inheritdoc />
    public Task<PromptMetaInfo?> GetPromptMetaAsync(string promptKey, CancellationToken cancellationToken = default)
        => _promptService.GetPromptMetaAsync(promptKey, cancellationToken);

    /// <inheritdoc />
    public Task<List<PromptVersionSummary>> ListPromptVersionsAsync(string promptKey, CancellationToken cancellationToken = default)
        => _promptService.ListPromptVersionsAsync(promptKey, cancellationToken);

    /// <inheritdoc />
    public Task<PromptVersionInfo?> GetPromptVersionDetailAsync(string promptKey, string version, CancellationToken cancellationToken = default)
        => _promptService.GetPromptVersionDetailAsync(promptKey, version, cancellationToken);

    /// <inheritdoc />
    public Task CreatePromptDraftAsync(string promptKey, string targetVersion, string template, string? basedOnVersion = null, IEnumerable<PromptVariable>? variables = null, string? commitMsg = null, string? description = null, IEnumerable<string>? bizTags = null, CancellationToken cancellationToken = default)
        => _promptService.CreatePromptDraftAsync(promptKey, targetVersion, template, basedOnVersion, variables, commitMsg, description, bizTags, cancellationToken);

    /// <inheritdoc />
    public Task UpdatePromptDraftAsync(string promptKey, string version, string template, IEnumerable<PromptVariable>? variables = null, string? commitMsg = null, CancellationToken cancellationToken = default)
        => _promptService.UpdatePromptDraftAsync(promptKey, version, template, variables, commitMsg, cancellationToken);

    /// <inheritdoc />
    public Task DeletePromptDraftAsync(string promptKey, string version, CancellationToken cancellationToken = default)
        => _promptService.DeletePromptDraftAsync(promptKey, version, cancellationToken);

    /// <inheritdoc />
    public Task SubmitPromptReviewAsync(string promptKey, string version, CancellationToken cancellationToken = default)
        => _promptService.SubmitPromptReviewAsync(promptKey, version, cancellationToken);

    /// <inheritdoc />
    public Task PublishPromptAsync(string promptKey, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
        => _promptService.PublishPromptAsync(promptKey, version, updateLatestLabel, cancellationToken);

    /// <inheritdoc />
    public Task PublishPromptAsync(string promptKey, string version, string template, string? commitMsg = null, string? description = null, IEnumerable<string>? bizTags = null, IEnumerable<PromptVariable>? variables = null, CancellationToken cancellationToken = default)
        => _promptService.PublishPromptAsync(promptKey, version, template, commitMsg, description, bizTags, variables, cancellationToken);

    /// <inheritdoc />
    public Task ForcePublishPromptAsync(string promptKey, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
        => _promptService.ForcePublishPromptAsync(promptKey, version, updateLatestLabel, cancellationToken);

    /// <inheritdoc />
    public Task RedraftPromptAsync(string promptKey, string version, CancellationToken cancellationToken = default)
        => _promptService.RedraftPromptAsync(promptKey, version, cancellationToken);

    /// <inheritdoc />
    public Task OnlinePromptAsync(string promptKey, string version, CancellationToken cancellationToken = default)
        => _promptService.OnlinePromptAsync(promptKey, version, cancellationToken);

    /// <inheritdoc />
    public Task OfflinePromptAsync(string promptKey, string version, CancellationToken cancellationToken = default)
        => _promptService.OfflinePromptAsync(promptKey, version, cancellationToken);

    /// <inheritdoc />
    public Task UpdatePromptLabelsAsync(string promptKey, IDictionary<string, string> labels, CancellationToken cancellationToken = default)
        => _promptService.UpdatePromptLabelsAsync(promptKey, labels, cancellationToken);

    /// <inheritdoc />
    public Task UpdatePromptDescriptionAsync(string promptKey, string description, CancellationToken cancellationToken = default)
        => _promptService.UpdatePromptDescriptionAsync(promptKey, description, cancellationToken);

    /// <inheritdoc />
    public Task UpdatePromptBizTagsAsync(string promptKey, IEnumerable<string> bizTags, CancellationToken cancellationToken = default)
        => _promptService.UpdatePromptBizTagsAsync(promptKey, bizTags, cancellationToken);

    /// <inheritdoc />
    public Task DeletePromptAsync(string promptKey, string? version = null, CancellationToken cancellationToken = default)
        => _promptService.DeletePromptAsync(promptKey, version, cancellationToken);

    #endregion

    #region Skill Download

    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipAsync(string skillName, CancellationToken cancellationToken = default)
        => _skillService.DownloadSkillZipAsync(skillName, cancellationToken);

    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipByVersionAsync(string skillName, string version, CancellationToken cancellationToken = default)
        => _skillService.DownloadSkillZipByVersionAsync(skillName, version, cancellationToken);

    /// <inheritdoc />
    public Task<SkillPackage?> DownloadSkillZipByLabelAsync(string skillName, string label, CancellationToken cancellationToken = default)
        => _skillService.DownloadSkillZipByLabelAsync(skillName, label, cancellationToken);

    /// <inheritdoc />
    public Task<PageResult<SkillSummary>> SearchSkillsAsync(string? query = null, IEnumerable<string>? tagsAll = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        => _skillService.SearchSkillsAsync(query, tagsAll, pageNo, pageSize, cancellationToken);

    #endregion

    #region Skill Subscription

    /// <inheritdoc />
    public Task<SkillPackage?> SubscribeSkillAsync(string skillName, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default)
        => _skillService.SubscribeSkillAsync(skillName, listener, cancellationToken);

    /// <inheritdoc />
    public Task<SkillPackage?> SubscribeSkillAsync(string skillName, string? version, string? label, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default)
        => _skillService.SubscribeSkillAsync(skillName, version, label, listener, cancellationToken);

    /// <inheritdoc />
    public Task UnsubscribeSkillAsync(string skillName, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default)
        => _skillService.UnsubscribeSkillAsync(skillName, listener, cancellationToken);

    /// <inheritdoc />
    public Task UnsubscribeSkillAsync(string skillName, string? version, string? label, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default)
        => _skillService.UnsubscribeSkillAsync(skillName, version, label, listener, cancellationToken);

    #endregion

    #region Skill Management

    /// <inheritdoc />
    public Task<PageResult<SkillSummary>> ListSkillsAsync(string? skillName = null, string? search = null, string? orderBy = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        => _skillService.ListSkillsAsync(skillName, search, orderBy, pageNo, pageSize, cancellationToken);

    /// <inheritdoc />
    public Task<SkillMeta?> GetSkillMetaAsync(string skillName, CancellationToken cancellationToken = default)
        => _skillService.GetSkillMetaAsync(skillName, cancellationToken);

    /// <inheritdoc />
    public Task<Skill?> GetSkillDetailAsync(string skillName, string? version = null, CancellationToken cancellationToken = default)
        => _skillService.GetSkillDetailAsync(skillName, version, cancellationToken);

    /// <inheritdoc />
    public Task UploadSkillZipAsync(byte[] zipContent, string fileName, bool overwrite = false, string? targetVersion = null, string? commitMsg = null, string? uploadAction = null, bool autoPublishIfNew = false, CancellationToken cancellationToken = default)
        => _skillService.UploadSkillZipAsync(zipContent, fileName, overwrite, targetVersion, commitMsg, uploadAction, autoPublishIfNew, cancellationToken);

    /// <inheritdoc />
    public Task CreateSkillDraftAsync(Skill skill, string? basedOnVersion = null, string? targetVersion = null, string? commitMsg = null, CancellationToken cancellationToken = default)
        => _skillService.CreateSkillDraftAsync(skill, basedOnVersion, targetVersion, commitMsg, cancellationToken);

    /// <inheritdoc />
    public Task UpdateSkillDraftAsync(Skill skill, string version, bool setAsLatest = false, string? commitMsg = null, CancellationToken cancellationToken = default)
        => _skillService.UpdateSkillDraftAsync(skill, version, setAsLatest, commitMsg, cancellationToken);

    /// <inheritdoc />
    public Task DeleteSkillDraftAsync(string skillName, string version, CancellationToken cancellationToken = default)
        => _skillService.DeleteSkillDraftAsync(skillName, version, cancellationToken);

    /// <inheritdoc />
    public Task SubmitSkillReviewAsync(string skillName, string version, CancellationToken cancellationToken = default)
        => _skillService.SubmitSkillReviewAsync(skillName, version, cancellationToken);

    /// <inheritdoc />
    public Task PublishSkillAsync(string skillName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
        => _skillService.PublishSkillAsync(skillName, version, updateLatestLabel, cancellationToken);

    /// <inheritdoc />
    public Task ForcePublishSkillAsync(string skillName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
        => _skillService.ForcePublishSkillAsync(skillName, version, updateLatestLabel, cancellationToken);

    /// <inheritdoc />
    public Task RedraftSkillAsync(string skillName, string version, CancellationToken cancellationToken = default)
        => _skillService.RedraftSkillAsync(skillName, version, cancellationToken);

    /// <inheritdoc />
    public Task OnlineSkillAsync(string skillName, string version, string? scope = null, CancellationToken cancellationToken = default)
        => _skillService.OnlineSkillAsync(skillName, version, scope, cancellationToken);

    /// <inheritdoc />
    public Task OfflineSkillAsync(string skillName, string version, CancellationToken cancellationToken = default)
        => _skillService.OfflineSkillAsync(skillName, version, cancellationToken);

    /// <inheritdoc />
    public Task UpdateSkillScopeAsync(string skillName, string scope, CancellationToken cancellationToken = default)
        => _skillService.UpdateSkillScopeAsync(skillName, scope, cancellationToken);

    /// <inheritdoc />
    public Task UpdateSkillLabelsAsync(string skillName, IDictionary<string, string> labels, CancellationToken cancellationToken = default)
        => _skillService.UpdateSkillLabelsAsync(skillName, labels, cancellationToken);

    /// <inheritdoc />
    public Task UpdateSkillBizTagsAsync(string skillName, IEnumerable<string> bizTags, CancellationToken cancellationToken = default)
        => _skillService.UpdateSkillBizTagsAsync(skillName, bizTags, cancellationToken);

    /// <inheritdoc />
    public Task DeleteSkillAsync(string skillName, string? version = null, CancellationToken cancellationToken = default)
        => _skillService.DeleteSkillAsync(skillName, version, cancellationToken);

    #endregion

    #region AgentSpec Query

    /// <inheritdoc />
    public Task<AgentSpecModel?> GetAgentSpecAsync(string agentSpecName, CancellationToken cancellationToken = default)
        => _agentSpecService.GetAgentSpecAsync(agentSpecName, cancellationToken);

    /// <inheritdoc />
    public Task<AgentSpecModel?> GetAgentSpecAsync(string agentSpecName, string? version, CancellationToken cancellationToken = default)
        => _agentSpecService.GetAgentSpecAsync(agentSpecName, version, cancellationToken);

    /// <inheritdoc />
    public Task<AgentSpecModel?> GetAgentSpecByLabelAsync(string agentSpecName, string label, CancellationToken cancellationToken = default)
        => _agentSpecService.GetAgentSpecByLabelAsync(agentSpecName, label, cancellationToken);

    /// <inheritdoc />
    public Task<PageResult<AgentSpecSummary>> SearchAgentSpecsAsync(string? keyword = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        => _agentSpecService.SearchAgentSpecsAsync(keyword, pageNo, pageSize, cancellationToken);

    #endregion

    #region AgentSpec Subscription

    /// <inheritdoc />
    public Task<AgentSpecModel?> SubscribeAgentSpecAsync(string agentSpecName, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default)
        => _agentSpecService.SubscribeAgentSpecAsync(agentSpecName, listener, cancellationToken);

    /// <inheritdoc />
    public Task<AgentSpecModel?> SubscribeAgentSpecAsync(string agentSpecName, string? version, string? label, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default)
        => _agentSpecService.SubscribeAgentSpecAsync(agentSpecName, version, label, listener, cancellationToken);

    /// <inheritdoc />
    public Task UnsubscribeAgentSpecAsync(string agentSpecName, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default)
        => _agentSpecService.UnsubscribeAgentSpecAsync(agentSpecName, listener, cancellationToken);

    /// <inheritdoc />
    public Task UnsubscribeAgentSpecAsync(string agentSpecName, string? version, string? label, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default)
        => _agentSpecService.UnsubscribeAgentSpecAsync(agentSpecName, version, label, listener, cancellationToken);

    #endregion

    #region AgentSpec Management

    /// <inheritdoc />
    public Task<PageResult<AgentSpecSummary>> ListAgentSpecsAsync(string? agentSpecName = null, string? search = null, int pageNo = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        => _agentSpecService.ListAgentSpecsAsync(agentSpecName, search, pageNo, pageSize, cancellationToken);

    /// <inheritdoc />
    public Task<AgentSpecMeta?> GetAgentSpecMetaAsync(string agentSpecName, CancellationToken cancellationToken = default)
        => _agentSpecService.GetAgentSpecMetaAsync(agentSpecName, cancellationToken);

    /// <inheritdoc />
    public Task<AgentSpecModel?> GetAgentSpecDetailAsync(string agentSpecName, string? version = null, CancellationToken cancellationToken = default)
        => _agentSpecService.GetAgentSpecDetailAsync(agentSpecName, version, cancellationToken);

    /// <inheritdoc />
    public Task UploadAgentSpecAsync(byte[] content, string fileName, bool overwrite = false, CancellationToken cancellationToken = default)
        => _agentSpecService.UploadAgentSpecAsync(content, fileName, overwrite, cancellationToken);

    /// <inheritdoc />
    public Task CreateAgentSpecDraftAsync(AgentSpecModel agentSpec, string? basedOnVersion = null, string? targetVersion = null, CancellationToken cancellationToken = default)
        => _agentSpecService.CreateAgentSpecDraftAsync(agentSpec, basedOnVersion, targetVersion, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAgentSpecDraftAsync(AgentSpecModel agentSpec, string version, bool setAsLatest = false, CancellationToken cancellationToken = default)
        => _agentSpecService.UpdateAgentSpecDraftAsync(agentSpec, version, setAsLatest, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAgentSpecDraftAsync(string agentSpecName, string version, CancellationToken cancellationToken = default)
        => _agentSpecService.DeleteAgentSpecDraftAsync(agentSpecName, version, cancellationToken);

    /// <inheritdoc />
    public Task SubmitAgentSpecReviewAsync(string agentSpecName, string version, CancellationToken cancellationToken = default)
        => _agentSpecService.SubmitAgentSpecReviewAsync(agentSpecName, version, cancellationToken);

    /// <inheritdoc />
    public Task PublishAgentSpecAsync(string agentSpecName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
        => _agentSpecService.PublishAgentSpecAsync(agentSpecName, version, updateLatestLabel, cancellationToken);

    /// <inheritdoc />
    public Task ForcePublishAgentSpecAsync(string agentSpecName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default)
        => _agentSpecService.ForcePublishAgentSpecAsync(agentSpecName, version, updateLatestLabel, cancellationToken);

    /// <inheritdoc />
    public Task RedraftAgentSpecAsync(string agentSpecName, string version, CancellationToken cancellationToken = default)
        => _agentSpecService.RedraftAgentSpecAsync(agentSpecName, version, cancellationToken);

    /// <inheritdoc />
    public Task OnlineAgentSpecAsync(string agentSpecName, string version, string? scope = null, CancellationToken cancellationToken = default)
        => _agentSpecService.OnlineAgentSpecAsync(agentSpecName, version, scope, cancellationToken);

    /// <inheritdoc />
    public Task OfflineAgentSpecAsync(string agentSpecName, string version, CancellationToken cancellationToken = default)
        => _agentSpecService.OfflineAgentSpecAsync(agentSpecName, version, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAgentSpecScopeAsync(string agentSpecName, string scope, CancellationToken cancellationToken = default)
        => _agentSpecService.UpdateAgentSpecScopeAsync(agentSpecName, scope, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAgentSpecLabelsAsync(string agentSpecName, IDictionary<string, string> labels, CancellationToken cancellationToken = default)
        => _agentSpecService.UpdateAgentSpecLabelsAsync(agentSpecName, labels, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAgentSpecBizTagsAsync(string agentSpecName, IEnumerable<string> bizTags, CancellationToken cancellationToken = default)
        => _agentSpecService.UpdateAgentSpecBizTagsAsync(agentSpecName, bizTags, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAgentSpecAsync(string agentSpecName, string? version = null, CancellationToken cancellationToken = default)
        => _agentSpecService.DeleteAgentSpecAsync(agentSpecName, version, cancellationToken);

    #endregion
}
