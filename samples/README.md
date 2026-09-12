# 示例

- Sample.Console：最小 gRPC 配置读取/监听，使用 NACOS_SERVER / NACOS_USERNAME / NACOS_PASSWORD / NACOS_DATA_ID 环境变量。
- Sample.WebApi：ASP.NET Core 配置热更新、服务注册与健康检查；连接信息来自 Nacos 配置节（可用 Nacos__Password 等环境变量覆盖）。
- Sample.AI：详细 AI 生命周期和可选 AI 运行时集成；支持 REDNB_NACOS_Nacos__ServerAddresses 等环境变量。该示例单独使用 .NET 10，不是基础 SDK 的必需依赖。

最小 AI 用法是 new RedNb.Nacos.Grpc.Ai.NacosAiClient(options)，它按能力路由；需要研究底层通道时才分别创建 Http/Grpc 服务。真实凭据不写入示例配置。
