# RedNb.Nacos

面向 Nacos 3.2.4 的 .NET 8 / .NET 10 SDK。配置和服务发现默认使用 gRPC；AI 与管理操作按服务器实际能力选择 HTTP Client、Admin 或 Console 通道。

当前源码版本为 **2.0.0**，包含命名空间和默认实现的破坏性调整。**不支持 Nacos 2.x**。其他 3.x 版本没有自动获得兼容承诺，请查看[能力矩阵](docs/CAPABILITIES.md)和[迁移说明](docs/MIGRATION.md)。

## 选择类库

| 包 | 用途 |
| --- | --- |
| RedNb.Nacos | 接口、模型、选项和公共功能 |
| RedNb.Nacos.Http | 显式 HTTP 客户端和 v3 管理接口 |
| RedNb.Nacos.Grpc | 配置、服务发现、原生锁和 AI 组合客户端 |
| RedNb.Nacos.DependencyInjection | 推荐的 DI 注册入口、命名客户端 |
| RedNb.Nacos.AspNetCore | IConfiguration 热更新、健康检查、服务注册 |
| RedNb.Nacos.All | 纯依赖聚合包，方便体验全部组件 |

## 推荐用法

```csharp
using Microsoft.Extensions.DependencyInjection;
using RedNb.Nacos.DependencyInjection;
using RedNb.Nacos.Config;

var services = new ServiceCollection();
services.AddNacos(options =>
{
    options.ServerAddresses = Environment.GetEnvironmentVariable("NACOS_SERVER") ?? "localhost:8848";
    options.ConsoleAddresses = Environment.GetEnvironmentVariable("NACOS_CONSOLE") ?? "localhost:8080";
    options.Username = Environment.GetEnvironmentVariable("NACOS_USERNAME");
    options.Password = Environment.GetEnvironmentVariable("NACOS_PASSWORD");
});
await using var provider = services.BuildServiceProvider();
var config = provider.GetRequiredService<IConfigService>();
var content = await config.GetConfigAsync("app-config", "DEFAULT_GROUP", 5000);
```

只需配置或服务发现时，可使用 `AddNacosConfig` / `AddNacosNaming`。AI 通过 `AddNacosAi` 单独启用，命名空间管理通过 `AddNacosAdministration` 单独启用。各模块延迟创建，不会因为注册基础服务就连接 AI 或锁模块。

多个 Nacos 环境使用 `services.AddNacos("environment-name", configure)`，再通过 `GetRequiredKeyedService<IConfigService>("environment-name")` 获取。不同名称的客户端和缓存相互隔离。

不使用 DI 时可用 `NacosGrpcFactory.CreateConfigServiceAsync(options)`、`CreateNamingServiceAsync(options)`；AI 推荐 `new NacosAiClient(options)`，不需要调用者自行组合两种通道。具体使用见 [samples](samples/README.md)。

## 环境和验证

- API 端口默认 8848，客户端 gRPC 默认 9848，控制台默认 8080；显式映射不同端口时请配置地址和 GrpcPortOffset。
- 默认鉴权使用用户名密码/token。默认 Nacos 插件不提供本 SDK 旧版本假设的 AK/SK 登录接口。
- [单机 Docker 部署](deploy/docker-compose/README.md)
- [构建和集成测试](docs/ENVIRONMENT_BOOTSTRAP.md)
- [完整改进计划](docs/IMPROVEMENT_PLAN.md)
- [测试报告](docs/TEST_REPORT.md)
- [文档索引](docs/README.md)

## 能力边界

纯 HTTP 配置监听和 Fuzzy Watch 会明确报不支持；使用推荐 gRPC 入口。MCP/Agent Console 操作在 3.2.4 上限定 public 空间。旧 Maintainer 接口保留迁移错误提示，不再发送 v1/v2 请求；已实现的新管理功能以能力矩阵为准。原生锁是服务器互斥原语，不提供 fencing 或可重入语义。

源码按 Apache-2.0 发布，见 [LICENSE](LICENSE)。感谢 [Nacos](https://github.com/alibaba/nacos) 社区。
