using RedNb.Nacos.Core.Ai.Model;
using RedNb.Nacos.Core.Ai.Model.Prompt;

namespace RedNb.Nacos.Core.Ai;

/// <summary>
/// Nacos AI Prompt client service interface.
/// Covers prompt runtime query/subscription (client API) and prompt management (admin API).
/// Mirrors <c>com.alibaba.nacos.api.ai.AiService</c> prompt operations.
/// </summary>
public interface IPromptService
{
    #region Prompt Query

    /// <summary>
    /// Gets the latest version of a prompt.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prompt, or null when not found.</returns>
    Task<Model.Prompt.Prompt?> GetPromptAsync(string promptKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version of a prompt.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Target version (null or empty for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prompt, or null when not found.</returns>
    Task<Model.Prompt.Prompt?> GetPromptAsync(string promptKey, string? version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the prompt version bound to a label, such as stable or canary.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="label">Label name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prompt, or null when not found.</returns>
    Task<Model.Prompt.Prompt?> GetPromptByLabelAsync(string promptKey, string label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches prompts with pagination (client API).
    /// </summary>
    /// <param name="query">Optional search keyword.</param>
    /// <param name="pageNo">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged prompt meta summaries.</returns>
    Task<PageResult<PromptMetaSummary>> SearchPromptsAsync(
        string? query = null,
        int pageNo = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    #endregion

    #region Prompt Subscription

    /// <summary>
    /// Subscribes to a prompt for the latest version.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="listener">Callback listener for prompt changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current prompt when subscription succeeds.</returns>
    Task<Model.Prompt.Prompt?> SubscribePromptAsync(string promptKey, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to a prompt for a specific version or label.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Version of the prompt (null or empty to use label or latest).</param>
    /// <param name="label">Label of the prompt (used when version is not specified).</param>
    /// <param name="listener">Callback listener for prompt changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current prompt when subscription succeeds.</returns>
    Task<Model.Prompt.Prompt?> SubscribePromptAsync(string promptKey, string? version, string? label, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a prompt for the latest version.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="listener">Callback listener registered before.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnsubscribePromptAsync(string promptKey, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a prompt for a specific version or label.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Version of the prompt.</param>
    /// <param name="label">Label of the prompt.</param>
    /// <param name="listener">Callback listener registered before.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnsubscribePromptAsync(string promptKey, string? version, string? label, AbstractNacosPromptListener listener, CancellationToken cancellationToken = default);

    #endregion

    #region Prompt Management

    /// <summary>
    /// Lists prompts with pagination (admin API).
    /// </summary>
    /// <param name="promptKey">Optional prompt key filter.</param>
    /// <param name="search">Optional search keyword.</param>
    /// <param name="bizTags">Optional business tag filter.</param>
    /// <param name="pageNo">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged prompt meta summaries.</returns>
    Task<PageResult<PromptMetaSummary>> ListPromptsAsync(
        string? promptKey = null,
        string? search = null,
        string? bizTags = null,
        int pageNo = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets meta information of a prompt, including all versions.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prompt meta information.</returns>
    Task<PromptMetaInfo?> GetPromptMetaAsync(string promptKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all versions of a prompt.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Version summaries of the prompt.</returns>
    Task<List<PromptVersionSummary>> ListPromptVersionsAsync(string promptKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detail of one prompt version, including template and variables.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prompt version detail.</returns>
    Task<PromptVersionInfo?> GetPromptVersionDetailAsync(string promptKey, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a prompt draft.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="targetVersion">Target draft version.</param>
    /// <param name="template">Prompt template content.</param>
    /// <param name="basedOnVersion">Base version of the draft (optional).</param>
    /// <param name="variables">Template variables (optional).</param>
    /// <param name="commitMsg">Commit message (optional).</param>
    /// <param name="description">Description (optional).</param>
    /// <param name="bizTags">Business tags (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreatePromptDraftAsync(
        string promptKey,
        string targetVersion,
        string template,
        string? basedOnVersion = null,
        IEnumerable<PromptVariable>? variables = null,
        string? commitMsg = null,
        string? description = null,
        IEnumerable<string>? bizTags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a prompt draft.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="template">Prompt template content.</param>
    /// <param name="variables">Template variables (optional).</param>
    /// <param name="commitMsg">Commit message (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdatePromptDraftAsync(
        string promptKey,
        string version,
        string template,
        IEnumerable<PromptVariable>? variables = null,
        string? commitMsg = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a prompt draft.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeletePromptDraftAsync(string promptKey, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a prompt draft for review.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubmitPromptReviewAsync(string promptKey, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a reviewing prompt version.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Version to publish.</param>
    /// <param name="updateLatestLabel">Whether to move the latest label to this version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishPromptAsync(string promptKey, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a prompt directly with template content, without the draft pipeline.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Version to publish.</param>
    /// <param name="template">Prompt template content.</param>
    /// <param name="commitMsg">Commit message (optional).</param>
    /// <param name="description">Description (optional).</param>
    /// <param name="bizTags">Business tags (optional).</param>
    /// <param name="variables">Template variables (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishPromptAsync(
        string promptKey,
        string version,
        string template,
        string? commitMsg = null,
        string? description = null,
        IEnumerable<string>? bizTags = null,
        IEnumerable<PromptVariable>? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Force publishes a prompt version, bypassing the review status.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Version to publish.</param>
    /// <param name="updateLatestLabel">Whether to move the latest label to this version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ForcePublishPromptAsync(string promptKey, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a published prompt version back to draft status.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RedraftPromptAsync(string promptKey, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings a published prompt version online.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnlinePromptAsync(string promptKey, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes an online prompt version offline.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OfflinePromptAsync(string promptKey, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the labels of a prompt.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="labels">Label to version mapping.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdatePromptLabelsAsync(string promptKey, IDictionary<string, string> labels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the description of a prompt.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="description">New description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdatePromptDescriptionAsync(string promptKey, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the business tags of a prompt.
    /// </summary>
    /// <param name="promptKey">Key of the prompt.</param>
    /// <param name="bizTags">New business tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdatePromptBizTagsAsync(string promptKey, IEnumerable<string> bizTags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a prompt.
    /// </summary>
    /// <param name="promptKey">Key of the prompt to delete.</param>
    /// <param name="version">Version to delete (null to delete all versions).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeletePromptAsync(string promptKey, string? version = null, CancellationToken cancellationToken = default);

    #endregion
}
