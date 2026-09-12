# 迁移到 2.0.0

服务器验收基线为 Nacos 3.2.4，停止 2.x 兼容。2.0.0 是源码版本，是否已经发布到 NuGet 以实际发布记录为准。

## 命名和入口

| 旧命名空间/入口 | 新入口 |
| --- | --- |
| RedNb.Nacos.Core.* | RedNb.Nacos.* |
| RedNb.Nacos.Client | RedNb.Nacos.Http |
| RedNb.Nacos.Client.Http | RedNb.Nacos.Http.Transport |
| RedNb.Nacos.GrpcClient.* | RedNb.Nacos.Grpc.* |
| Ai.Model / Model.Skills | Ai.Models / Models.Skill |
| Maintainer 命名空间 | Administration |
| Grpc 包内 DI 扩展 | RedNb.Nacos.DependencyInjection 包 |
| AddNacos 默认 HTTP | AddNacos 默认 gRPC Config/Naming |
| 分别组合 HTTP/gRPC AI | NacosAiClient 或 AddNacosAi |

接口中的 JSON 属性和 gRPC TYPE 继续以服务器协议为准，不因 C# 命名空间改动而改变。底层客户端原来的 SendStreamRequest[WithResponse]Async 更名为 SendRequest[WithResponse]Async，准确表达 unary 请求。

## 行为变化

- 普通 GetConfigAsync 不再永久返回旧缓存。不存在会清理旧值；权限拒绝不会回退到旧快照。网络故障快照按服务器、命名空间、用户隔离。
- HTTP 配置监听/Fuzzy Watch 明确抛 NotSupportedException；迁移至 gRPC。ASP.NET Core 提供器已使用 gRPC，并重建键集合处理删除。
- 默认命名空间统一为 public（配置接口的空 tenant 由服务器规范化）。Naming 的模糊初始快照与普通注册使用同一空间。
- 不再支持默认插件的虚构 AK/SK 登录分支；有用户名密码时使用默认认证，没有对应认证插件时明确拒绝 AK/SK-only。
- 旧 Maintainer 操作停止发送旧协议并报明确迁移错误。namespace CRUD 使用 IAdministrationService；Config/Naming 运行时能力使用各自接口。没有重写的管理功能不标记已支持。
- 原生锁使用 LockOperationRequest 和 NACOS_LOCK；HTTP 锁操作不支持，Reentrant=true 明确拒绝。租约不会自动重获；调用方必须按失锁语义设计。
- All 包不再携带空 DLL。直接引用聚合包里的空程序集的代码应移除该依赖。
- 删除核心包中未接入运行链的旧 Redo 类型，保留并修复 gRPC 内部恢复实现。

完整支持范围与验证状态见 [能力矩阵](CAPABILITIES.md)；历史设计移至 archive，不是当前执行规范。
