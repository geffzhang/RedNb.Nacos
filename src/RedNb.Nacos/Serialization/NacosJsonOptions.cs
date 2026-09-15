using System.Text.Json;

namespace RedNb.Nacos.Serialization;

/// <summary>
/// Single construction point for core-library <see cref="JsonSerializerOptions"/>.
/// The settings must stay in sync with <see cref="NacosJsonContext"/>'s
/// <see cref="System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute"/>.
/// </summary>
internal static class NacosJsonOptions
{
    public static JsonSerializerOptions Create(bool writeIndented = false, System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? user = null, bool allowReflectionFallback = true)
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = NacosJsonResolver.Create(NacosJsonContext.Default, user, allowReflectionFallback),
            PropertyNameCaseInsensitive = true,
            WriteIndented = writeIndented
        };
        options.MakeReadOnly();
        return options;
    }
}
