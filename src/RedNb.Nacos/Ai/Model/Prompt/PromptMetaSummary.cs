namespace RedNb.Nacos.Core.Ai.Model.Prompt;

/// <summary>
/// Summary of a Prompt (without version details).
/// Mirrors <c>com.alibaba.nacos.api.ai.model.prompt.PromptMetaSummary</c>.
/// </summary>
public class PromptMetaSummary
{
    /// <summary>
    /// Metadata schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Prompt key.
    /// </summary>
    public string? PromptKey { get; set; }

    /// <summary>
    /// Prompt description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Business tags.
    /// </summary>
    public List<string> BizTags { get; set; } = new();

    /// <summary>
    /// Business tags in raw string form, as returned by some endpoints.
    /// </summary>
    public string? BizTagsStr { get; set; }

    /// <summary>
    /// Version currently pointed to by the <c>latest</c> label.
    /// </summary>
    public string? LatestVersion { get; set; }

    /// <summary>
    /// Last modification time (epoch milliseconds).
    /// </summary>
    public long? GmtModified { get; set; }

    /// <summary>
    /// Version currently in draft (editing) state, if any.
    /// </summary>
    public string? EditingVersion { get; set; }

    /// <summary>
    /// Version currently under review, if any.
    /// </summary>
    public string? ReviewingVersion { get; set; }

    /// <summary>
    /// Number of online versions.
    /// </summary>
    public int? OnlineCnt { get; set; }

    /// <summary>
    /// Label to version mapping.
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>
    /// Total download count.
    /// </summary>
    public long? DownloadCount { get; set; }
}

/// <summary>
/// Full prompt metadata including version list.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.prompt.PromptMetaInfo</c>.
/// </summary>
public class PromptMetaInfo : PromptMetaSummary
{
    /// <summary>
    /// All version strings of this prompt.
    /// </summary>
    public List<string> Versions { get; set; } = new();

    /// <summary>
    /// Summary of every version.
    /// </summary>
    public List<PromptVersionSummary> VersionDetails { get; set; } = new();
}
