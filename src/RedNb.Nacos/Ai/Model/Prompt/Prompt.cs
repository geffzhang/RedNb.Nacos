namespace RedNb.Nacos.Core.Ai.Model.Prompt;

/// <summary>
/// A concrete Prompt template resolved from Nacos Prompt Registry.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.prompt.Prompt</c>.
/// </summary>
public class Prompt
{
    /// <summary>
    /// Prompt key (unique within a namespace).
    /// </summary>
    public string? PromptKey { get; set; }

    /// <summary>
    /// Resolved version of this prompt.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Prompt template content with <c>{{variable}}</c> placeholders.
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    /// MD5 of the template content, used for conditional (304) requests.
    /// </summary>
    public string? Md5 { get; set; }

    /// <summary>
    /// Declared template variables.
    /// </summary>
    public List<PromptVariable>? Variables { get; set; }

    /// <summary>
    /// Renders the template with the given variable values.
    /// Variables not supplied fall back to their declared default value,
    /// then to the empty string.
    /// </summary>
    /// <param name="values">Variable values keyed by variable name.</param>
    /// <returns>The rendered prompt text.</returns>
    public string Render(IDictionary<string, string>? values = null)
    {
        var result = Template ?? string.Empty;
        var defaults = new Dictionary<string, string>();

        if (Variables != null)
        {
            foreach (var variable in Variables)
            {
                if (!string.IsNullOrEmpty(variable.Name))
                {
                    defaults[variable.Name] = variable.DefaultValue ?? string.Empty;
                }
            }
        }

        if (values != null)
        {
            foreach (var kv in values)
            {
                defaults[kv.Key] = kv.Value;
            }
        }

        foreach (var kv in defaults)
        {
            result = result.Replace("{{" + kv.Key + "}}", kv.Value);
        }

        return result;
    }
}
