using Microsoft.Extensions.Configuration;
using RedNb.Nacos.AspNetCore.Configuration;
using RedNb.Nacos;
using RedNb.Nacos.Config;
using RedNb.Nacos.Grpc;
using Xunit;

namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class ConfigReliabilityTests
{
    internal static NacosClientOptions Options() => new()
    {
        ServerAddresses = NacosServerFixture.ServerAddress,
        ConsoleAddresses = NacosServerFixture.ConsoleAddress,
        Username = NacosServerFixture.Username,
        Password = NacosServerFixture.Password,
        Namespace = NacosServerFixture.Namespace,
        DefaultTimeout = 5000
    };

    internal static async Task Eventually(Func<Task<bool>> condition, int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try { if (await condition()) return; } catch (Exception ex) { last = ex; }
            await Task.Delay(250);
        }
        Assert.Fail("Condition not met within deadline. " + last?.Message);
    }

    [Fact]
    public async Task IndependentReaderObservesUpdateAndDelete()
    {
        var id = "audit-read-" + Guid.NewGuid().ToString("N");
        await using var writer = await NacosGrpcFactory.CreateConfigServiceAsync(Options());
        await using var reader = await NacosGrpcFactory.CreateConfigServiceAsync(Options());
        try
        {
            Assert.True(await writer.PublishConfigAsync(id, "DEFAULT_GROUP", "v1"));
            await Eventually(async () => await reader.GetConfigAsync(id, "DEFAULT_GROUP", 5000) == "v1");
            Assert.True(await writer.PublishConfigAsync(id, "DEFAULT_GROUP", "v2"));
            await Eventually(async () => await reader.GetConfigAsync(id, "DEFAULT_GROUP", 5000) == "v2");
            Assert.True(await writer.RemoveConfigAsync(id, "DEFAULT_GROUP"));
            await Eventually(async () => await reader.GetConfigAsync(id, "DEFAULT_GROUP", 5000) == null);
        }
        finally { await writer.RemoveConfigAsync(id, "DEFAULT_GROUP"); }
    }

    [Fact]
    public async Task ListenerReceivesExternalDeletion()
    {
        var id = "audit-delete-" + Guid.NewGuid().ToString("N");
        await using var writer = await NacosGrpcFactory.CreateConfigServiceAsync(Options());
        await using var reader = await NacosGrpcFactory.CreateConfigServiceAsync(Options());
        var deleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new Listener(info => { if (info.Content == null) deleted.TrySetResult(true); });
        try
        {
            await writer.PublishConfigAsync(id, "DEFAULT_GROUP", "exists");
            await Eventually(async () => await reader.GetConfigAsync(id, "DEFAULT_GROUP", 5000) == "exists");
            await reader.AddListenerAsync(id, "DEFAULT_GROUP", listener);
            await writer.RemoveConfigAsync(id, "DEFAULT_GROUP");
            await deleted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { reader.RemoveListener(id, "DEFAULT_GROUP", listener); await writer.RemoveConfigAsync(id, "DEFAULT_GROUP"); }
    }

    [Fact]
    public async Task AspNetConfigurationReloadsAndRemovesKeys()
    {
        var id = "audit-options-" + Guid.NewGuid().ToString("N");
        await using var writer = await NacosGrpcFactory.CreateConfigServiceAsync(Options());
        try
        {
            await writer.PublishConfigAsync(id, "DEFAULT_GROUP", "{\"Value\":\"one\",\"Removed\":\"old\"}", "json");
            await Eventually(async () => (await writer.GetConfigAsync(id, "DEFAULT_GROUP", 5000))?.Contains("one") == true);
            var builder = new ConfigurationBuilder();
            builder.AddNacosConfiguration(source =>
            {
                source.Options = Options(); source.ReloadOnChange = true;
                source.ConfigItems.Add(new NacosConfigurationItem { DataId = id, Group = "DEFAULT_GROUP", ConfigType = "json" });
            });
            using var config = (IDisposable)builder.Build();
            var root = (IConfiguration)config;
            Assert.Equal("one", root["Value"]);
            await writer.PublishConfigAsync(id, "DEFAULT_GROUP", "{\"Value\":\"two\"}", "json");
            await Eventually(() => Task.FromResult(root["Value"] == "two" && root["Removed"] == null));
            await writer.RemoveConfigAsync(id, "DEFAULT_GROUP");
            await Eventually(() => Task.FromResult(root["Value"] == null));
        }
        finally { await writer.RemoveConfigAsync(id, "DEFAULT_GROUP"); }
    }

    internal sealed class Listener(Action<ConfigInfo> action) : IConfigChangeListener
    { public void OnReceiveConfigInfo(ConfigInfo info) => action(info); }
}
