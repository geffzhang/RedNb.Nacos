using RedNb.Nacos.Ai.Listener;

namespace RedNb.Nacos.Ai.Models.Skill;

/// <summary>
/// Event fired when the subscribed skill changes.
/// Carries the downloaded skill package bytes together with the resolved md5 and version.
/// </summary>
public class NacosSkillEvent : INacosAiEvent
{
    /// <summary>
    /// Create a skill change event.
    /// </summary>
    /// <param name="skillName">Skill name.</param>
    /// <param name="zipContent">Downloaded skill package (ZIP bytes).</param>
    /// <param name="md5">Md5 of the skill, from the X-Nacos-Skill-Md5 header.</param>
    /// <param name="version">Resolved version, from the X-Nacos-Skill-Resolved-Version header.</param>
    public NacosSkillEvent(string skillName, byte[] zipContent, string? md5, string? version)
    {
        SkillName = skillName;
        ZipContent = zipContent;
        Md5 = md5;
        Version = version;
    }

    /// <summary>
    /// Skill name.
    /// </summary>
    public string SkillName { get; }

    /// <summary>
    /// Downloaded skill package (ZIP bytes).
    /// </summary>
    public byte[] ZipContent { get; }

    /// <summary>
    /// Md5 of the skill.
    /// </summary>
    public string? Md5 { get; }

    /// <summary>
    /// Resolved version.
    /// </summary>
    public string? Version { get; }
}

/// <summary>
/// Abstract listener for skill change events.
/// Mirrors <c>com.alibaba.nacos.api.ai.listener.skills.AbstractNacosSkillListener</c>.
/// </summary>
public abstract class AbstractNacosSkillListener : INacosAiListener<NacosSkillEvent>
{
    /// <inheritdoc />
    public abstract void OnEvent(NacosSkillEvent @event);
}
