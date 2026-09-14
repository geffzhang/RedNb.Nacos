using System.Text.Json;

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
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = NacosGrpcJsonContext.Default
    };

    public static JsonSerializerOptions Create() => Instance;
}
