using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai.Listener;

namespace RedNb.Nacos.Sample.AI.Listeners;

public sealed class DemoAgentCardListener : AbstractNacosAgentCardListener
{
    private readonly ILogger _logger;

    public DemoAgentCardListener(ILogger logger) => _logger = logger;

    public override void OnEvent(NacosAgentCardEvent @event)
    {
        _logger.LogInformation(
            "[A2A event] agentName={AgentName} type={EventType}",
            @event.AgentName, @event.GetType().Name);
    }
}
