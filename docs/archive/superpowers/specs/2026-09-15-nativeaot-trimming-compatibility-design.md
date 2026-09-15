# NativeAOT 裁剪兼容设计

日期:2026-09-15
版本:RedNb.Nacos.All 2.x(下一个发布版本)
分支:nativeaot

## 背景与问题

RedNb.Nacos.All 2.0.0 的 gRPC 载荷(以及 HTTP 侧、core 侧)使用反射式 System.Text.Json 序列化。
JIT 构建下一切正常;NativeAOT(裁剪)发布下载荷类型被 linker 裁剪掉,反射序列化在运行时抛
`NotSupportedException`,SDK 不可用。

本设计将 SDK 全部序列化改为**源生成(Source Generation)**,使 core、Http、Grpc 三个库
在 NativeAOT 下可用,并保持线上 wire 行为与 2.0.0 逐字节一致。

## 目标与非目标

### 目标
- gRPC 载荷、HTTP 模型、core 的 Failover/缓存序列化全部走源生成,零反射回退。
- RedNb.Nacos.All 全组件 NativeAOT 可用,有 AOT 发布冒烟验证(本地 + CI)。
- 2.x 线 API 兼容:不移动类型、不改命名空间、object 成员保持原有公开类型。
- 线上 wire 行为不变:Grpc 保持 CamelCase + 忽略大小写,Http 保持默认选项。

### 非目标
- 不移动任何模型类型(不做集中序列化工程)。
- 不改 gRPC protobuf 载荷(Google.Protobuf/Grpc.Tools 生成代码,本身 AOT 兼容)。
- 不改 `JsonDocument` 系代码(错误路径、NacosEnvelope、FuzzyInitializationTracker、
  NacosConfigurationProvider 等,本身 AOT 安全)。
- 不处理已废弃的 CoreMaintainerModels(Http 实现直接 throw NotSupportedException,不注册、不碰)。

## 已确认决策

| # | 决策 | 选择 |
|---|---|---|
| D1 | 覆盖范围 | 全套:core + Grpc + Http |
| D2 | 解析器策略 | 纯源生成,零回退(JIT 构建同样走源生成) |
| D3 | 验证方式 | AOT 发布冒烟工程 + CI job |
| D4 | 上下文可见性 | public,支持用户 `JsonTypeInfoResolver.Combine` 扩展 |
| D5 | 上下文切分 | 方案一:每工程独立上下文 |
| D6 | object 成员处理 | 手写 AOT 安全转换器(API 不变) |

## 设计 1:上下文切分与序列化规则

三个 public 源生成上下文,选项配置严格对齐现有线上行为:

| 上下文 | 工程 | 序列化选项 |
|---|---|---|
| `NacosJsonContext` | core | 默认 |
| `NacosHttpJsonContext` | Http | 默认(现 AI 服务 `JsonOptions = new()` 即默认) |
| `NacosGrpcJsonContext` | Grpc | `CamelCase` + `PropertyNameCaseInsensitive` |

配套 internal 上下文(public 上下文不能声明 internal/private 类型):

| internal 上下文 | 工程 | 覆盖 |
|---|---|---|
| `NacosHttpInternalJsonContext` | Http | `SecurityProxy.LoginResponse`(private 嵌套,提升为 internal 顶层类) |
| `NacosGrpcInternalJsonContext` | Grpc | 3 个 AI 通知类(private 嵌套,提升为 internal 顶层类) |

### 接线规则
- 各工程现有 6+ 处重复 `new JsonSerializerOptions` 收敛为每工程一个内部静态工厂;
  core 的 resolver 直接指向 `NacosJsonContext`,Http/Grpc 的 resolver = 该工程
  public + internal 上下文的 `JsonTypeInfoResolver.Combine`。
- options 的命名策略/大小写设置必须与上下文的 `[JsonSourceGenerationOptions]`
  严格一致,否则运行时抛 `InvalidOperationException` —— 由工厂单点保证。

### 两条严格规则(纯源生成、零回退的必然结果)
1. **注册制**:凡经 SDK 序列化的类型必须显式声明进上下文,包括泛型的每个封闭实例
   (如 `ApiResult<PagedData<AgentSpecSummary>>`、`ApiResult<string>`,约 50 个)。
   `object` 静态类型的 `Serialize(request, options)` 调用点签名不变,靠 options 的
   resolver 按运行时类型解析;未注册类型直接抛 `NotSupportedException`。
2. **成员可表示性**:注册进上下文的类型,其成员必须是源生成可表示的。
   `Dictionary<string, object>` / `object?` 成员通过手写转换器解决(见下),
   类型本身不换(API 兼容)。

### object 成员处理(D6)

新增 core 内部 AOT 安全转换器(Utf8JsonReader/JsonElement
手动装箱,零反射),以 `[JsonConverter]` 特性挂到属性/类型:

- `ObjectValueConverter`:`object?` 成员(如 `AgentSpecSummary.PublishPipelineInfo`)
- `ObjectDictionaryConverter`:`Dictionary<string, object>` 成员(LockInstance.Params、
  EncryptObject/SkillResource/AgentSpecResource.Metadata、AgentExtension.Params)
- `SecurityScheme`(A2a,`Dictionary<string, object>` 子类):类型级转换器

## 设计 2:分工程改造清单

### core(RedNb.Nacos)
- 新增 public `NacosJsonContext`(默认选项),声明:`ServiceInfo`、`Instance`、
  `List<Instance>`(InstancesDiffer 日志序列化 `Hosts`)、`ConfigItem` 等
  Failover 泛型封闭实例(LocalDiskFailoverDataSource 的 `Deserialize<T>/Serialize`)、
  `McpCapability`(现有特性转换器源生成下可用,不动)。
- 新增 public `NamingSelector` 小 DTO(`{type, expression}`),取代 Http/Grpc 两处匿名类型;
  Http/Grpc 两个上下文都声明它。
- 新增上述手写转换器。

### Http(RedNb.Nacos.Http)
- 新增 public `NacosHttpJsonContext`(默认选项)+ internal `NacosHttpInternalJsonContext`。
- 声明:AI 全系模型(Prompt/Skill/AgentSpec/McpServer/AgentCard)及
  `ApiResult<>`/`PagedData<>` 全部封闭实例、`ServiceInfo`、`NamingSelector`、`LoginResponse`。
- 4 个 static `JsonOptions`(NacosAgentSpecService/NacosAiService/NacosPromptService/
  NacosSkillService)收敛到工厂;约 50 处调用点本身不动。

### Grpc(RedNb.Nacos.Grpc)
- 新增 public `NacosGrpcJsonContext`(CamelCase + 忽略大小写)+ internal
  `NacosGrpcInternalJsonContext`;3 个 private 嵌套通知类(McpServerNotification、
  AgentEndpointRequest、AgentCardNotification)提升为 internal 顶层类进 internal 上下文。
- 声明:ConfigRpcModels 全部类、NamingRpcModels 全部类、ConnectionSetupRequest、
  ServerCheckRequest/Response、HealthCheckRequest/Response、`List<AgentEndpoint>`
  (NacosAiClient.Recovery 深拷贝,当前裸调用改为走上下文)、`NamingSelector`。
- `_jsonOptions`(NacosGrpcClient)与 transport client 的 options 收敛到工厂;
  NacosGrpcNamingService 匿名类型 → `NamingSelector`。
- NamingServiceInfoHolder 本地磁盘缓存统一到 CamelCase 上下文:缓存文件格式变化一次,
  解析失败按"无缓存"处理(实现时若无 catch 则补)。

### DI / AspNetCore
- 无序列化代码,仅加 csproj 标记。

## 设计 3:错误处理与行为边界

- **不做 wrap,保持最小行为差异**:未注册类型抛 `NotSupportedException`
  ("metadata for type 'X' was not provided")。SDK 内部类型靠编译期清单 + 单测全覆盖 +
  AOT 冒烟保证不遗漏;用户自定义类型走 `SendRequestAsync(type, customRequest)` 得到该
  清晰异常,迁移指南写明。
- options 与上下文不一致由工厂单点保证。
- 线上 wire 行为零变化:Grpc CamelCase + 忽略大小写、Http 默认选项,与 2.0.0 逐字节一致,
  现有集成测试必须全绿。
- 唯一行为变化:Grpc NamingServiceInfoHolder 本地缓存格式变化一次(可再生成,不影响功能)。
- 源生成自 .NET 6 起可用,net8.0/net10.0 双目标无兼容问题。

## 设计 4:裁剪属性 + AOT 验证工程

- 5 个 csproj(core/Http/Grpc/DI/AspNetCore)加 `IsAotCompatible=true` + `IsTrimmable=true`;
  All 是 meta 包自动继承。
- 新冒烟工程 `samples/RedNb.Nacos.Sample.Aot`(`PublishAot=true` console,不进 NuGet 打包),
  自检内容:
  1. 全类型 roundtrip:经 SDK 工厂 options 对每个上下文声明的类型做序列化↔反序列化断言
     (含 McpCapability 双形态、object 转换器、NamingSelector);
  2. 严格模式负例:未注册类型必须抛异常;
  3. Fake gRPC client 走 config 请求/响应真实路径(复用现有 FakeNacosGrpcClient 思路,
     不依赖外部服务器);
  4. 可选:环境变量指向真实 Nacos 时跑一轮真实请求(本地手工验证)。
  exit code 非 0 即失败。
- CI:ci.yml 加 NativeAOT job —— `dotnet publish` AOT 发布 → 运行冒烟 exe → 校验
  exit code 0;runner 与现有 job 保持一致。
- 本地脚本 `scripts/run-aot-smoke.ps1`:Windows 本地一键发布 + 运行。

## 设计 5:测试策略

- 单测(每工程新增):上下文声明类型 roundtrip 全覆盖;未注册类型负例(纯源生成下 JIT
  测试同样抛,可抓住漏注册);ObjectValue/ObjectDictionary 转换器边界值(数字/字符串/
  嵌套对象/数组/null);McpCapability 源生成模式下双形态 roundtrip。
- 现有集成测试全绿(wire 行为不变是硬约束)。
- AOT 冒烟:本地脚本 + CI job。
- 迁移指南(docs)补:NativeAOT 使用说明、未注册类型行为、Grpc 缓存格式变更。

## 风险与回滚

| 风险 | 缓解 |
|---|---|
| 类型清单遗漏导致运行时 NotSupportedException | 单测每上下文全覆盖 + AOT 冒烟 + JIT 下同样抛(测试可抓) |
| options 与上下文不一致 | 工厂单点 + roundtrip 测试隐式覆盖 |
| 手写转换器边界行为与反射序列化不一致 | 边界值单测 + 与 2.0.0 反射输出对比测试 |
| Grpc 本地缓存格式变更 | 解析失败按无缓存处理,可再生成 |

## 验收标准

1. `dotnet publish -c Release`(JIT)全部工程零警告(裁剪分析器开启后)。
2. NativeAOT 冒烟工程 publish + 运行 exit 0,全部自检通过。
3. 现有单元/集成测试全绿;新增测试覆盖设计 5 全部条目。
4. 三个 public 上下文可由用户 `JsonTypeInfoResolver.Combine` 使用(冒烟含此用例)。
5. 迁移指南更新完毕;wire 行为与 2.0.0 一致(集成测试佐证)。
