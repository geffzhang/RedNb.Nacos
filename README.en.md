# RedNb.Nacos

[![NuGet](https://img.shields.io/nuget/v/RedNb.Nacos.All.svg)](https://www.nuget.org/packages/RedNb.Nacos.All/)
[![GitHub Release](https://img.shields.io/github/v/release/yinghongzhen/RedNb.Nacos)](https://github.com/yinghongzhen/RedNb.Nacos/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%2010-512BD4)](docs/CAPABILITIES.md)
[![Tested Nacos](https://img.shields.io/badge/tested%20Nacos-3.2.4-blue)](docs/TEST_REPORT.md)

English | [简体中文](README.md)

A .NET 8 / .NET 10 SDK targeting Nacos 3.2.4. Stable SDK version **2.0.0** is available on [NuGet.org](https://www.nuget.org/packages/RedNb.Nacos.All/2.0.0) and [GitHub Releases](https://github.com/yinghongzhen/RedNb.Nacos/releases/tag/v2.0.0). The SDK version and tested Nacos server version are separate.

Version 2.0.0 changes public namespaces and defaults Config/Naming to gRPC. Read the [migration guide](docs/MIGRATION.md) before upgrading from 1.x. Nacos 2.x is not supported; other 3.x releases require explicit validation.

## Installation

For typical .NET applications:

```bash
dotnet add package RedNb.Nacos.DependencyInjection --version 2.0.0
```

For ASP.NET Core configuration reload, health checks and service registration:

```bash
dotnet add package RedNb.Nacos.AspNetCore --version 2.0.0
```

For all components:

```bash
dotnet add package RedNb.Nacos.All --version 2.0.0
```

Required dependencies are installed automatically; you do not need to add all six packages manually.

## Usage

Use `RedNb.Nacos.DependencyInjection` and `AddNacos` for lazy Config/Naming services. Add AI with `AddNacosAi`, namespace administration with `AddNacosAdministration`, or a named/keyed client with `AddNacos(name, configure)`. Use `await using` for service-provider and client disposal.

Read server addresses, username and password from environment variables or your secret store. API/gRPC/Console default ports are 8848/9848/8080. The default authentication plugin uses username/password or tokens, not AK/SK login.

The six packages remain Core (`RedNb.Nacos`), Http, Grpc, DependencyInjection, AspNetCore and the dependency-only All package. AI runtime frameworks are sample dependencies, not SDK dependencies.

See the [Chinese quick start](README.md), [capability matrix](docs/CAPABILITIES.md), [migration guide](docs/MIGRATION.md), [test setup](docs/ENVIRONMENT_BOOTSTRAP.md), [test report](docs/TEST_REPORT.md) and [samples](samples/README.md).

HTTP-only config listeners and fuzzy watch fail explicitly; use gRPC. MCP/A2A Console resources are public-namespace scoped in 3.2.4. Legacy maintenance contracts are migration-only. Native locks provide the server mutex primitive, not fencing or reentrancy. TLS, proxy and multi-node failure behavior require separate validation.

Apache-2.0; see [LICENSE](LICENSE).
