using RedNb.Nacos.Core.Ai.Listener;

namespace RedNb.Nacos.Core.Ai.Model.AgentSpec;

/// <summary>
/// Event fired when the subscribed AgentSpec changes.
/// </summary>
public class NacosAgentSpecEvent : INacosAiEvent
{
    /// <summary>
    /// Create an AgentSpec change event.
    /// </summary>
    /// <param name="agentSpec">Latest AgentSpec detail.</param>
    /// <param name="version">Resolved version, from the X-Nacos-AgentSpec-Resolved-Version header.</param>
    public NacosAgentSpecEvent(AgentSpec agentSpec, string? version)
    {
        AgentSpec = agentSpec;
        Name = agentSpec?.Name;
        Version = version;
    }

    /// <summary>
    /// AgentSpec name.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Resolved version.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Latest AgentSpec detail.
    /// </summary>
    public AgentSpec AgentSpec { get; }
}

/// <summary>
/// Abstract listener for AgentSpec change events.
/// Mirrors <c>com.alibaba.nacos.api.ai.listener.agentspec.AbstractNacosAgentSpecListener</c>.
/// </summary>
public abstract class AbstractNacosAgentSpecListener : INacosAiListener<NacosAgentSpecEvent>
{
    /// <inheritdoc />
    public abstract void OnEvent(NacosAgentSpecEvent @event);
}
