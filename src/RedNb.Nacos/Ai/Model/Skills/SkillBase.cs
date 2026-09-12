namespace RedNb.Nacos.Core.Ai.Model.Skills;

/// <summary>
/// Base identity of a Skill.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.skills.SkillBase</c>.
/// </summary>
public class SkillBase
{
    /// <summary>
    /// Namespace the skill belongs to.
    /// </summary>
    public string? NamespaceId { get; set; }

    /// <summary>
    /// Skill name (unique within a namespace).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Skill description.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Basic skill info with update timestamp.
/// Mirrors <c>com.alibaba.nacos.api.ai.model.skills.SkillBasicInfo</c>.
/// </summary>
public class SkillBasicInfo : SkillBase
{
    /// <summary>
    /// Last update time (epoch milliseconds).
    /// </summary>
    public long? UpdateTime { get; set; }
}
