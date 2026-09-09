using RedNb.Nacos.Core.Ai.Model;
using RedNb.Nacos.Core.Ai.Model.AgentSpec;

namespace RedNb.Nacos.Core.Ai;

/// <summary>
/// Nacos AI AgentSpec client service interface.
/// Covers AgentSpec query/subscription (client API) and AgentSpec management (admin API).
/// Mirrors <c>com.alibaba.nacos.api.ai.AiService</c> AgentSpec operations.
/// </summary>
public interface IAgentSpecService
{
    #region AgentSpec Query

    /// <summary>
    /// Gets the latest version of an AgentSpec.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AgentSpec, or null when not found.</returns>
    Task<Model.AgentSpec.AgentSpec?> GetAgentSpecAsync(string agentSpecName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version of an AgentSpec.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Target version (null or empty for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AgentSpec, or null when not found.</returns>
    Task<Model.AgentSpec.AgentSpec?> GetAgentSpecAsync(string agentSpecName, string? version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the AgentSpec version bound to a label, such as stable or canary.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="label">Label name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AgentSpec, or null when not found.</returns>
    Task<Model.AgentSpec.AgentSpec?> GetAgentSpecByLabelAsync(string agentSpecName, string label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches AgentSpecs with pagination (client API).
    /// </summary>
    /// <param name="keyword">Optional search keyword.</param>
    /// <param name="pageNo">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged AgentSpec summaries.</returns>
    Task<PageResult<AgentSpecSummary>> SearchAgentSpecsAsync(
        string? keyword = null,
        int pageNo = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    #endregion

    #region AgentSpec Subscription

    /// <summary>
    /// Subscribes to an AgentSpec for the latest version.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="listener">Callback listener for AgentSpec changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current AgentSpec when subscription succeeds.</returns>
    Task<Model.AgentSpec.AgentSpec?> SubscribeAgentSpecAsync(string agentSpecName, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to an AgentSpec for a specific version or label.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Version of the AgentSpec (null or empty to use label or latest).</param>
    /// <param name="label">Label of the AgentSpec (used when version is not specified).</param>
    /// <param name="listener">Callback listener for AgentSpec changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current AgentSpec when subscription succeeds.</returns>
    Task<Model.AgentSpec.AgentSpec?> SubscribeAgentSpecAsync(string agentSpecName, string? version, string? label, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from an AgentSpec for the latest version.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="listener">Callback listener registered before.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnsubscribeAgentSpecAsync(string agentSpecName, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from an AgentSpec for a specific version or label.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Version of the AgentSpec.</param>
    /// <param name="label">Label of the AgentSpec.</param>
    /// <param name="listener">Callback listener registered before.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnsubscribeAgentSpecAsync(string agentSpecName, string? version, string? label, AbstractNacosAgentSpecListener listener, CancellationToken cancellationToken = default);

    #endregion

    #region AgentSpec Management

    /// <summary>
    /// Lists AgentSpecs with pagination (admin API).
    /// </summary>
    /// <param name="agentSpecName">Optional AgentSpec name filter.</param>
    /// <param name="search">Optional search keyword.</param>
    /// <param name="pageNo">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged AgentSpec summaries.</returns>
    Task<PageResult<AgentSpecSummary>> ListAgentSpecsAsync(
        string? agentSpecName = null,
        string? search = null,
        int pageNo = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets meta information of an AgentSpec, including all versions.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AgentSpec meta information.</returns>
    Task<AgentSpecMeta?> GetAgentSpecMetaAsync(string agentSpecName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detail of one AgentSpec version, including content and resources.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Target version (null or empty for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AgentSpec detail.</returns>
    Task<Model.AgentSpec.AgentSpec?> GetAgentSpecDetailAsync(string agentSpecName, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an AgentSpec file package.
    /// </summary>
    /// <param name="content">AgentSpec package bytes.</param>
    /// <param name="fileName">File name of the package.</param>
    /// <param name="overwrite">Whether to overwrite an existing AgentSpec.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UploadAgentSpecAsync(
        byte[] content,
        string fileName,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an AgentSpec draft.
    /// </summary>
    /// <param name="agentSpec">AgentSpec detail content.</param>
    /// <param name="basedOnVersion">Base version of the draft (optional).</param>
    /// <param name="targetVersion">Target draft version (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAgentSpecDraftAsync(
        Model.AgentSpec.AgentSpec agentSpec,
        string? basedOnVersion = null,
        string? targetVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an AgentSpec draft.
    /// </summary>
    /// <param name="agentSpec">AgentSpec detail content.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="setAsLatest">Whether to set the draft as latest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAgentSpecDraftAsync(
        Model.AgentSpec.AgentSpec agentSpec,
        string version,
        bool setAsLatest = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an AgentSpec draft.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAgentSpecDraftAsync(string agentSpecName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an AgentSpec draft for review.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubmitAgentSpecReviewAsync(string agentSpecName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a reviewing AgentSpec version.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Version to publish.</param>
    /// <param name="updateLatestLabel">Whether to move the latest label to this version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAgentSpecAsync(string agentSpecName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Force publishes an AgentSpec version, bypassing the review status.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Version to publish.</param>
    /// <param name="updateLatestLabel">Whether to move the latest label to this version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ForcePublishAgentSpecAsync(string agentSpecName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a published AgentSpec version back to draft status.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RedraftAgentSpecAsync(string agentSpecName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings a published AgentSpec version online.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Target version.</param>
    /// <param name="scope">Online scope, such as public or private (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnlineAgentSpecAsync(string agentSpecName, string version, string? scope = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes an online AgentSpec version offline.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OfflineAgentSpecAsync(string agentSpecName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the online scope of an AgentSpec.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="scope">New scope, such as public or private.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAgentSpecScopeAsync(string agentSpecName, string scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the labels of an AgentSpec.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="labels">Label to version mapping.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAgentSpecLabelsAsync(string agentSpecName, IDictionary<string, string> labels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the business tags of an AgentSpec.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec.</param>
    /// <param name="bizTags">New business tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAgentSpecBizTagsAsync(string agentSpecName, IEnumerable<string> bizTags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an AgentSpec.
    /// </summary>
    /// <param name="agentSpecName">Name of the AgentSpec to delete.</param>
    /// <param name="version">Version to delete (null to delete all versions).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAgentSpecAsync(string agentSpecName, string? version = null, CancellationToken cancellationToken = default);

    #endregion
}
