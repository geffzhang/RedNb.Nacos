using RedNb.Nacos.Http.Ai;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Ai;

public class AiManagementContractTests
{
    [Fact]
    public async Task PromptVersionsCollectAllPages()
    {
        using var server = TestHttpServer.Start();
        foreach (var page in new[] { 1, 2 })
            server.Given(Request.Create().WithPath("/nacos/v3/admin/ai/prompt/versions").WithParam("pageNo", page.ToString()).UsingGet())
                .RespondWith(Response.Create().WithBody("""{"code":0,"data":{"totalCount":2,"pageItems":[{"version":"PAGE.0.0","status":"online"}]}}""".Replace("PAGE", page.ToString())));
        await using var client = new NacosAiService(new NacosClientOptions { ServerAddresses = $"localhost:{server.Port}" });
        var versions = await client.ListPromptVersionsAsync("test");
        Assert.Equal(new[] { "1.0.0", "2.0.0" }, versions.Select(v => v.Version));
    }

    [Fact]
    public async Task SkillMetadataAndVersionDetailUseDistinctRoutes()
    {
        using var server = TestHttpServer.Start();
        server.Given(Request.Create().WithPath("/nacos/v3/admin/ai/skills").UsingGet())
            .RespondWith(Response.Create().WithBody("""{"code":0,"data":{"name":"test","labels":{"latest":"1.0.0"},"versions":[{"version":"1.0.0","status":"online"}]}}"""));
        server.Given(Request.Create().WithPath("/nacos/v3/admin/ai/skills/version").WithParam("version", "1.0.0").UsingGet())
            .RespondWith(Response.Create().WithBody("""{"code":0,"data":{"name":"test","version":"1.0.0"}}"""));
        await using var client = new NacosAiService(new NacosClientOptions { ServerAddresses = $"localhost:{server.Port}" });
        Assert.Equal("online", Assert.Single((await client.GetSkillMetaAsync("test"))!.Versions!).Status);
        Assert.NotNull(await client.GetSkillDetailAsync("test", "1.0.0"));
        Assert.NotNull(await client.GetSkillDetailAsync("test"));
    }

    [Fact]
    public async Task AgentSpecMetadataAndVersionDetailUseDistinctRoutes()
    {
        using var server = TestHttpServer.Start();
        server.Given(Request.Create().WithPath("/nacos/v3/admin/ai/agentspecs").UsingGet())
            .RespondWith(Response.Create().WithBody("""{"code":0,"data":{"name":"test","updateTime":1789457953293,"labels":{"latest":"1.0.0"},"versions":[{"version":"1.0.0","status":"online","createTime":1789457953292,"updateTime":1789457953293}]}}"""));
        server.Given(Request.Create().WithPath("/nacos/v3/admin/ai/agentspecs/version").WithParam("version", "1.0.0").UsingGet())
            .RespondWith(Response.Create().WithBody("""{"code":0,"data":{"name":"test","version":"1.0.0"}}"""));
        await using var client = new NacosAiService(new NacosClientOptions { ServerAddresses = $"localhost:{server.Port}" });
        var meta = (await client.GetAgentSpecMetaAsync("test"))!;
        Assert.Equal("1789457953293", meta.UpdateTime);
        Assert.Equal("1789457953292", Assert.Single(meta.Versions!).CreateTime);
        Assert.Equal("online", Assert.Single(meta.Versions!).Status);
        Assert.NotNull(await client.GetAgentSpecDetailAsync("test", "1.0.0"));
        Assert.NotNull(await client.GetAgentSpecDetailAsync("test"));
    }
}
