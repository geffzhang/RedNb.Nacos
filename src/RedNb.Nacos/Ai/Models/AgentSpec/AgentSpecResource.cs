using System.Collections.Generic;

namespace RedNb.Nacos.Ai.Models.AgentSpec;

/// <summary>
/// An attached resource of an AgentSpec.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.agentspec.AgentSpecResource</c>.
/// </summary>
public class AgentSpecResource
{
    /// <summary>
    /// Resource name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Resource type, such as file or prompt.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Resource content or reference.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Extra metadata of the resource.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
