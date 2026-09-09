using System.Collections.Generic;

namespace RedNb.Nacos.Core.Ai.Model.AgentSpec;

/// <summary>
/// Detail of an AgentSpec of one version, including content and resources.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.agentspec.AgentSpec</c>.
/// </summary>
public class AgentSpec : AgentSpecBase
{
    /// <summary>
    /// Business tags.
    /// </summary>
    public List<string>? BizTags { get; set; }

    /// <summary>
    /// Main content of the AgentSpec (YAML or JSON).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Attached resources of the AgentSpec.
    /// </summary>
    public Dictionary<string, AgentSpecResource>? Resource { get; set; }
}
