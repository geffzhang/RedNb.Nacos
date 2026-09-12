using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai.Listener;

namespace RedNb.Nacos.Sample.AI.Listeners;

public sealed class DemoMcpListener : AbstractNacosMcpServerListener
{
    private readonly ILogger _logger;

    public DemoMcpListener(ILogger logger) => _logger = logger;

    public override void OnEvent(NacosMcpServerEvent @event)
    {
        _logger.LogInformation(
            "[MCP event] mcpName={McpName} mcpId={McpId} type={EventType}",
            @event.McpName, @event.McpId, @event.GetType().Name);
    }
}
