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

    private static readonly JsonSerializerOptions Instance = new()
    {
        TypeInfoResolver = Combined
    };

    private static readonly JsonSerializerOptions Ai = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = CombinedAi
    };

    private static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = CombinedWithCore
    };

    public static JsonSerializerOptions Create() => Instance;

    public static JsonSerializerOptions CreateAi() => Ai;

    public static JsonSerializerOptions CreateCaseInsensitive() => CaseInsensitive;
}
