using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using RedNb.Nacos.Serialization;

namespace RedNb.Nacos.Http.Serialization;

/// <summary>
/// Single construction point for HTTP <see cref="JsonSerializerOptions"/>.
/// <see cref="Create"/> keeps 2.0.0's <c>new()</c> defaults (PascalCase, strict
/// reads); <see cref="CreateAi"/> mirrors the AI services' 2.0.0 options
/// (camelCase output, case-insensitive reads); <see cref="CreateCaseInsensitive"/>
/// matches the former <c>JsonSerializerDefaults.Web</c> reads of
/// <c>JsonElement.Deserialize</c> call sites.
/// </summary>
internal static class NacosHttpJsonOptions
{
    private static readonly IJsonTypeInfoResolver Combined = JsonTypeInfoResolver.Combine(
        NacosHttpJsonContext.Default,
        NacosHttpInternalJsonContext.Default);

    private static readonly IJsonTypeInfoResolver CombinedAi = JsonTypeInfoResolver.Combine(
        NacosHttpJsonContext.Default,
        NacosHttpInternalJsonContext.Default,
        NacosHttpAiJsonContext.Default);

    private static readonly IJsonTypeInfoResolver CombinedWithCore = JsonTypeInfoResolver.Combine(
        NacosHttpJsonContext.Default,
        NacosHttpInternalJsonContext.Default,
        NacosJsonContext.Default);

    public static JsonSerializerOptions Create(IJsonTypeInfoResolver? user = null, bool allowReflectionFallback = true)
        => Build(Combined, user, false, false, allowReflectionFallback);

    public static JsonSerializerOptions CreateAi(IJsonTypeInfoResolver? user = null)
        => Build(CombinedAi, user, true, true);

    public static JsonSerializerOptions CreateCaseInsensitive(IJsonTypeInfoResolver? user = null)
        => Build(CombinedWithCore, user, false, true);

    private static JsonSerializerOptions Build(IJsonTypeInfoResolver sdk, IJsonTypeInfoResolver? user, bool camelCase, bool caseInsensitive, bool allowReflectionFallback = true)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = camelCase ? JsonNamingPolicy.CamelCase : null,
            PropertyNameCaseInsensitive = caseInsensitive,
            TypeInfoResolver = NacosJsonResolver.Create(sdk, user, allowReflectionFallback)
        };
        options.MakeReadOnly();
        return options;
    }
}
