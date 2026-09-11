# Migration Guide — RedNb.Nacos to Nacos 3.x

## What changed

The client now speaks the Nacos 3.x HTTP v3 API for runtime Config and
Naming operations, and uses gRPC bi-stream for the listener/subscribe paths.
The Java client has shipped this architecture since 2.0; we are aligning
with it.

## Server requirements

- **Minimum Nacos server version: 3.2.0**
- Recommended: 3.2.4 (the version we integration-test against)
- Nacos 2.x and 3.0/3.1 servers are no longer supported

## Public API stability

- `IConfigService`, `INamingService`, `IAiService` and AI sub-interfaces
  (`IPromptService`, `ISkillService`, `IAgentSpecService`, `IA2aService`):
  **signatures unchanged.** Existing callers continue to work.
  `IAiService` gained registry members (`IAsyncDisposable` plus the
  `IPromptService`, `ISkillService` and `IAgentSpecService` interfaces), so an
  implementer of `IAiService` written against the previous version must add the
  corresponding members to compile.
- `IA2aService` gained `ListAgentVersionInfosAsync`, returning rich
  `AgentVersionInfo` objects (`version`, `createdAt`, `updatedAt`, `latest`) as
  the console endpoint actually serves them. `ListAgentVersionsAsync` is
  **unchanged** (`List<string>`) and now projects `.Version` from the rich list.
- `IMaintainerService` and its sub-interfaces: marked `[Obsolete]`. Calls
  will return HTTP 404 at runtime. The Maintainer APIs have no v3 equivalent;
  migration paths:
  - Deploy `nacos-api-legacy-adapter` JAR on your 3.x server, or
  - Keep the Maintainer APIs on a frozen 2.x deployment, or
  - Wait for the Maintainer rewrite
- `ILockService`: marked `[Obsolete]`. Implementation still works on 3.x
  (the underlying `/nacos/v3/lock/...` endpoints are alive) but the
  interface will be removed in the next major version.

## Wire-format changes (server-facing)

- Config write (publish / remove) response is now `{code, message, data: bool}`
  instead of a bare `"true"`/`"false"` string.
- Naming register/deregister response is now `{code, message, data}`.
- Naming heartbeat uses the same endpoint as register with `beat=true`.
- Service listing moved to `/v3/admin/ns/service/list` (was `/v1/ns/service/list`).
- Config listener no longer has an HTTP endpoint; subscribe/listen happens
  over the gRPC bi-stream connection.

## Auth

- Login endpoint changed from `/v1/auth/login` to `/v3/auth/user/login`.
- The token still travels as `accessToken: <jwt>` (header) or `?accessToken=<jwt>`
  (query) — unchanged.
- `NacosClientOptions.Username` and `Password` are still the public surface.
- On the gRPC transport the JWT travels **inside the request payload headers**
  (`Payload.Metadata.Headers["accessToken"]`), injected by the SDK's
  `SecurityProxy`; the server's `NacosAuthPluginService` resolves it from there.
  Without this the server rejects gRPC calls (including the config/naming push
  subscriptions) with 401 `User not found`.
- `NacosClientOptions.Validate()` now rejects credentials combined with an empty
  `ServerAddresses` (config-time `InvalidParam`), because `SecurityProxy` can only
  log in against the server API port. A console-only configuration *without*
  credentials remains valid.

## AI endpoints

Nacos 3.2.4 exposes AI management endpoints **only on the console listener**
(port 8080 by default), not on the API port (8848). Their paths start with
`/v3/console/ai/**`, and the console listener has no `/nacos` context path.
Authentication is unchanged: the console accepts the same `accessToken` issued
by `/v3/auth/user/login`, which the SDK obtains from `Username`/`Password`.

The HTTP AI service (`NacosFactory.CreateAiService`) targets the console port
automatically. Configure `NacosClientOptions.ConsoleAddresses` to override the
default (`ServerAddresses` with port 8080):

```csharp
var options = new NacosClientOptions
{
    ServerAddresses = "nacos-1:8848,nacos-2:8848",     // API port(s)
    ConsoleAddresses = "nacos-1:8080,nacos-2:8080",    // Console port(s)
    Username = "nacos",
    Password = "nacos"
};
```

Both properties are comma-separated `string` values. Explicit
`ConsoleAddresses` entries are used verbatim (no port substitution); when the
property is empty, the SDK derives console addresses from `ServerAddresses` by
replacing each port with 8080.

The console AI endpoints serve the **public namespace only**: the HTTP AI service
ignores `NacosClientOptions.Namespace` and does not send an
`X-Nacos-Namespace-Id` header.

Channel availability in 3.2.4 differs per operation:

- **Endpoint register/deregister (MCP and Agent)** — no HTTP endpoint on the
  console, so the HTTP service throws `NacosException` (fail loud). The server
  **does** expose gRPC handlers for these, so use the gRPC-backed service
  (`NacosGrpcFactory.CreateAiServiceAsync`). The .NET gRPC AI channel is
  live-verified against Nacos 3.2.4 (2026-09-11) — see
  [SDK_COMPLETENESS_REPORT.md](SDK_COMPLETENESS_REPORT.md) §一.3. Two caveats
  learned live: the server only accepts endpoint registration on a connection
  labelled `module=naming` (the SDK announces it), and a server released as
  `stdio` has a null `remoteServerConfig`, which makes the server's
  `McpServerEndpointRequestHandler` NPE — release with
  `RemoteServerConfig.ServiceRef` instead.
- **MCP tool CRUD** — available on **neither** channel in Nacos 3.2.4: there is
  no console HTTP endpoint and no server-side gRPC handler. Both channels fail
  loud with `NacosException`.
- **Other gRPC ops** — the 3.2.4 server registers only 8 AI gRPC handlers. The
  remaining operations (delete/list/subscribe MCP and agent, MCP import and
  validation, MCP tool CRUD, agent version lists) throw
  `NacosException.ServerNotImplemented` (501) on the gRPC channel with a message
  pointing at the HTTP console channel. Note the subscription operations are
  polling-based over HTTP (10s), not push.

## Questions

Open an issue at https://github.com/redNb/RedNb.Nacos/issues.
