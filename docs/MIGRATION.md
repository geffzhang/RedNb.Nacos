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
- `IMaintainerService` and its sub-interfaces: marked `[Obsolete]`. Calls
  will return HTTP 404 at runtime. Migration paths:
  - Stay on Nacos 2.x for legacy Maintainer APIs, or
  - Deploy `nacos-api-legacy-adapter` JAR on your 3.x server, or
  - Wait for a future Maintainer rewrite
- `ILockService`: marked `[Obsolete]`. Implementation still works on 3.x
  (the underlying `/nacos/v3/lock/...` endpoints are alive) but the
  interface will be removed in the next major version.

## Wire-format changes (server-facing)

- Config write (publish / remove) response is now `{code, message, data: bool}`
  instead of a bare `"true"`/`"false"` string.
- Naming register/deregister response is now `{code, message, data}`.
- Naming heartbeat uses the same endpoint as register with `heartBeat=true`.
- Service listing moved to `/v3/console/ns/service/list` (was `/v1/ns/service/list`).
- Config listener no longer has an HTTP endpoint; subscribe/listen happens
  over the gRPC bi-stream connection.

## Auth

- Login endpoint changed from `/v1/auth/login` to `/v3/auth/user/login`.
- The token still travels as `accessToken: <jwt>` (header) or `?accessToken=<jwt>`
  (query) — unchanged.
- `NacosClientOptions.Username` and `Password` are still the public surface.

## Questions

Open an issue at https://github.com/redNb/RedNb.Nacos/issues.
