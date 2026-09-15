using System.Text.Json;

namespace RedNb.Nacos.Serialization;

/// <summary>
/// Single construction point for core-library <see cref="JsonSerializerOptions"/>.
/// The settings must stay in sync with <see cref="NacosJsonContext"/>'s
/// <see cref="System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute"/>.
/// </summary>
internal static class NacosJsonOptions
{
    private static readonly JsonSerializerOptions Plain = CreateCore(false);
    private static readonly JsonSerializerOptions Indented = CreateCore(true);

    public static JsonSerializerOptions Create(bool writeIndented = false) => writeIndented ? Indented : Plain;

    private static JsonSerializerOptions CreateCore(bool writeIndented) => new()
    {
        TypeInfoResolver = NacosJsonContext.Default,
        PropertyNameCaseInsensitive = true,
        WriteIndented = writeIndented
    };
}
