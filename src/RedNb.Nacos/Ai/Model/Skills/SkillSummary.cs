namespace RedNb.Nacos.Core.Ai.Model.Skills;

/// <summary>
/// Summary of a skill for list views.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.skills.SkillSummary</c>.
/// </summary>
public class SkillSummary : SkillBase
{
    /// <summary>
    /// Owner (creator) of the skill.
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// Whether the skill is enabled (discoverable) as a whole.
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// Business tags (JSON array string).
    /// </summary>
    public string? BizTags { get; set; }

    /// <summary>
    /// Origin of the skill (e.g. local, imported).
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// Visibility scope: PUBLIC or PRIVATE.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Label to version mapping.
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }

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
    /// Total download count.
    /// </summary>
    public long? DownloadCount { get; set; }

    /// <summary>
    /// Whether the current caller has write permission.
    /// </summary>
    public bool Writable { get; set; }
}

/// <summary>
/// Summary of a single skill version.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.skills.SkillMeta.SkillVersionSummary</c>.
/// </summary>
public class SkillVersionSummary
{
    /// <summary>
    /// Version string (SemVer).
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Lifecycle status: draft / reviewing / online / offline.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Author of the version.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Commit message recorded when the version was created.
    /// </summary>
    public string? CommitMsg { get; set; }

    /// <summary>
    /// Create time (epoch milliseconds).
    /// </summary>
    public long? CreateTime { get; set; }

    /// <summary>
    /// Last update time (epoch milliseconds).
    /// </summary>
    public long? UpdateTime { get; set; }

    /// <summary>
    /// Publish pipeline execution information, if a pipeline ran.
    /// </summary>
    public string? PublishPipelineInfo { get; set; }

    /// <summary>
    /// Download count of this version.
    /// </summary>
    public long? DownloadCount { get; set; }
}

/// <summary>
/// Full skill metadata including version summaries.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.skills.SkillMeta</c>.
/// </summary>
public class SkillMeta : SkillSummary
{
    /// <summary>
    /// Summaries of all versions.
    /// </summary>
    public List<SkillVersionSummary>? Versions { get; set; }
}
