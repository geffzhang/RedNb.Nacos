using RedNb.Nacos.Config.FuzzyWatch;
using RedNb.Nacos.Naming.FuzzyWatch;
using RedNb.Nacos.Grpc;
using Xunit;

namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class FuzzyWatchTests
{
    [Fact]
    public async Task ConfigPatternReceivesNewResource()
    {
        var prefix = "audit-fuzzy-" + Guid.NewGuid().ToString("N");
        await using var client = await NacosGrpcFactory.CreateConfigServiceAsync(ConfigReliabilityTests.Options());
        await using var writer = await NacosGrpcFactory.CreateConfigServiceAsync(ConfigReliabilityTests.Options());
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var watcher = new ConfigFuzzyWatchEventWatcher(e => { if (e.DataId == prefix) received.TrySetResult(true); });
        try
        {
            await writer.PublishConfigAsync(prefix + "initial", "DEFAULT_GROUP", "initial");
            await ConfigReliabilityTests.Eventually(async () => await writer.GetConfigAsync(prefix + "initial", "DEFAULT_GROUP", 5000) == "initial");
            var keys = await client.FuzzyWatchWithGroupKeysAsync(prefix + "*", "DEFAULT_GROUP", watcher);
            Assert.Contains(keys, key => key.Contains(prefix + "initial"));
            await writer.PublishConfigAsync(prefix, "DEFAULT_GROUP", "fuzzy");
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            await client.CancelFuzzyWatchAsync(prefix + "*", "DEFAULT_GROUP", watcher);
            await writer.RemoveConfigAsync(prefix, "DEFAULT_GROUP");
            await writer.RemoveConfigAsync(prefix + "initial", "DEFAULT_GROUP");
        }
    }

    [Fact]
    public async Task NamingPatternReceivesNewService()
    {
        var prefix = "audit-fuzzy-" + Guid.NewGuid().ToString("N");
        await using var client = await NacosGrpcFactory.CreateNamingServiceAsync(ConfigReliabilityTests.Options());
        await using var writer = await NacosGrpcFactory.CreateNamingServiceAsync(ConfigReliabilityTests.Options());
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var watcher = new NamingFuzzyWatchEventWatcher(e => { if (e.ServiceName == prefix) received.TrySetResult(true); });
        try
        {
            await writer.RegisterInstanceAsync(prefix + "initial", "127.0.0.1", 19322);
            await ConfigReliabilityTests.Eventually(async () => (await writer.GetAllInstancesAsync(prefix + "initial", false)).Count > 0);
            await ConfigReliabilityTests.Eventually(async () =>
            {
                var keys = await client.FuzzyWatchWithGroupKeysAsync(prefix + "*", "DEFAULT_GROUP", watcher);
                if (keys.Any(key => key.Contains(prefix + "initial"))) return true;
                await client.CancelFuzzyWatchAsync(prefix + "*", "DEFAULT_GROUP", watcher);
                return false;
            });
            await writer.RegisterInstanceAsync(prefix, "127.0.0.1", 19321);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            await client.CancelFuzzyWatchAsync(prefix + "*", "DEFAULT_GROUP", watcher);
            await writer.DeregisterInstanceAsync(prefix, "127.0.0.1", 19321);
            await writer.DeregisterInstanceAsync(prefix + "initial", "127.0.0.1", 19322);
        }
    }
}
