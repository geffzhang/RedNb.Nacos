using RedNb.Nacos.Ai.Models;
using RedNb.Nacos.Ai.Models.Skill;

namespace RedNb.Nacos.Ai;

/// <summary>
/// Nacos AI Skill client service interface.
/// Covers skill download/subscription (client API) and skill management (admin API).
/// Mirrors <c>com.alibaba.nacos.api.ai.AiService</c> skill operations.
/// </summary>
public interface ISkillService
{
    #region Skill Download

    /// <summary>
    /// Downloads the latest version of a skill as a ZIP package.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Skill package with ZIP bytes and resolved md5/version.</returns>
    Task<SkillPackage?> DownloadSkillZipAsync(string skillName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a specific version of a skill as a ZIP package.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Skill package with ZIP bytes and resolved md5/version.</returns>
    Task<SkillPackage?> DownloadSkillZipByVersionAsync(string skillName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the skill version bound to a label, such as stable or canary.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="label">Label name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Skill package with ZIP bytes and resolved md5/version.</returns>
    Task<SkillPackage?> DownloadSkillZipByLabelAsync(string skillName, string label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches skills with pagination (client API).
    /// </summary>
    /// <param name="query">Optional search keyword.</param>
    /// <param name="tagsAll">Optional tags that all must match.</param>
    /// <param name="pageNo">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged skill summaries.</returns>
    Task<PageResult<SkillSummary>> SearchSkillsAsync(
        string? query = null,
        IEnumerable<string>? tagsAll = null,
        int pageNo = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    #endregion

    #region Skill Subscription

    /// <summary>
    /// Subscribes to a skill for the latest version.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="listener">Callback listener for skill changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current skill package when subscription succeeds.</returns>
    Task<SkillPackage?> SubscribeSkillAsync(string skillName, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to a skill for a specific version or label.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Version of the skill (null or empty to use label or latest).</param>
    /// <param name="label">Label of the skill (used when version is not specified).</param>
    /// <param name="listener">Callback listener for skill changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current skill package when subscription succeeds.</returns>
    Task<SkillPackage?> SubscribeSkillAsync(string skillName, string? version, string? label, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a skill for the latest version.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="listener">Callback listener registered before.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnsubscribeSkillAsync(string skillName, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a skill for a specific version or label.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Version of the skill.</param>
    /// <param name="label">Label of the skill.</param>
    /// <param name="listener">Callback listener registered before.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnsubscribeSkillAsync(string skillName, string? version, string? label, AbstractNacosSkillListener listener, CancellationToken cancellationToken = default);

    #endregion

    #region Skill Management

    /// <summary>
    /// Lists skills with pagination (admin API).
    /// </summary>
    /// <param name="skillName">Optional skill name filter.</param>
    /// <param name="search">Optional search keyword.</param>
    /// <param name="orderBy">Optional order-by field.</param>
    /// <param name="pageNo">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged skill summaries.</returns>
    Task<PageResult<SkillSummary>> ListSkillsAsync(
        string? skillName = null,
        string? search = null,
        string? orderBy = null,
        int pageNo = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets meta information of a skill, including all versions.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Skill meta information.</returns>
    Task<SkillMeta?> GetSkillMetaAsync(string skillName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detail of one skill version, including SKILL.md and resources.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Target version (null or empty for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Skill detail.</returns>
    Task<Skill?> GetSkillDetailAsync(string skillName, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a skill ZIP package.
    /// </summary>
    /// <param name="zipContent">Skill package bytes.</param>
    /// <param name="fileName">File name of the package, ending with .zip.</param>
    /// <param name="overwrite">Whether to overwrite an existing skill.</param>
    /// <param name="targetVersion">Target version (optional).</param>
    /// <param name="commitMsg">Commit message (optional).</param>
    /// <param name="uploadAction">Upload action (optional).</param>
    /// <param name="autoPublishIfNew">Whether to auto publish when the skill is new.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UploadSkillZipAsync(
        byte[] zipContent,
        string fileName,
        bool overwrite = false,
        string? targetVersion = null,
        string? commitMsg = null,
        string? uploadAction = null,
        bool autoPublishIfNew = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a skill draft.
    /// </summary>
    /// <param name="skill">Skill detail content.</param>
    /// <param name="basedOnVersion">Base version of the draft (optional).</param>
    /// <param name="targetVersion">Target draft version (optional).</param>
    /// <param name="commitMsg">Commit message (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateSkillDraftAsync(
        Skill skill,
        string? basedOnVersion = null,
        string? targetVersion = null,
        string? commitMsg = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a skill draft.
    /// </summary>
    /// <param name="skill">Skill detail content.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="setAsLatest">Whether to set the draft as latest.</param>
    /// <param name="commitMsg">Commit message (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSkillDraftAsync(
        Skill skill,
        string version,
        bool setAsLatest = false,
        string? commitMsg = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a skill draft.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSkillDraftAsync(string skillName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a skill draft for review.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Draft version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubmitSkillReviewAsync(string skillName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a reviewing skill version.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Version to publish.</param>
    /// <param name="updateLatestLabel">Whether to move the latest label to this version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishSkillAsync(string skillName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Force publishes a skill version, bypassing the review status.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Version to publish.</param>
    /// <param name="updateLatestLabel">Whether to move the latest label to this version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ForcePublishSkillAsync(string skillName, string version, bool updateLatestLabel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a published skill version back to draft status.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RedraftSkillAsync(string skillName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings a published skill version online.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Target version.</param>
    /// <param name="scope">Online scope, such as public or private (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnlineSkillAsync(string skillName, string version, string? scope = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes an online skill version offline.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="version">Target version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OfflineSkillAsync(string skillName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the online scope of a skill.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="scope">New scope, such as public or private.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSkillScopeAsync(string skillName, string scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the labels of a skill.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="labels">Label to version mapping.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSkillLabelsAsync(string skillName, IDictionary<string, string> labels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the business tags of a skill.
    /// </summary>
    /// <param name="skillName">Name of the skill.</param>
    /// <param name="bizTags">New business tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSkillBizTagsAsync(string skillName, IEnumerable<string> bizTags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a skill.
    /// </summary>
    /// <param name="skillName">Name of the skill to delete.</param>
    /// <param name="version">Version to delete (null to delete all versions).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSkillAsync(string skillName, string? version = null, CancellationToken cancellationToken = default);

    #endregion
}
