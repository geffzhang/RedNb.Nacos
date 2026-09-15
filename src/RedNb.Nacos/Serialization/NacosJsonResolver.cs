using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Diagnostics.CodeAnalysis;

namespace RedNb.Nacos.Serialization;

/// <summary>SDK contracts are source-generated; reflection can only serve user-owned types.</summary>
internal sealed class NacosJsonResolver(
    IJsonTypeInfoResolver sdk,
    IJsonTypeInfoResolver? user,
    IJsonTypeInfoResolver? reflection) : IJsonTypeInfoResolver
{
    // This is the sole JIT compatibility boundary. JSON's link-time switch and
    // RuntimeFeature remove it in strict/NativeAOT applications; it does not
    // preserve arbitrary application types in trimmed applications.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Guarded JIT-only compatibility fallback. The branch is disabled by the JSON reflection feature switch for trimmed/strict builds and by IsDynamicCodeSupported for NativeAOT; all SDK contracts bypass this fallback.")]
#if NET8_0
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = ".NET 8 analyzer cannot infer the runtime gate; NativeAOT sets IsDynamicCodeSupported=false, so this JIT-only branch is unreachable.")]
#endif
    public static IJsonTypeInfoResolver Create(IJsonTypeInfoResolver sdk, IJsonTypeInfoResolver? user = null, bool allowReflectionFallback = true)
    {
        IJsonTypeInfoResolver? fallback = null;
        // Keep the feature guard as a distinct branch: NativeAOT's IL scanner
        // does not infer the guard through a compound boolean/ternary expression.
        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            if (JsonSerializer.IsReflectionEnabledByDefault && allowReflectionFallback)
                fallback = JsonSerializerOptions.Default.TypeInfoResolver;
        }
        return new NacosJsonResolver(sdk, user, fallback);
    }

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var metadata = sdk.GetTypeInfo(type, options);
        if (metadata != null) return metadata;
        if (!ContainsSdkType(type))
        {
            metadata = user?.GetTypeInfo(type, options) ?? reflection?.GetTypeInfo(type, options);
            if (metadata != null) return metadata;
        }
        throw new NotSupportedException(
            $"No JSON metadata registered for '{type.FullName}'. Register a source-generated context with NacosClientOptions.JsonTypeInfoResolver, or supply JsonTypeInfo<T> to the generic cache.");
    }

    private static bool ContainsSdkType(Type type)
    {
        var assembly = type.Assembly.GetName().Name;
        if (assembly is "RedNb.Nacos" or "RedNb.Nacos.Http" or "RedNb.Nacos.Grpc" or
            "RedNb.Nacos.DependencyInjection" or "RedNb.Nacos.AspNetCore") return true;
        if (type.IsArray) return ContainsSdkType(type.GetElementType()!);
        return type.IsGenericType && type.GetGenericArguments().Any(ContainsSdkType);
    }
}
