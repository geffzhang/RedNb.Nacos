# NativeAOT 与自定义 JSON

本文对应已发布的 2.1.0，目标服务器为 Nacos 3.2.4，不支持 Nacos 2.x。约定的 Windows/Linux 原生矩阵与 CI 已通过；实际结果及未认证范围见 [本轮验收报告](NATIVEAOT_TEST_REPORT.md)。

## 用户类型

SDK 自有协议模型始终使用 SDK 源生成元数据。自定义对象（例如 AgentExtension.Params、锁 Params、底层 gRPC 的自定义请求/响应）通过客户端选项注册：

```csharp
using System.Text.Json.Serialization;
using RedNb.Nacos;

var options = new NacosClientOptions
{
    ServerAddresses = "localhost:8848",
    ConsoleAddresses = "localhost:8080",
    JsonTypeInfoResolver = ApplicationJsonContext.Default
};

public sealed class ApplicationPayload
{
    [JsonPropertyName("application_value")]
    public int Value { get; set; }
}

[JsonSerializable(typeof(ApplicationPayload))]
internal partial class ApplicationJsonContext : JsonSerializerContext { }
```

将此 options 传入工厂/客户端，或在 `AddNacos` / `AddNacosAi` 的配置委托中设置相同属性。需要多个用户 Context 时，仅组合用户 Context。不要尝试用用户 resolver 重命名 SDK 类型字段。不同客户端构建独立的只读序列化 options；应用也不应在客户端创建后修改共享 resolver 的行为。

普通 JIT 在 JSON 反射默认启用且允许运行时动态代码时，未注册用户类型可回退反射。设置 `JsonSerializerIsReflectionEnabledByDefault=false` 或原生发布后，未注册类型明确报错。注册请求时也要注册自定义响应及其嵌套类型。

SDK 内部仅有一个受运行时特性开关保护的 JIT 兼容边界，对该边界有局部分析器注解；没有全局忽略 AOT/裁剪警告。SDK 类型缺失元数据始终失败，不使用该反射回退。

`object` 读取保留 `JsonElement`。写入支持常见数值、字符串、日期、Guid、字节数组和嵌套容器；自定义对象交给当前 options 的 resolver。循环引用不支持，超深结构会抛 `JsonException`。不要把这个行为视为支持 `ReferenceHandler.Preserve`。

## 泛型缓存

```csharp
var cache = new RedNb.Nacos.Failover.LocalDiskFailoverDataSource<ApplicationPayload>(
    Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
    cacheDirectory,
    "failover-switch",
    ApplicationJsonContext.Default.ApplicationPayload);
```

原三参数构造函数保留；JIT 自定义类型仍可使用它。原生应用应给自定义 T 显式提供元数据。磁盘缓存读取不新增 CLR 类型推断，旧 JSON 缓存的大小写兼容行为保留。

## 生产路径与验证对应

| 实际入口 | 元数据来源 / 用户扩展 | 验证入口 |
|---|---|---|
| NacosGrpcClient 握手、健康请求、RequestAsync、推送 ACK | Grpc 公共 + 内部 Context；用户请求/响应使用客户端 resolver | 原 Aot Program；LiveChecks 自定义请求/响应、配置/模糊监听 |
| ConfigRpcTransportClient 查询、发布、CAS、监听、Fuzzy Watch | Grpc Context + 客户端 resolver | LiveChecks、ConfigReliabilityTests |
| NamingRpcTransportClient 实例、订阅、Fuzzy Watch | Grpc Context + 客户端 resolver | LiveChecks、FuzzyWatchTests / 现有 Naming 集成测试 |
| NacosGrpcLockService LockOperationRequest / LockInstance | Grpc Context；嵌套 Params 传递当前 options | LiveChecks 双客户端竞争、自定义锁参数；Grpc 元数据单测 |
| HTTP SecurityProxy 登录、Config/Naming/Admin 信封 | Http/Core Context（固定 SDK 类型） | SecurityProxyTests、HTTP 单测、集成测试 |
| NacosAiService / Prompt / Skill / AgentSpec 的 HTTP 模型和 AI 信封 | 每实例 Http AI Context + 客户端 resolver | UserContextHttpTests、LiveChecks AI 生命周期 |
| gRPC AI 注册、推送与恢复 | Grpc Context；固定端点克隆使用已注册端点模型 | LiveChecks A2A；恢复模式 MCP backend |
| Config 快照、NamingServiceInfoHolder 磁盘缓存 | SDK Context（内置模型，不由用户覆盖） | 现有快照/缓存回归和重连集成测试 |
| LocalDiskFailoverDataSource<T> | 显式 JsonTypeInfo<T>；旧构造函数使用 Core 策略 | UserContextTests、LiveChecks 文件往返 |
| ASP.NET Core DI、配置源、健康检查 | SDK 工厂策略；Web 响应独立 WebContext | 独立 Sample.AotWeb HTTP 自检 |

Context 类型注册单测与真实链路配合使用，类型注册数量不等于覆盖率。ACK 有/无 requestId 除了字节格式检查，还由 AotWeb 内的独立 HTTP/2 协议对端实际发送推送、读取 ACK 并确认结果。真实 Nacos 的监听与模糊初始同步另行验证；缺失 requestId 的故障输入由受控对端构造，不冒充真实 Nacos 常规行为。

## 可复现验证

准备本地独立 Nacos 3.2.4，设置 `NACOS_TEST_SERVER`、`NACOS_TEST_CONSOLE`、`NACOS_TEST_USERNAME`、`NACOS_TEST_PASSWORD`。示例默认值仅用于本地测试（localhost，nacos/nacos），不要用于生产部署。

```powershell
# 托管运行，禁用隐式 JSON 反射
./scripts/aot/validate.ps1 -Managed -Framework net8.0 -Rid win-x64 -App Aot
./scripts/aot/validate.ps1 -Managed -Framework net10.0 -Rid win-x64 -App AotWeb
# 发布后直接运行原生二进制；分别对 net8.0/net10.0 与 Aot/AotWeb 执行
./scripts/aot/validate.ps1 -Framework net10.0 -Rid win-x64 -App Aot
# 使用本轮本地打包结果，不发布到 NuGet.org
./scripts/aot/validate.ps1 -Framework net10.0 -Rid win-x64 -App AotWeb -PackageDirectory artifacts/packages -Version 2.1.0
```

Linux 在 Linux 主机运行相同 PowerShell 脚本，RID 换为 linux-x64，或使用 `scripts/aot/Dockerfile` 和 `run-linux.sh`。Windows 需要 MSVC/Windows SDK，Linux 需要 clang/zlib；参见 [微软 NativeAOT 前置条件](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot)。

`run-linux.sh` 在 /source 读取 Git 跟踪及新增的未忽略文件，复制到容器 /work 编译，并在 /results 保存产物；随后执行本地打包及包消费者验证。包消费者核对实际还原图没有源码项目引用，并比较安装包和输入包的 SHA-256，拒绝同版本旧缓存。`run-packages-linux.sh` 用包哈希隔离 SDK 缓存。

`run-recovery.ps1` 只接受本地地址和命名受限的测试容器，在原生客户端发出 ready 信号后重启容器，由独立观察客户端验证 Config/Naming/MCP 恢复。Windows CI 的独立 Java 服务只做功能验证；容器故障注入在 Linux CI 进行。

Windows 本地也可通过 Docker 运行已生成的 Linux 二进制进行恢复验收：

```powershell
./scripts/aot/run-recovery.ps1 -Executable artifacts/aot-210/linux-x64/net10.0/Aot/RedNb.Nacos.Sample.Aot -DockerImage rednb-nacos-aot-toolchain:net8-net10 -OutputDirectory artifacts/aot-210/recovery/linux-x64/net10.0
```

CI 将普通回归、strict-serialization、native-sdk、native-web 分开；原生两个 job 均展开 .NET 8/10 × Windows/Linux，验证源码和实际 NuGet 消费者。任何 publish/run 失败、IL 警告或缺少成功标识都会失败。普通测试通过 TRX 拒绝零测试发现。

ARM64、TLS/代理、多节点集群、外部 LLM、所有 AI 插件组合和性能压测未纳入此轮认证。现有 MVC/Swagger 示例不属于 NativeAOT 应用。
