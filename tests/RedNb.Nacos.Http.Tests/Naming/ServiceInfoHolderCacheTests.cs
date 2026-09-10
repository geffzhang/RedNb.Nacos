using FluentAssertions;
using RedNb.Nacos.Client.Naming;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Naming;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Naming;

public class ServiceInfoHolderCacheTests
{
    [Fact]
    public void GetServiceInfo_ReturnsCachedEntry_WhenNotExpired()
    {
        var options = new NacosClientOptions { NamingLoadCacheAtStart = false };
        var holder = new ServiceInfoHolder(options);

        var info = new ServiceInfo
        {
            Name = "svc",
            GroupName = "DEFAULT_GROUP",
            Clusters = "",
            Hosts = new List<Instance>(),
            CacheMillis = 60_000  // 60 s TTL — well in the future
            // ProcessServiceInfo will set LastRefTime automatically
        };

        holder.ProcessServiceInfo(info);

        var fetched = holder.GetServiceInfo("svc", "DEFAULT_GROUP", "");
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("svc");
        fetched.CacheMillis.Should().Be(60_000);
    }

    [Fact]
    public void GetServiceInfo_ReturnsNull_WhenExpired()
    {
        var options = new NacosClientOptions { NamingLoadCacheAtStart = false };
        var holder = new ServiceInfoHolder(options);

        var info = new ServiceInfo
        {
            Name = "svc",
            GroupName = "DEFAULT_GROUP",
            Clusters = "",
            Hosts = new List<Instance>(),
            CacheMillis = 100  // 100 ms TTL
        };

        holder.ProcessServiceInfo(info);

        // Manually overwrite LastRefTime to simulate expiry (avoid 100 ms real-time wait).
        // ServiceInfo.LastRefTime has a public setter.
        info.LastRefTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 5000;  // 5 s ago

        var fetched = holder.GetServiceInfo("svc", "DEFAULT_GROUP", "");
        fetched.Should().BeNull("entry exists in cache but LastRefTime is 5 s ago and CacheMillis is 100 ms");
    }

    [Fact]
    public void ProcessServiceInfo_UpdatesLastRefTime()
    {
        var options = new NacosClientOptions { NamingLoadCacheAtStart = false };
        var holder = new ServiceInfoHolder(options);

        var info = new ServiceInfo
        {
            Name = "svc",
            GroupName = "DEFAULT_GROUP",
            Clusters = "",
            Hosts = new List<Instance>(),
            CacheMillis = 60_000,
            LastRefTime = 0  // never refreshed
        };

        holder.ProcessServiceInfo(info);

        var fetched = holder.GetServiceInfo("svc", "DEFAULT_GROUP", "");
        fetched.Should().NotBeNull();
        fetched!.LastRefTime.Should().BeGreaterThan(0);  // updated by ProcessServiceInfo
    }
}
