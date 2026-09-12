namespace RedNb.Nacos.Core.Ai.Model.Skills;

/// <summary>
/// Downloaded skill package (ZIP bytes) together with the resolved md5 and version
/// carried by the X-Nacos-Skill-Md5 and X-Nacos-Skill-Resolved-Version response headers.
/// </summary>
public class SkillPackage
{
    /// <summary>
    /// Skill name.
    /// </summary>
    public string? SkillName { get; set; }

    /// <summary>
    /// Downloaded skill package (ZIP bytes).
    /// </summary>
    public byte[] ZipContent { get; set; } = [];

    /// <summary>
    /// Md5 of the skill, from the X-Nacos-Skill-Md5 header.
    /// </summary>
    public string? Md5 { get; set; }

    /// <summary>
    /// Resolved version, from the X-Nacos-Skill-Resolved-Version header.
    /// </summary>
    public string? Version { get; set; }
}
