using Microsoft.Extensions.DependencyInjection;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Config;
using RedNb.Nacos.Naming;
using RedNb.Nacos.DependencyInjection;
using RedNb.Nacos.Grpc.Ai;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public async Task AiShutdownDisposesTheWholeFacadeWithoutOpeningUnusedClients()
    {
        await using var client = new NacosAiClient(new NacosClientOptions());
        await client.ShutdownAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetPromptAsync("unused"));
    }
    [Fact]
    public async Task DefaultServicesUseGrpcAndConfigureOnce()
    {
        var count = 0;
        var services = new ServiceCollection();
        services.AddNacos(options => { count++; options.ServerAddresses = "localhost:8848"; });
        await using var provider = services.BuildServiceProvider();
        Assert.IsType<NacosGrpcConfigService>(provider.GetRequiredService<IConfigService>());
        Assert.IsType<NacosGrpcNamingService>(provider.GetRequiredService<INamingService>());
        Assert.Null(provider.GetService<IAiService>());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task NamedClientsHaveIndependentLifetimes()
    {
        var services = new ServiceCollection();
        services.AddNacos("one", options => options.ServerAddresses = "localhost:8848");
        services.AddNacos("two", options => options.ServerAddresses = "localhost:8849");
        await using var provider = services.BuildServiceProvider();
        var one = provider.GetRequiredKeyedService<IConfigService>("one");
        var two = provider.GetRequiredKeyedService<IConfigService>("two");
        Assert.NotSame(one, two);
        await one.DisposeAsync();
        Assert.NotNull(two);
    }

    [Fact]
    public async Task AiInterfacesShareTheCompositeClient()
    {
        var services = new ServiceCollection();
        services.AddNacosAi(options => options.ServerAddresses = "localhost:8848");
        await using var provider = services.BuildServiceProvider();
        var ai = provider.GetRequiredService<IAiService>();
        Assert.IsType<NacosAiClient>(ai);
        Assert.Same(ai, provider.GetRequiredService<IPromptService>());
        Assert.Same(ai, provider.GetRequiredService<ISkillService>());
        Assert.Same(ai, provider.GetRequiredService<IAgentSpecService>());
    }
}
