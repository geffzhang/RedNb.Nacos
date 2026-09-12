<div align="center">

# RedNb.Nacos

**A Modern .NET SDK for Nacos**

[![NuGet](https://img.shields.io/nuget/v/RedNb.Nacos.svg?style=flat-square)](https://www.nuget.org/packages/RedNb.Nacos)
[![NuGet Downloads](https://img.shields.io/nuget/dt/RedNb.Nacos.svg?style=flat-square)](https://www.nuget.org/packages/RedNb.Nacos)
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%2010.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-green.svg?style=flat-square)](LICENSE)
[![Nacos](https://img.shields.io/badge/Nacos-3.2%2B-00C7B7.svg?style=flat-square)](https://nacos.io/)
[![GitHub stars](https://img.shields.io/github/stars/redNb/RedNb.Nacos?style=flat-square)](https://github.com/redNb/RedNb.Nacos/stargazers)
[![GitHub issues](https://img.shields.io/github/issues/redNb/RedNb.Nacos?style=flat-square)](https://github.com/redNb/RedNb.Nacos/issues)

[English](README.en.md) | 简体中文

</div>

---

**RedNb.Nacos** 是一个功能完整的现代化 .NET Nacos 客户端 SDK，面向 Nacos 3.2+ 服务器，提供 **200+ 个 API 方法**，涵盖配置中心、服务发现、分布式锁、AI 服务（MCP/A2A）和运维管理等全部功能。

> 🎯 **为什么选择 RedNb.Nacos？**
> - 🆕 支持 **.NET 8.0** 和 **.NET 10.0**，采用最新语言特性
> - ✅ **API 完整度最高** - 200+ 个 API，覆盖 Nacos 全部功能
> - 🔄 完整支持 **Nacos 3.x** 新功能（Fuzzy Watch、AI Service、分布式锁）
> - 🛠️ 独有 **维护服务 API** - 命名空间、集群、客户端连接管理
> - 📦 模块化设计，按需引用，减少依赖

## ⚠️ Breaking: Nacos 3.2.0+ Required

This client (post-v1.x release) targets Nacos 3.2.0+ exclusively. v1/v2 HTTP
endpoints are no longer used; `Maintainer` and `Lock` interfaces are marked
`[Obsolete]` and will be removed in the next major version. See
`docs/MIGRATION.md` for upgrade details.

## ✨ 特性

| 特性 | 描述 |
|------|------|
| 🚀 **高性能** | 支持 HTTP 和 gRPC 两种通信协议 |
| 📦 **模块化设计** | 按需引用，灵活组合 |
| 🔄 **Nacos 3.2+ 专属** | 面向 v3 协议重写，支持 Fuzzy Watch、AI Service、分布式锁等新特性 |
| 🔒 **分布式锁** | 原生支持 Nacos 3.0 分布式锁功能 |
| 🤖 **AI 服务** | 支持 MCP、A2A、Prompt、Skill、AgentSpec 全量 AI Registry 资源 |
| 🛠️ **运维管理** | 完整的 Maintainer API，支持命名空间、集群、客户端管理 |
| 💉 **依赖注入** | 原生支持 Microsoft.Extensions.DependencyInjection |
| 🏗️ **ASP.NET Core 集成** | 配置提供程序、健康检查、服务自动注册 |
| ⚡ **异步优先** | 全异步 API 设计 |
| 📝 **强类型** | 完整的类型支持和 XML 文档 |

## 📦 NuGet 包

| 包名 | 描述 | NuGet |
|------|------|-------|
| `RedNb.Nacos` | 核心抽象层：接口、模型和常量定义 | [![NuGet](https://img.shields.io/nuget/v/RedNb.Nacos.svg?style=flat-square)](https://www.nuget.org/packages/RedNb.Nacos) |
| `RedNb.Nacos.Http` | HTTP 客户端实现 | [![NuGet](https://img.shields.io/nuget/v/RedNb.Nacos.Http.svg?style=flat-square)](https://www.nuget.org/packages/RedNb.Nacos.Http) |
| `RedNb.Nacos.Grpc` | gRPC 高性能客户端实现 | [![NuGet](https://img.shields.io/nuget/v/RedNb.Nacos.Grpc.svg?style=flat-square)](https://www.nuget.org/packages/RedNb.Nacos.Grpc) |
| `RedNb.Nacos.DependencyInjection` | 依赖注入扩展 | [![NuGet](https://img.shields.io/nuget/v/RedNb.Nacos.DependencyInjection.svg?style=flat-square)](https://www.nuget.org/packages/RedNb.Nacos.DependencyInjection) |
| `RedNb.Nacos.AspNetCore` | ASP.NET Core 集成 | [![NuGet](https://img.shields.io/nuget/v/RedNb.Nacos.AspNetCore.svg?style=flat-square)](https://www.nuget.org/packages/RedNb.Nacos.AspNetCore) |
| `RedNb.Nacos.All` | 全功能包（包含以上所有） | [![NuGet](https://img.shields.io/nuget/v/RedNb.Nacos.All.svg?style=flat-square)](https://www.nuget.org/packages/RedNb.Nacos.All) |

## 🚀 快速开始

### 安装

```bash
# 基础包（推荐）
dotnet add package RedNb.Nacos.Http

# 或全功能包
dotnet add package RedNb.Nacos.All

# ASP.NET Core 集成
dotnet add package RedNb.Nacos.AspNetCore
```

### 基础用法

#### 1. 直接使用工厂创建服务

```csharp
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;

// 配置选项
var options = new NacosClientOptions
{
    ServerAddresses = "localhost:8848",
    Username = "nacos",
    Password = "nacos",
    Namespace = "",
    DefaultTimeout = 5000
};

// 创建服务工厂
var factory = new NacosFactory();

// 获取各种服务
var configService = factory.CreateConfigService(options);
var namingService = factory.CreateNamingService(options);
var lockService = factory.CreateLockService(options);
var aiService = factory.CreateAiService(options);
var maintainerService = factory.CreateMaintainerService(options);
```

#### 2. 配置中心

```csharp
// 获取配置
var content = await configService.GetConfigAsync("app-config", "DEFAULT_GROUP", 5000);

// 发布配置
await configService.PublishConfigAsync("app-config", "DEFAULT_GROUP", jsonContent, ConfigType.Json);

// CAS 乐观锁发布
await configService.PublishConfigCasAsync("app-config", "DEFAULT_GROUP", content, "oldMd5");

// 监听配置变更
await configService.AddListenerAsync("app-config", "DEFAULT_GROUP", new MyConfigListener());

// 模糊监听（Nacos 3.0）- 监听所有以 "app-" 开头的配置
await configService.FuzzyWatchAsync("app-*", "DEFAULT_GROUP", myWatcher);
```

#### 3. 服务发现

```csharp
// 注册实例
var instance = new Instance
{
    Ip = "192.168.1.100",
    Port = 8080,
    Weight = 1.0,
    Healthy = true,
    Ephemeral = true,
    Metadata = new Dictionary<string, string>
    {
        { "version", "1.0.0" }
    }
};
await namingService.RegisterInstanceAsync("my-service", instance);

// 批量注册
await namingService.BatchRegisterInstanceAsync("my-service", "DEFAULT_GROUP", instances);

// 获取所有实例
var instances = await namingService.GetAllInstancesAsync("my-service");

// 选择一个健康实例（加权随机）
var selected = await namingService.SelectOneHealthyInstanceAsync("my-service");

// 订阅服务变更
await namingService.SubscribeAsync("my-service", evt =>
{
    Console.WriteLine($"Service changed: {evt.Instances?.Count} instances");
});

// 模糊监听（Nacos 3.0）
await namingService.FuzzyWatchAsync("*", "production-group", myServiceWatcher);

// 注销实例
await namingService.DeregisterInstanceAsync("my-service", instance);
```

#### 4. 分布式锁 (Nacos 3.0)

```csharp
// 创建锁实例
var lockInstance = LockInstance.Create("my-lock-key")
    .WithExpireTime(TimeSpan.FromSeconds(30))
    .WithOwner("client-1")
    .WithReentrant();

// 获取锁
var acquired = await lockService.LockAsync(lockInstance);
if (acquired)
{
    try
    {
        // 执行临界区代码
        await DoSomethingCritical();
    }
    finally
    {
        // 释放锁
        await lockService.UnlockAsync(lockInstance);
    }
}

// 或使用 TryLock 带超时
var success = await lockService.TryLockAsync(lockInstance, TimeSpan.FromSeconds(10));
```

#### 5. AI 服务 - MCP/A2A/Prompt/Skill/AgentSpec (Nacos 3.x)

```csharp
// aiService：HTTP 通道的 IAiService；grpcAi：gRPC 通道的 IAiService（NacosGrpcFactory.CreateAiService）

// === MCP 服务 ===
// 发布 MCP 服务器（参数顺序：serverSpec → toolSpec → endpointSpec）
await aiService.ReleaseMcpServerAsync(serverSpec, toolSpec, endpointSpec);

// 注册 MCP 端点（gRPC 专用：Nacos 3.2.4 的 HTTP IAiService 抛 NacosException(ServerError)）
await grpcAi.RegisterMcpServerEndpointAsync("my-mcp-server", "127.0.0.1", 9100, "1.0.0");

// 获取 MCP 服务器详情
var mcpServer = await aiService.GetMcpServerAsync("my-mcp-server");

// 订阅 MCP 服务器变更
await aiService.SubscribeMcpServerAsync("my-mcp-server", myMcpListener);

// === A2A 服务 ===
// 发布 Agent Card（另有 (agentCard, registrationType, setAsLatest) 重载）
await aiService.ReleaseAgentCardAsync(agentCard);

// 注册 Agent 端点（gRPC 专用：HTTP IAiService 抛 NacosException(ServerError)；
// transport 为字符串："JSONRPC" / "GRPC" / "HTTP+JSON"，见 AiConstants.A2a）
await grpcAi.RegisterAgentEndpointAsync("my-agent", "1.0.0", "127.0.0.1", 9200, AiConstants.A2a.TransportHttpJson);

// 批量注册 Agent 端点（gRPC 专用：单次 BatchAgentEndpointRequest 往返）
await grpcAi.RegisterAgentEndpointsAsync("my-agent", endpoints);

// 获取 Agent Card 详情
var agentCardDetail = await aiService.GetAgentCardAsync("my-agent");

// 列出所有 Agents
var agents = await aiService.ListAgentCardsAsync(pageNo: 1, pageSize: 20);

// === Prompt 服务 ===
// 获取 Prompt（支持版本与标签）
var prompt = await aiService.GetPromptAsync("code-review");
var stablePrompt = await aiService.GetPromptByLabelAsync("code-review", "stable");

// 渲染模板变量
var text = prompt!.Render(new Dictionary<string, string> { ["language"] = "C#" });

// 订阅 Prompt 变更（md5 条件请求 + 轮询）
await aiService.SubscribePromptAsync("code-review", new MyPromptListener());

// 草稿 → 提交 → 发布 → 上线 全生命周期管理
await aiService.CreatePromptDraftAsync("code-review", "1.1.0", "Review {{language}} code");
await aiService.SubmitPromptReviewAsync("code-review", "1.1.0");
await aiService.PublishPromptAsync("code-review", "1.1.0");
await aiService.OnlinePromptAsync("code-review", "1.1.0");

// === Skill 服务 ===
// 下载 Skill ZIP 包（返回 md5 与解析版本）
var package = await aiService.DownloadSkillZipByLabelAsync("doc-writer", "stable");

// 订阅 Skill 变更
await aiService.SubscribeSkillAsync("doc-writer", new MySkillListener());

// 上传与生命周期管理
await aiService.UploadSkillZipAsync(zipBytes, "doc-writer.zip");
await aiService.PublishSkillAsync("doc-writer", "1.0.0");
await aiService.OnlineSkillAsync("doc-writer", "1.0.0", scope: "public");

// === AgentSpec 服务 ===
// 获取 AgentSpec（支持版本与标签）
var agentSpec = await aiService.GetAgentSpecByLabelAsync("travel-agent", "stable");

// 订阅 AgentSpec 变更
await aiService.SubscribeAgentSpecAsync("travel-agent", new MyAgentSpecListener());

// 生命周期管理
await aiService.CreateAgentSpecDraftAsync(agentSpecDetail, targetVersion: "1.1.0");
await aiService.PublishAgentSpecAsync("travel-agent", "1.1.0");
await aiService.OnlineAgentSpecAsync("travel-agent", "1.1.0");
```

> **注**：Nacos 3.2.4 默认启用 AI pipeline 插件，普通发布会因 “Pipeline not approved”（HTTP 400）被拦截；无人值守运行请改用 `ForcePublishPromptAsync` / `ForcePublishSkillAsync` / `ForcePublishAgentSpecAsync` [since=3.2.1]。
>
> 📦 Full sample: see [`samples/RedNb.Nacos.Sample.AI/`](samples/RedNb.Nacos.Sample.AI/).

#### 6. 维护服务

```csharp
// === 命名空间管理 ===
var namespaces = await maintainerService.GetNamespacesAsync();
await maintainerService.CreateNamespaceAsync("new-ns", "New Namespace");
await maintainerService.UpdateNamespaceAsync("ns-id", "Updated Name", "Description");
await maintainerService.DeleteNamespaceAsync("ns-id");

// === 服务管理 ===
await maintainerService.CreateServiceAsync("my-service", "DEFAULT_GROUP", 0.5f, true, "metadata");
var services = await maintainerService.ListServicesAsync(1, 10, "DEFAULT_GROUP");
var serviceDetail = await maintainerService.GetServiceAsync("my-service", "DEFAULT_GROUP");
await maintainerService.DeleteServiceAsync("my-service", "DEFAULT_GROUP");

// === 实例管理 ===
await maintainerService.UpdateInstanceAsync("my-service", instance);
await maintainerService.UpdateInstanceHealthAsync("my-service", "192.168.1.100", 8080, true);
await maintainerService.BatchUpdateMetadataAsync("my-service", instances, metadata);

// === 配置管理 ===
await maintainerService.PublishConfigAsync("app-config", "DEFAULT_GROUP", content, ConfigType.Yaml);
var configs = await maintainerService.ListConfigsAsync(1, 10, "app", "DEFAULT_GROUP");
var history = await maintainerService.ListConfigHistoryAsync("app-config", "DEFAULT_GROUP", 1, 20);
await maintainerService.DeleteConfigsAsync(new[] { configId1, configId2 });

// === Beta/灰度配置 ===
await maintainerService.PublishBetaConfigAsync("app-config", "DEFAULT_GROUP", content, "192.168.1.*");
await maintainerService.StopBetaConfigAsync("app-config", "DEFAULT_GROUP");

// === 配置导入导出 ===
var exportData = await maintainerService.ExportConfigsAsync(new[] { configId1, configId2 });
await maintainerService.ImportConfigsAsync("DEFAULT_GROUP", policy, configData);
await maintainerService.CloneConfigsAsync(configIds, targetNamespaceId, policy);

// === 客户端连接管理 ===
var clients = await maintainerService.ListClientsAsync();
var clientDetail = await maintainerService.GetClientDetailAsync(clientId);
var subscriptions = await maintainerService.GetClientSubscribedServicesAsync(clientId);
var sdkStats = await maintainerService.GetSdkVersionStatisticsAsync();

// === 集群管理 ===
var members = await maintainerService.GetClusterMembersAsync();
var leader = await maintainerService.GetLeaderAsync();
var health = await maintainerService.HealthCheckAsync();
var metrics = await maintainerService.GetMetricsAsync();
```

### ASP.NET Core 集成

#### 1. 添加 Nacos 配置源

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加 Nacos 作为配置源
builder.Configuration.AddNacosConfiguration(source =>
{
    source.Options.ServerAddresses = "localhost:8848";
    source.Options.Username = "nacos";
    source.Options.Password = "nacos";
    source.ConfigItems.Add(new NacosConfigurationItem 
    { 
        DataId = "app-config", 
        Group = "DEFAULT_GROUP",
        ConfigType = ConfigType.Json
    });
    source.ConfigItems.Add(new NacosConfigurationItem 
    { 
        DataId = "db-config", 
        Group = "DEFAULT_GROUP",
        Optional = true  // 可选配置
    });
});
```

#### 2. 依赖注入

```csharp
// 注册 Nacos 服务
builder.Services.AddNacos(options =>
{
    options.ServerAddresses = "localhost:8848";
    options.Username = "nacos";
    options.Password = "nacos";
});

// 或只注册配置服务
builder.Services.AddNacosConfig(options => { /* ... */ });

// 或只注册命名服务
builder.Services.AddNacosNaming(options => { /* ... */ });

// 注册 AI 服务（IAiService，含 MCP/A2A/Prompt/Skill/AgentSpec；
// IPromptService、ISkillService、IAgentSpecService 解析为同一单例）
builder.Services.AddNacosAi(options => { /* ... */ });

// 添加健康检查
builder.Services.AddHealthChecks()
    .AddNacos();
```

#### 3. 服务自动注册

```csharp
var app = builder.Build();

// 自动注册当前服务到 Nacos，应用关闭时自动注销
app.UseNacosServiceRegistry(
    serviceName: "my-webapi",
    port: 5000,
    metadata: new Dictionary<string, string>
    {
        { "version", "1.0.0" },
        { "env", app.Environment.EnvironmentName }
    });

app.MapHealthChecks("/health");
app.Run();
```

#### 4. 控制器中使用

```csharp
[ApiController]
[Route("[controller]")]
public class DemoController : ControllerBase
{
    private readonly IConfigService _configService;
    private readonly INamingService _namingService;
    private readonly ILockService _lockService;

    public DemoController(
        IConfigService configService, 
        INamingService namingService,
        ILockService lockService)
    {
        _configService = configService;
        _namingService = namingService;
        _lockService = lockService;
    }

    [HttpGet("config")]
    public async Task<string?> GetConfig()
    {
        return await _configService.GetConfigAsync("app-config", "DEFAULT_GROUP", 5000);
    }

    [HttpGet("instances")]
    public async Task<List<Instance>> GetInstances(string serviceName)
    {
        return await _namingService.GetAllInstancesAsync(serviceName);
    }

    [HttpPost("lock")]
    public async Task<bool> AcquireLock(string key)
    {
        var lockInstance = LockInstance.Create(key).WithExpireTime(TimeSpan.FromSeconds(30));
        return await _lockService.LockAsync(lockInstance);
    }
}
```

## 🧩 功能模块详解

### 📁 配置中心 (IConfigService)

| 功能 | 方法 | 描述 |
|------|------|------|
| 获取配置 | `GetConfigAsync()` | 获取配置内容 |
| 获取并监听 | `GetConfigAndSignListenerAsync()` | 获取配置并注册监听器 |
| 发布配置 | `PublishConfigAsync()` | 发布/更新配置 |
| CAS 发布 | `PublishConfigCasAsync()` | 基于 MD5 的乐观锁更新 |
| 删除配置 | `RemoveConfigAsync()` | 删除配置 |
| 添加监听 | `AddListenerAsync()` | 添加配置变更监听器 |
| 移除监听 | `RemoveListener()` | 移除配置变更监听器 |
| 模糊监听 | `FuzzyWatchAsync()` | 模式匹配批量监听 (Nacos 3.0) |
| 取消模糊监听 | `CancelFuzzyWatchAsync()` | 取消模糊监听 |
| 配置过滤器 | `AddConfigFilter()` | 添加配置拦截过滤器 |
| 服务状态 | `GetServerStatus()` | 获取服务器健康状态 |

### 🌐 服务发现 (INamingService)

| 功能 | 方法 | 描述 |
|------|------|------|
| **服务注册** | | |
| 注册实例 | `RegisterInstanceAsync()` | 注册服务实例（多重载） |
| 批量注册 | `BatchRegisterInstanceAsync()` | 批量注册实例 |
| 注销实例 | `DeregisterInstanceAsync()` | 注销服务实例（多重载） |
| 批量注销 | `BatchDeregisterInstanceAsync()` | 批量注销实例 |
| **实例查询** | | |
| 获取实例 | `GetAllInstancesAsync()` | 获取所有实例 |
| 按集群获取 | `GetInstancesOfClusterAsync()` | 获取指定集群的实例 |
| 选择实例 | `SelectInstancesAsync()` | 按健康状态筛选实例 |
| 单实例选择 | `SelectOneHealthyInstanceAsync()` | 加权随机选择健康实例 |
| **服务订阅** | | |
| 订阅服务 | `SubscribeAsync()` | 订阅服务变更事件 |
| 取消订阅 | `UnsubscribeAsync()` | 取消服务订阅 |
| 模糊监听 | `FuzzyWatchAsync()` | 模式匹配批量监听 (Nacos 3.0) |
| **服务列表** | | |
| 服务列表 | `GetServicesOfServerAsync()` | 分页获取服务列表 |
| 订阅列表 | `GetSubscribeServicesAsync()` | 获取已订阅的服务列表 |
| 服务状态 | `GetServerStatus()` | 获取服务器健康状态 |

### 🔒 分布式锁 (ILockService)

| 功能 | 方法 | 描述 |
|------|------|------|
| 获取锁 | `LockAsync()` | 获取分布式锁 |
| 释放锁 | `UnlockAsync()` | 释放分布式锁 |
| 尝试获取 | `TryLockAsync()` | 带超时的锁获取 |
| 远程获取 | `RemoteLockAsync()` | gRPC 远程获取锁 |
| 远程释放 | `RemoteUnlockAsync()` | gRPC 远程释放锁 |
| 服务状态 | `GetServerStatus()` | 获取服务器状态 |

**LockInstance 流式 API：**

```csharp
var lock = LockInstance.Create("my-key")
    .WithExpireTime(TimeSpan.FromSeconds(30))  // 过期时间
    .WithLockType("distributed")               // 锁类型
    .WithNamespace("prod-namespace")           // 命名空间
    .WithOwner("service-A")                    // 所有者
    .WithParam("priority", "high")             // 自定义参数
    .WithReentrant();                          // 可重入
```

### 🤖 AI 服务 (IAiService) - Nacos 3.0

> **说明**：Prompt / Skill / AgentSpec 端点需要启用 Nacos 控制台（8080 端口），不在实时集成测试的覆盖范围内。

#### MCP 服务 (Model Context Protocol)

| 功能 | 方法 | 描述 |
|------|------|------|
| 获取服务器 | `GetMcpServerAsync()` | 获取 MCP 服务器详情 |
| 发布服务器 | `ReleaseMcpServerAsync()` | 发布 MCP 服务器 |
| 注册端点 | `RegisterMcpServerEndpointAsync()` | 注册 MCP 端点 |
| 注销端点 | `DeregisterMcpServerEndpointAsync()` | 注销 MCP 端点 |
| 订阅 | `SubscribeMcpServerAsync()` | 订阅 MCP 服务器变更 |
| 取消订阅 | `UnsubscribeMcpServerAsync()` | 取消订阅 |
| 删除服务器 | `DeleteMcpServerAsync()` | 删除 MCP 服务器 |
| 列表 | `ListMcpServersAsync()` | 分页列出 MCP 服务器 |
| 工具管理 | `GetMcpToolSpecAsync()` | 获取 MCP 工具规格 |
| 刷新工具 | `RefreshMcpToolsAsync()` | 刷新 MCP 工具 |

#### A2A 服务 (Agent-to-Agent)

| 功能 | 方法 | 描述 |
|------|------|------|
| 获取 Agent | `GetAgentCardAsync()` | 获取 Agent Card 详情 |
| 发布 Agent | `ReleaseAgentCardAsync()` | 发布 Agent Card |
| 注册端点 | `RegisterAgentEndpointAsync()` | 注册 Agent 端点 |
| 批量注册 | `RegisterAgentEndpointsAsync()` | 批量注册端点（gRPC 通道：单次 `BatchAgentEndpointRequest` op） |
| 注销端点 | `DeregisterAgentEndpointAsync()` | 注销 Agent 端点 |
| 订阅 | `SubscribeAgentCardAsync()` | 订阅 Agent Card 变更 |
| 取消订阅 | `UnsubscribeAgentCardAsync()` | 取消订阅 |
| 删除 Agent | `DeleteAgentAsync()` | 删除 Agent |
| 列表 | `ListAgentCardsAsync()` | 分页列出 Agent Cards |
| 版本列表 | `ListAgentVersionsAsync()` | 列出 Agent 版本 |

#### Prompt 服务 (IPromptService) - Nacos 3.x

| 功能 | 方法 | 描述 |
|------|------|------|
| 获取 Prompt | `GetPromptAsync()` | 按 key/版本获取 Prompt |
| 按标签获取 | `GetPromptByLabelAsync()` | 获取标签绑定的版本 |
| 搜索 | `SearchPromptsAsync()` | 客户端搜索 Prompt |
| 订阅 | `SubscribePromptAsync()` | 订阅变更（md5 条件请求） |
| 取消订阅 | `UnsubscribePromptAsync()` | 取消订阅 |
| 列表/元数据 | `ListPromptsAsync()` / `GetPromptMetaAsync()` | 分页列表与元信息 |
| 版本 | `ListPromptVersionsAsync()` / `GetPromptVersionDetailAsync()` | 版本列表与详情 |
| 草稿 | `CreatePromptDraftAsync()` / `UpdatePromptDraftAsync()` / `DeletePromptDraftAsync()` | 草稿管理 |
| 审核发布 | `SubmitPromptReviewAsync()` / `PublishPromptAsync()` / `ForcePublishPromptAsync()` / `RedraftPromptAsync()` | 提交、发布、强制发布、回草稿 |
| 上下线 | `OnlinePromptAsync()` / `OfflinePromptAsync()` | 上线/下线 |
| 标签与元数据 | `UpdatePromptLabelsAsync()` / `UpdatePromptDescriptionAsync()` / `UpdatePromptBizTagsAsync()` | 标签、描述、业务标签 |
| 删除 | `DeletePromptAsync()` | 删除 Prompt |

#### Skill 服务 (ISkillService) - Nacos 3.x

| 功能 | 方法 | 描述 |
|------|------|------|
| 下载 ZIP | `DownloadSkillZipAsync()` / `DownloadSkillZipByVersionAsync()` / `DownloadSkillZipByLabelAsync()` | 下载 Skill 包（含 md5/版本头） |
| 搜索 | `SearchSkillsAsync()` | 客户端搜索 Skill |
| 订阅 | `SubscribeSkillAsync()` | 订阅变更（304 增量） |
| 取消订阅 | `UnsubscribeSkillAsync()` | 取消订阅 |
| 列表/元数据 | `ListSkillsAsync()` / `GetSkillMetaAsync()` / `GetSkillDetailAsync()` | 列表、元信息与详情 |
| 上传 | `UploadSkillZipAsync()` | multipart 上传 ZIP 包 |
| 草稿 | `CreateSkillDraftAsync()` / `UpdateSkillDraftAsync()` / `DeleteSkillDraftAsync()` | 草稿管理 |
| 审核发布 | `SubmitSkillReviewAsync()` / `PublishSkillAsync()` / `ForcePublishSkillAsync()` / `RedraftSkillAsync()` | 提交、发布、强制发布、回草稿 |
| 上下线与范围 | `OnlineSkillAsync()` / `OfflineSkillAsync()` / `UpdateSkillScopeAsync()` | 上线/下线/范围 |
| 标签与业务标签 | `UpdateSkillLabelsAsync()` / `UpdateSkillBizTagsAsync()` | 标签管理 |
| 删除 | `DeleteSkillAsync()` | 删除 Skill |

#### AgentSpec 服务 (IAgentSpecService) - Nacos 3.x

| 功能 | 方法 | 描述 |
|------|------|------|
| 获取 AgentSpec | `GetAgentSpecAsync()` / `GetAgentSpecByLabelAsync()` | 按版本或标签获取 |
| 搜索 | `SearchAgentSpecsAsync()` | 客户端搜索 |
| 订阅 | `SubscribeAgentSpecAsync()` | 订阅变更（304 增量） |
| 取消订阅 | `UnsubscribeAgentSpecAsync()` | 取消订阅 |
| 列表/元数据 | `ListAgentSpecsAsync()` / `GetAgentSpecMetaAsync()` / `GetAgentSpecDetailAsync()` | 列表、元信息与详情 |
| 上传 | `UploadAgentSpecAsync()` | multipart 上传 |
| 草稿 | `CreateAgentSpecDraftAsync()` / `UpdateAgentSpecDraftAsync()` / `DeleteAgentSpecDraftAsync()` | 草稿管理 |
| 审核发布 | `SubmitAgentSpecReviewAsync()` / `PublishAgentSpecAsync()` / `ForcePublishAgentSpecAsync()` / `RedraftAgentSpecAsync()` | 提交、发布、强制发布、回草稿 |
| 上下线与范围 | `OnlineAgentSpecAsync()` / `OfflineAgentSpecAsync()` / `UpdateAgentSpecScopeAsync()` | 上线/下线/范围 |
| 标签与业务标签 | `UpdateAgentSpecLabelsAsync()` / `UpdateAgentSpecBizTagsAsync()` | 标签管理 |
| 删除 | `DeleteAgentSpecAsync()` | 删除 AgentSpec |

#### 🧪 AI 示例

| 示例 | 内容 |
|------|------|
| [`RedNb.Nacos.Sample.AI`](samples/RedNb.Nacos.Sample.AI/) | AI 服务全套演示（net10.0）：MCP / A2A / Prompt / Skill / AgentSpec CRUD，gRPC 通道的端点注册与批量注册，Nacos 作为注册中心 + `Microsoft.Extensions.AI` / `ModelContextProtocol` 集成，以及 `Microsoft.Agents.AI` 集成（MCP 工具 + 内联 Skill + A2A 卡片解析，`ChatClientAgent`）的端到端示例 |

### 🛠️ 维护服务 (IMaintainerService)

#### 命名空间管理 (ICoreMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 列表 | `GetNamespacesAsync()` | 获取所有命名空间 |
| 详情 | `GetNamespaceAsync()` | 获取命名空间详情 |
| 创建 | `CreateNamespaceAsync()` | 创建命名空间 |
| 更新 | `UpdateNamespaceAsync()` | 更新命名空间 |
| 删除 | `DeleteNamespaceAsync()` | 删除命名空间 |

#### 服务管理 (IServiceMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 创建 | `CreateServiceAsync()` | 创建服务（多重载） |
| 更新 | `UpdateServiceAsync()` | 更新服务 |
| 删除 | `DeleteServiceAsync()` | 删除服务 |
| 详情 | `GetServiceAsync()` | 获取服务详情 |
| 列表 | `ListServicesAsync()` | 分页列出服务 |
| 详情列表 | `ListServiceDetailsAsync()` | 列出服务（含详情） |
| 订阅者 | `GetServiceSubscribersAsync()` | 获取服务订阅者 |
| 选择器类型 | `ListSelectorTypesAsync()` | 列出选择器类型 |

#### 实例管理 (IInstanceMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 注册 | `RegisterInstanceAsync()` | 注册实例 |
| 注销 | `DeregisterInstanceAsync()` | 注销实例 |
| 更新 | `UpdateInstanceAsync()` | 更新实例 |
| 部分更新 | `PatchInstanceAsync()` | 部分更新实例 |
| 元数据更新 | `BatchUpdateMetadataAsync()` | 批量更新元数据 |
| 元数据删除 | `BatchDeleteMetadataAsync()` | 批量删除元数据 |
| 列表 | `ListInstancesAsync()` | 列出实例 |
| 详情 | `GetInstanceAsync()` | 获取实例详情 |

#### 配置管理 (IConfigMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 获取 | `GetConfigDetailAsync()` | 获取配置详情 |
| 发布 | `PublishConfigAsync()` | 发布配置（多重载） |
| 更新元数据 | `UpdateConfigMetadataAsync()` | 更新配置元数据 |
| 删除 | `DeleteConfigAsync()` | 删除配置 |
| 批量删除 | `DeleteConfigsAsync()` | 批量删除配置 |
| 列表 | `ListConfigsAsync()` | 列出配置 |
| 搜索 | `SearchConfigsAsync()` | 搜索配置 |
| 按命名空间 | `GetConfigsByNamespaceAsync()` | 按命名空间获取配置 |
| 监听者 | `GetConfigListenersAsync()` | 获取配置监听者 |
| 克隆 | `CloneConfigAsync()` | 克隆配置 |

#### 配置历史 (IConfigHistoryMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 历史列表 | `ListConfigHistoryAsync()` | 列出配置历史 |
| 历史详情 | `GetConfigHistoryAsync()` | 获取历史详情 |
| 前一版本 | `GetPreviousConfigHistoryAsync()` | 获取前一版本历史 |

#### Beta/灰度配置 (IBetaConfigMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 获取 Beta | `GetBetaConfigAsync()` | 获取 Beta 配置 |
| 发布 Beta | `PublishBetaConfigAsync()` | 发布 Beta 配置 |
| 停止 Beta | `StopBetaConfigAsync()` | 停止 Beta 配置 |
| 获取灰度 | `GetGrayConfigAsync()` | 获取灰度配置 (Nacos 3.0) |
| 发布灰度 | `PublishGrayConfigAsync()` | 发布灰度配置 (Nacos 3.0) |
| 删除灰度 | `DeleteGrayConfigAsync()` | 删除灰度配置 (Nacos 3.0) |

#### 配置导入导出 (IConfigOpsMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 导入 | `ImportConfigsAsync()` | 导入配置 |
| 导出 | `ExportConfigsAsync()` | 导出配置 |
| 按 ID 导出 | `ExportConfigsByIdAsync()` | 按 ID 导出配置 |
| 导出所有 | `ExportAllConfigsAsync()` | 导出所有配置 |
| 克隆 | `CloneConfigsAsync()` | 克隆配置到另一命名空间 |
| 克隆所有 | `CloneAllConfigsAsync()` | 克隆所有配置 |

#### 客户端管理 (IClientMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 列表 | `ListClientsAsync()` | 列出所有客户端连接 |
| 命名客户端 | `ListNamingClientsAsync()` | 列出命名服务客户端 |
| 配置客户端 | `ListConfigClientsAsync()` | 列出配置服务客户端 |
| 详情 | `GetClientDetailAsync()` | 获取客户端详情 |
| 订阅服务 | `GetClientSubscribedServicesAsync()` | 获取客户端订阅服务 |
| 发布服务 | `GetClientPublishedServicesAsync()` | 获取客户端发布服务 |
| 监听配置 | `GetClientListenedConfigsAsync()` | 获取客户端监听配置 |
| SDK 统计 | `GetSdkVersionStatisticsAsync()` | 获取 SDK 版本统计 |
| 节点统计 | `GetCurrentNodeStatisticsAsync()` | 获取当前节点统计 |
| 重载连接 | `ReloadConnectionCountAsync()` | 重载连接计数 |
| 重置限制 | `ResetConnectionLimitAsync()` | 重置连接限制 |

#### 集群管理 (ICoreMaintainer)

| 功能 | 方法 | 描述 |
|------|------|------|
| 成员列表 | `GetClusterMembersAsync()` | 获取集群成员 |
| 当前节点 | `GetSelfAddressAsync()` | 获取当前节点地址 |
| Leader | `GetLeaderAsync()` | 获取集群 Leader |
| 更新查找 | `UpdateMemberLookupAsync()` | 更新成员查找地址 |
| 离开集群 | `LeaveClusterAsync()` | 离开集群 |
| 服务状态 | `GetServerStateAsync()` | 获取服务器状态 |
| 开关配置 | `GetServerSwitchesAsync()` | 获取服务器开关配置 |
| 更新开关 | `UpdateServerSwitchAsync()` | 更新服务器开关 |
| 就绪状态 | `GetReadinessAsync()` | 获取就绪状态 |
| 存活状态 | `GetLivenessAsync()` | 获取存活状态 |
| 健康检查 | `HealthCheckAsync()` | 健康检查 |
| 指标 | `GetMetricsAsync()` | 获取指标 |
| Prometheus | `GetPrometheusMetricsAsync()` | 获取 Prometheus 指标 |
| Raft Leader | `GetRaftLeaderAsync()` | 获取 Raft Leader |
| 转移 Leader | `TransferRaftLeaderAsync()` | 转移 Raft Leader |
| 重置 Raft | `ResetRaftClusterAsync()` | 重置 Raft 集群 |

## 📁 项目结构

```
RedNb.Nacos/
├── src/
│   ├── RedNb.Nacos/                     # 核心抽象层
│   │   ├── Config/                      # 配置中心接口和模型
│   │   ├── Naming/                      # 服务发现接口和模型
│   │   ├── Lock/                        # 分布式锁接口和模型
│   │   ├── Ai/                          # AI 服务接口 (MCP/A2A)
│   │   ├── Maintainer/                  # 维护服务接口
│   │   ├── Ability/                     # 能力接口
│   │   ├── Failover/                    # 故障转移
│   │   ├── Constants/                   # 常量定义
│   │   ├── Exceptions/                  # 异常类型
│   │   └── Utils/                       # 工具类
│   ├── RedNb.Nacos.Http/                # HTTP 客户端实现
│   │   ├── Config/                      # 配置服务实现
│   │   ├── Naming/                      # 命名服务实现
│   │   ├── Lock/                        # 锁服务实现
│   │   ├── Ai/                          # AI 服务实现
│   │   ├── Maintainer/                  # 维护服务实现
│   │   └── Http/                        # HTTP 客户端基础设施
│   ├── RedNb.Nacos.Grpc/                # gRPC 客户端实现
│   │   ├── Config/                      # gRPC 配置服务
│   │   ├── Naming/                      # gRPC 命名服务
│   │   ├── Lock/                        # gRPC 锁服务
│   │   └── Protos/                      # Protocol Buffer 定义
│   ├── RedNb.Nacos.DependencyInjection/ # DI 扩展
│   ├── RedNb.Nacos.AspNetCore/          # ASP.NET Core 集成
│   │   ├── Configuration/               # 配置提供程序
│   │   ├── HealthChecks/                # 健康检查
│   │   └── ServiceRegistry/             # 服务自动注册
│   └── RedNb.Nacos.All/                 # 全功能聚合包
├── samples/
│   ├── RedNb.Nacos.Sample.Console/      # 控制台示例
│   ├── RedNb.Nacos.Sample.WebApi/       # WebAPI 示例
│   └── RedNb.Nacos.Sample.AI/           # AI 服务示例
├── tests/
│   ├── RedNb.Nacos.Tests/               # 单元测试
│   ├── RedNb.Nacos.Http.Tests/          # HTTP 客户端测试
│   └── RedNb.Nacos.IntegrationTests/    # 集成测试
└── deploy/
    └── docker-compose/                  # Docker 部署配置
```

## ⚙️ 配置选项

```csharp
public class NacosClientOptions
{
    // ====== 服务器连接 ======
    /// <summary>服务器地址，多个用逗号分隔</summary>
    public string ServerAddresses { get; set; } = "localhost:8848";

    /// <summary>控制台地址，多个用逗号分隔；为空时由 ServerAddresses 派生（端口替换为 8080）</summary>
    public string? ConsoleAddresses { get; set; }

    /// <summary>命名空间 ID</summary>
    public string? Namespace { get; set; }
    
    /// <summary>上下文路径</summary>
    public string ContextPath { get; set; } = "nacos";
    
    /// <summary>集群名称</summary>
    public string ClusterName { get; set; } = "DEFAULT";
    
    /// <summary>地址服务器端点</summary>
    public string? Endpoint { get; set; }
    
    // ====== 认证配置 ======
    /// <summary>用户名</summary>
    public string? Username { get; set; }
    
    /// <summary>密码</summary>
    public string? Password { get; set; }
    
    /// <summary>访问密钥</summary>
    public string? AccessKey { get; set; }
    
    /// <summary>秘密密钥</summary>
    public string? SecretKey { get; set; }
    
    // ====== 超时设置 ======
    /// <summary>默认超时时间（毫秒），默认 5000</summary>
    public int DefaultTimeout { get; set; } = 5000;
    
    /// <summary>长轮询超时时间（毫秒），默认 30000</summary>
    public int LongPollTimeout { get; set; } = 30000;
    
    /// <summary>重试次数，默认 3</summary>
    public int RetryCount { get; set; } = 3;
    
    // ====== 功能开关 ======
    /// <summary>启用 gRPC，默认 true</summary>
    public bool EnableGrpc { get; set; } = true;
    
    /// <summary>gRPC 端口偏移，默认 1000</summary>
    public int GrpcPortOffset { get; set; } = 1000;
    
    /// <summary>启用日志，默认 true</summary>
    public bool EnableLogging { get; set; } = true;
    
    /// <summary>启动时加载缓存</summary>
    public bool NamingLoadCacheAtStart { get; set; }
    
    /// <summary>空推送保护</summary>
    public bool NamingPushEmptyProtection { get; set; }
    
    // ====== TLS 设置 ======
    /// <summary>启用 TLS</summary>
    public bool EnableTls { get; set; }
    
    /// <summary>TLS 证书路径</summary>
    public string? TlsCertPath { get; set; }
    
    /// <summary>TLS 密钥路径</summary>
    public string? TlsKeyPath { get; set; }
    
    /// <summary>TLS CA 证书路径</summary>
    public string? TlsCaPath { get; set; }
    
    // ====== 其他 ======
    /// <summary>应用名称</summary>
    public string? AppName { get; set; }
}
```

## 🔧 高级特性

### 配置监听器

```csharp
public class MyConfigListener : IConfigChangeListener
{
    public void OnReceiveConfigInfo(ConfigInfo configInfo)
    {
        Console.WriteLine($"Config changed: {configInfo.DataId}");
        Console.WriteLine($"New content: {configInfo.Content}");
        Console.WriteLine($"MD5: {configInfo.Md5}");
    }
}
```

### 配置过滤器

```csharp
public class EncryptionFilter : IConfigFilter
{
    public string Name => "EncryptionFilter";
    public int Order => 1;

    public void DoFilter(IConfigRequest? request, IConfigResponse? response, IConfigFilterChain chain)
    {
        // 解密响应内容
        if (response?.Content != null)
        {
            response.Content = Decrypt(response.Content);
        }
        chain.DoFilter(request, response);
    }
}

// 注册过滤器
configService.AddConfigFilter(new EncryptionFilter());
```

### 模糊监听 (Fuzzy Watch) - Nacos 3.0

```csharp
// 监听所有以 "app-" 开头的配置
await configService.FuzzyWatchAsync("app-*", "DEFAULT_GROUP", new MyFuzzyWatcher());

// 监听某个组下的所有服务
await namingService.FuzzyWatchAsync("*", "production-group", myServiceWatcher);

// 取消模糊监听
await configService.CancelFuzzyWatchAsync("app-*", "DEFAULT_GROUP", myWatcher);
```

## 📊 功能统计

| 模块 | 方法数量 | 描述 |
|------|---------|------|
| IConfigService | 17 | 配置中心 |
| INamingService | 50+ | 服务发现（含重载） |
| ILockService | 7 | 分布式锁 |
| IAiService (MCP) | 18 | MCP 服务 |
| IA2aService (A2A) | 16 | A2A 服务 |
| IPromptService | 20+ | Prompt 查询/订阅/生命周期 |
| ISkillService | 20+ | Skill 下载/订阅/上传/生命周期 |
| IAgentSpecService | 20+ | AgentSpec 查询/订阅/生命周期 |
| IServiceMaintainer | 12 | 服务管理 |
| IInstanceMaintainer | 11 | 实例管理 |
| INamingMaintainer | 5 | 命名服务运维 |
| IConfigMaintainer | 18 | 配置管理 |
| IConfigHistoryMaintainer | 3 | 配置历史 |
| IBetaConfigMaintainer | 7 | Beta/灰度配置 |
| IConfigOpsMaintainer | 6 | 配置导入导出 |
| IClientMaintainer | 11 | 客户端管理 |
| ICoreMaintainer | 20 | 核心运维 |
| **总计** | **200+** | **API 方法** |

## 🔗 兼容性

| 组件 | 版本要求 |
|------|---------|
| .NET | 8.0+ / 10.0+ |
| Nacos Server | 3.2.0+ |
| C# | 12.0+ |

## 🗺️ 路线图

- [x] 配置中心 (Config Service)
- [x] 服务发现 (Naming Service)
- [x] 模糊监听 (Fuzzy Watch) - Nacos 3.0
- [x] AI 服务 - MCP/A2A (Nacos 3.0)
- [x] 分布式锁 (Lock Service) - Nacos 3.0
- [x] 维护服务 (Maintainer Service)
- [x] ASP.NET Core 集成
- [x] 依赖注入支持
- [x] 健康检查
- [x] 服务自动注册
- [x] HTTP 客户端实现
- [x] gRPC 客户端实现
- [x] 安全认证 (Security Proxy) — Username/Password 与 AccessKey/SecretKey 双路径
- [ ] Prometheus 指标监控

## 📄 许可证

本项目采用 [Apache License 2.0](LICENSE) 许可证开源。

```
Copyright 2024-2026 RedNb

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

## 🤝 贡献指南

我们欢迎任何形式的贡献！请阅读以下指南：

### 如何贡献

1. **Fork** 本仓库
2. **创建** 功能分支 (`git checkout -b feature/AmazingFeature`)
3. **提交** 更改 (`git commit -m 'Add some AmazingFeature'`)
4. **推送** 到分支 (`git push origin feature/AmazingFeature`)
5. **创建** Pull Request

### 贡献类型

- 🐛 Bug 修复
- ✨ 新功能
- 📝 文档改进
- 🧪 测试用例
- 🎨 代码优化

### 代码规范

- 遵循 [C# 编码约定](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- 所有公共 API 必须有 XML 文档注释
- 提交信息遵循 [Conventional Commits](https://www.conventionalcommits.org/)

## 🙏 致谢

- [Nacos](https://nacos.io/) - 阿里巴巴开源的动态服务发现、配置管理和服务管理平台
- [nacos-sdk-csharp](https://github.com/nacos-group/nacos-sdk-csharp) - 官方 C# SDK 参考

## ⭐ Star History

如果这个项目对你有帮助，请给我们一个 ⭐ Star！

[![Star History Chart](https://api.star-history.com/svg?repos=redNb/RedNb.Nacos&type=Date)](https://star-history.com/#redNb/RedNb.Nacos&Date)

## 📞 联系方式

- 📧 Email: [442962355@qq.com](mailto:442962355@qq.com)
- 💬 Issues: [GitHub Issues](https://github.com/redNb/RedNb.Nacos/issues)
- 📖 Discussions: [GitHub Discussions](https://github.com/redNb/RedNb.Nacos/discussions)

---

<div align="center">

**如果觉得有用，请给个 ⭐ Star 支持一下！**

Made with ❤️ by [RedNb](https://github.com/redNb)

</div>
