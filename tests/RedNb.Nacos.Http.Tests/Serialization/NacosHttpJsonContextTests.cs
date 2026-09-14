using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Http.Ai;
using RedNb.Nacos.Http.Serialization;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Naming.Models;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Serialization;

public class NacosHttpJsonContextTests
{
    [Fact]
    public void AgentSpec_RoundtripsWithDefaultCasing()
    {
        var options = NacosHttpJsonOptions.Create();
        var spec = new AgentSpec { Name = "n", Description = "d" };

        var json = JsonSerializer.Serialize(spec, options);
        json.Should().Contain("\"Name\":\"n\"");   // 默认策略 = PascalCase,与 2.0.0 一致

        var back = JsonSerializer.Deserialize<AgentSpec>(json, options)!;
        back.Name.Should().Be("n");
    }

    [Fact]
    public void AgentSpec_AiProfile_SerializesCamelCase()
    {
        // The AI services' 2.0.0 JsonOptions were CamelCase + case-insensitive;
        // the factory must reproduce that profile exactly.
        var json = JsonSerializer.Serialize(new AgentSpec { Name = "n" }, NacosHttpJsonOptions.CreateAi());

        json.Should().Contain("\"name\":\"n\"");
    }

    [Fact]
    public void ApiResultPagedData_RoundtripsThroughInternalContext()
    {
        var options = NacosHttpJsonOptions.CreateAi();
        var json = """{"code":200,"data":{"totalCount":1,"pageItems":[{"promptKey":"p"}]}}""";

        var back = JsonSerializer.Deserialize<NacosPromptService.ApiResult<NacosPromptService.PagedData<PromptMetaSummary>>>(json, options)!;

        back.Code.Should().Be(200);
        back.Data!.PageItems![0].PromptKey.Should().Be("p");
    }

    [Fact]
    public void ListInstance_DeserializesCamelCaseCaseInsensitively()
    {
        // NacosNamingService reads `data` elements via JsonElement.Deserialize
        // (Web defaults in 2.0.0: camelCase, case-insensitive).
        var back = JsonSerializer.Deserialize<List<Instance>>(
            """[{"ip":"1.2.3.4","port":8848}]""", NacosHttpJsonOptions.CreateCaseInsensitive())!;

        back[0].Ip.Should().Be("1.2.3.4");
        back[0].Port.Should().Be(8848);
    }

    [Fact]
    public void NamingSelector_SerializesLowercaseTypeExpression()
    {
        var json = JsonSerializer.Serialize(
            new NamingSelector { Type = "label", Expression = "a=1" },
            NacosHttpJsonOptions.Create());

        json.Should().Be("""{"type":"label","expression":"a=1"}""");
    }

    [Fact]
    public void UnregisteredType_ThrowsNotSupported()
    {
        var act = () => JsonSerializer.Serialize(new { x = 1 }, NacosHttpJsonOptions.Create());
        act.Should().Throw<NotSupportedException>();
    }
}
