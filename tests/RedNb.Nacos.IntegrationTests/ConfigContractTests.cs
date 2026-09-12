using System.Security.Cryptography;
using System.Text;
using RedNb.Nacos.Grpc;
using Xunit;

namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class ConfigContractTests
{
    [Fact]
    public async Task CasRejectsStaleMd5WithoutOverwritingContent()
    {
        var id = "audit-cas-" + Guid.NewGuid().ToString("N");
        await using var client = await NacosGrpcFactory.CreateConfigServiceAsync(ConfigReliabilityTests.Options());
        try
        {
            await client.PublishConfigAsync(id, "DEFAULT_GROUP", "original");
            await ConfigReliabilityTests.Eventually(async () => await client.GetConfigAsync(id, "DEFAULT_GROUP", 5000) == "original");
            var md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("original"))).ToLowerInvariant();
            Assert.True(await client.PublishConfigCasAsync(id, "DEFAULT_GROUP", "updated", md5));
            await ConfigReliabilityTests.Eventually(async () => await client.GetConfigAsync(id, "DEFAULT_GROUP", 5000) == "updated");
            Assert.False(await client.PublishConfigCasAsync(id, "DEFAULT_GROUP", "wrong", md5));
            Assert.Equal("updated", await client.GetConfigAsync(id, "DEFAULT_GROUP", 5000));
        }
        finally { await client.RemoveConfigAsync(id, "DEFAULT_GROUP"); }
    }

    [Fact]
    public async Task CallerCancellationDoesNotReturnCachedSuccess()
    {
        await using var client = await NacosGrpcFactory.CreateConfigServiceAsync(ConfigReliabilityTests.Options());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetConfigAsync("audit-cancel", "DEFAULT_GROUP", 5000, cancellation.Token));
    }
}
