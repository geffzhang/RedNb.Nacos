# RedNb.Nacos .NET SDK 代码现状分析

## 概述

本报告分析 RedNb.Nacos .NET SDK 代码库的当前状态：已删除的历史遗留代码、已集成的基础设施、
以及 Nacos v3 迁移后的废弃面。完整度与测试覆盖率详见
[SDK_COMPLETENESS_REPORT.md](SDK_COMPLETENESS_REPORT.md)。

**最后更新**: 2026-09-11（Nacos v3 迁移完成后；本文档此前为 GBK 编码，本次更新转存为 UTF-8）

---

## 一、已删除 / 废弃代码

### 1. Redo 机制（曾删除，后为 gRPC 命名服务重新引入）

历史：以下 4 个文件曾因"为 gRPC 重连设计但未使用"而被移除：
- `IRedoService.cs` / `AbstractRedoService.cs` / `RedoData.cs` / `RedoType.cs`

现状：gRPC 命名服务需要重做机制（重连后重新注册短暂实例），现已存在：
- `src/RedNb.Nacos/Naming/Redo/RedoScheduledTask.cs` — 重做调度任务
- `src/RedNb.Nacos.Grpc/Naming/NamingGrpcRedoService.cs` — gRPC 命名重做服务

⚠️ **遗留问题**：`RedoScheduledTask` 目前未被实例化接线——重连后丢失的服务端状态尚无重做。
已记录在终审的低优先级发现中，作为后续跟进项。

### 2. HTTP 配置长轮询（v3 迁移中删除）

v1 协议的长轮询监听机制已整体移除：
- `ListenerApiPath`（`v1/cs/configs/listener`）
- `StartLongPollingAsync` / `CheckConfigChangesAsync` / `NotifyListenersAsync` 及 MD5 轮询状态

原因：Nacos 3.x 无 HTTP 配置监听端点；服务端推送由 gRPC bi-stream 承担。
HTTP `AddListenerAsync` 保留本地监听器注册 + 一次性 MD5 种子（取自 `GetConfigAsync`），不再启动轮询循环。

### 3. v1/v2 协议路径（v3 迁移中删除）

- HTTP Config：`v1/cs/configs` → `v3/client/cs/config`（GET）/ `v3/admin/cs/config`（POST/DELETE）
- HTTP Naming：`/v1/ns/**`、`/v3/ns/**`（计划错误路径）→ `/v3/client/ns/instance[/list]` + `/v3/admin/ns/service/list`
- 心跳独立端点：无（心跳 = 注册端点 + `beat=true`）
- gRPC：`RequestService/SendRequest` → `Request/request`；`RequestBiStream` → `requestBiStream`（小写方法名）
- 命名空间头 `X-Nacos-Namespace-Id`：v3 不再使用，改走 `namespaceId` 查询/表单参数（常量保留仅因 AI 代码引用，见下）

### 4. 废弃面（`[Obsolete]`，Nacos 3.2+ 无对应端点）

| 接口 | 状态 | 运行时行为 |
|------|------|-----------|
| `IMaintainerService` + 10 个子接口 | `[Obsolete]` | HTTP 404（v3 无对应端点） |
| `ILockService` | `[Obsolete]` | 仍可用（`/v3/lock/**` 存活），下个大版本移除 |
| `PublishConfigCasAsync`（HTTP 重载） | `[Obsolete]` | 非空 casMd5 抛 `NotSupportedException`（v3 admin 端点忽略 CAS） |
| `InstancesChangeNotifier`（Http 实现） | `[Obsolete]` | 订阅通知走 gRPC |

---

## 二、已集成基础设施

### 1. Failover 机制（已集成 ✅）

历史：6 个文件曾因"故障转移机制未集成到任何服务中"被移除（`FailoverReactor.cs`、
`FailoverSwitch.cs`、`FailoverData.cs`、`FailoverDataType.cs`、`IFailoverDataSource.cs`、
`LocalDiskFailoverDataSource.cs`）。

现状：**已重新引入并集成到 NamingService**，测试覆盖 100%
（FailoverSwitchTests / FailoverDataTests / FailoverReactorTests）。

### 2. MetricsMonitor 监控（已集成 ✅）

历史：7 个文件曾因"监控功能未集成到任何服务中"被移除（`MetricsMonitor.cs`、`MetricNames.cs`、
`MetricType.cs`、`MetricsSnapshot.cs`、`GaugeMetric.cs`、`CounterMetric.cs`、`HistogramMetric.cs`）。

现状：**已重新引入并集成到 NamingService 和 ConfigService**，测试覆盖 100%
（MetricsMonitorTests / MetricNamesTests）。

### 3. gRPC 连接生命周期（v3 迁移新增 ✅）

- 连接就绪握手：ConnectionSetup 携带非空 `AbilityTable` → 服务器回 `SetupAckRequest` → 收到后 `_connected = true`
- 单 HTTP/2 连接：`EnableMultipleHttp2Connections = false`（一元请求必须走已注册的连接）
- 模块标签：命名服务连接 `module=naming`（服务器拒绝在 `module=config` 连接上注册短暂实例）
- 重连代数隔离：每次连接持有独立 generation（CTS/channel/stream/task）；重连前清理旧代；
  陈旧循环受 `IsCurrent` 守卫，不会翻转新连接的 `_connected`
- `DeadlineExceeded` 视为连接失效信号

### 4. v3 信封处理（v3 迁移新增 ✅）

`src/RedNb.Nacos.Http/Http/NacosEnvelope.cs` — 统一的 `{code, message, data}` 信封解析/校验，
配置与命名两个服务共用（取代此前各写各的解析）。

错误语义：
- 配置 GET：`code:20004` → `null`（不存在）；其他非零码 → 抛 `NacosException`
- 命名 register/deregister/list：非零码 → 抛 `NacosException`
- 心跳：非零码 → 记日志并返回 `false`
- 公开 API 边界的 `catch (Exception)` 已审计（Http 94 + gRPC 39 处），拒绝类响应不再被伪装成空结果

---

## 三、已知遗留项（终审低优先级发现，均已记录）

| # | 位置 | 描述 |
|---|------|------|
| 1 | `NacosNamingService.cs` | 被拒绝的实例列表查询会先计入成功指标再抛异常（顺序问题，净效果正确） |
| 2 | `NacosNamingService.cs` | `SubscribeAsync` 失败时本地监听器保持注册（后续推送可重新填充，可辩护） |
| 3 | `NacosGrpcClient.cs` | 清理旧连接时可能在持锁状态下等待 keep-alive 循环，受 2s `WaitAsync` 超时约束 |
| 4 | `NacosGrpcClient.cs` | `_current` 读写非原子，Dispose/Connect 竞态可导致新代未被发布（既有形态） |
| 5 | `NacosGrpcClient.cs` | `RedoScheduledTask` 未实例化（见上文）；`DeadlineExceeded` 重连会丢弃服务端状态 |
| 6 | `NacosGrpcConfigService.cs` | 非成功 `ConfigQueryResponse` 不检查 `ErrorCode`（Java 映射 300 = not found，待验证后跟进） |
| 7 | `NacosConstants.NamespaceHeader` | ~~常量保留仅为已搁置的 AI 代码引用~~（2026-09-11：`NacosAiService` 已不再发送该 header；常量暂保留——`NacosSkillService`/`NacosPromptService`/`NacosAgentSpecService` 3 个 helper 服务仍读取它，待后续清理） |

---

## 四、重复文件清理

- `tests\RedNb.Nacos.Tests\Naming\NamingSelectorTests.cs`（旧路径，与
  `tests\RedNb.Nacos.Tests\Naming\Selector\NamingSelectorTests.cs` 重复）— 已删除，保持删除状态。

---

## 五、结论

代码库已完成 Nacos 3.2+ 协议迁移：v1/v2 路径与 HTTP 长轮询清除，gRPC 传输对齐 3.2.4 契约，
Failover / Monitor 基础设施已集成并全量测试，废弃面按计划 `[Obsolete]` + 大声失败。
剩余工作集中在 gRPC 服务层测试覆盖（ErrorCode 语义、Redo 接线、连接健壮性），详见 REPORT 的"待完善"章节。
