namespace RedNb.Nacos.Ai.Models.Skill;

/// <summary>
/// Full skill content of one version.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.skills.Skill</c>.
/// </summary>
public class Skill : SkillBase
{
    /// <summary>
    /// The SKILL.md content (YAML frontmatter + Markdown instructions).
    /// </summary>
    public string? SkillMd { get; set; }

    /// <summary>
    /// Bundled resource files keyed by relative path.
    /// </summary>
    public Dictionary<string, SkillResource>? Resource { get; set; }
}
