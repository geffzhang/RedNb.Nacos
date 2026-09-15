# NativeAOT 裁剪兼容 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 RedNb.Nacos SDK(core + Http + Grpc)全部 JSON 序列化改为源生成,使 RedNb.Nacos.All 在 NativeAOT 下可用。

**Architecture:** 每工程一个 public `JsonSerializerContext`(+ 有 internal 类型的工程再配一个 internal 上下文),所有 `JsonSerializerOptions` 收敛到每工程一个内部静态工厂,resolver 指向源生成上下文;纯源生成零回退,未注册类型抛 `NotSupportedException`;`Dictionary<string, object>`/`object` 成员用 AOT 安全手写转换器保持 API 不变。

**Tech Stack:** .NET 8/10、System.Text.Json Source Generation、xunit + FluentAssertions、Grpc.Net.Client、GitHub Actions。

**Spec:** `docs/superpowers/specs/2026-09-15-nativeaot-trimming-compatibility-design.md`

## Global Constraints

- 分支 `nativeaot`,不切分支;每任务结束提交,commit 消息带前缀(feat:/test:/docs:)并以 `Co-Authored-By: Claude Code <noreply@anthropic.com>` 结尾。
- **wire 行为与 2.0.0 逐字节一致**:Grpc 序列化 = CamelCase + `WhenWritingNull` + `PropertyNameCaseInsensitive`;Http 序列化 = 默认选项;core 反序列化 = `PropertyNameCaseInsensitive`(序列化输出不变)。任何输出差异都是 bug。
- **纯源生成零回退**:所有 options 的 `TypeInfoResolver` 只能指向源生成上下文或其 Combine;未注册类型必须抛 `NotSupportedException`。
- 命名锁定(跨任务引用):core 用 `RedNb.Nacos.Serialization` 命名空间(`NacosJsonContext`、`NacosJsonOptions`、转换器);Grpc 用 `RedNb.Nacos.Grpc.Serialization`(`NacosGrpcJsonContext`、`NacosGrpcInternalJsonContext`、`NacosGrpcJsonOptions`);Http 用 `RedNb.Nacos.Http.Serialization`(`NacosHttpJsonContext`、`NacosHttpInternalJsonContext`、`NacosHttpJsonOptions`);DTO `NamingSelector` 在 `RedNb.Nacos.Naming.Models`。
- 测试框架 xunit + FluentAssertions,测试工程多目标 net8.0;net10.0;运行命令 `dotnet test <csproj> -f net10.0 --filter <name>`。
- 不移动任何现有 public 类型、不改命名空间;`object` 成员类型保持不变(用转换器解决)。
- 冒烟工程 `samples/RedNb.Nacos.Sample.Aot` 只 target net10.0,不进 NuGet 打包。

---

### Task 1: core 手写 AOT 安全转换器(ObjectValue / ObjectDictionary / SecurityScheme)

**Files:**
- Create: `src/RedNb.Nacos/Serialization/JsonObjectConverters.cs`
- Modify: `src/RedNb.Nacos/Lock/LockInstance.cs:46`(属性加特性)
- Modify: `src/RedNb.Nacos/Ai/Models/Mcp/EncryptObject.cs:26`
- Modify: `src/RedNb.Nacos/Ai/Models/Skill/SkillResource.cs:27`
- Modify: `src/RedNb.Nacos/Ai/Models/AgentSpec/AgentSpecResource.cs:29`
- Modify: `src/RedNb.Nacos/Ai/Models/AgentSpec/AgentSpecSummary.cs:106`
- Modify: `src/RedNb.Nacos/Ai/Models/A2a/AgentExtension.cs:32`
- Modify: `src/RedNb.Nacos/Ai/Models/A2a/SecurityScheme.cs:6`(类型级特性)
- Test: `tests/RedNb.Nacos.Tests/Serialization/ObjectConvertersTests.cs`

**Interfaces:**
- Consumes: 无(第一任务)。
- Produces(后续任务通过 `[JsonConverter(typeof(...))]` 特性引用,不直接调用):
  - `RedNb.Nacos.Serialization.ObjectValueConverter : JsonConverter<object>`(public、无参构造)
  - `RedNb.Nacos.Serialization.ObjectDictionaryConverter : JsonConverter<Dictionary<string, object>>`(public、无参构造)
  - `RedNb.Nacos.Serialization.SecuritySchemeConverter : JsonConverter<SecurityScheme>`(public、无参构造)
  - 语义:读 = 原值照抄为 `JsonElement`(与反射式 STJ 对 `object` 成员的默认行为一致);写 = 原样输出。**不使用任何反射**。

- [ ] **Step 1: 写失败测试**

创建 `tests/RedNb.Nacos.Tests/Serialization/ObjectConvertersTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Serialization;
using Xunit;

namespace RedNb.Nacos.Tests.Serialization;

public class ObjectConvertersTests
{
    private sealed class Holder
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(ObjectDictionaryConverter))]
        public Dictionary<string, object>? Params { get; set; }

        [System.Text.Json.Serialization.JsonConverter(typeof(ObjectValueConverter))]
        public object? Anything { get; set; }
    }

    [Fact]
    public void ObjectDictionaryConverter_RoundtripsMixedValues()
    {
        var json = """{"params":{"s":"x","i":5,"d":1.5,"b":true,"n":null,"arr":[1,"a"],"obj":{"k":"v"}}}""";

        var holder = JsonSerializer.Deserialize<Holder>(json)!;

        holder.Params!["s"].Should().BeEquivalentTo(JsonDocument.Parse("\"x\"").RootElement);
        holder.Params!["i"].Should().BeEquivalentTo(JsonDocument.Parse("5").RootElement);
        holder.Params!["d"].Should().BeEquivalentTo(JsonDocument.Parse("1.5").RootElement);
        holder.Params!["b"].Should().BeEquivalentTo(JsonDocument.Parse("true").RootElement);
        holder.Params!["n"].Should().Be(JsonDocument.Parse("null").RootElement.GetValueKind());
        holder.Params!["arr"].Should().BeEquivalentTo(JsonDocument.Parse("[1,\"a\"]").RootElement);
        holder.Params!["obj"].Should().BeEquivalentTo(JsonDocument.Parse("{\"k\":\"v\"}").RootElement);

        var back = JsonSerializer.Serialize(holder);
        var reparsed = JsonDocument.Parse(back).RootElement;
        reparsed.GetProperty("params").GetProperty("s").GetString().Should().Be("x");
        reparsed.GetProperty("params").GetProperty("i").GetInt32().Should().Be(5);
        reparsed.GetProperty("params").GetProperty("arr")[1].GetString().Should().Be("a");
    }

    [Fact]
    public void ObjectValueConverter_RoundtripsAnything()
    {
        var holder = JsonSerializer.Deserialize<Holder>("""{"anything":{"deep":[1,2,{"z":null}]}}""")!;
        holder.Anything.Should().BeOfType<JsonElement>();

        var back = JsonDocument.Parse(JsonSerializer.Serialize(holder)).RootElement;
        back.GetProperty("anything").GetProperty("deep")[2].GetProperty("z").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void ObjectDictionaryConverter_WritesUserSetPrimitives()
    {
        var holder = new Holder { Params = new Dictionary<string, object> { ["n"] = 42, ["s"] = "txt", ["f"] = true } };

        var back = JsonDocument.Parse(JsonSerializer.Serialize(holder)).RootElement.GetProperty("params");
        back.GetProperty("n").GetInt32().Should().Be(42);
        back.GetProperty("s").GetString().Should().Be("txt");
        back.GetProperty("f").GetBoolean().Should().Be(true);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net10.0 --filter ObjectConvertersTests`
Expected: FAIL(编译错误:找不到 ObjectDictionaryConverter)

- [ ] **Step 3: 实现转换器**

创建 `src/RedNb.Nacos/Serialization/JsonObjectConverters.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;

namespace RedNb.Nacos.Serialization;

/// <summary>
/// AOT-safe converter for <c>object</c> members. Mirrors reflection-based STJ:
/// unknown JSON values are kept as <see cref="JsonElement"/>; user-set primitives
/// are written as-is. Never uses reflection.
/// </summary>
public sealed class ObjectValueConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        => JsonObjectConverters.WriteValue(writer, value);
}

/// <summary>
/// AOT-safe converter for <c>Dictionary&lt;string, object&gt;</c> members.
/// </summary>
public sealed class ObjectDictionaryConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var result = new Dictionary<string, object>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.Clone();
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value)
        {
            writer.WritePropertyName(pair.Key);
            JsonObjectConverters.WriteValue(writer, pair.Value);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// AOT-safe converter for <see cref="SecurityScheme"/> (a
/// <see cref="Dictionary{TKey,TValue}"/> of arbitrary values).
/// </summary>
public sealed class SecuritySchemeConverter : JsonConverter<SecurityScheme>
{
    public override SecurityScheme Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var values = new Dictionary<string, object>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        return new SecurityScheme(values);
    }

    public override void Write(Utf8JsonWriter writer, SecurityScheme value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value)
        {
            writer.WritePropertyName(pair.Key);
            JsonObjectConverters.WriteValue(writer, pair.Value);
        }

        writer.WriteEndObject();
    }
}

/// <summary>Shared value writer for the AOT-safe object converters.</summary>
internal static class JsonObjectConverters
{
    public static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                writer.WriteNumberValue(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case float or double or decimal:
                writer.WriteNumberValue(Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case Dictionary<string, object> dictionary:
                writer.WriteStartObject();
                foreach (var pair in dictionary)
                {
                    writer.WritePropertyName(pair.Key);
                    WriteValue(writer, pair.Value);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable<object> items:
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new JsonException($"Unsupported value type '{value.GetType().FullName}' in AOT-safe converter.");
        }
    }
}
```

- [ ] **Step 4: 挂特性到受影响属性/类型**

六处属性加 `[JsonConverter(typeof(...))]`,SecurityScheme 加类型级 `[JsonConverter(typeof(SecuritySchemeConverter))]`:

```csharp
// LockInstance.cs:46
[JsonConverter(typeof(ObjectDictionaryConverter))]
public Dictionary<string, object>? Params { get; set; }

// EncryptObject.cs:26 / SkillResource.cs:27 / AgentSpecResource.cs:29
[JsonConverter(typeof(ObjectDictionaryConverter))]
public Dictionary<string, object>? Metadata { get; set; }

// AgentSpecSummary.cs:106
[JsonConverter(typeof(ObjectValueConverter))]
public object? PublishPipelineInfo { get; set; }

// AgentExtension.cs:32
[JsonConverter(typeof(ObjectDictionaryConverter))]
public Dictionary<string, object>? Params { get; set; }

// SecurityScheme.cs:6
[JsonConverter(typeof(SecuritySchemeConverter))]
public class SecurityScheme : Dictionary<string, object>
```

(文件顶部按需补 `using System.Text.Json.Serialization;` 与 `using RedNb.Nacos.Serialization;`。)

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net10.0 --filter ObjectConvertersTests`
Expected: PASS(3/3)

- [ ] **Step 6: 提交**

```bash
git add src/RedNb.Nacos/Serialization tests/RedNb.Nacos.Tests/Serialization src/RedNb.Nacos/Lock/LockInstance.cs src/RedNb.Nacos/Ai/Models
git commit -m "feat: add AOT-safe JSON converters for object-typed members

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 2: core 源生成上下文 NacosJsonContext + NamingSelector + core 调用点接线

**Files:**
- Create: `src/RedNb.Nacos/Serialization/NacosJsonContext.cs`
- Create: `src/RedNb.Nacos/Serialization/NacosJsonOptions.cs`
- Create: `src/RedNb.Nacos/Naming/Models/NamingSelector.cs`
- Modify: `src/RedNb.Nacos/Naming/Cache/InstancesDiffer.cs:36,94,101,108`
- Modify: `src/RedNb.Nacos/Failover/DiskNamingFailoverDataSource.cs:29-33`
- Modify: `src/RedNb.Nacos/Failover/LocalDiskFailoverDataSource.cs:28-32`
- Test: `tests/RedNb.Nacos.Tests/Serialization/NacosJsonContextTests.cs`

**Interfaces:**
- Consumes: Task 1 的转换器(经特性,无代码依赖)。
- Produces(Http/Grpc 上下文声明 `NamingSelector` 时引用):
  - `public sealed partial class NacosJsonContext : JsonSerializerContext`(core 全部类型)
  - `internal static class NacosJsonOptions`:`internal static JsonSerializerOptions Create(bool writeIndented = false)`(缓存实例,`TypeInfoResolver = NacosJsonContext.Default` + `PropertyNameCaseInsensitive = true`)
  - `public sealed class NamingSelector { public string? Type; public string? Expression; }`,属性带 `[JsonPropertyName("type")]`/`[JsonPropertyName("expression")]`

- [ ] **Step 1: 写失败测试**

创建 `tests/RedNb.Nacos.Tests/Serialization/NacosJsonContextTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Config;
using RedNb.Nacos.Naming.Models;
using RedNb.Nacos.Serialization;
using Xunit;

namespace RedNb.Nacos.Tests.Serialization;

public class NacosJsonContextTests
{
    [Fact]
    public void ServiceInfo_RoundtripsThroughContext()
    {
        var options = NacosJsonOptions.Create();
        var original = new ServiceInfo("svc")
        {
            Hosts = new List<Instance> { new() { Ip = "1.2.3.4", Port = 8848 } }
        };

        var json = JsonSerializer.Serialize(original, options);
        var back = JsonSerializer.Deserialize<ServiceInfo>(json, options)!;

        back.Hosts![0].Ip.Should().Be("1.2.3.4");
        back.Hosts![0].Port.Should().Be(8848);
    }

    [Fact]
    public void ConfigInfo_RoundtripsCaseInsensitively()
    {
        var options = NacosJsonOptions.Create();
        var back = JsonSerializer.Deserialize<ConfigInfo>(
            """{"dataid":"d","group":"g","content":"c"}""", options)!;

        back.DataId.Should().Be("d");
        back.Group.Should().Be("g");
        back.Content.Should().Be("c");
    }

    [Fact]
    public void NamingSelector_SerializesAsLowercaseTypeExpression()
    {
        var options = NacosJsonOptions.Create();
        var json = JsonSerializer.Serialize(new NamingSelector { Type = "label", Expression = "a=1" }, options);

        json.Should().Be("""{"type":"label","expression":"a=1"}""");
    }

    [Fact]
    public void UnregisteredType_ThrowsNotSupported()
    {
        var options = NacosJsonOptions.Create();
        var act = () => JsonSerializer.Serialize(new { x = 1 }, options);
        act.Should().Throw<NotSupportedException>();
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net10.0 --filter NacosJsonContextTests`
Expected: FAIL(编译错误:`NacosJsonOptions`、`NamingSelector` 不存在)

- [ ] **Step 3: 创建 NamingSelector**

创建 `src/RedNb.Nacos/Naming/Models/NamingSelector.cs`:

```csharp
using System.Text.Json.Serialization;

namespace RedNb.Nacos.Naming.Models;

/// <summary>
/// Wire shape of the naming selector parameter (<c>{"type": ..., "expression": ...}</c>).
/// Replaces the anonymous types previously serialized by the Http and Grpc naming services.
/// </summary>
public sealed class NamingSelector
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("expression")]
    public string? Expression { get; set; }
}
```

- [ ] **Step 4: 创建 NacosJsonContext 与 NacosJsonOptions**

创建 `src/RedNb.Nacos/Serialization/NacosJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Config;
using RedNb.Nacos.Naming.Models;

namespace RedNb.Nacos.Serialization;

/// <summary>
/// Source-generated serialization metadata for the core library. Property names keep
/// their C# casing on the wire (matching the reflection-based behavior of 2.0.0);
/// deserialization is case-insensitive, as the failover disk cache requires.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ServiceInfo))]
[JsonSerializable(typeof(Instance))]
[JsonSerializable(typeof(List<Instance>))]
[JsonSerializable(typeof(ConfigInfo))]
[JsonSerializable(typeof(NamingSelector))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(McpCapability))]
public sealed partial class NacosJsonContext : JsonSerializerContext
{
}
```

创建 `src/RedNb.Nacos/Serialization/NacosJsonOptions.cs`:

```csharp
using System.Text.Json;

namespace RedNb.Nacos.Serialization;

/// <summary>
/// Single construction point for core-library <see cref="JsonSerializerOptions"/>.
/// The settings must stay in sync with <see cref="NacosJsonContext"/>'s
/// <see cref="System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute"/>.
/// </summary>
internal static class NacosJsonOptions
{
    private static readonly JsonSerializerOptions Plain = CreateCore(false);
    private static readonly JsonSerializerOptions Indented = CreateCore(true);

    public static JsonSerializerOptions Create(bool writeIndented = false) => writeIndented ? Indented : Plain;

    private static JsonSerializerOptions CreateCore(bool writeIndented) => new()
    {
        TypeInfoResolver = NacosJsonContext.Default,
        PropertyNameCaseInsensitive = true,
        WriteIndented = writeIndented
    };
}
```

- [ ] **Step 5: 接线 core 调用点**

先给测试工程开内部可见性 —— `src/RedNb.Nacos/RedNb.Nacos.csproj` 的 `<ItemGroup>` 中已有两行 `InternalsVisibleTo`,追加一行:

```xml
<InternalsVisibleTo Include="RedNb.Nacos.Tests" />
```

(测试要直接使用 internal 的 `NacosJsonOptions` 工厂。)

`InstancesDiffer.cs` 四处裸调用改为上下文 TypeInfo 重载(默认选项行为,序列化输出不变):

```csharp
// 36
JsonSerializer.Serialize(newService.Hosts, NacosJsonContext.Default.ListInstance)
// 94
JsonSerializer.Serialize(newHosts, NacosJsonContext.Default.ListInstance)
// 101
JsonSerializer.Serialize(remvHosts, NacosJsonContext.Default.ListInstance)
// 108
JsonSerializer.Serialize(modHosts, NacosJsonContext.Default.ListInstance)
```

`DiskNamingFailoverDataSource.cs` 与 `LocalDiskFailoverDataSource.cs` 的 `_jsonOptions` 初始化替换为 `NacosJsonOptions.Create(writeIndented: true)`(删除原 `new JsonSerializerOptions {...}`,并补 `using RedNb.Nacos.Serialization;`;若 `System.Text.Json` using 不再需要可移除)。

- [ ] **Step 6: 运行测试确认通过**

Run: `dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net10.0 --filter "NacosJsonContextTests|ObjectConvertersTests"`
Expected: PASS(7/7)

- [ ] **Step 7: 提交**

```bash
git add src/RedNb.Nacos/Serialization src/RedNb.Nacos/Naming/Models/NamingSelector.cs src/RedNb.Nacos/Naming/Cache/InstancesDiffer.cs src/RedNb.Nacos/Failover tests/RedNb.Nacos.Tests/Serialization/NacosJsonContextTests.cs
git commit -m "feat: add NacosJsonContext source-gen metadata for core serialization

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 3: Grpc public 上下文 + 工厂 + 全部 Grpc 调用点接线

**Files:**
- Create: `src/RedNb.Nacos.Grpc/Serialization/NacosGrpcJsonContext.cs`
- Create: `src/RedNb.Nacos.Grpc/Serialization/NacosGrpcJsonOptions.cs`
- Modify: `src/RedNb.Nacos.Grpc/NacosGrpcClient.cs:141-146`(options 换工厂)
- Modify: `src/RedNb.Nacos.Grpc/Config/ConfigRpcTransportClient.cs:35-38`
- Modify: `src/RedNb.Nacos.Grpc/Naming/NamingRpcTransportClient.cs:40-43`
- Modify: `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs:37-41`
- Modify: `src/RedNb.Nacos.Grpc/Naming/NamingServiceInfoHolder.cs:286,314`
- Test: `tests/RedNb.Nacos.Grpc.Tests/Serialization/NacosGrpcJsonContextTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `NamingSelector`。
- Produces(冒烟工程经 InternalsVisibleTo 引用):
  - `public sealed partial class NacosGrpcJsonContext : JsonSerializerContext`
  - `internal static class NacosGrpcJsonOptions`:`internal static JsonSerializerOptions Create()`,resolver = `JsonTypeInfoResolver.Combine(NacosGrpcJsonContext.Default, NacosGrpcInternalJsonContext.Default)`(internal 上下文 Task 4 创建,本任务先不引用)

- [ ] **Step 1: 写失败测试**

创建 `tests/RedNb.Nacos.Grpc.Tests/Serialization/NacosGrpcJsonContextTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;
using RedNb.Nacos.Grpc.Serialization;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Serialization;

public class NacosGrpcJsonContextTests
{
    [Fact]
    public void ConfigPublishRequest_SerializesCamelCaseAndOmitsNulls()
    {
        var options = NacosGrpcJsonOptions.Create();
        var request = new ConfigPublishRequest
        {
            RequestId = "r1",
            DataId = "d",
            Group = "g",
            Content = "c"
        };

        var json = JsonSerializer.Serialize(request, options);

        json.Should().Contain("\"requestId\":\"r1\"").And.Contain("\"dataId\":\"d\"");
        json.Should().NotContain("\"RequestId\"");
        json.Should().NotContain("\"tenant\"");
    }

    [Fact]
    public void ConfigChangeNotifyRequest_RoundtripsCaseInsensitively()
    {
        var options = NacosGrpcJsonOptions.Create();
        var back = JsonSerializer.Deserialize<ConfigChangeNotifyRequest>(
            """{"RequestId":"r","dataId":"d","group":"g"}""", options)!;

        back.RequestId.Should().Be("r");
        back.DataId.Should().Be("d");
    }

    [Fact]
    public void NamingServiceInfo_RoundtripsThroughContext()
    {
        var options = NacosGrpcJsonOptions.Create();
        var original = new NamingServiceInfo
        {
            Name = "n",
            GroupName = "g",
            Hosts = new List<NamingInstance> { new() { Ip = "1.2.3.4", Port = 8848 } }
        };

        var back = JsonSerializer.Deserialize<NamingServiceInfo>(
            JsonSerializer.Serialize(original, options), options)!;

        back.Name.Should().Be("n");
        back.Hosts![0].Ip.Should().Be("1.2.3.4");
    }

    [Fact]
    public void UnregisteredType_ThrowsNotSupported()
    {
        var act = () => JsonSerializer.Serialize(new { x = 1 }, NacosGrpcJsonOptions.Create());
        act.Should().Throw<NotSupportedException>();
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net10.0 --filter NacosGrpcJsonContextTests`
Expected: FAIL(编译错误:`NacosGrpcJsonOptions` 不存在)

- [ ] **Step 3: 创建 Grpc 上下文与工厂**

创建 `src/RedNb.Nacos.Grpc/Serialization/NacosGrpcJsonContext.cs`(声明全部具体载荷类;抽象基类 `ConfigRpcRequest/ConfigRpcResponse/NamingRpcRequest/NamingRpcResponse` 不声明):

```csharp
using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;
using RedNb.Nacos.Naming.Models;

namespace RedNb.Nacos.Grpc.Serialization;

/// <summary>
/// Source-generated serialization metadata for the gRPC wire payloads. The options
/// mirror the reflection-based <c>_jsonOptions</c> of 2.0.0: camelCase property
/// names, nulls omitted, case-insensitive reads.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConfigQueryRequest))]
[JsonSerializable(typeof(ConfigQueryResponse))]
[JsonSerializable(typeof(ConfigPublishRequest))]
[JsonSerializable(typeof(ConfigPublishResponse))]
[JsonSerializable(typeof(ConfigRemoveRequest))]
[JsonSerializable(typeof(ConfigRemoveResponse))]
[JsonSerializable(typeof(ConfigListenContext))]
[JsonSerializable(typeof(ConfigBatchListenRequest))]
[JsonSerializable(typeof(ConfigBatchListenResponse))]
[JsonSerializable(typeof(ConfigChangeNotifyRequest))]
[JsonSerializable(typeof(ConfigChangeNotifyResponse))]
[JsonSerializable(typeof(ConfigFuzzyListenContext))]
[JsonSerializable(typeof(ConfigFuzzyWatchRequest))]
[JsonSerializable(typeof(ConfigFuzzyWatchResponse))]
[JsonSerializable(typeof(ConfigFuzzyWatchChangeNotifyRequest))]
[JsonSerializable(typeof(ConfigFuzzyWatchChangeNotifyResponse))]
[JsonSerializable(typeof(ConnectionSetupRequest))]
[JsonSerializable(typeof(HealthCheckRequest))]
[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(ClientDetectionRequest))]
[JsonSerializable(typeof(ClientDetectionResponse))]
[JsonSerializable(typeof(ServerCheckRequest))]
[JsonSerializable(typeof(ServerCheckResponse))]
[JsonSerializable(typeof(InstanceRequest))]
[JsonSerializable(typeof(InstanceResponse))]
[JsonSerializable(typeof(BatchInstanceRequest))]
[JsonSerializable(typeof(BatchInstanceResponse))]
[JsonSerializable(typeof(ServiceQueryRequest))]
[JsonSerializable(typeof(ServiceQueryResponse))]
[JsonSerializable(typeof(SubscribeServiceRequest))]
[JsonSerializable(typeof(SubscribeServiceResponse))]
[JsonSerializable(typeof(ServiceListRequest))]
[JsonSerializable(typeof(ServiceListResponse))]
[JsonSerializable(typeof(NotifySubscriberRequest))]
[JsonSerializable(typeof(NotifySubscriberResponse))]
[JsonSerializable(typeof(NamingFuzzyWatchRequest))]
[JsonSerializable(typeof(NamingFuzzyWatchResponse))]
[JsonSerializable(typeof(NamingFuzzyWatchChangeItem))]
[JsonSerializable(typeof(NamingFuzzyWatchCancelRequest))]
[JsonSerializable(typeof(NamingFuzzyWatchCancelResponse))]
[JsonSerializable(typeof(NamingFuzzyWatchNotifyRequest))]
[JsonSerializable(typeof(NamingFuzzyWatchNotifyResponse))]
[JsonSerializable(typeof(PersistentInstanceRequest))]
[JsonSerializable(typeof(NamingInstance))]
[JsonSerializable(typeof(NamingServiceInfo))]
[JsonSerializable(typeof(AgentEndpoint))]
[JsonSerializable(typeof(List<AgentEndpoint>))]
[JsonSerializable(typeof(NamingSelector))]
public sealed partial class NacosGrpcJsonContext : JsonSerializerContext
{
}
```

创建 `src/RedNb.Nacos.Grpc/Serialization/NacosGrpcJsonOptions.cs`(本任务先不含 internal 上下文,Task 4 改一行):

```csharp
using System.Text.Json;

namespace RedNb.Nacos.Grpc.Serialization;

/// <summary>
/// Single construction point for gRPC <see cref="JsonSerializerOptions"/>.
/// Settings must stay in sync with <see cref="NacosGrpcJsonContext"/>'s options.
/// </summary>
internal static class NacosGrpcJsonOptions
{
    private static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = NacosGrpcJsonContext.Default
    };

    public static JsonSerializerOptions Create() => Instance;
}
```

- [ ] **Step 4: 接线全部 Grpc 调用点**

- `NacosGrpcClient.cs:141-146`:`_jsonOptions = NacosGrpcJsonOptions.Create();`(删原初始化块,补 `using RedNb.Nacos.Grpc.Serialization;`)
- `ConfigRpcTransportClient.cs:35-38` / `NamingRpcTransportClient.cs:40-43`:同样替换
- `NacosGrpcAiService.cs:37-41`:`JsonOptions = NacosGrpcJsonOptions.Create();`(保持 static readonly 字段名不变)
- `NamingServiceInfoHolder.cs:286`:`JsonSerializer.Deserialize<NamingServiceInfo>(json, NacosGrpcJsonOptions.Create())`;`:314`:`JsonSerializer.Serialize(serviceInfo, NacosGrpcJsonOptions.Create())`(两处已有 try/catch,保留;该文件补 using)

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net10.0 --filter NacosGrpcJsonContextTests`
Expected: PASS(4/4)

- [ ] **Step 6: 提交**

```bash
git add src/RedNb.Nacos.Grpc/Serialization src/RedNb.Nacos.Grpc/NacosGrpcClient.cs src/RedNb.Nacos.Grpc/Config/ConfigRpcTransportClient.cs src/RedNb.Nacos.Grpc/Naming tests/RedNb.Nacos.Grpc.Tests/Serialization
git commit -m "feat: source-generate gRPC payload serialization via NacosGrpcJsonContext

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 4: Grpc internal 上下文 + private wire 类提升 + Recovery 深拷贝 + 命名服务 selector

**Files:**
- Create: `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiWireModels.cs`
- Create: `src/RedNb.Nacos.Grpc/Serialization/NacosGrpcInternalJsonContext.cs`
- Modify: `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs`(删除 14 个 private 嵌套类,改引用提升后的类;`NacosGrpcAiService.Registry.cs` 如含 private wire 类同样处理)
- Modify: `src/RedNb.Nacos.Grpc/Serialization/NacosGrpcJsonOptions.cs`(resolver 加 Combine)
- Modify: `src/RedNb.Nacos.Grpc/Ai/NacosAiClient.Recovery.cs:42`
- Modify: `src/RedNb.Nacos.Grpc/Naming/NacosGrpcNamingService.cs:758-762`
- Test: `tests/RedNb.Nacos.Grpc.Tests/Serialization/NacosGrpcInternalJsonContextTests.cs`

**Interfaces:**
- Consumes: Task 3 的 `NacosGrpcJsonContext`/`NacosGrpcJsonOptions`;Task 2 的 `NamingSelector`。
- Produces: 提升后的 internal 类:`McpServerQueryRequest`、`McpServerQueryResponse`、`McpServerReleaseRequest`、`McpServerReleaseResponse`、`McpEndpointRequest`、`McpServerNotification`、`AgentCardQueryRequest`、`AgentCardQueryResponse`、`AgentCardReleaseRequest`、`AgentEndpointRequest`、`BatchAgentEndpointRequest`、`AgentCardNotification`、`OperationResponse`、`BatchAgentEndpointResponse`(命名空间 `RedNb.Nacos.Grpc.Ai`)。

- [ ] **Step 1: 提升 private wire 类**

创建 `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiWireModels.cs`,把 `NacosGrpcAiService.cs` 中 14 个 private 嵌套类(**原文照抄,只改声明为 internal 顶层类**;`NacosGrpcAiService.Registry.cs` 中如有 private wire 类先 `grep -n "private class"` 一并处理)搬入,并删除原文件中的嵌套定义。文件骨架:

```csharp
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.Mcp;

namespace RedNb.Nacos.Grpc.Ai;

// 原 NacosGrpcAiService 的 private 嵌套 wire 类,提升为 internal 顶层类
// 以便源生成上下文声明(源生成上下文无法引用 private 类型)。
internal class McpServerQueryRequest { ... }   // 内容照抄
internal class McpServerQueryResponse { ... }
internal class McpServerReleaseRequest { ... }
internal class McpServerReleaseResponse { ... }
internal class McpEndpointRequest { ... }
internal class McpServerNotification { ... }
internal class AgentCardQueryRequest { ... }
internal class AgentCardQueryResponse { ... }
internal class AgentCardReleaseRequest { ... }
internal class AgentEndpointRequest { ... }
internal class BatchAgentEndpointRequest { ... }
internal class BatchAgentEndpointResponse { ... }
internal class AgentCardNotification { ... }
internal class OperationResponse { ... }
```

- [ ] **Step 2: 构建确认无引用断裂**

Run: `dotnet build src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj -c Release`
Expected: 0 errors

- [ ] **Step 3: 创建 internal 上下文并改工厂**

创建 `src/RedNb.Nacos.Grpc/Serialization/NacosGrpcInternalJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;
using RedNb.Nacos.Grpc.Ai;

namespace RedNb.Nacos.Grpc.Serialization;

/// <summary>
/// Source-generated metadata for gRPC AI wire models that stay internal to the SDK.
/// Same options as <see cref="NacosGrpcJsonContext"/>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(McpServerQueryRequest))]
[JsonSerializable(typeof(McpServerQueryResponse))]
[JsonSerializable(typeof(McpServerReleaseRequest))]
[JsonSerializable(typeof(McpServerReleaseResponse))]
[JsonSerializable(typeof(McpEndpointRequest))]
[JsonSerializable(typeof(McpServerNotification))]
[JsonSerializable(typeof(AgentCardQueryRequest))]
[JsonSerializable(typeof(AgentCardQueryResponse))]
[JsonSerializable(typeof(AgentCardReleaseRequest))]
[JsonSerializable(typeof(AgentEndpointRequest))]
[JsonSerializable(typeof(BatchAgentEndpointRequest))]
[JsonSerializable(typeof(BatchAgentEndpointResponse))]
[JsonSerializable(typeof(AgentCardNotification))]
[JsonSerializable(typeof(OperationResponse))]
internal sealed partial class NacosGrpcInternalJsonContext : JsonSerializerContext
{
}
```

`NacosGrpcJsonOptions.cs` 的 `Instance` 初始化改为:

```csharp
TypeInfoResolver = JsonTypeInfoResolver.Combine(
    NacosGrpcJsonContext.Default,
    NacosGrpcInternalJsonContext.Default)
```

(其余设置不变;`JsonTypeInfoResolver.Combine` 在 `System.Text.Json.Serialization` 命名空间。)

- [ ] **Step 4: 写失败测试**

创建 `tests/RedNb.Nacos.Grpc.Tests/Serialization/NacosGrpcInternalJsonContextTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Grpc.Ai;
using RedNb.Nacos.Grpc.Serialization;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Serialization;

public class NacosGrpcInternalJsonContextTests
{
    [Fact]
    public void McpServerNotification_RoundtripsThroughCombinedResolver()
    {
        var options = NacosGrpcJsonOptions.Create();
        var notification = new McpServerNotification { McpName = "m", Version = "1" };

        var json = JsonSerializer.Serialize(notification, options);
        var back = JsonSerializer.Deserialize<McpServerNotification>(json, options)!;

        back.McpName.Should().Be("m");
        back.Version.Should().Be("1");
    }

    [Fact]
    public void AgentCardReleaseRequest_SerializesCamelCase()
    {
        var options = NacosGrpcJsonOptions.Create();
        var request = new AgentCardReleaseRequest
        {
            AgentName = "a",
            AgentCard = new AgentCard { Url = "http://a" }
        };

        var json = JsonSerializer.Serialize(request, options);
        json.Should().Contain("\"agentName\":\"a\"").And.Contain("\"agentCard\"");
    }

    [Fact]
    public void AgentEndpoint_RoundtripsDeepCopyShape()
    {
        var options = NacosGrpcJsonOptions.Create();
        var endpoints = new List<AgentEndpoint>
        {
            new() { Address = "1.2.3.4", Port = 8848, Version = "v" }
        };

        var json = JsonSerializer.Serialize(endpoints, options);
        var copy = JsonSerializer.Deserialize<List<AgentEndpoint>>(json, options)!;

        copy.Should().NotBeSameAs(endpoints);
        copy[0].Address.Should().Be("1.2.3.4");
        copy[0].Port.Should().Be(8848);
    }
}
```

- [ ] **Step 5: 运行测试确认失败**

Run: `dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net10.0 --filter NacosGrpcInternalJsonContextTests`
Expected: FAIL(`NacosGrpcJsonOptions` 未接 internal 上下文前,通知类序列化抛 NotSupportedException —— 即 Step 3 未做时的状态;若已做 Step 3,本步预期为 PASS 也不影响,继续)

- [ ] **Step 6: 接线 Recovery 深拷贝与命名服务 selector**

`NacosAiClient.Recovery.cs:42` 替换为:

```csharp
var options = NacosGrpcJsonOptions.Create();
var copy = JsonSerializer.Deserialize<List<AgentEndpoint>>(JsonSerializer.Serialize(endpoints, options), options)!;
```

(深拷贝的 JSON 不上线,输出格式变化无影响。)

`NacosGrpcNamingService.cs:758-762` 替换为:

```csharp
selectorJson = JsonSerializer.Serialize(
    new NamingSelector { Type = selector.Type, Expression = selector.Expression },
    NacosGrpcJsonOptions.Create());
```

(补 using `RedNb.Nacos.Naming.Models;` 与 `RedNb.Nacos.Grpc.Serialization;`,删除多余的全限定名。)

- [ ] **Step 7: 运行测试确认通过**

Run: `dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net10.0 --filter "NacosGrpcInternalJsonContextTests|NacosGrpcJsonContextTests"`
Expected: PASS(7/7)

- [ ] **Step 8: 提交**

```bash
git add src/RedNb.Nacos.Grpc/Ai src/RedNb.Nacos.Grpc/Serialization src/RedNb.Nacos.Grpc/Naming/NacosGrpcNamingService.cs tests/RedNb.Nacos.Grpc.Tests/Serialization/NacosGrpcInternalJsonContextTests.cs
git commit -m "feat: cover internal gRPC AI wire models with source-gen metadata

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 5: Http 上下文(public + internal)+ 工厂 + AI 服务/Naming/SecurityProxy 接线

**Files:**
- Create: `src/RedNb.Nacos.Http/Serialization/NacosHttpJsonContext.cs`
- Create: `src/RedNb.Nacos.Http/Serialization/NacosHttpInternalJsonContext.cs`
- Create: `src/RedNb.Nacos.Http/Serialization/NacosHttpJsonOptions.cs`
- Create: `src/RedNb.Nacos.Http/Transport/LoginResponse.cs`
- Modify: `src/RedNb.Nacos.Http/Transport/SecurityProxy.cs:179-192`(删嵌套类)、`:140`(接工厂)
- Modify: `src/RedNb.Nacos.Http/Ai/NacosPromptService.cs:46`
- Modify: `src/RedNb.Nacos.Http/Ai/NacosSkillService.cs:46`
- Modify: `src/RedNb.Nacos.Http/Ai/NacosAgentSpecService.cs:47`
- Modify: `src/RedNb.Nacos.Http/Ai/NacosAiService.cs:51`
- Modify: `src/RedNb.Nacos.Http/Naming/ServiceInfoHolder.cs:159,182`
- Modify: `src/RedNb.Nacos.Http/Naming/NacosNamingService.cs:650-654,964`
- Test: `tests/RedNb.Nacos.Http.Tests/Serialization/NacosHttpJsonContextTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `NamingSelector`、`ServiceInfo`(core 类型,声明进 Http 上下文)。
- Produces:
  - `public sealed partial class NacosHttpJsonContext : JsonSerializerContext`(默认选项)
  - `internal sealed partial class NacosHttpInternalJsonContext : JsonSerializerContext`(`ApiResult<>`/`PagedData<>` 封闭实例 + `LoginResponse`)
  - `internal static class NacosHttpJsonOptions`:`internal static JsonSerializerOptions Create()`
  - `internal sealed class LoginResponse`(`RedNb.Nacos.Http.Transport` 命名空间)

- [ ] **Step 1: 创建 LoginResponse 提升文件**

创建 `src/RedNb.Nacos.Http/Transport/LoginResponse.cs`(内容照抄 SecurityProxy.cs:179-192,声明改 internal 顶层):

```csharp
using System.Text.Json.Serialization;

namespace RedNb.Nacos.Http.Transport;

internal sealed class LoginResponse
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("tokenTtl")]
    public long TokenTtl { get; set; }

    [JsonPropertyName("globalAdmin")]
    public bool GlobalAdmin { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}
```

删除 `SecurityProxy.cs` 中的嵌套定义。

- [ ] **Step 2: 创建 Http 上下文与工厂**

创建 `src/RedNb.Nacos.Http/Serialization/NacosHttpJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Ai.Models.Skill;
using RedNb.Nacos.Naming.Models;

namespace RedNb.Nacos.Http.Serialization;

/// <summary>
/// Source-generated metadata for HTTP service models. Default options, matching the
/// reflection-based <c>JsonOptions = new()</c> of 2.0.0 (PascalCase output,
/// case-sensitive reads).
/// </summary>
[JsonSerializable(typeof(AgentSpec))]
[JsonSerializable(typeof(AgentSpecMeta))]
[JsonSerializable(typeof(AgentSpecSummary))]
[JsonSerializable(typeof(AgentCard))]
[JsonSerializable(typeof(AgentCardBasicInfo))]
[JsonSerializable(typeof(AgentCardDetailInfo))]
[JsonSerializable(typeof(AgentVersionInfo))]
[JsonSerializable(typeof(List<AgentVersionInfo>))]
[JsonSerializable(typeof(McpServerBasicInfo))]
[JsonSerializable(typeof(McpServerDetailInfo))]
[JsonSerializable(typeof(McpServerImportValidationResult))]
[JsonSerializable(typeof(McpServerImportResponse))]
[JsonSerializable(typeof(McpServerRemoteServiceConfig))]
[JsonSerializable(typeof(Prompt))]
[JsonSerializable(typeof(PromptMetaSummary))]
[JsonSerializable(typeof(PromptMetaInfo))]
[JsonSerializable(typeof(PromptVersionSummary))]
[JsonSerializable(typeof(PromptVersionInfo))]
[JsonSerializable(typeof(List<PromptVersionSummary>))]
[JsonSerializable(typeof(Skill))]
[JsonSerializable(typeof(SkillMeta))]
[JsonSerializable(typeof(SkillSummary))]
[JsonSerializable(typeof(ServiceInfo))]
[JsonSerializable(typeof(NamingSelector))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public sealed partial class NacosHttpJsonContext : JsonSerializerContext
{
}
```

创建 `src/RedNb.Nacos.Http/Serialization/NacosHttpInternalJsonContext.cs`(internal 泛型包装 `ApiResult<T>`/`PagedData<T>` 的全部封闭实例):

```csharp
using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Ai.Models.Skill;
using RedNb.Nacos.Http.Ai;
using RedNb.Nacos.Http.Transport;

namespace RedNb.Nacos.Http.Serialization;

/// <summary>
/// Source-generated metadata for the SDK-internal API envelope types
/// (<see cref="NacosPromptService.ApiResult{T}"/>/<c>PagedData&lt;T&gt;</c>) and
/// the login response.
/// </summary>
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<AgentSpecSummary>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<AgentSpecMeta>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<AgentSpec>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<McpServerDetailInfo>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<string>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<McpServerBasicInfo>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<McpServerImportValidationResult>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<McpServerImportResponse>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<AgentCardDetailInfo>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<AgentCardBasicInfo>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<List<AgentVersionInfo>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<PromptMetaSummary>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<PromptMetaInfo>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<List<PromptVersionSummary>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<PromptVersionInfo>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<Prompt>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<NacosPromptService.PagedData<SkillSummary>>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<SkillMeta>))]
[JsonSerializable(typeof(NacosPromptService.ApiResult<Skill>))]
[JsonSerializable(typeof(LoginResponse))]
internal sealed partial class NacosHttpInternalJsonContext : JsonSerializerContext
{
}
```

创建 `src/RedNb.Nacos.Http/Serialization/NacosHttpJsonOptions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedNb.Nacos.Http.Serialization;

/// <summary>
/// Single construction point for HTTP <see cref="JsonSerializerOptions"/>.
/// Default settings (no naming policy), matching 2.0.0's <c>new()</c> options.
/// </summary>
internal static class NacosHttpJsonOptions
{
    private static readonly JsonSerializerOptions Instance = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            NacosHttpJsonContext.Default,
            NacosHttpInternalJsonContext.Default)
    };

    public static JsonSerializerOptions Create() => Instance;
}
```

- [ ] **Step 3: 接线 Http 调用点**

先给测试工程开内部可见性 —— `src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj` 的 `<ItemGroup>` 加:

```xml
<InternalsVisibleTo Include="RedNb.Nacos.Http.Tests" />
```

- `NacosPromptService.cs:46` / `NacosSkillService.cs:46` / `NacosAgentSpecService.cs:47` / `NacosAiService.cs:51`:`private static readonly JsonSerializerOptions JsonOptions = NacosHttpJsonOptions.Create();`(补 using;其余调用点不动)
- `SecurityProxy.cs:140`:`JsonSerializer.Deserialize<LoginResponse>(responseBody, NacosHttpJsonOptions.Create())`(补 using)
- `ServiceInfoHolder.cs:159` / `:182`:加 `, NacosHttpJsonOptions.Create()` 到裸调用(补 using)
- `NacosNamingService.cs:650-654`:

```csharp
parameters["selector"] = JsonSerializer.Serialize(
    new NamingSelector { Type = selector.Type, Expression = selector.Expression },
    NacosHttpJsonOptions.Create());
```

- `NacosNamingService.cs:964`:`JsonSerializer.Serialize(instance.Metadata, NacosHttpJsonOptions.Create())`

- [ ] **Step 4: 写测试并运行**

创建 `tests/RedNb.Nacos.Http.Tests/Serialization/NacosHttpJsonContextTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Ai.Models.AgentSpec;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Http.Ai;
using RedNb.Nacos.Http.Serialization;
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
    public void ApiResultPagedData_RoundtripsThroughInternalContext()
    {
        var options = NacosHttpJsonOptions.Create();
        var json = """{"Code":200,"Data":{"TotalCount":1,"PageItems":[{"PromptKey":"p"}]}}""";

        var back = JsonSerializer.Deserialize<NacosPromptService.ApiResult<NacosPromptService.PagedData<PromptMetaSummary>>>(json, options)!;

        back.Code.Should().Be(200);
        back.Data!.PageItems![0].PromptKey.Should().Be("p");
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
```

Run: `dotnet test tests/RedNb.Nacos.Http.Tests/RedNb.Nacos.Http.Tests.csproj -f net10.0 --filter NacosHttpJsonContextTests`
Expected: PASS(4/4)

- [ ] **Step 5: 回归验证(此任务验收核心)**

Run: `dotnet test tests/RedNb.Nacos.Http.Tests/RedNb.Nacos.Http.Tests.csproj -f net10.0`
Expected: 全绿。若有测试因未注册类型抛 `NotSupportedException`,说明某调用点用了清单外类型 —— 找到该类型,补进对应上下文(public 类型进 `NacosHttpJsonContext`,internal 类型进 `NacosHttpInternalJsonContext`),重跑。

- [ ] **Step 6: 提交**

```bash
git add src/RedNb.Nacos.Http tests/RedNb.Nacos.Http.Tests/Serialization
git commit -m "feat: source-generate HTTP AI/naming serialization metadata

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 6: csproj 裁剪兼容标记(IsAotCompatible / IsTrimmable)

**Files:**
- Modify: `src/RedNb.Nacos/RedNb.Nacos.csproj`
- Modify: `src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj`
- Modify: `src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj`
- Modify: `src/RedNb.Nacos.DependencyInjection/RedNb.Nacos.DependencyInjection.csproj`
- Modify: `src/RedNb.Nacos.AspNetCore/RedNb.Nacos.AspNetCore.csproj`

**Interfaces:**
- Consumes: Task 1-5 的代码。
- Produces: 无代码接口;5 个包在 NuGet 元数据上声明 AOT/裁剪兼容。

- [ ] **Step 1: 加属性**

五个 csproj 的 `<PropertyGroup>` 内各加两行:

```xml
<IsAotCompatible>true</IsAotCompatible>
<IsTrimmable>true</IsTrimmable>
```

(`RedNb.Nacos.All.csproj` 是 meta 包,不加。)

- [ ] **Step 2: 全解决方案 Release 构建,确认零裁剪警告**

Run: `dotnet build RedNb.Nacos.sln -c Release`
Expected: 0 errors、0 warnings(尤其 IL2xxx/IL3xxx 裁剪分析器警告)。若有警告:定位到具体调用点 —— 若该调用实际安全,加 `[UnconditionalSuppressMessage]` 并附注释说明;若不安全,改代码(不得压制而不处理)。

- [ ] **Step 3: 提交**

```bash
git add src/RedNb.Nacos/RedNb.Nacos.csproj src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj src/RedNb.Nacos.DependencyInjection/RedNb.Nacos.DependencyInjection.csproj src/RedNb.Nacos.AspNetCore/RedNb.Nacos.AspNetCore.csproj
git commit -m "feat: mark SDK packages AOT and trimming compatible

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 7: NativeAOT 冒烟工程 + 本地脚本

**Files:**
- Create: `samples/RedNb.Nacos.Sample.Aot/RedNb.Nacos.Sample.Aot.csproj`
- Create: `samples/RedNb.Nacos.Sample.Aot/Program.cs`
- Create: `scripts/run-aot-smoke.ps1`
- Modify: `src/RedNb.Nacos/RedNb.Nacos.csproj`、`src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj`、`src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj`(加 InternalsVisibleTo 让冒烟工程能引用内部工厂)

**Interfaces:**
- Consumes: Task 1-6 全部产物(经 `InternalsVisibleTo` 访问 `NacosJsonOptions`/`NacosHttpJsonOptions`/`NacosGrpcJsonOptions` 工厂)。
- Produces: `scripts/run-aot-smoke.ps1`(CI 与本地复用)。

- [ ] **Step 1: 加 InternalsVisibleTo**

三个 src csproj 的 `<ItemGroup>` 内各加:

```xml
<InternalsVisibleTo Include="RedNb.Nacos.Sample.Aot" />
```

- [ ] **Step 2: 创建冒烟工程**

创建 `samples/RedNb.Nacos.Sample.Aot/RedNb.Nacos.Sample.Aot.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <IsPackable>false</IsPackable>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\RedNb.Nacos.Http\RedNb.Nacos.Http.csproj" />
    <ProjectReference Include="..\..\src\RedNb.Nacos.Grpc\RedNb.Nacos.Grpc.csproj" />
  </ItemGroup>
</Project>
```

创建 `samples/RedNb.Nacos.Sample.Aot/Program.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Config;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;
using RedNb.Nacos.Grpc.Serialization;
using RedNb.Nacos.Http.Ai;
using RedNb.Nacos.Http.Serialization;
using RedNb.Nacos.Naming.Models;
using RedNb.Nacos.Serialization;

// NativeAOT smoke: every check runs against the SDK's real serializer factories.
// Any NotSupportedException here means a payload type was trimmed or unregistered.
var failures = 0;

void Check(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}

// 1. core: failover + naming roundtrips
Check("core ServiceInfo roundtrip", () =>
{
    var options = NacosJsonOptions.Create(writeIndented: true);
    var info = new ServiceInfo("svc")
    {
        Hosts = new List<Instance> { new() { Ip = "1.2.3.4", Port = 8848 } }
    };
    var back = JsonSerializer.Deserialize<ServiceInfo>(JsonSerializer.Serialize(info, options), options);
    if (back!.Hosts![0].Ip != "1.2.3.4") throw new Exception("roundtrip mismatch");
});

Check("core ConfigInfo roundtrip", () =>
{
    var options = NacosJsonOptions.Create();
    var back = JsonSerializer.Deserialize<ConfigInfo>("""{"dataid":"d","group":"g","content":"c"}""", options);
    if (back!.DataId != "d") throw new Exception("roundtrip mismatch");
});

// 2. gRPC: camelCase wire shape + payload roundtrips
Check("grpc ConfigPublishRequest camelCase wire", () =>
{
    var options = NacosGrpcJsonOptions.Create();
    var json = JsonSerializer.Serialize(new ConfigPublishRequest
    {
        RequestId = "r", DataId = "d", Group = "g", Content = "c"
    }, options);
    if (!json.Contains("\"requestId\":\"r\"") || !json.Contains("\"dataId\":\"d\""))
        throw new Exception($"unexpected wire shape: {json}");
});

Check("grpc NamingServiceInfo roundtrip", () =>
{
    var options = NacosGrpcJsonOptions.Create();
    var info = new NamingServiceInfo { Name = "n", GroupName = "g" };
    var back = JsonSerializer.Deserialize<NamingServiceInfo>(JsonSerializer.Serialize(info, options), options);
    if (back!.Name != "n") throw new Exception("roundtrip mismatch");
});

// 3. Http: AI envelope roundtrip(经 internal 上下文)
Check("http ApiResult<PagedData<PromptMetaSummary>> roundtrip", () =>
{
    var options = NacosHttpJsonOptions.Create();
    var json = """{"Code":200,"Data":{"TotalCount":1,"PageItems":[{"PromptKey":"p"}]}}""";
    var back = JsonSerializer.Deserialize<NacosPromptService.ApiResult<NacosPromptService.PagedData<PromptMetaSummary>>>(json, options);
    if (back!.Code != 200 || back.Data!.PageItems![0].PromptKey != "p") throw new Exception("roundtrip mismatch");
});

Check("http McpServerBasicInfo with McpCapability token form", () =>
{
    var options = NacosHttpJsonOptions.Create();
    var server = JsonSerializer.Deserialize<McpServerBasicInfo>("""{"name":"s","capabilities":["TOOL"]}""", options);
    if (server!.Capabilities == null || server.Capabilities[0].Name != "TOOL") throw new Exception("capability mismatch");
});

// 4. 严格模式负例:未注册类型必须抛
Check("unregistered type throws", () =>
{
    try { JsonSerializer.Serialize(new { x = 1 }, NacosGrpcJsonOptions.Create()); }
    catch (NotSupportedException) { return; }
    throw new Exception("expected NotSupportedException");
});

// 5. public 上下文 + 用户类型 Combine(扩展性用例)
Check("user context combines with SDK context", () =>
{
    var userOptions = new JsonSerializerOptions
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            NacosGrpcJsonContext.Default, UserContext.Default)
    };
    var json = JsonSerializer.Serialize(new UserPayload { Id = 7 }, userOptions);
    if (!json.Contains("\"id\":7")) throw new Exception($"unexpected: {json}");
});

Console.WriteLine(failures == 0 ? "AOT SMOKE OK" : $"AOT SMOKE FAILED ({failures})");
return failures == 0 ? 0 : 1;

[JsonSerializable(typeof(UserPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class UserContext : JsonSerializerContext
{
}

internal class UserPayload
{
    public int Id { get; set; }
}
```

- [ ] **Step 3: 本地脚本**

创建 `scripts/run-aot-smoke.ps1`:

```powershell
# NativeAOT smoke: publish the AOT sample and run its self-checks.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

dotnet publish "$root/samples/RedNb.Nacos.Sample.Aot/RedNb.Nacos.Sample.Aot.csproj" -c Release -r win-x64 --self-contained

& "$root/samples/RedNb.Nacos.Sample.Aot/bin/Release/net10.0/win-x64/publish/RedNb.Nacos.Sample.Aot.exe"
exit $LASTEXITCODE
```

- [ ] **Step 4: 本地发布 + 运行验证**

Run: `powershell -File scripts/run-aot-smoke.ps1`
Expected: 输出 `AOT SMOKE OK`,exit 0。若 AOT 发布报 linker/序列化错误或运行时抛 `NotSupportedException`:按错误定位漏声明类型,补进对应上下文,重跑。(首次 AOT 发布会下载 native 编译工具链,耗时数分钟属正常。)

- [ ] **Step 5: 提交**

```bash
git add samples/RedNb.Nacos.Sample.Aot scripts/run-aot-smoke.ps1 src/RedNb.Nacos/RedNb.Nacos.csproj src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj
git commit -m "feat: add NativeAOT publish smoke sample and local runner

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 8: CI 加 NativeAOT 冒烟 job

**Files:**
- Modify: `.github/workflows/ci.yml`(新增 `aot-smoke` job)

**Interfaces:**
- Consumes: Task 7 的冒烟工程。
- Produces: 无。

- [ ] **Step 1: 加 job**

在 `ci.yml` 的 `jobs:` 下、`test` job 之后追加:

```yaml
  aot-smoke:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            10.0.x
      - name: Install AOT toolchain
        run: sudo apt-get update && sudo apt-get install -y clang zlib1g-dev
      - name: Publish NativeAOT smoke
        run: dotnet publish samples/RedNb.Nacos.Sample.Aot/RedNb.Nacos.Sample.Aot.csproj -c Release -r linux-x64 --self-contained
      - name: Run AOT smoke
        run: samples/RedNb.Nacos.Sample.Aot/bin/Release/net10.0/linux-x64/publish/RedNb.Nacos.Sample.Aot
```

- [ ] **Step 2: 验证 YAML 语法与 job 引用路径**

Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('yaml ok')"`
Expected: `yaml ok`

- [ ] **Step 3: 提交**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: publish and run NativeAOT smoke in validation pipeline

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 9: 迁移指南 + spec 状态收尾

**Files:**
- Modify: `docs/MIGRATION.md`(追加 2.1.0 NativeAOT 章节)

**Interfaces:**
- Consumes: 全部任务产物。
- Produces: 无。

- [ ] **Step 1: 追加迁移说明**

在 `docs/MIGRATION.md` 末尾追加:

```markdown
## 2.1.0:NativeAOT 裁剪兼容

- **序列化改为源生成**:gRPC 载荷、HTTP 模型、core 缓存序列化全部走
  `JsonSerializerContext` 源生成,零反射回退,JIT 与 NativeAOT 行为一致。
  未注册进 SDK 上下文的类型会抛 `NotSupportedException`(含自定义类型直传
  `NacosGrpcClient.SendRequestAsync` 的场景);如需序列化自有类型,用
  `JsonTypeInfoResolver.Combine(NacosGrpcJsonContext.Default, 你的上下文)`
  自行构建 options。
- **扩展点**:三个 public 上下文 —— `NacosJsonContext`(core)、
  `NacosHttpJsonContext`(Http)、`NacosGrpcJsonContext`(Grpc)。
- **Grpc 本地磁盘缓存格式变更**:`NamingServiceInfoHolder` 的缓存文件改用
  camelCase 且省略 null 字段,旧缓存文件会被忽略并自动重建,无需手工处理。
- **API 不变**:`Dictionary<string, object>` / `object` 成员类型保持原样
  (内部用 AOT 安全转换器,值仍以 `JsonElement` 呈现,与 2.0.0 一致)。
- **验证**:`scripts/run-aot-smoke.ps1`(本地)+ CI `aot-smoke` job。
```

- [ ] **Step 2: 全量回归**

Run: `dotnet test RedNb.Nacos.sln -c Release -m:1 -p:TestTfmsInParallel=false`
Expected: 全绿(集成测试依赖本地 Nacos 3.2.4 容器;无容器环境时仅跑单测工程:
`dotnet test tests/RedNb.Nacos.Tests -c Release` 等四个测试工程分别跑)

- [ ] **Step 3: 提交**

```bash
git add docs/MIGRATION.md
git commit -m "docs: document NativeAOT compatibility in migration guide

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

## 完成定义

1. `dotnet build RedNb.Nacos.sln -c Release` 零警告(含裁剪分析器)。
2. 全部单测/集成测试绿;新增测试覆盖 Task 1/2/3/4/5 全部条目。
3. `scripts/run-aot-smoke.ps1` 输出 `AOT SMOKE OK`。
4. CI `aot-smoke` job 通过(合并/PR 到 master 时验证)。
5. `docs/MIGRATION.md` 更新完毕;Grpc/Http wire 行为与 2.0.0 一致(现有集成测试佐证)。
