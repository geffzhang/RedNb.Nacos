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
    public static JsonSerializerOptions Create(System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? user = null, bool allowReflectionFallback = true)
    {
        var options = new JsonSerializerOptions
        {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = RedNb.Nacos.Serialization.NacosJsonResolver.Create(JsonTypeInfoResolver.Combine(
            NacosGrpcJsonContext.Default,
            NacosGrpcInternalJsonContext.Default), user, allowReflectionFallback)
        };
        options.MakeReadOnly();
        return options;
    }
}
