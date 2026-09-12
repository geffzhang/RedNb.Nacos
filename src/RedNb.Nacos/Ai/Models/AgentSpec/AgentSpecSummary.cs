using System.Collections.Generic;

namespace RedNb.Nacos.Ai.Models.AgentSpec;

/// <summary>
/// Summary information of an AgentSpec.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.agentspec.AgentSpecSummary</c>.
/// </summary>
public class AgentSpecSummary : AgentSpecBasicInfo
{
    /// <summary>
    /// Owner of the AgentSpec.
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// Whether the AgentSpec is enabled.
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// Business tags.
    /// </summary>
    public List<string>? BizTags { get; set; }

    /// <summary>
    /// Source of the AgentSpec, such as local or imported.
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// Online scope, such as public or private.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Labels and the versions they point to.
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>
    /// Version currently in editing status.
    /// </summary>
    public string? EditingVersion { get; set; }

    /// <summary>
    /// Version currently in reviewing status.
    /// </summary>
    public string? ReviewingVersion { get; set; }

    /// <summary>
    /// Count of online versions.
    /// </summary>
    public int OnlineCnt { get; set; }

    /// <summary>
    /// Download count.
    /// </summary>
    public long DownloadCount { get; set; }

    /// <summary>
    /// Whether the current operator has write permission.
    /// </summary>
    public bool Writable { get; set; }
}

/// <summary>
/// Summary of one AgentSpec version.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.agentspec.AgentSpecVersionSummary</c>.
/// </summary>
public class AgentSpecVersionSummary
{
    /// <summary>
    /// Version string.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Lifecycle status, such as draft, reviewing or online.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Author of the version.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Description of the version.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Create time (formatted string).
    /// </summary>
    public string? CreateTime { get; set; }

    /// <summary>
    /// Update time (formatted string).
    /// </summary>
    public string? UpdateTime { get; set; }

    /// <summary>
    /// Publish pipeline information.
    /// </summary>
    public object? PublishPipelineInfo { get; set; }

    /// <summary>
    /// Download count of the version.
    /// </summary>
    public long DownloadCount { get; set; }
}

/// <summary>
/// Meta information of an AgentSpec, including the version list.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.agentspec.AgentSpecMeta</c>.
/// </summary>
public class AgentSpecMeta : AgentSpecSummary
{
    /// <summary>
    /// All versions of the AgentSpec.
    /// </summary>
    public List<AgentSpecVersionSummary>? Versions { get; set; }
}
