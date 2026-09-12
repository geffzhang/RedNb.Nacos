# RedNb.Nacos Samples

This directory contains sample projects demonstrating how to use the RedNb.Nacos SDK.

## Sample Projects

| Project | Description |
|---------|-------------|
| [RedNb.Nacos.Sample.Console](RedNb.Nacos.Sample.Console/) | Console application demonstrating basic SDK usage |
| [RedNb.Nacos.Sample.WebApi](RedNb.Nacos.Sample.WebApi/) | ASP.NET Core WebAPI demonstrating DI integration |
| [RedNb.Nacos.Sample.AI](RedNb.Nacos.Sample.AI/) | Console application (net10.0) demonstrating the full `IAiService` surface plus `Microsoft.Extensions.AI`, `ModelContextProtocol` and `Microsoft.Agents.AI` integration |

## Prerequisites

- **.NET 10 SDK** — every sample project in this directory targets `net10.0`
- A Nacos server running on `localhost:8848` — the SDK targets **Nacos 3.2.0+**;
  the AI sample is verified on **Nacos 3.2.4** (its force-publish path needs
  ≥ 3.2.1)

### Quick Start with Nacos

You can use Docker Compose to quickly start a Nacos server:

```bash
cd ../deploy/docker-compose
./start.sh   # Linux/macOS
start.bat    # Windows
```

Or run Nacos standalone:

```bash
docker run -d --name nacos -p 8848:8848 -p 9848:9848 \
  -e MODE=standalone \
  -e NACOS_AUTH_ENABLE=true \
  nacos/nacos-server:v3.2.4
```

## Running the Samples

### Console Sample

```bash
cd RedNb.Nacos.Sample.Console
dotnet run
```

This sample demonstrates:
- Creating services using `NacosFactory`
- Publishing and retrieving configurations
- Adding config change listeners
- Registering and discovering service instances
- Weighted random instance selection

### WebAPI Sample

```bash
cd RedNb.Nacos.Sample.WebApi
dotnet run
```

This sample demonstrates:
- Adding Nacos as a configuration source
- Dependency injection with `AddNacos()`
- Health checks with `AddNacos()`
- Automatic service registration with `UseNacosServiceRegistry()`
- Using services in controllers

Access the API:
- Swagger UI: http://localhost:5000/swagger
- Health Check: http://localhost:5000/health

### AI Sample

```bash
cd RedNb.Nacos.Sample.AI
dotnet run
```

This sample demonstrates:
- MCP / A2A / Prompt / Skill / AgentSpec CRUD lifecycles
- gRPC-only endpoint register / batch endpoint register / endpoint deregister
- Nacos-as-registry + `Microsoft.Extensions.AI` `IChatClient` + `ModelContextProtocol` `McpClient` integration
- `Microsoft.Agents.AI` integration: MCP tool wiring, inline skills, A2A card resolution
- An `EchoChatClient` for the default zero-API-key path; OpenAI is the only opt-in provider (`-p:DefineConstants=OPENAI_PROVIDER`)

Requires the **.NET 10 SDK** and **Nacos 3.2.x** (the sample is verified on 3.2.4; the force-publish path needs ≥ 3.2.1). The Docker quick-start above uses Nacos 3.2.4.

## Key Concepts

### Factory Pattern (Console)

```csharp
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;

var options = new NacosClientOptions
{
    ServerAddresses = "localhost:8848",
    Username = "nacos",
    Password = "nacos"
};

var factory = new NacosFactory();
var configService = factory.CreateConfigService(options);
var namingService = factory.CreateNamingService(options);
```

### Dependency Injection (WebAPI)

```csharp
// Register all Nacos services
builder.Services.AddNacos(options =>
{
    options.ServerAddresses = "localhost:8848";
    options.Username = "nacos";
    options.Password = "nacos";
});

// Or register specific services
builder.Services.AddNacosConfig(options => { /* ... */ });
builder.Services.AddNacosNaming(options => { /* ... */ });
```

### Configuration Source

```csharp
builder.Configuration.AddNacosConfiguration(source =>
{
    source.Options.ServerAddresses = "localhost:8848";
    source.ConfigItems.Add(new NacosConfigurationItem 
    { 
        DataId = "app-config", 
        Group = "DEFAULT_GROUP" 
    });
});
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddNacos();

app.MapHealthChecks("/health");
```

### Automatic Service Registration

```csharp
app.UseNacosServiceRegistry(
    serviceName: "my-service",
    port: 5000,
    metadata: new Dictionary<string, string>
    {
        { "version", "1.0.0" }
    });
```

## Troubleshooting

### Connection Failed

1. Ensure Nacos server is running on `localhost:8848`
2. Check credentials (default: `nacos`/`nacos`)
3. Verify network connectivity

### Config Not Found

1. Create the configuration in Nacos console first, or
2. Use `Optional = true` for optional configurations

### Service Not Registered

1. Ensure `AddNacos()` is called before `UseNacosServiceRegistry()`
2. Check Nacos console for registered instances
