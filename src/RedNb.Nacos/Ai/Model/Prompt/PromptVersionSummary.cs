namespace RedNb.Nacos.Core.Ai.Model.Prompt;

/// <summary>
/// Summary of a single Prompt version.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.prompt.PromptVersionSummary</c>.
/// </summary>
public class PromptVersionSummary
{
    /// <summary>
    /// Prompt key.
    /// </summary>
    public string? PromptKey { get; set; }

    /// <summary>
    /// Version string.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Lifecycle status: draft / reviewing / online / offline.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Commit message recorded when the version was created.
    /// </summary>
    public string? CommitMsg { get; set; }

    /// <summary>
    /// Author of the version.
    /// </summary>
    public string? SrcUser { get; set; }

    /// <summary>
    /// Last modification time (epoch milliseconds).
    /// </summary>
    public long? GmtModified { get; set; }

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
/// Detailed information of a single Prompt version, including the template.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.prompt.PromptVersionInfo</c>.
/// </summary>
public class PromptVersionInfo : PromptVersionSummary
{
    /// <summary>
    /// Prompt template content.
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    /// MD5 of the template content.
    /// </summary>
    public string? Md5 { get; set; }

    /// <summary>
    /// Declared template variables.
    /// </summary>
    public List<PromptVariable>? Variables { get; set; }
}
