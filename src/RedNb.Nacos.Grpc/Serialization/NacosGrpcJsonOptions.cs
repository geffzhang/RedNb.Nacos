using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RedNb.Nacos.Grpc.Serialization;

/// <summary>
/// Single construction point for gRPC <see cref="JsonSerializerOptions"/>.
/// Settings must stay in sync with <see cref="NacosGrpcJsonContext"/>'s options.
/// </summary>
internal static class NacosGrpcJsonOptions
{
    private static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            NacosGrpcJsonContext.Default,
            NacosGrpcInternalJsonContext.Default)
    };

    public static JsonSerializerOptions Create() => Instance;
}
