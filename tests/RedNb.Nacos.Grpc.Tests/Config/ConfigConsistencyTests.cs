using RedNb.Nacos;
using RedNb.Nacos.Config;
using RedNb.Nacos.Grpc.Config;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Config;

public class ConfigConsistencyTests
{
    [Fact]
    public async Task RepeatedReadWithoutSubscriptionFetchesLatestContent()
    {
        var options = new NacosClientOptions { Namespace = Guid.NewGuid().ToString("N") };
        var fake = new FakeNacosGrpcClient(options) { Response = Response("v1") };
        await using var service = new NacosGrpcConfigService(options, fake);
        Assert.Equal("v1", await service.GetConfigAsync("config", "G", 1000));
        fake.Response = Response("v2");
        Assert.Equal("v2", await service.GetConfigAsync("config", "G", 1000));
        Assert.Equal(2, fake.Captured.Count(c => c.request is ConfigQueryRequest));
    }

    [Fact]
    public async Task DeletedConfigDoesNotReturnOldSnapshot()
    {
        var options = new NacosClientOptions { Namespace = Guid.NewGuid().ToString("N") };
        var fake = new FakeNacosGrpcClient(options) { Response = Response("old") };
        await using var service = new NacosGrpcConfigService(options, fake);
        await service.GetConfigAsync("config", "G", 1000);
        fake.Response = new ConfigQueryResponse { ErrorCode = 300, ResultCode = 500 };
        Assert.Null(await service.GetConfigAsync("config", "G", 1000));
    }

    [Fact]
    public async Task PermissionDeniedMustNotFallBackToCachedContent()
    {
        var options = new NacosClientOptions { Namespace = Guid.NewGuid().ToString("N") };
        var fake = new FakeNacosGrpcClient(options) { Response = Response("private") };
        await using var service = new NacosGrpcConfigService(options, fake);
        await service.GetConfigAsync("config", "G", 1000);
        fake.Response = new ConfigQueryResponse { ErrorCode = 403, ResultCode = 500, Message = "denied" };
        var error = await Assert.ThrowsAsync<NacosException>(() => service.GetConfigAsync("config", "G", 1000));
        Assert.Equal(403, error.ErrorCode);
    }

    private static ConfigQueryResponse Response(string content) => new() { ResultCode = 200, Content = content, Md5 = content };
}
