# RedNb.Nacos.Sample.AI

控制台示例，演示 RedNb.Nacos SDK 的完整 `IAiService` 能力面（MCP、A2A、
Prompt、Skill、AgentSpec），并集成 `Microsoft.Extensions.AI`、官方
`ModelContextProtocol` SDK 与 `Microsoft.Agents.AI`。

## 前置条件

- **.NET 10 SDK**（示例目标框架为 `net10.0`）
- 一台可达的 **Nacos 3.2.x** 服务器（示例已在 3.2.4 上验证；force-publish
  需要 ≥ 3.2.1）——默认 `localhost:8848`；gRPC 端口 = HTTP 端口 + 配置的
  偏移量，即 8848 → 9848
- 无需任何 LLM API Key——示例自带默认的 `EchoChatClient`

示例针对 **Nacos 3.2.4** 编写并验证（见
`deploy/docker-compose/docker-compose.yml`）。

## 运行

```bash
dotnet run --project samples/RedNb.Nacos.Sample.AI
```

进程逐章节打印一行结果，随后打印 `--- Summary ---` 汇总块并返回退出码：

- **0** ——运行正常结束；各章节结果（包括 `Failed`）都在汇总块中。部分章节
  失败仍然退出 0。
- **2** ——运行没有取得任何进展：启动路径上的连接失败，或**所有**章节都报告
  `Failed`（例如 Nacos 不可达——登录是各章节惰性进行的，因此服务器宕机时
  逐章节体现失败，而不是在启动路径上抛异常）。

## 演示内容

共八个章节，按顺序运行：

- `McpSamples` ——发布 MCP 服务器（Streamable HTTP 协议元数据 + `REF` 端点
  规格）→ 列表 → 详情 → 通过 HTTP 长轮询通道订阅/退订 → **仅 gRPC** 的端点
  注册/注销 → 删除。MCP 工具 CRUD 在 Nacos 3.2.4 上未开放，示例跳过。
- `A2aSamples` ——发布 Agent Card → **仅 gRPC** 的批量端点注册
  （`RegisterAgentEndpointsAsync`，一次往返注册两个端点）→ 列表 → 详情 →
  订阅 → 退订 → **仅 gRPC** 的端点注销 → 删除。
- `PromptSamples` ——草稿 → 提交审核 → **force-publish** → 上线 → 客户端读取
  + `Render()` → 列表 → 下线 → 删除。
- `SkillSamples` ——在内存中构建最小 skill ZIP（含 YAML front matter 的
  `SKILL.md`）→ 上传 → 提交审核 → **force-publish** → 上线 → 按版本下载
  （校验 md5 与条目结构）→ 下线 → 删除。
- `AgentSpecSamples` ——在内存中构建最小 AgentSpec ZIP（`manifest.json` +
  `AGENTS.md`）→ 上传（落库版本 `0.0.1`）→ 提交审核 → **force-publish** →
  上线 → 按版本与 `latest` 标签回读 → 下线 → 删除。
- `Integration/PromptChatIntegrationSample` ——发布一个一次性 Prompt，将其
  模板渲染为 system message 交给 `IChatClient`（echo 客户端）——注册中心
  模板到聊天请求的闭环。
- `Integration/McpChatIntegrationSample` ——进程内托管真实 MCP 服务器
  （临时端口上的 Streamable HTTP），发布到 MCP 注册中心并注册真实端点，
  再从 **Nacos** 回读端点，用 `ModelContextProtocol.McpClient` 拨号，在线
  调用 `get_weather`，然后把发现的工具接入 `IChatClient`
  （`UseFunctionInvocation`）。
- `Integration/AgentsAISamples` ——三个子演示，各自独立出结果：
  1. **MCP** ——同样的托管/发布/发现/拨号闭环，但把发现的工具接入运行在
     echo 客户端上的 Microsoft.Agents.AI `ChatClientAgent`。
  2. **Skill** ——内联、代码定义的 `AgentInlineSkill` 通过
     `AgentSkillsProviderBuilder` 提供给 `ChatClientAgent`。完全离线：不访问
     Nacos，也不需要模型凭据。
  3. **A2A** ——发布一次性卡片，从注册中心回读卡片 JSON，将存储的 0.3.7
     形态映射到 A2A 1.0 协议卡片，并由此构建 `AIAgent`（`AsAIAgent`）与
     `A2AClientFactory` 客户端，打印传输元数据。没有 A2A 在线往返：这需要
     托管的 A2A agent，而托管包（`Microsoft.Agents.AI.Hosting.A2A*`）不在
     本示例锁定的包集合内——代码注释中已说明。

### force-publish 与 AI pipeline 插件

原生 Nacos 3.2.4 自带默认 AI pipeline 插件
（`nacos-default-ai-pipeline-plugin-3.2.4.jar`），把普通发布拦在一道审批
之后，而原版服务器没有暴露任何审批 API——直接调用 publish 会失败并返回
`Pipeline not approved`（HTTP 400）。因此 Prompt、Skill、AgentSpec 章节改用
`ForcePublish*Async`（[since=3.2.1]），即官方文档中的无人值守旁路，效果
与 `reviewing → online` 相同。

### 自包含的集成章节

`PromptChat`、`McpChat`、`AgentsAI` 不依赖其他章节创建的产物：各自发布
带时间戳的 Prompt、MCP 服务器或 Agent Card，并在 `finally` 中清理（逐步
保护，清理失败不会掩盖运行结果）。它们的 `MCP`/`A2A` 注册中心读取即
"发现"时刻——端点或卡片取自 Nacos 的返回值，而不是本地变量。

## 配置

`Program.cs` 从输出目录读取 `appsettings.json`。可直接编辑该文件，或用
前缀为 `REDNB_NACOS_` 的环境变量覆盖（嵌套键用 `__`，如
`REDNB_NACOS_Nacos__ServerAddresses`）。

| 键 | 默认值 | 说明 |
|---|---|---|
| `Nacos:ServerAddresses` | `localhost:8848` | 逗号分隔的 `host:port` 列表 |
| `Nacos:GrpcPortOffset` | `1000` | gRPC 端口 = HTTP 端口 + 偏移量（8848 → 9848） |
| `Nacos:Username` / `Password` | `nacos` / `nacos` | |
| `Nacos:Namespace` | （空 = `public`） | |
| `ChatProvider` | `echo` | `echo` 或 `openai`，由 `Chat/ChatClientFactory.cs` 解析；未知值或缺少 `OpenAI:ApiKey` 时回退到 echo 并告警（运行时各章节直接构造 `EchoChatClient`，`ChatProvider`/`OpenAI:*` 仅由工厂读取——配置驱动路径由 `tests/RedNb.Nacos.Sample.AI.Tests` 覆盖） |
| `OpenAI:ApiKey` | （空） | 仅 `openai` 分支读取 |
| `OpenAI:Model` | `gpt-4o-mini` | |
| `Logging:LogLevel:Default` | `Information` | `trace`/`debug`/`information`/`warning`/`error` |

启动日志打印配置的 provider，例如 `ChatProvider: echo`（`ChatClientFactory`
的回退告警只出现在配置驱动路径上）。

`openai` 分支仅在定义了 `OPENAI_PROVIDER` 时编译：

```bash
dotnet run --project samples/RedNb.Nacos.Sample.AI -p:DefineConstants=OPENAI_PROVIDER
```

默认构建保持精简——`Microsoft.Extensions.AI.OpenAI` 包引用由该常量门控，
运行时只使用 `EchoChatClient`。集成章节直接构造 `EchoChatClient`，因此
示例运行本身永远不需要凭据；`ChatClientFactory` 是配置驱动路径（由
`tests/RedNb.Nacos.Sample.AI.Tests` 覆盖）。

## 通道指南

| 操作 | 通道 |
|---|---|
| Get / List / Release / Upload / Subscribe（MCP、A2A、Prompt、Skill、AgentSpec） | HTTP |
| `RegisterAgentEndpointAsync` / `DeregisterAgentEndpointAsync` / `RegisterAgentEndpointsAsync` | **仅 gRPC** |
| `RegisterMcpServerEndpointAsync` / `DeregisterMcpServerEndpointAsync` | **仅 gRPC** |
| MCP 工具 CRUD（`RefreshMcpToolAsync`、`GetMcpToolAsync`、`DeleteMcpToolAsync`、`UpdateMcpToolAsync`） | Nacos 3.2.4 未开放——示例跳过 |
| Prompt / Skill / AgentSpec 生命周期 | 仅 HTTP（gRPC 的 `IAiService` 将其委托给同一 HTTP 子服务） |

HTTP 通道的 `IAiService` 对仅 gRPC 的操作抛出 `NacosException(ServerError)`；
示例把这些调用交给 gRPC 通道的 `IAiService`。

## 排障

- **"Nacos connection failed"** ——确认 Nacos 3.x 服务器运行在
  `localhost:8848`（或修改 `Nacos:ServerAddresses`），gRPC 端口（默认
  `9848`）可达，且账号密码与 `Nacos:Username`/`Nacos:Password` 一致。
- **集成章节报告 `Failed`** ——这些章节自包含（自行发布/释放产物），失败
  意味着注册中心或链路有问题，而不是缺少前置产物；不存在"先跑 CRUD
  章节"的顺序要求。在 Nacos 3.2.4 上任何章节都不应报告 `Skipped`——示例
  未调用 MCP 工具 CRUD，全部 `Ok` 的汇总才是预期结果。
- **`Skill: Failed — DownloadSkillZipByVersion returned null after publish`** ——
  发布后立即下载时服务端的读后写延迟（大约每 4 次运行出现 1 次）：发布
  本身已成功，稍后同一产物可以正常下载。章节如实上报 `Failed`，重跑即
  干净；没有客户端规避手段（示例按设计不重试）。
- **自己的 publish 调用报 `Pipeline not approved`（HTTP 400）** ——3.2.4 的
  AI pipeline 插件拦截了普通发布；改用 force-publish 路由
  （[since=3.2.1]），就像本示例那样。
- **MCP 传输** ——示例使用 Streamable HTTP（`AiConstants.Mcp.ProtocolStreamable`
  / `HttpTransportMode.StreamableHttp`）。只支持 SSE 的服务器需要在
  `McpChatIntegrationSample` / `AgentsAISamples` 中改用其他传输模式。

## 手工验收清单

针对真实 Nacos 3.2.4 运行示例后逐项核对：

```
[ ] Mcp：发布 → 列表 → 详情 → 订阅/退订 → gRPC 端点注册/注销 → 删除
[ ] A2a：发布卡片 → gRPC 批量端点注册 → 列表 → 详情 → 订阅/退订 → gRPC 注销 → 删除
[ ] Prompt：草稿 → 提交审核 → force-publish → 上线 → 读取 + 渲染 → 列表 → 下线 → 删除
[ ] Skill：上传 ZIP → 提交审核 → force-publish → 上线 → 下载（SKILL.md）→ 下线 → 删除
[ ] AgentSpec：上传 ZIP（0.0.1）→ 提交审核 → force-publish → 上线 → 按版本 + 标签读取 → 下线 → 删除
[ ] PromptChat：自建 Prompt → 渲染 → echo system-message + 用户回合闭环 → 清理
[ ] McpChat：托管 MCP 服务器 → 发布 → gRPC 端点注册 → Nacos 回读 → MCP 握手 → 在线调用 get_weather → 工具接入 IChatClient
[ ] AgentsAI：MCP agent（注册中心工具 → ChatClientAgent）、离线 Skill 子演示、A2A 卡片回读 → AsAIAgent + A2AClientFactory
[ ] 汇总块八个章节全部 Ok，进程退出码 0
```

任何一项无法勾选都视为回归——发布前必须修复。
