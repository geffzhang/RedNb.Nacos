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
  As with `IAiService`, an implementer of `IA2aService` written against the
  previous version must add the new member to compile.
- `NacosHttpClient` and `NacosConsoleHttpClient` now derive from the new public
  `NacosHttpClientBase`, which holds the shared HTTP transport (the extension
  seam is the `protected virtual BuildBaseUrl`). The public members of the
  concrete classes are unchanged — they are inherited from the base.
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
- **AccessKey/SecretKey authentication** is now wired in. Configure both
  `NacosClientOptions.AccessKey` and `NacosClientOptions.SecretKey` and the SDK
  signs the login with the Nacos standard
  `Base64(HMAC-SHA1(secretKey, accessKey + timestamp))` (see
  `SignatureUtils.SignRequest`) and POSTs the signed query string
  `?accessKey=…&timestamp=…&signature=…` to the same `/v3/auth/user/login`
  endpoint. Strategy precedence: AK/SK wins when both are set, then
  Username/Password, then anonymous; `Validate()` rejects half-set AK/SK
  symmetrically (one of the two without the other throws `InvalidParam`) before
  the `ServerAddresses` check. The gRPC transport picks up the resulting JWT
  transparently via `SecurityProxy.GetAccessTokenAsync` — `IAiService`,
  `INamingService` and `IConfigService` over gRPC need no extra wiring.

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
- **Other gRPC ops** — the 3.2.4 server registers only 8 AI gRPC handlers, so
  the remaining 14 operations throw `NacosException.ServerNotImplemented` (501)
  on the gRPC channel. What the exception message says depends on the operation:
  - The subscribe / delete / list / validate / import operations (MCP server
    subscribe, delete, list, validate-import and import; agent subscribe, delete
    and list, including both agent version-list members) point at the HTTP
    console channel: `... has no Nacos 3.2.4 gRPC handler; use the HTTP console
    channel (IAiService via NacosFactory with ConsoleAddresses) instead`.
  - The four MCP-tool operations (`RefreshMcpToolAsync`, `GetMcpToolAsync`,
    `DeleteMcpToolAsync`, `UpdateMcpToolAsync`) say that **no channel exists at
    all**: `MCP tool management has no Nacos 3.2.4 gRPC handler and no HTTP
    console endpoint — operation unavailable on this server`.
  Note the subscription operations are polling-based over HTTP (10s), not push.
- **gRPC subscribe is inert** — `SubscribeMcpServerAsync` and
  `SubscribeAgentCardAsync` now throw 501 on the gRPC channel, so the local
  listener registries and push-delivery branches inside the gRPC AI service stay
  inert until a real subscribe implementation exists. Subscription over the HTTP
  console channel is the live path.

## Questions

Open an issue at https://github.com/redNb/RedNb.Nacos/issues.
