# RedNb.Nacos 2.0.0 改进与集成测试报告

日期：2026-09-13（Asia/Shanghai）
实现提交：`e5e9cb5a7ec2e443b1ae8062de09dd9387f76f3a`
原始基线：`c4a8959`（合并 PR #7）。报告和当前说明文档可位于后续文档提交。

## 1. 验收结论

本次范围内验收通过：**本地 1,356 个测试实例全部通过；云端 110 个通过，2 个按设计跳过；0 个失败**。这些是按目标框架/环境执行的测试实例，不是 1,466 个互不重复的测试方法。

云端跳过的是同一个本地容器重启测试在 .NET 8 / .NET 10 下的两个实例。两者均已在本地执行通过，且验证了 Config/Naming/MCP 后端自动恢复；没有重启或中断云端服务器。

另外：AI 综合示例 8 个模块全部 Ok；6 个 NuGet 包内容校验通过；独立 .NET 8 和 .NET 10 项目安装聚合包并解析默认客户端成功。

本报告不将“API 数量”或旧文档的“100% 完成”当作验证证据。完整能力边界见 [CAPABILITIES.md](CAPABILITIES.md)，方法路由见 [AI_API_ROUTES.md](AI_API_ROUTES.md)。

## 2. 环境

- 本机：Windows / PowerShell，.NET SDK 10.0.201；.NET 8 runtime 8.0.25、.NET 10 runtime 10.0.5。
- 本地服务：Docker Desktop，独立 `rednb-nacos-324-test` 容器，Nacos 3.2.4，standalone + 内置数据库，启用鉴权。
- 云端：用户提供的 Nacos 环境；Console server/state 实际返回 version=3.2.4，readiness HTTP 200，登录成功，HTTP/gRPC 功能矩阵实际执行。
- 本地和云端地址/密码通过进程环境变量注入。真实凭据未写入源文件、本文、CSV 或 JSON。
- 云端 Config/Naming 测试使用新建独立命名空间，执行后删除；AI Console public 资源使用本次随机名称逐项清理。命名空间管理测试只删除自己创建的空间。

## 3. 最终执行矩阵

| 环境 | 项目 | 框架 | 通过 | 失败 | 跳过 |
| --- | --- | --- | ---: | ---: | ---: |
| local | rednb.nacos.tests | net8.0 | 388 | 0 | 0 |
| local | rednb.nacos.tests | net10.0 | 388 | 0 | 0 |
| local | rednb.nacos.http.tests | net8.0 | 144 | 0 | 0 |
| local | rednb.nacos.http.tests | net10.0 | 144 | 0 | 0 |
| local | rednb.nacos.integrationtests | net8.0 | 56 | 0 | 0 |
| local | rednb.nacos.integrationtests | net10.0 | 56 | 0 | 0 |
| local | rednb.nacos.grpc.tests | net8.0 | 84 | 0 | 0 |
| local | rednb.nacos.grpc.tests | net10.0 | 84 | 0 | 0 |
| local | rednb.nacos.sample.ai.tests | net10.0 | 12 | 0 | 0 |
| cloud | rednb.nacos.integrationtests | net8.0 | 55 | 0 | 1 |
| cloud | rednb.nacos.integrationtests | net10.0 | 55 | 0 | 1 |

机器摘要：[TEST_SUMMARY.json](TEST_SUMMARY.json)。
逐项清单：[TEST_CASES.csv](TEST_CASES.csv)，包含环境、项目、框架、测试名、结果和耗时。

完整 TRX 保留在本机 `artifacts/test-results/acceptance-local/` 与 `acceptance-cloud/`，不入 Git。原始失败轮次也保留在 artifacts/test-results 下，最终统计只读取验收目录，未混入旧结果。

## 4. 核心行为验证

| 行为 | 主要证据 |
| --- | --- |
| 普通读取更新 | ConfigReliabilityTests.IndependentReaderObservesUpdateAndDelete；两个独立客户端 |
| 外部删除 | ListenerReceivesExternalDeletion；清理缓存并发送删除通知 |
| ASP.NET Core 热更新 | AspNetConfigurationReloadsAndRemovesKeys；新值、移除键、整个配置删除 |
| CAS | ConfigContractTests.CasRejectsStaleMd5WithoutOverwritingContent |
| 调用取消 | CallerCancellationDoesNotReturnCachedSuccess |
| 拒绝访问不回退 | ConfigConsistencyTests.PermissionDeniedMustNotFallBackToCachedContent |
| Naming 和 Config 模糊监听 | FuzzyWatchTests；初始快照和后续新增，取消订阅 |
| 断线恢复 | ConnectionRecoveryTests；原客户端不主动查询，本地重启后普通实例和 MCP 后端恢复，配置继续推送 |
| namespace CRUD/隔离 | AdministrationTests.NamespaceLifecycleAndIsolation |
| AI 跨通道 facade | AiFacadeTests；HTTP 创建/读取/删除与 gRPC 批量端点登记/注销 |
| MCP/A2A 订阅 | 恢复两个原先为空且 Skip 的测试，实际更新版本并等待轮询回调 |
| Prompt | 草稿→审核→强制发布→标签/版本查询→变量渲染→下线/删除 |
| Skill | ZIP 上传→发布→下载 ZIP→验证 SKILL.md 与 MD5→清理 |
| AgentSpec | 上传 manifest + AGENTS.md→发布→读取 manifest 与资源→清理 |
| 原生 mutex | 两个独立客户端竞争、未持有客户端不能通过 SDK 释放、持有者释放、另一客户端重新获取 |
| DI | 默认 gRPC、配置委托一次执行、命名客户端独立、AI 窄接口同实例、Shutdown 关闭整个 facade |

## 5. 实施过程中发现并修复的问题

1. gRPC 普通配置读取永久使用旧缓存；已改为新鲜读取并隔离故障快照。
2. HTTP 监听已失去执行链，却返回成功；纯 HTTP 明确拒绝，推荐入口和 ASP.NET Core 迁移到 gRPC。
3. 监听批量响应的 changedConfigs 未消费；已处理订阅竞争窗口及校正。
4. 外部删除配置不能清理旧值；已增加不存在分支和删除通知。
5. 断线后后台任务跳过工作，Redo 从未被调用；已增加后台重连和注册/订阅重放，失败保留状态重试。
6. Naming 差异计算误用过期缓存；保留独立旧快照，并检测实例健康/权重/元数据变化。
7. 模糊监听请求字段与 3.2.4 不符；按 groupKeyPattern、watchType 和同步批次实现。
8. Naming 默认 namespace 使用空字符串，而模糊索引使用 public，导致初始列表为空；已统一。
9. AK/SK 登录分支并不存在于默认鉴权插件；已拒绝该假设，保留用户名密码/token。
10. MCP/A2A 已有资源再次发布仍发送创建 POST；已区分创建和更新，轮询更新测试通过。
11. AI 示例测试的 IsTestProject 为空，dotnet test 可零执行成功；已修复，并加入零发现校验。
12. AI facade Shutdown 原来只关闭 HTTP；已关闭整个组合客户端并加入回归测试。
13. 旧锁请求类型、字段与服务器不符；按 LockOperationRequest/LockOperationEnum/expiredTime/NACOS_LOCK 重写，删除伪本地可重入逻辑。
14. AgentSpec 旧说明把 AGENTS.md 当主 Content；实际 Content 是 manifest，AGENTS.md 位于 Resource，已据实验证。
15. 旧集成测试仍从 HTTP 测配置监听；已迁移到推荐 gRPC 入口。
16. 并行启动多个测试宿主时，本地 WireMock 出现资源争用超时；已限定 HTTP 测试并发、按项目及框架顺序执行，完整重跑通过。
17. 清理误入库的 project.assets.json、空 All 占位源码、未接入运行链的另一套 Redo；历史计划归档而非直接删除决策来源。

## 6. 构建、包和示例

- Debug / Release 完整构建均为 0 错误。全量构建仍有 XML 文档缺失和旧接口 Obsolete 等编译警告；本次没有将这些警告描述为已经清零。
- NuGet 集中版本管理启用；测试工具 WireMock 与示例 Swagger 依赖升级后，`dotnet list package --vulnerable --include-transitive` 对全部 14 个项目未报告已知漏洞。该结果受当前源的漏洞数据库覆盖范围限制。
- 6 个 2.0.0 nupkg 已验证版本、作者、Apache-2.0、README、net8/net10 DLL/XML 文档。All 是纯依赖包，不包含空 DLL。
- 在仓库外独立项目安装本地 nupkg，.NET 8 / .NET 10 均输出：`Installed aggregate package resolves the default gRPC client: 2.0.0.0`。
- AI 示例：Mcp、A2a、Prompt、Skill、AgentSpec、PromptChat、McpChat、AgentsAI 均为 Ok，退出码 0。使用 Echo 后端；未调用付费外部 LLM。
- 示例退出码已改为任意模块 Failed 就返回 2，避免部分失败仍被当作成功。
- GitHub CI 文件覆盖 master push/PR，使用临时 Nacos、零测试发现检查和包校验。工作流已落地；本报告的执行证据来自本地命令，未宣称 GitHub Actions 已远程运行。

## 7. 重现命令

```powershell
dotnet restore RedNb.Nacos.sln
dotnet build RedNb.Nacos.sln -c Release
dotnet test RedNb.Nacos.sln --no-build -c Release -m:1 -p:TestTfmsInParallel=false --logger trx --results-directory artifacts/test-results/new-run
python scripts/verify-test-results.py artifacts/test-results/new-run --minimum-runs 9
dotnet pack RedNb.Nacos.sln --no-build -c Release -o artifacts/packages
python scripts/verify-packages.py artifacts/packages
```

云端变量和故障注入的启用方式见 [ENVIRONMENT_BOOTSTRAP.md](ENVIRONMENT_BOOTSTRAP.md)。不要把密码粘贴到仓库中的脚本。重启测试仅允许本地地址和 rednb-nacos-* 容器名。

## 8. 明确未包含的认证范围

- 不兼容 Nacos 2.x；其他 3.x 小版本没有被本次测试自动认证。
- TLS、自定义反向代理、多节点集群故障切换、长期压力/极端并发及所有 AI 参数组合没有完成专门实测。
- 原生 mutex 没有 fencing 或服务端持有者校验；SDK 的本地租约检查不能升级成跨进程所有权保证。
- 新管理服务覆盖 namespace 生命周期。旧 Maintainer 的全部运维操作未重写，不再宣称完整支持。
- 默认 pipeline 的常规发布需要服务端审批；测试只对临时资源使用 ForcePublish，没有伪造审批结果。
- 未向 NuGet 发布、未推送远端、未创建 GitHub Release；本轮交付为本地源码、提交、包与测试报告。
