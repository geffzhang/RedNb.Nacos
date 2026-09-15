using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using RedNb.Nacos.Config;
using RedNb.Nacos.Failover;
using RedNb.Nacos.Serialization;
using Xunit;

namespace RedNb.Nacos.Tests.Serialization;

public class UserContextTests
{
    [Fact]
    public void JitRetainsUnregisteredApplicationTypes()
    {
        var options = NacosJsonOptions.Create();
        var value = new UserValue { Value = 42 };
        Assert.Equal("{\"Value\":42}", JsonSerializer.Serialize(value, options.GetTypeInfo(typeof(UserValue))));
    }

    [Fact]
    public void StrictModeAcceptsRegisteredTypesAndRejectsUnknownTypes()
    {
        var options = NacosJsonOptions.Create(user: ExtensionContext.Default, allowReflectionFallback: false);
        Assert.Equal("{\"Value\":42}", JsonSerializer.Serialize(new UserValue { Value = 42 }, options.GetTypeInfo(typeof(UserValue))));
        var error = Assert.Throws<NotSupportedException>(() => options.GetTypeInfo(typeof(UnknownValue)));
        Assert.Contains(nameof(UnknownValue), error.Message);
        Assert.Contains("JsonTypeInfoResolver", error.Message);
    }

    [Fact]
    public void ClientContextsAreIsolatedAndCannotOverrideSdkContracts()
    {
        var first = NacosJsonOptions.Create(user: new RenamingResolver("first"), allowReflectionFallback: false);
        var second = NacosJsonOptions.Create(user: new RenamingResolver("second"), allowReflectionFallback: false);
        Assert.True(first.IsReadOnly);
        Assert.True(second.IsReadOnly);
        Assert.Contains("\"first\":7", JsonSerializer.Serialize(new UserValue { Value = 7 }, first.GetTypeInfo(typeof(UserValue))));
        Assert.Contains("\"second\":7", JsonSerializer.Serialize(new UserValue { Value = 7 }, second.GetTypeInfo(typeof(UserValue))));
        Assert.Contains("\"DataId\":", JsonSerializer.Serialize(new ConfigInfo { DataId = "d" }, first.GetTypeInfo(typeof(ConfigInfo))));
    }

    [Fact]
    public void ExplicitGenericCacheMetadataRoundtripsCustomData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nacos-context-" + Guid.NewGuid().ToString("N"));
        var store = new LocalDiskFailoverDataSource<UserValue>(NullLogger.Instance, directory, "switch", ExtensionContext.Default.UserValue);
        try
        {
            store.SaveFailoverData("custom", new UserValue { Value = 17 });
            Assert.Equal(17, store.GetFailoverData()["custom"].Data.Value);
        }
        finally { store.DeleteFailoverData("custom"); Directory.Delete(directory); }
    }
}

public sealed class UserValue { public int Value { get; set; } }
public sealed class UnknownValue { public string? Name { get; set; } }

[JsonSerializable(typeof(UserValue))]
internal partial class ExtensionContext : JsonSerializerContext { }

internal sealed class RenamingResolver(string name) : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        // The SDK resolver must never ask this resolver to override ConfigInfo.
        Assert.NotEqual(typeof(ConfigInfo), type);
        var info = ((IJsonTypeInfoResolver)ExtensionContext.Default).GetTypeInfo(type, options);
        if (type == typeof(UserValue) && info != null) info.Properties[0].Name = name;
        return info;
    }
}
