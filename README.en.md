# RedNb.Nacos

A .NET 8 / .NET 10 SDK targeting Nacos 3.2.4. Source version 2.0.0 changes public namespaces and defaults Config/Naming to gRPC. Nacos 2.x is not supported; other 3.x releases require explicit validation.

Use `RedNb.Nacos.DependencyInjection` and `AddNacos` for lazy Config/Naming services. Add AI with `AddNacosAi`, namespace administration with `AddNacosAdministration`, or a named/keyed client with `AddNacos(name, configure)`. Use `await using` for service-provider and client disposal.

Read server addresses, username and password from environment variables or your secret store. API/gRPC/Console default ports are 8848/9848/8080. The default authentication plugin uses username/password or tokens, not AK/SK login.

The six packages remain Core (`RedNb.Nacos`), Http, Grpc, DependencyInjection, AspNetCore and the dependency-only All package. AI runtime frameworks are sample dependencies, not SDK dependencies.

See the [Chinese quick start](README.md), [capability matrix](docs/CAPABILITIES.md), [migration guide](docs/MIGRATION.md), [test setup](docs/ENVIRONMENT_BOOTSTRAP.md), [test report](docs/TEST_REPORT.md) and [samples](samples/README.md).

HTTP-only config listeners and fuzzy watch fail explicitly; use gRPC. MCP/A2A Console resources are public-namespace scoped in 3.2.4. Legacy maintenance contracts are migration-only. Native locks provide the server mutex primitive, not fencing or reentrancy. TLS, proxy and multi-node failure behavior require separate validation.

Apache-2.0; see [LICENSE](LICENSE).
