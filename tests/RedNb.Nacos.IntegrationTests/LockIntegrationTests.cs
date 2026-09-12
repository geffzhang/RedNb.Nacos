using RedNb.Nacos.Lock;
using RedNb.Nacos.Grpc.Lock;
using Xunit;
namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class LockIntegrationTests
{
    [Fact]
    public async Task NativeMutexContendsAndReleases()
    {
        await using var first = new NacosGrpcLockService(ConfigReliabilityTests.Options());
        await using var second = new NacosGrpcLockService(ConfigReliabilityTests.Options());
        var key = "audit-lock-" + Guid.NewGuid().ToString("N");
        var one = new LockInstance { Key = key, ExpireTime = 10000 };
        var two = new LockInstance { Key = key, ExpireTime = 10000 };
        Assert.True(await first.LockAsync(one));
        try
        {
            Assert.False(await second.LockAsync(two));
            Assert.False(await second.UnlockAsync(two));
            Assert.True(await first.UnlockAsync(one));
            Assert.True(await second.LockAsync(two));
            Assert.True(await second.UnlockAsync(two));
        }
        finally { await first.UnlockAsync(one); await second.UnlockAsync(two); }
    }
}
