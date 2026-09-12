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

- **0** ——所有执行章节均未失败。
- **2** ——启动失败、没有执行结果，或任意章节报告 Failed。

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
