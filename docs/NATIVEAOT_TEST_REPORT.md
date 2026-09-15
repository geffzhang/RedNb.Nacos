# 2.1.0 NativeAOT 改进验收报告

**结论：约定验收任务已完成。Windows/Linux 原生源码与 NuGet 消费者、两个框架的重启恢复均通过，master 已推送，GitHub Actions 11 项检查全部成功。验收结束后，经用户另行授权，2.1.0 已完成 Release 与 NuGet 发布，见 [发布记录](RELEASE_2.1.0.md)。**

日期：2026-09-15。基线：master / 7b7b471；最终代码提交：a2f7a2d。最终代码的 [CI 运行 34943737938](https://github.com/yinghongzhen/RedNb.Nacos/actions/runs/34943737938) 全部成功。

## 继续执行记录（2026-09-15）

- 再次检查：工作区干净，远端 master 仍为 7b7b471，MSVC C++ 组件仍缺失。
- 已通过管理员安装入口尝试向现有 Visual Studio Enterprise 2026 添加 `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`，没有使用强制关闭或自动重启选项。
- 安装器内部预检查报告 `VSProcessesRunning`（Visual Studio / DevHub 仍运行），以 Cancel 结束。启动器退出码 0 不代表组件安装成功；`vswhere` 复查仍未找到组件。
- 已复跑 .NET 10 / win-x64 SDK 发布，C# 编译完成后在原生链接阶段失败：`Platform linker not found`。证据：`artifacts/aot-210/windows-resume-net10-sdk.log`。
- 用户关闭 Visual Studio 后，重新安装成功，MSVC 版本 14.50.35717。Windows 的 8 次原生应用验收和两次原生恢复均通过，之前的安装/链接阻塞已解除。

## 提交与改动

| 提交 | 内容 |
|---|---|
| 06f3cc8 | JSON 数值、数组/字典、YAML 合并优先级与循环保护 |
| d1384cc | 客户端 JsonTypeInfoResolver 实际传递、用户类型 JIT 回退、泛型缓存元数据重载 |
| 15f9956 | 修复 .NET 8 嵌套序列化快速路径绕过用户 Context |
| 3af565e | ExpandoObject、自定义集合转换器、严格模式嵌套容器回归 |
| 83a47b2 | 明确 NativeAOT 运行时保护分支、磁盘缓存显式元数据、旧缓存兼容测试 |
| a575830 | HTTP/gRPC 客户端标识跟随程序集版本；HTTP 测试就绪检查 |
| 2a83133 | 原生 SDK/Web、HTTP/2 ACK 对端、恢复驱动、NuGet 消费者、CI 与 2.1.0 发布准备 |
| 64a7313 / 2e6ec44 | 补齐官方 Windows JVM 参数，在同一 CI shell 内管理服务器与验收生命周期 |
| a95c671 | 修复 AI 管理路由、Prompt 版本分页及 AgentSpec 数字时间戳读取，保留字符串 API |
| a2f7a2d | 根据精确版本状态处理自动审核上线与手工发布，完善实际管理接口验收 |

报告、迁移说明、能力边界和个人配置清理由后续文档提交收尾。历史设计保留在 archive。

## 环境与隔离

- 普通 JIT / 托管严格模式：Windows x64，.NET SDK 10.0.201，分别针对 net8.0 / net10.0。
- Linux 原生：Ubuntu 24.04 工具链容器，SDK 10.0.401；目标 net8.0 / net10.0，RID linux-x64；实际运行 ELF 二进制。
- 服务器：本地 Docker Nacos 3.2.4，独立容器 `rednb-nacos-324-test`，启用默认鉴权。
- 配置/服务使用随机测试标识；AI public 资源使用独立随机名称并清理。故障注入仅重启该本地测试容器，没有重启云端或其他业务容器。
- Windows MSVC x64/x86 14.50.35717 已安装；实际运行 PE x64 原生可执行文件，未以托管运行替代。

## 普通 JIT 结果

| 测试项目 | .NET 8 | .NET 10 |
|---|---:|---:|
| Core | 434 通过 | 434 通过 |
| HTTP | 156 通过 | 156 通过 |
| gRPC / DI | 98 通过 | 98 通过 |
| Nacos 集成 | 56 通过 | 56 通过 |
| AI 示例单测 | 不适用 | 12 通过 |

最终 CI 合计 **1500 个框架内用例全部通过，0 失败、0 跳过**，共 9 个非空测试运行。此前本地基线为 1494 个用例，主全量运行跳过的两个重启用例随后独立运行通过；新增 AI 管理契约回归使两个框架各增加 3 个用例。最终 CI 已重新执行全部用例。

本地证据：`artifacts/aot-210/release-jit`、`release-jit-recovery`、`ai-contract-final`。最终 CI 证据：`artifacts/aot-210/ci-final-tests.log`、`ci-final-evidence.json` 及上述 GitHub 运行链接。

## 应用与包消费者矩阵

托管严格模式确认 `dynamic=True, reflection=False`；原生模式确认 `dynamic=False, reflection=False`。包消费者同时核对还原图中 SDK 引用全部为 package，并核对安装包与输入 nupkg 的 SHA-256。

| 框架 / 应用 | Windows 托管严格 / 源码 | Windows 托管严格 / NuGet | Linux 原生 / 源码 | Linux 原生 / NuGet | Windows 原生 / 源码和 NuGet |
|---|---|---|---|---|---|
| .NET 8 SDK | 通过 | 通过 | 通过 | 通过 | 通过 |
| .NET 8 Minimal API | 通过 | 通过 | 通过 | 通过 | 通过 |
| .NET 10 SDK | 通过 | 通过 | 通过 | 通过 | 通过 |
| .NET 10 Minimal API | 通过 | 通过 | 通过 | 通过 | 通过 |

共 24 次应用运行通过，其中 16 次为 Windows/Linux 原生源码与包消费者，8 次为托管禁用反射运行。各次日志、运行模式、原生二进制及包哈希见 [机器可读结果](NATIVEAOT_TEST_SUMMARY.json)。

机器结果保留本地原始产物哈希；CI 收尾期间又修复了管理接口契约，最终提交的完整原生源码/包矩阵以链接 CI 的重新执行为准，不将旧本地产物冒充最终提交的二进制。

## 实际验证内容

- 数值：ulong.MaxValue、double.MaxValue、极小浮点数、decimal 边界；数组、字节数组 Base64、嵌套字典、ExpandoObject、自定义集合转换器及循环失败。
- YAML：显式值不受书写顺序影响，合并序列前项优先，引号 `"<<"`、null/quoted-null、数组、别名、循环保护。
- Context：SDK 模型不可被用户替换；两个实际 AI 客户端使用不同字段映射，交替写入后各自保持语义；JIT 未注册用户类型成功，严格/原生未注册类型明确失败，注册后的请求和响应成功。
- 缓存：自定义 JsonTypeInfo<T> 文件往返；SDK Naming 故障缓存读写；旧缓存大小写与缩进格式回归。
- 配置：发布/读取/修改/删除、CAS 成功及过期拒绝、监听/删除通知、取消、模糊初始同步。
- Naming / 锁：注册、发现、模糊初始同步、注销；双客户端锁竞争、释放和再次获取，嵌套自定义锁参数。
- AI：Prompt、Skill、AgentSpec 生命周期；A2A 元数据和端点；MCP 发布、注册、重启恢复、注销与删除。
- Web：真实 HTTP 请求、DI、命名客户端隔离、配置热更新、删除键/删除配置、健康检查、正常关闭。
- ACK：AotWeb 内独立的 HTTP/2 双向流对端分别发出带/不带 requestId 的推送，实际读到并确认 ACK；两个框架、源码/包、托管/原生均通过。正常 Nacos 监听和模糊同步另行实测，不将受控缺失 ID 输入冒充真实服务器常规行为。

## 原生恢复

| 框架 / RID | 配置监听 | Naming 注册 | MCP 后端 |
|---|---|---|---|
| .NET 8 / linux-x64 | 通过 | 通过 | 通过 |
| .NET 10 / linux-x64 | 通过 | 通过 | 通过 |
| .NET 8 / win-x64 | 通过 | 通过 | 通过 |
| .NET 10 / win-x64 | 通过 | 通过 | 通过 |

驱动等待原生客户端准备完成，再重启本地 Nacos。恢复检查通过独立观察客户端和独立发布客户端进行，不向原客户端发业务请求来驱动重连。证据：`artifacts/aot-210/recovery/<RID>/<TFM>/recovery.log`。

## 发现的问题与修复证据

1. 首批 25 个数据正确性回归为 16 失败、9 通过，修复后全通过。后续集合回归又复现 2 个失败：ExpandoObject 写成键值对数组、自定义集合转换器被跳过；均已修复。
2. .NET 8 的源生成快速路径曾使用默认 Context 写嵌套锁参数，使已注册用户对象失败。SDK Context 固定为 Metadata 模式，新增回归及真实严格/原生链路通过。
3. .NET 10 原生 IL 扫描器未识别复合条件中的动态代码保护，严格门槛报 IL3050。拆分保护分支后通过，没有扩大警告抑制范围。
4. HTTP 冷启动超时曾多次出现，单纯降低并发未根治。现先用独立就绪请求完成 WireMock 管线初始化，再清空预热日志并开始测试，不延长生产 SDK 超时、不重试业务断言。修复后独立双框架复跑及最终全量双框架运行均为 153/153 通过。
5. 磁盘 Naming 缓存移除四处旧分析器抑制，改用显式 JsonTypeInfo。最终 NativeAOT publish 日志无 IL 警告/错误；保留的仅是 JIT 回退边界的局部注解（IL2026，以及 .NET 8 的 IL3050），没有全局 NoWarn。既有 XML 注释/废弃接口警告仍存在。
6. Windows CI 的 Java 进程在步骤切换后消失。补齐官方启动参数，并将启动、源码/包验收、清理合并到同一 shell 生命周期后，服务器启动和原生 Web 通过。
7. Windows 发行包的审核流水线会自动上线 Skill，重复 ForcePublish 被正确拒绝。验收改为检查精确版本是否 online，并处理检查后的上线竞争；offline 或其他错误仍失败。
8. 增加状态查询后，发现 Skill/AgentSpec 元数据与版本详情路由互换、Prompt 版本列表应读取分页信封、AgentSpec 时间戳可能为数字。已对照 3.2.4 官方控制器修正；字符串属性保留，数字时间戳转换为原始文本；新增回归和最终原生 CI 均通过。

## 发布准备与未完成项

- 六包 2.1.0 已本地生成，校验身份、双 TFM、版本、README、许可证、内部依赖版本；实际包消费者通过。
- 包校验器要求显式 `--version`。`nuget-publish.yml` 文件名保留，接收版本并核对 v 标签、六个 nupkg、GitHub SHA-256 摘要及包内版本。使用本地合成 Release 元数据验证正常摘要通过、篡改摘要被拒绝；没有调用真实发布。
- CI 已拆分普通回归、严格序列化、原生 SDK 和原生 Web；最终 11 项检查全部通过，包括 8 项原生框架/平台任务、2 项严格序列化和普通回归。
- **master 已正常推送，无强推；本报告完成时仅准备发布，后续另行授权的 2.1.0 正式发布已完成，详见发布记录。**

ARM64、macOS、Linux musl/其他发行版、TLS/代理、多节点集群、外部 LLM、所有 AI 插件/参数组合及压力测试未认证。Nacos 2.x 不支持，其他 3.x 版本未按本轮矩阵认证。现有 MVC/Swagger 示例不属于 NativeAOT 声明。
