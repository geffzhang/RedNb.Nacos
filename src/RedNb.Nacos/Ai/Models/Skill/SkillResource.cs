using System.Text.Json.Serialization;
using RedNb.Nacos.Serialization;

namespace RedNb.Nacos.Ai.Models.Skill;

/// <summary>
/// A resource file bundled with a Skill (script, reference doc, asset...).
/// Mirrors <c>com.alibaba.nacos.api.ai.model.skills.SkillResource</c>.
/// </summary>
public class SkillResource
{
    /// <summary>
    /// Resource file name (relative path inside the skill package).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Resource type, e.g. <c>scripts</c>, <c>references</c>, <c>assets</c>.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Text content of the resource.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Additional metadata of the resource.
    /// </summary>
    [JsonConverter(typeof(ObjectDictionaryConverter))]
    public Dictionary<string, object>? Metadata { get; set; }
}
