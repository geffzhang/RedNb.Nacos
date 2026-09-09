namespace RedNb.Nacos.Core.Ai.Model.Prompt;

/// <summary>
/// A variable placeholder declared by a Prompt template.
/// Variables are referenced in templates with <c>{{name}}</c> syntax.
/// </summary>
public class PromptVariable
{
    /// <summary>
    /// Variable name, e.g. <c>role</c> in <c>{{role}}</c>.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Default value used when the caller does not provide one.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Human-readable description of the variable.
    /// </summary>
    public string? Description { get; set; }
}
