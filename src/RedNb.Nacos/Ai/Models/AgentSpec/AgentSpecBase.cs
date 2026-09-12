namespace RedNb.Nacos.Ai.Models.AgentSpec;

/// <summary>
/// Base information of an AgentSpec resource.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.agentspec.AgentSpecBase</c>.
/// </summary>
public class AgentSpecBase
{
    /// <summary>
    /// Namespace id of the AgentSpec.
    /// </summary>
    public string? NamespaceId { get; set; }

    /// <summary>
    /// AgentSpec name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of the AgentSpec.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Basic information of an AgentSpec with the last update time.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.agentspec.AgentSpecBasicInfo</c>.
/// </summary>
public class AgentSpecBasicInfo : AgentSpecBase
{
    /// <summary>
    /// Last update time (formatted string).
    /// </summary>
    public string? UpdateTime { get; set; }
}
