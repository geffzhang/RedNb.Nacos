using RedNb.Nacos.Http.Administration;
using RedNb.Nacos.Grpc;
using Xunit;

namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class AdministrationTests
{
    [Fact]
    public async Task NamespaceLifecycleAndIsolation()
    {
        var id = "audit-ns-" + Guid.NewGuid().ToString("N");
        var options = ConfigReliabilityTests.Options();
        await using var admin = new NacosAdministrationService(options);
        await admin.CreateNamespaceAsync(id, "SDK integration test");
        try
        {
            Assert.Contains(await admin.ListNamespacesAsync(), n => n.Id == id);
            await admin.UpdateNamespaceAsync(id, "SDK updated namespace");
            Assert.Contains(await admin.ListNamespacesAsync(), n => n.Id == id && n.DisplayName == "SDK updated namespace");
            var isolated = ConfigReliabilityTests.Options(); isolated.Namespace = id;
            await using var client = await NacosGrpcFactory.CreateConfigServiceAsync(isolated);
            await using var other = await NacosGrpcFactory.CreateConfigServiceAsync(options);
            await client.PublishConfigAsync(id, "DEFAULT_GROUP", "isolated");
            try
            {
                await ConfigReliabilityTests.Eventually(async () => await client.GetConfigAsync(id, "DEFAULT_GROUP", 5000) == "isolated");
                Assert.Null(await other.GetConfigAsync(id, "DEFAULT_GROUP", 5000));
            }
            finally { await client.RemoveConfigAsync(id, "DEFAULT_GROUP"); }
        }
        finally { await admin.DeleteNamespaceAsync(id); }
        Assert.DoesNotContain(await admin.ListNamespacesAsync(), n => n.Id == id);
    }
}
