using Microsoft.Extensions.Logging.Abstractions;
using RedNb.Nacos.Failover;
using RedNb.Nacos.Naming;
using Xunit;

namespace RedNb.Nacos.Tests.Failover;

public class DiskNamingFailoverDataSourceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedMetadataRetainsCacheFormatAndReadsLegacyCasing(bool legacy)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nacos-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new DiskNamingFailoverDataSource(NullLogger.Instance, directory);
            cache.SaveServiceInfo(new ServiceInfo { Name = "cache-service" });
            var file = Assert.Single(Directory.GetFiles(Path.Combine(directory, "failover")));
            Assert.Contains("\n", File.ReadAllText(file));
            if (legacy) File.WriteAllText(file, """{"Name":"cache-service","GroupName":"DEFAULT_GROUP","Hosts":[]}""");
            cache.SetSwitch(true);
            Assert.True(cache.GetSwitch().Enabled);
            Assert.Equal("cache-service", Assert.Single(cache.GetFailoverData()).Value.Data.Name);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
