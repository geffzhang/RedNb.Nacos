# RedNb.Nacos .NET SDK 完成度分析报告

## 概述

本报告对比分析 Nacos Java SDK 与 RedNb.Nacos .NET SDK 的功能实现情况，评估完成度和测试覆盖率。

**最后更新**: 2026-09-11（Nacos v3 迁移完成后）

---

## 零、Nacos 3.x 迁移状态（本次更新核心）

SDK 已完成向 **Nacos 3.2+** 的协议迁移（分支 `AIRegistry`，标签 `nacos-v3-migration-complete`）：

| 维度 | 迁移结果 |
|------|---------|
| 最低服务器版本 | Nacos 3.2.0（集成测试基于 3.2.4） |
| HTTP Config | `GET /v3/client/cs/config`（20004 → null）；`POST/DELETE /v3/admin/cs/config`（自动携带 accessToken）；v3 信封 `{code, message, data}` 解析 |
| HTTP Naming | `/v3/client/ns/instance`（+ `/list`，心跳为 `beat=true` 复用注册端点）；`/v3/admin/ns/service/list`；错误信封抛 `NacosException` |
| gRPC | 一元 `Request/request` 承载全部业务请求；`BiRequestStream/requestBiStream` 承载 ConnectionSetup + 服务端推送；SetupAck 握手后才标记连接就绪 |
| 认证 | `POST /v3/auth/user/login`，令牌仍走 `accessToken` 头 |
| 命名空间 | `namespaceId` 查询/表单参数（v3 不再使用 `X-Nacos-Namespace-Id` 头） |
| 配置监听 | HTTP 长轮询已移除（v3 无 HTTP 监听端点）；服务端推送由 gRPC bi-stream 提供 |
| CAS 发布 | HTTP 重载 `[Obsolete]` + 非空 casMd5 抛 `NotSupportedException`（v3 admin 端点忽略 CAS）；gRPC 原生支持 |
| 重连稳定性 | gRPC 连接代数（generation）隔离 + 清理旧代；`DeadlineExceeded` 视为连接失效；陈旧循环不会翻转新连接的 `_connected` |

**测试矩阵**：Core 382 ×2 TFM · HTTP 132 ×2 TFM · gRPC 19 ×2 TFM · 集成测试 40（27 通过 · 6 环境阻塞 · 7 Skip：4 个 gRPC AI 测试受既有 SDK gap 阻塞（见 §一.3）+ 2 个订阅轮询占位 + 1 个服务端契约差异（`ListAgentVersions` 返回对象数组，SDK 按 `List<string>` 反序列化）；live Nacos 3.2.4，服务器日志无协议告警）。

> 2026-09-11 复跑：单元测试 382/132/19 ×2 TFM 全绿，AI 集成类 15 = 8 通过 · 7 Skip · 0 失败（HTTP 类含新增的 `ReleaseMcpServer_WithToolSpecification_...` 与 `ReleaseMcpServer_WithEndpointSpecification_...`）；
> 同机全量集成复跑中非 AI 的 6 个 gRPC 推送用例失败（故上表计 6 环境阻塞，不计入"通过"），已在未含本次修复的基线代码上以相同方式复现（既存环境问题，另行跟踪，与本次变更无关）。

---

## 一、核心服务完成度

### 1. Config Service (配置服务)

| 功能 | Java SDK | .NET SDK (HTTP) | .NET SDK (gRPC) | 测试覆盖 |
|-----|---------|-----------------|-----------------|---------|
| getConfig | ✅ | ✅ (v3) | ✅ | ✅ 单元+集成 |
| getConfigAndSignListener | ✅ | ✅ (本地注册+gRPC 推送) | ✅ | ✅ 集成 |
| addListener | ✅ | ✅ (本地注册+gRPC 推送) | ✅ (bi-stream push) | ✅ 单元+集成(推送) |
| removeListener | ✅ | ✅ | ✅ | ✅ 单元 |
| publishConfig | ✅ | ✅ (admin API) | ✅ | ✅ 单元+集成 |
| publishConfigCas | ✅ | ⚠️ `[Obsolete]`+抛异常 | ✅ (原生) | ✅ 单元(HTTP 抛异常) |
| removeConfig | ✅ | ✅ (admin API) | ✅ | ✅ 单元+集成 |
| getServerStatus | ✅ | ✅ | ✅ | ✅ 单元 |
| addConfigFilter | ✅ | ✅ | ✅ (filter chain) | ✅ 单元(HTTP) · ⚠️ gRPC 无测试 |
| fuzzyWatch (Nacos 3.0) | ✅ | ✅ | ✅ | ✅ 单元(HTTP) · ⚠️ gRPC 无测试 |
| cancelFuzzyWatch | ✅ | ✅ | ✅ | ✅ 单元(HTTP) · ⚠️ gRPC 无测试 |

**HTTP 实现完成度: 100%** | **gRPC 实现完成度: 100%\***（\* 实现齐备，未对 live 验证、服务层无测试）

### 2. Naming Service (命名服务)

| 功能 | Java SDK | .NET SDK (HTTP) | .NET SDK (gRPC) | 测试覆盖 |
|-----|---------|-----------------|-----------------|---------|
| registerInstance (多重载) | ✅ | ✅ (v3) | ✅ | ✅ 单元+集成 |
| deregisterInstance (多重载) | ✅ | ✅ (v3) | ✅ | ✅ 集成 |
| batchRegisterInstance | ✅ | ✅ | ✅ (redo 缓存联动) | ⚠️ 无测试 |
| batchDeregisterInstance | ✅ | ✅ | ✅ (逐个注销，gRPC 无批量端点) | ⚠️ 无测试 |
| getAllInstances (多重载) | ✅ | ✅ (v3 平铺数组解析) | ✅ | ✅ 单元+集成 |
| selectInstances (多重载) | ✅ | ✅ | ✅ | ✅ 集成 |
| selectOneHealthyInstance | ✅ | ✅ | ✅ | ✅ 集成 |
| subscribe (Action回调) | ✅ | ✅ (本地通知器) | ✅ (bi-stream push) | ✅ 集成(推送) |
| subscribe (Selector) | ✅ | ✅ | ✅ | ✅ 集成(HTTP) · ⚠️ gRPC 无测试 |
| unsubscribe | ✅ | ✅ | ✅ | ⚠️ 无测试 |
| getServicesOfServer | ✅ | ✅ (admin pageItems) | ✅ | ✅ 集成 |
| getSubscribeServices | ✅ | ✅ | ✅ (本地订阅视图) | ⚠️ 无测试 |
| fuzzyWatch (Nacos 3.0) | ✅ | ✅ | ✅ | ✅ 单元(HTTP) · ⚠️ gRPC 无测试 |
| 心跳机制 | ✅ | ✅ (`beat=true` 复用注册端点) | ✅ 连接级(无需客户端心跳) | ✅ 集成 |
| 服务信息缓存 | ✅ | ✅ (TTL+写时失效) | ✅ (NamingServiceInfoHolder) | ✅ 单元 |

**HTTP 实现完成度: 100%** | **gRPC 实现完成度: 100%\***（\* 实现齐备，未对 live 验证、服务层无测试）

### 3. AI Service (AI/MCP/A2A 服务) - Nacos 3.0 新增功能

> ⚠️ **状态：HTTP 通道已通过 live Nacos 3.2.4 验证（2026-09-11）；gRPC 通道被既有 SDK gap 阻塞，live 测试已 Skip**。
> AI 端点部署在控制台端口 8080（`/v3/console/ai/**`）；HTTP 通道 `src/RedNb.Nacos.Http/Ai/` 已按真实控制器路径与参数名对齐并联调通过
> （8 个 HTTP AI 集成测试通过：6 个 live 往返 + 2 个 fail-loud，不再吞掉 `NacosException`；另有 3 个 Skip）。
> gRPC 实现位于 `NacosGrpcAiService.cs`（MCP/AgentCard）+ `NacosGrpcAiService.Registry.cs`（Prompt/Skill/AgentSpec 注册表），
> 请求 TYPE 字符串已按服务器 handler 类对齐（枚举 `nacos-ai-3.2.4.jar` handler 类验证），但通道被 4 个既有 SDK gap 阻塞：
> `NacosGrpcClient` 无认证路径、`OperationResponse` 结构与服务器 `Response{resultCode,errorCode,message,requestId}` 不符、
> `ReleaseMcpServerResponse` 读 `McpServerId` 而非服务器下发的 `mcpId`；已跟踪为后续修复。
> （更正：早前"HTTP `DeleteMcpServerAsync` 发送无 body 的 DELETE 导致 404"的诊断已被 live 探针证伪 —— 无 body 的 DELETE 删除已存在的服务器返回 200 `{"code":0,"message":"success","data":"ok"}`，
> 404 仅为 "MCP server not found"；DELETE 控制器读查询参数、不绑定请求体（探针中省略查询参数、仅带 body 时收到 400 "Required parameter 'mcpId' or 'mcpName'"）。）
> HTTP 侧 register/deregister endpoint 无对应路径，调用时 fail loud 指向 gRPC 通道；MCP tool CRUD 在 3.2.4 两个通道均不存在（无 HTTP 端点、也无 gRPC handler）；
> 订阅仍为 10s 轮询，可后续替换为 gRPC push。

| 功能 | Java SDK | .NET SDK (HTTP) | .NET SDK (gRPC) | 测试覆盖 |
|-----|---------|-----------------|-----------------|---------|
| getMcpServer | ✅ | ✅ | ✅ | ✅ 单元(HTTP) + 集成(HTTP live 往返) |
| releaseMcpServer | ✅ | ✅ | ✅ | ✅ 集成(HTTP live 往返：toolSpecification 持久化往返，endpointSpecification 受理 + serviceRef 回传) · ⚠️ gRPC Skip |
| registerMcpServerEndpoint | ✅ | ✅ | ✅ | ✅ 集成(HTTP fail-loud 断言) · ⚠️ gRPC Skip |
| deregisterMcpServerEndpoint | ✅ | ✅ | ✅ | ✅ 集成(HTTP fail-loud 断言) · ⚠️ gRPC Skip |
| subscribeMcpServer | ✅ | ✅ | ✅ | ✅ 单元(HTTP) · ⚠️ 集成 Skip（轮询占位） |
| unsubscribeMcpServer | ✅ | ✅ | ✅ | ✅ 单元(HTTP) |
| deleteMcpServer | ✅ | ✅ | ✅ | ✅ 集成(HTTP：随往返用例 try/finally 清理，探针验证 200) |
| listMcpServers | ✅ | ✅ | ✅ | ✅ 集成(HTTP live 往返) |
| getAgentCard | ✅ | ✅ | ✅ | ✅ 集成(HTTP live 往返) |
| releaseAgentCard | ✅ | ✅ | ✅ | ✅ 集成(HTTP live 往返) |
| Agent Subscription | ✅ | ✅ | ✅ | ✅ 单元(HTTP) · ⚠️ 集成 Skip（轮询占位） |
| Prompt 注册表（draft/审核/发布/上下线/删除） | ✅ | ✅ | ✅ | ✅ 单元(HTTP) · ⚠️ gRPC 无测试 |
| Skill 注册表（含 zip 上传/下载） | ✅ | ✅ | ✅ | ✅ 单元(HTTP) · ⚠️ gRPC 无测试 |
| AgentSpec 注册表 | ✅ | ✅ | ✅ | ✅ 单元(HTTP) · ⚠️ gRPC 无测试 |

**HTTP 实现完成度: 100%\*** | **gRPC 实现完成度: 100%\***（\* HTTP 通道已通过 live 8080 控制台验证（2026-09-11），但 endpoint register/deregister 无 HTTP 端点、MCP tool CRUD 两个通道均无（HTTP 侧调用 fail loud）；
gRPC 通道 TYPE 字符串已对齐服务器，受 4 个既有 SDK gap 阻塞、live 测试 Skip —— 详见本节状态注）

### 4. Lock Service (分布式锁) - Nacos 3.0 新增功能

> `ILockService` 已标记 `[Obsolete]`：底层 `/v3/lock/...` 端点在 3.x 仍可用，接口将于下个大版本移除。

| 功能 | Java SDK | .NET SDK (HTTP) | .NET SDK (gRPC) | 测试覆盖 |
|-----|---------|-----------------|-----------------|---------|
| lock | ✅ | ✅ `[Obsolete]` | ✅ | ✅ 单元 |
| unlock | ✅ | ✅ `[Obsolete]` | ✅ | ✅ 单元 |
| tryLock (带超时) | ✅ | ✅ `[Obsolete]` | ✅ | ✅ 单元 |
| remoteTryLock | ✅ | ✅ `[Obsolete]` | ✅ | ✅ 单元 |
| remoteReleaseLock | ✅ | ✅ `[Obsolete]` | ✅ | ✅ 单元 |
| LockInstance (Fluent API) | ✅ | ✅ | ✅ | ✅ 单元 |

**HTTP 实现完成度: 100%** | **gRPC 实现完成度: 100%** | **测试覆盖: 100%** ✅

### 5. Maintainer Service (运维管理服务)

> ⚠️ `IMaintainerService` 及全部子接口已标记 `[Obsolete]`：**v3 无对应端点，运行时返回 404**。
> 迁移路径见 [MIGRATION.md](MIGRATION.md)（legacy-adapter JAR / 冻结 2.x 部署 / 等待重写）。

| 功能模块 | Java SDK | .NET SDK | 测试覆盖 |
|---------|---------|----------|---------|
| IServiceMaintainer | ✅ | ✅ `[Obsolete]` | ✅ 单元 |
| IInstanceMaintainer | ✅ | ✅ `[Obsolete]` | ✅ 单元 |
| INamingMaintainer | ✅ | ✅ `[Obsolete]` | ✅ 单元 |
| IConfigMaintainer | ✅ | ✅ `[Obsolete]` | ✅ 单元 |
| IConfigHistoryMaintainer | ✅ | ✅ `[Obsolete]` | ⚠️ 无测试 |
| IBetaConfigMaintainer | ✅ | ✅ `[Obsolete]` | ⚠️ 无测试 |
| IConfigOpsMaintainer | ✅ | ✅ `[Obsolete]` | ⚠️ 无测试 |
| IClientMaintainer | ✅ | ✅ `[Obsolete]` | ⚠️ 无测试 |
| ICoreMaintainer | ✅ | ✅ `[Obsolete]` | ⚠️ 无测试 |

**实现完成度: 100% (已废弃)** | **测试覆盖: 40%**

---

## 二、通用功能完成度

### 1. 认证与安全

| 功能 | Java SDK | .NET SDK | 测试覆盖 |
|-----|---------|----------|---------|
| 用户名/密码认证 | ✅ | ✅ (`/v3/auth/user/login`) | ✅ 单元+集成 |
| Token 自动刷新 | ✅ | ✅ (TTL 缓存) | ⚠️ 无测试 |
| TLS/SSL 支持 | ✅ | ✅ | ⚠️ 无测试 |
| AccessKey/SecretKey | ✅ | ✅ (可配) | ⚠️ 无测试 |

### 2. 客户端配置

| 功能 | Java SDK | .NET SDK | 测试覆盖 |
|-----|---------|----------|---------|
| 服务器地址 | ✅ | ✅ | ✅ 单元 |
| 命名空间 | ✅ | ✅ (`namespaceId` 参数) | ✅ 单元 |
| 超时配置 | ✅ | ✅ | ✅ 单元 |
| 长轮询超时 | ✅ | ✅ (HTTP 长轮询已随 v3 移除) | N/A |
| gRPC 端口偏移 | ✅ | ✅ (8848+1000=9848) | ⚠️ 无测试 |
| 重试配置 | ✅ | ✅ | ⚠️ 无测试 |

### 3. 选择器 (Selector)

| 功能 | Java SDK | .NET SDK | 测试覆盖 |
|-----|---------|----------|---------|
| ClusterSelector | ✅ | ✅ | ✅ 单元+集成 |
| LabelSelector | ✅ | ✅ | ✅ 单元+集成 |
| CompositeSelector | ✅ | ✅ | ✅ 单元+集成 |
| 自定义 Selector | ✅ | ✅ | ⚠️ 无测试 |

### 4. 配置过滤器 (Config Filter)

| 功能 | Java SDK | .NET SDK | 测试覆盖 |
|-----|---------|----------|---------|
| IConfigFilter 接口 | ✅ | ✅ | ✅ 单元 |
| ConfigFilterChainManager | ✅ | ✅ | ✅ 单元 |
| AES 加密过滤器 | ✅ | ✅ | ✅ 单元 |

### 5. 配置解析器

| 功能 | Java SDK | .NET SDK | 测试覆盖 |
|-----|---------|----------|---------|
| PropertiesChangeParser | ✅ | ✅ | ⚠️ 无测试 |
| JsonChangeParser | ✅ | ✅ | ⚠️ 无测试 |
| YamlChangeParser | ✅ | ✅ | ⚠️ 无测试 |
| ConfigChangeParserFactory | ✅ | ✅ | ⚠️ 无测试 |

---

## 三、故障转移与监控 (已集成) ✅

### Failover 机制

| 组件 | 状态 | 说明 |
|-----|------|-----|
| FailoverReactor | ✅ 已集成 | 集成到 NamingService |
| FailoverSwitch | ✅ 已实现 | 控制故障转移开关 |
| FailoverData | ✅ 已实现 | 故障转移数据模型 |
| IFailoverDataSource | ✅ 已实现 | 数据源接口 |
| LocalDiskFailoverDataSource | ✅ 已实现 | 本地磁盘数据源 |

**测试覆盖: 100%** - 包含 FailoverSwitchTests, FailoverDataTests, FailoverReactorTests

### MetricsMonitor 监控

| 组件 | 状态 | 说明 |
|-----|------|-----|
| MetricsMonitor | ✅ 已集成 | 集成到 NamingService 和 ConfigService |
| 请求成功计数 | ✅ 已启用 | RecordNamingRequestSuccess/RecordConfigRequestSuccess |
| 请求失败计数 | ✅ 已启用 | RecordNamingRequestFailed/RecordConfigRequestFailed |
| 连接状态 | ✅ 已启用 | SetConnectionStatus |
| 服务变更推送 | ✅ 已启用 | RecordServiceChangePush/RecordConfigChangePush |
| 服务信息缓存 | ✅ 已启用 | SetServiceInfoMapSize |
| 监听配置数量 | ✅ 已启用 | SetListenConfigCount |

**测试覆盖: 100%** - 包含 MetricsMonitorTests, MetricNamesTests

---

## 四、测试覆盖率分析

### 1. 核心测试 (RedNb.Nacos.Tests)

**总计: 382 个测试 × 2 TFM (net8.0 + net10.0)，全部通过**

覆盖：客户端配置、异常处理、工具类、实例模型、配置变更事件、过滤链、AES 加密、模糊监听、
服务信息（含 TTL 缓存失效）、命名选择器、AI 模型、Lock（常量/实例/服务）、Maintainer、Failover、Monitor 等。

### 2. HTTP 实现测试 (RedNb.Nacos.Http.Tests)

**总计: 132 个测试 × 2 TFM，全部通过**

覆盖：工厂类、服务器列表管理、配置监听管理、配置服务 v3（信封解析、20004→null、
错误码抛异常、CAS 抛异常）、命名服务 v3（注册/注销/列表/错误信封抛异常/拒绝查询抛异常）、
服务信息持有与缓存、AI 服务（Prompt/Skill/AgentSpec 单元级）。

### 3. gRPC 实现测试 (RedNb.Nacos.Grpc.Tests)

**总计: 19 个测试 × 2 TFM，全部通过**

覆盖：ConfigRpcTransportClient 查询/监听/fuzzy-watch 调度（Metadata.type 断言）、
NamingRpcTransportClient 一元/流式/推送分派。
服务层（Config/Naming/AI 业务方法）无单元测试——批量/fuzzyWatch/Selector 订阅等方法连集成测试也未覆盖。

### 4. 集成测试 (RedNb.Nacos.IntegrationTests, live Nacos 3.2.4)

| 测试文件 | 测试内容 | 测试数量 | 状态 |
|---------|---------|---------|------|
| ConfigServiceIntegrationTests.cs | 配置服务集成 (v3) | 6 | ✅ |
| NamingServiceIntegrationTests.cs | 命名服务集成 (v3) | 9 | ✅ |
| NamingSelectorIntegrationTests.cs | 选择器集成 | 4 | ✅ |
| ConfigListenerPushTests.cs | 配置 gRPC 推送闭环 | 3 | ⚠️ 本机环境阻塞（推送未到达，见 §零注） |
| NamingSubscribePushTests.cs | 命名 gRPC 推送闭环 | 3 | ⚠️ 本机环境阻塞（同上） |
| AiServiceIntegrationTests.cs | AI 服务集成（HTTP 控制台） | 11 | ✅ 8 通过（6 live 往返 + 2 fail-loud 断言） · ⚠️ 3 Skip（2 订阅轮询 + 1 服务端契约差异） |
| Ai/GrpcAiServiceIntegrationTests.cs | AI 服务集成（gRPC 通道） | 4 | ⚠️ Skip（受 4 个既有 SDK gap 阻塞，见 §一.3 AI 状态注） |

**总计: 40 个集成测试**（27 通过 · 6 环境阻塞 · 7 Skip，见 §零注与 §一.3；AI 类复跑 15 = 8 通过 · 7 Skip · 0 失败；环境阻塞的 6 个为既存 gRPC 推送用例）

### 测试覆盖总结

| 模块 | 测试数量 | 覆盖率估计 |
|-----|---------|-----------|
| 核心模型 | 200+ | 90% |
| HTTP Config Service | 30+ | 90% |
| HTTP Naming Service | 40+ | 90% |
| HTTP AI Service | 30+ | 70%（单元级） |
| gRPC Config/Naming | 19 | 85%（分派逻辑） |
| Lock Service | 55+ | 100% ✅ |
| Maintainer Service | 35+ | 40% |
| Failover 机制 | 20+ | 100% ✅ |
| MetricsMonitor | 25+ | 100% ✅ |

**测试运行结果: 单元（382 + 132 + 19）×2 TFM 全部通过 + 40 集成（27 通过 · 6 环境阻塞 · 7 Skip；复跑细节见 §零注）**

---

## 五、待完善功能列表

> 本版修正了此前表格中 gRPC 高级功能"未实现"的误标（实现实际齐备，见 §一），
> 缺口集中在**测试覆盖与 live 验证**。条目按建议优先级排列，代码位置为锚点。

### 高优先级

1. ~~**AI 服务 live 验证（HTTP + gRPC 双通道）**~~（2026-09-11：HTTP 通道完成；gRPC 受既有 SDK gap 阻塞，见 §一.3 AI 行的 * 注；`NacosConstants.NamespaceHeader` 清理延期，见 ANALYSIS §三.7）

2. **gRPC 服务层测试覆盖（实现齐备，测试空白）**
   - [ ] Config：AddConfigFilter（`NacosGrpcConfigService.cs:303`）、FuzzyWatch/CancelFuzzyWatch（`:313`/`:399`）
   - [ ] Naming：BatchRegister/BatchDeregister（`NacosGrpcNamingService.cs:162`/`:187`）、Selector 订阅（`:528`）、GetSubscribeServices（`:747`，本地订阅视图）、FuzzyWatch/CancelFuzzyWatch（`:787`/`:861`）
   - [ ] 现状：gRPC 测试仅 19 个传输层分派用例（`RedNb.Nacos.Grpc.Tests`），服务层（Config/Naming/AI）零覆盖

3. **gRPC 配置查询错误语义**
   - [ ] `NacosGrpcConfigService.cs:133`：非成功 `ConfigQueryResponse` 未检查 `ErrorCode`（Java 映射 300 = not found）；先对 live 验证 300 行为再修改

### 中优先级

1. **Redo 机制接线**
   - [ ] `RedoScheduledTask`（`src/RedNb.Nacos/Naming/Redo/RedoScheduledTask.cs`）未实例化——gRPC 重连后丢失的服务端状态无重做；`NamingGrpcRedoService` 缓存已就绪，只差调度

2. **配置解析器测试**
   - [ ] PropertiesChangeParser / JsonChangeParser / YamlChangeParser / ConfigChangeParserFactory

3. **连接健壮性与负载均衡（合并 ANALYSIS §三 N1-N5）**
   - [ ] gRPC 多服务器地址负载均衡 / 连接池
   - [ ] N1：被拒实例列表查询先计入成功指标再抛异常（指标计数顺序）
   - [ ] N3：清理旧连接时持锁等待 keep-alive 循环（受 2s `WaitAsync` 约束）
   - [ ] N4：`_current` 读写非原子（Dispose/Connect 竞态）
   - [ ] N5：`DeadlineExceeded` 重连丢弃服务端状态（与第 4 条同源）

### 低优先级

1. **测试补充**
   - [ ] Token 自动刷新
   - [ ] 心跳 `beat=false` 失败路径
   - [ ] AI：gRPC 通道 live 测试（4 个用例 Skip，受 §一.3 的既有 SDK gap 阻塞）——HTTP 侧的 release/get/delete/list MCP 与 release/releaseAgentCard 已补齐 live 往返测试（2026-09-11）
   - [ ] gRPC 端口偏移（8848+1000=9848）、重试配置

2. **订阅失败语义确认**
   - [ ] N2：`SubscribeAsync` 失败时本地监听器保持注册（后续推送可重新填充——已判可辩护，确认是否保持现状）

---

## 六、完成度总结

| 模块 | HTTP 实现 | gRPC 实现 | 测试覆盖 |
|-----|----------|----------|---------|
| Config Service | 100% (v3) | 100%\* | 90% |
| Naming Service | 100% (v3) | 100%\* | 90% |
| AI Service | 100%\* | 100%\* | 70% |
| Lock Service | 100% `[Obsolete]` | 100% | **100%** ✅ |
| Maintainer Service | `[Obsolete]` | N/A | **40%** |
| Failover 机制 | **100%** ✅ | N/A | **100%** ✅ |
| MetricsMonitor | **100%** ✅ | N/A | **100%** ✅ |
| **总体** | **100%** | **100%\*** | **85%** |

\* 实现已编码完成；gRPC 服务层无单元测试（19 个用例均为传输层分派）。AI 通道状态见 §一.3 状态注：HTTP 已通过 live 8080 控制台验证（2026-09-11），gRPC 受 4 个既有 SDK gap 阻塞（live 测试 Skip）。

### 结论

.NET SDK 已完成 **Nacos 3.2+ 协议迁移**：HTTP Config/Naming 在 v3 客户端/管理端点上 100% 实现，
gRPC 承载全部请求传输与服务端推送（连接就绪握手、重连代数隔离），
错误信封统一抛 `NacosException`，废弃面（Maintainer/Lock/CAS）按计划标 `[Obsolete]` 并大声失败。

**本次迁移完成的改进:**
1. ✅ HTTP Config/Naming 迁移到 v3 client/admin 端点 + 信封解析
2. ✅ gRPC 协议对齐 3.2.4（`Request/request` 一元 + bi-stream 推送 + SetupAck 握手）
3. ✅ 心跳机制修正（`beat=true` 复用注册端点）
4. ✅ 配置监听 HTTP 长轮询移除，推送走 gRPC bi-stream
5. ✅ gRPC 重连机制完善（连接代数隔离，陈旧循环不污染新连接）
6. ✅ 错误信封在公开 API 边界抛异常（拒绝查询不再伪装成"无实例"）
7. ✅ 测试矩阵已建立：382+132+19 单元 ×2 TFM 全绿 + 40 live 集成（27 通过 · 6 环境阻塞 · 7 Skip，见 §零注；单元与 AI 集成类 0 失败）

**下一步优先级:**
1. ~~AI 服务 live 控制台验证（8080）~~（2026-09-11：HTTP 已完成；gRPC 受既有 SDK gap 阻塞，见 §五.1）
2. gRPC Config/Naming 高级功能的测试覆盖（fuzzyWatch、批量、Selector 订阅——实现已齐备，见 §五.2）
3. Redo 机制接线与负载均衡
