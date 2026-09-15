using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Config;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;
using RedNb.Nacos.Grpc.Serialization;
using RedNb.Nacos.Http.Ai;
using RedNb.Nacos.Http.Serialization;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Serialization;

// NativeAOT smoke: every check runs against the SDK's real serializer factories.
// A NotSupportedException here means a payload type was trimmed or unregistered.
var failures = 0;

void Check(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}

// AOT-safe entry into the options path: runtime resolution through the
// source-generated resolver chain, then the JsonTypeInfo-based overloads.
JsonTypeInfo<T> Info<T>(JsonSerializerOptions options) =>
    (JsonTypeInfo<T>)(options.GetTypeInfo(typeof(T))
        ?? throw new NotSupportedException($"no JSON metadata for {typeof(T).FullName}"));

// 1. core: failover + naming roundtrips
Check("core ServiceInfo roundtrip", () =>
{
    var options = NacosJsonOptions.Create(writeIndented: true);
    var info = new ServiceInfo("svc")
    {
        Hosts = new List<Instance> { new() { Ip = "1.2.3.4", Port = 8848 } }
    };
    var infoType = Info<ServiceInfo>(options);
    var back = JsonSerializer.Deserialize(JsonSerializer.Serialize(info, infoType), infoType);
    if (back!.Hosts![0].Ip != "1.2.3.4") throw new Exception("roundtrip mismatch");
});

Check("core ConfigInfo roundtrip", () =>
{
    var options = NacosJsonOptions.Create();
    var configType = Info<ConfigInfo>(options);
    var back = JsonSerializer.Deserialize("""{"dataid":"d","group":"g","content":"c"}""", configType);
    if (back!.DataId != "d") throw new Exception("roundtrip mismatch");
});

// 2. gRPC: camelCase wire shape + payload roundtrips
Check("grpc ConfigPublishRequest camelCase wire", () =>
{
    var options = NacosGrpcJsonOptions.Create();
    var json = JsonSerializer.Serialize(new ConfigPublishRequest
    {
        RequestId = "r", DataId = "d", Group = "g", Content = "c"
    }, Info<ConfigPublishRequest>(options));
    if (!json.Contains("\"requestId\":\"r\"") || !json.Contains("\"dataId\":\"d\""))
        throw new Exception($"unexpected wire shape: {json}");
});

Check("grpc NamingServiceInfo roundtrip", () =>
{
    var options = NacosGrpcJsonOptions.Create();
    var info = new NamingServiceInfo { Name = "n", GroupName = "g" };
    var infoType = Info<NamingServiceInfo>(options);
    var back = JsonSerializer.Deserialize(JsonSerializer.Serialize(info, infoType), infoType);
    if (back!.Name != "n") throw new Exception("roundtrip mismatch");
});

// 3. Http: AI envelope roundtrip(经 internal 上下文)
Check("http ApiResult<PagedData<PromptMetaSummary>> roundtrip", () =>
{
    var options = NacosHttpJsonOptions.Create();
    var envelopeType = Info<NacosPromptService.ApiResult<NacosPromptService.PagedData<PromptMetaSummary>>>(options);
    var back = JsonSerializer.Deserialize("""{"Code":200,"Data":{"TotalCount":1,"PageItems":[{"PromptKey":"p"}]}}""", envelopeType);
    if (back!.Code != 200 || back.Data!.PageItems![0].PromptKey != "p") throw new Exception("roundtrip mismatch");
});

Check("http McpServerBasicInfo with McpCapability token form", () =>
{
    var options = NacosHttpJsonOptions.Create();
    var server = JsonSerializer.Deserialize("""{"name":"s","capabilities":["TOOL"]}""", Info<McpServerBasicInfo>(options));
    if (server!.Capabilities == null || server.Capabilities[0].Name != "TOOL") throw new Exception("capability mismatch");
});

// 4. 严格模式负例:未注册类型解析元数据必须失败(序列化时抛 NotSupportedException)
Check("unregistered type yields no metadata", () =>
{
    try { NacosGrpcJsonOptions.Create().GetTypeInfo(typeof(UnregisteredPayload)); }
    catch (NotSupportedException) { return; } // strict resolver: metadata refused
    throw new Exception("unregistered type unexpectedly resolved");
});

// 5. public 上下文 + 用户类型 Combine(扩展性用例)
Check("user context combines with SDK context", () =>
{
    var userOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            NacosGrpcJsonContext.Default, UserContext.Default)
    };
    var json = JsonSerializer.Serialize(new UserPayload { Id = 7 }, Info<UserPayload>(userOptions));
    if (!json.Contains("\"id\":7")) throw new Exception($"unexpected: {json}");
});

Console.WriteLine(failures == 0 ? "AOT SMOKE OK" : $"AOT SMOKE FAILED ({failures})");
return failures == 0 ? 0 : 1;

internal class UnregisteredPayload
{
    public int X { get; set; }
}

[JsonSerializable(typeof(UserPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class UserContext : JsonSerializerContext
{
}

internal class UserPayload
{
    public int Id { get; set; }
}
