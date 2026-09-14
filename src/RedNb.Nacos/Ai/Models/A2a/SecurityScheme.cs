using System.Text.Json.Serialization;
using RedNb.Nacos.Serialization;

namespace RedNb.Nacos.Ai.Models.A2a;

/// <summary>
/// Security scheme definition (flexible key-value structure).
/// </summary>
[JsonConverter(typeof(SecuritySchemeConverter))]
public class SecurityScheme : Dictionary<string, object>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityScheme"/> class.
    /// </summary>
    public SecurityScheme() : base(4)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityScheme"/> class with existing data.
    /// </summary>
    /// <param name="dictionary">The dictionary to copy from.</param>
    public SecurityScheme(IDictionary<string, object> dictionary) : base(dictionary)
    {
    }
}
