using RedNb.Nacos.Ai.Listener;

namespace RedNb.Nacos.Ai.Models.Prompt;

/// <summary>
/// Event for Prompt changes.
/// </summary>
public class NacosPromptEvent : INacosAiEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NacosPromptEvent"/> class.
    /// </summary>
    /// <param name="prompt">The resolved prompt.</param>
    public NacosPromptEvent(Prompt prompt)
    {
        PromptKey = prompt.PromptKey ?? string.Empty;
        Version = prompt.Version ?? string.Empty;
        Prompt = prompt;
    }

    /// <summary>
    /// Gets the prompt key.
    /// </summary>
    public string PromptKey { get; }

    /// <summary>
    /// Gets the resolved version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the resolved prompt.
    /// </summary>
    public Prompt Prompt { get; }
}

/// <summary>
/// Abstract base class for Prompt event listeners.
/// </summary>
public abstract class AbstractNacosPromptListener : INacosAiListener<NacosPromptEvent>
{
    /// <summary>
    /// Callback when a prompt event is received.
    /// </summary>
    /// <param name="event">The prompt event.</param>
    public abstract void OnEvent(NacosPromptEvent @event);
}
