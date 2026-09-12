# Nacos 3.x v1→v3 Client Migration — Design

**Date**: 2026-09-10
**Status**: Draft (brainstorming output, pre-spec-review)
**Branch context**: Started from `AIRegistry` (current branch)
**Target Nacos server version**: ≥ 3.2.0 (initial deployment target 3.2.4)

---

## 1. Goals & Scope

### 1.1 Goal

Make the RedNb.Nacos C# client fully functional against a Nacos 3.2.0+ server. Specifically, get all 25 existing integration tests (covering Config / Naming / MCP / Prompt / Skill / AgentSpec) passing against Nacos 3.2.4, while preserving the AI registry surface introduced on the AIRegistry branch.

### 1.2 In scope

- `IConfigService` — all CRUD + listener migrated to gRPC bi-stream
- `INamingService` — register/deregister/list/heartbeat on HTTP v3; subscribe push on gRPC bi-stream
- `IAiService` (MCP + A2A) and AI sub-services (`IPromptService`, `ISkillService`, `IAgentSpecService`) — verify v3 path alignment, fix any drift
- `SecurityProxy` — implicit support update: `POST /v3/auth/user/login` (because every in-scope call depends on auth header when enabled)
- Compose files + `NacosServerFixture` — upgrade to Nacos 3.2.4
- `NacosClientOptions.ContextPath` — keep `nacos` (v3 endpoints require `/nacos` prefix)

### 1.3 Out of scope (mark `[Obsolete]`, do not actively fix)

- `IMaintainerService` and its 9 sub-interfaces (`IServiceMaintainer`, `IInstanceMaintainer`, `INamingMaintainer`, `IConfigMaintainer`, `IConfigHistoryMaintainer`, `IBetaConfigMaintainer`, `IConfigOpsMaintainer`, `IClientMaintainer`, `ICoreMaintainer`)
- `ILockService` interface (mark Obsolete; HTTP paths already `/nacos/v3/lock/...` so the implementation may continue to work — leave it as-is to avoid breaking passing lock tests)

### 1.4 Non-goals

- Support for Nacos 2.x or Nacos 3.0.x / 3.1.x servers (the public minimum is 3.2.0)
- Rewriting wire-format model classes (reuse `ConfigBasicInfo`, `ServiceInfo`, `PageResult`, etc.)
- Changing `INacosFactory` / DI registration signatures
- Sample apps using Maintainer/Lock APIs
- A future v4 transport layer

---

## 2. Architecture Overview

### 2.1 Component layout after migration

```
NacosFactory ──┬── ConfigRpcTransportClient (gRPC bi-stream)
              │     ├─ ConfigQuery / Publish / Remove (unary)
              │     ├─ ConfigBatchListen ↔ ConfigChangeNotify (push)
              │     ├─ ConfigFuzzyWatch ↔ ConfigChangeNotify (push)
              │     └─ ConnectionSetupRequest / HealthCheckRequest (lifecycle)
              │
              ├── NamingRpcTransportClient (gRPC bi-stream)
              │     ├─ NotifySubscriberRequest (subscribe push)
              │     ├─ NamingFuzzyWatchNotifyRequest (push)
              │     └─ PersistentInstanceRequest / ServiceQueryRequest (queries)
              │
              ├── NacosHttpClient (HTTP, single client)
              │     ├─ Naming CRUD: POST/DELETE/GET /nacos/v3/client/ns/instance*
              │     ├─ Naming service list: GET /nacos/v3/console/ns/service/list
              │     └─ AI registry: /nacos/v3/{client,admin,console}/ai/*
              │
              ├── NacosConfigService (uses ConfigRpcTransportClient)
              │   └─ ConfigListenerManager (gRPC push-driven scheduler)
              │
              ├── NacosNamingService (uses NacosHttpClient + NamingRpcTransportClient)
              │   ├─ ServiceInfoHolder (gRPC push consumer)
              │   ├─ InstancesChangeNotifier
              │   └─ BeatReactor (HTTP POST ?heartBeat=true)
              │
              └── NacosAiService (uses NacosHttpClient + NamingRpcTransportClient)
                  ├─ MCP (gRPC + HTTP hybrid, existing pattern)
                  ├─ A2A (gRPC subscribe + HTTP CRUD, existing pattern)
                  └─ Prompt / Skill / AgentSpec (HTTP v3 client/admin)

[Obsolete] Maintainer interfaces  (warn at compile time)
[Obsolete] ILockService interface (warn at compile time; impl kept)
```

### 2.2 Decisions already baked in

- `NacosClientOptions.EnableGrpc` default `true`; **no new fields added** — `NamingPushCacheMillis` reuses the existing `LongPollTimeout` option value (10s default)
- `INacosFactory` / DI registration signatures unchanged; the service decides its transport
- `NacosRawResponse` already prepared for v3 wire; reuse
- New protocol-type constants (gRPC `Metadata.type` strings) live in `ConfigRpcPaths.cs` / `NamingRpcPaths.cs` next to the existing transport clients
- HTTP v3 path constants live at the top of each service file (`NamingApiPaths.RegisterInstance = "v3/client/ns/instance"`, etc.)
- `BeatReactor` interval stays at the existing 5s default; consumers needing a different cadence override via the existing `InstanceHeartBeatInterval` model property

---

## 3. Config Service migration

### 3.1 Transport: gRPC bi-stream for everything

All `IConfigService` operations go through `BiRequestStream.RequestBiStream`, with `Metadata.type` dispatching to the right `Body` payload:

| `Metadata.type` | Payload | Public method |
|---|---|---|
| `ConfigQueryRequest` | `{ dataId, group, tenant, tag? }` | `GetConfigAsync` |
| `ConfigPublishRequest` | `{ dataId, group, tenant, content, casMd5?, type?, encryptedDataKey? }` | `PublishConfigAsync` / `PublishConfigCasAsync` |
| `ConfigRemoveRequest` | `{ dataId, group, tenant, tag? }` | `RemoveConfigAsync` |
| `ConfigBatchListenRequest` | `{ listen, configListenContexts[] }` | `AddListenerAsync` / `RemoveListener` / `GetConfigAndSignListenerAsync` |
| `ConfigChangeNotifyRequest` (push) | `{ changedConfigs[] }` | internal → `IConfigChangeListener` |
| `ConfigFuzzyWatchRequest` | `{ dataIdPattern, groupPattern, watch, contexts }` | `FuzzyWatchAsync` |
| `ConfigFuzzyWatchChangeNotifyRequest` (push) | `{ matchedGroupKeys[], changedConfigs[] }` | internal → `IConfigFuzzyWatchEventWatcher` |
| `ConnectionSetupRequest` | `{ clientVersion, abilities: { config: { supportRemoteConfig: true } }, tenant, labels }` | startup |
| `HealthCheckRequest` | `{}` | keepalive |

### 3.2 Response handling

- `ConfigPublishResponse.success == true` AND `resultCode == 0` → success; else throw `NacosException(PublishFail, errorCode, errorMsg)`
- `ConfigQueryResponse.content == null/empty` → return `null` (caller treats as 404, same as today)
- Auth failure (`resultCode == 401` or `403`) → `NacosException(NoRight)`
- `ConfigRemoveResponse.success` boolean

### 3.3 Listener semantics

- `AddListenerAsync(dataId, group, listener)`:
  1. Maintain in-memory `(dataId, group, md5)` cache
  2. Send `ConfigBatchListenRequest { listen: true, configListenContexts: [ctx] }`
  3. On server push `ConfigChangeNotifyRequest`, look up listeners, fire `IConfigChangeListener.OnChanged(configInfo)`
- `GetConfigAndSignListenerAsync`: do `ConfigQueryRequest` for initial value + md5, then `ConfigBatchListenRequest` for registration
- `RemoveListener(dataId, group, listener)`: remove from in-memory map; if last listener for a context, send `{ listen: false }`
- `FuzzyWatchAsync(pattern, watcher)`: send `ConfigFuzzyWatchRequest { dataIdPattern, groupPattern, watch: true }`; on push, resolve matched keys and fire watcher

### 3.4 Connection lifecycle

- Startup: send `ConnectionSetupRequest` registering `ConfigAbility { supportRemoteConfig: true }`
- Heartbeat: periodic `HealthCheckRequest` using existing `HeartBeatInterval` config
- Disconnect: auto-reconnect; on reconnect, resend `ConnectionSetupRequest` then replay all registered listeners

### 3.5 Untouched files

- `IConfigService` (public signature)
- `ConfigChangeEvent`, `IConfigChangeListener`, `IConfigFuzzyWatchEventWatcher` (abstractions)
- `ConfigLongPollConfig` — internal config class; left in place as a no-op reference for callers that still inspect it, but no longer drives the listener loop (which is now gRPC-push). Not marked `[Obsolete]` since it is internal-only.

### 3.6 Files to modify

- `src/RedNb.Nacos.Grpc/Config/ConfigRpcTransportClient.cs` — wrap bi-stream
- `src/RedNb.Nacos.Http/Config/NacosConfigService.cs` — rewrite as partial class delegating to `ConfigRpcTransportClient`
- `src/RedNb.Nacos.Http/Config/ConfigListenerManager.cs` — switch from HTTP long-poll to gRPC push consumer
- `src/RedNb.Nacos.Grpc/Config/ConfigRpcModels.cs` — extend with `contentType`, `encryptedDataKey`, `tag` fields

---

## 4. Naming Service migration

### 4.1 HTTP v3 CRUD endpoints

| Client method | v3 endpoint | Notes |
|---|---|---|
| `RegisterInstanceAsync` | `POST /nacos/v3/client/ns/instance` | form-encoded; `groupName` (not `group`); fields: `serviceName`, `ip`, `port`, `namespaceId?`, `groupName?`, `clusterName?`, `healthy?`, `weight?`, `enabled?`, `metadata?` (JSON), `ephemeral?` |
| `DeregisterInstanceAsync` | `DELETE /nacos/v3/client/ns/instance` | query params |
| `GetAllInstancesAsync` / `SelectInstancesAsync` / `SelectOneHealthyInstanceAsync` | `GET /nacos/v3/client/ns/instance/list` | response is `{ code, message, data: ServiceInfo }`; `data` is the `ServiceInfo` |
| `GetServicesOfServerAsync` | `GET /nacos/v3/console/ns/service/list` | service listing is **not** in client API → use console path; response is `{ code, message, data: { count, pageItems: string[] } }` |
| `IsServiceAvailableAsync` | `GET /nacos/v3/client/ns/instance/list?healthyOnly=true` | service considered available if `data.hosts.length >= 1` |

### 4.2 Heartbeat (merged into register endpoint)

- v3 spec: heartbeat uses the same endpoint as register, with `heartBeat=true`
- Old `BeatReactor.SendBeatAsync` (PUT v1) → `SendHeartbeatAsync` doing `POST /v3/client/ns/instance?heartBeat=true`
- BeatInfo JSON still travels in form field `beat`
- Error code `21003` (instance expired) → trigger full `RegisterInstanceAsync` to re-register with metadata
- Continue honouring server-returned `instanceHeartBeatInterval` if present in `ServiceInfo`

### 4.3 Response parsing changes

- Today: HTTP body `"ok"` literal
- v3: `{ code, message, data }` envelope — success when `code == 0` and `data` truthy
- 404 on list operations → `null` (no exception)
- 401/403 → `NacosException(NoRight)`

### 4.4 gRPC subscribe push

- `SubscribeAsync(serviceName, groupName, listener)`:
  1. Initial fetch via `PersistentInstanceRequest` (HTTP or unary gRPC) to populate `ServiceInfoHolder`
  2. Send `SubscribeServiceRequest { serviceName, groupName, subscribe: true }`
  3. On push `NotifySubscriberRequest` → re-fetch latest `ServiceInfo` → `ServiceInfoHolder.ProcessServiceInfo(info)` → `InstancesChangeNotifier` invokes listeners
- `UnsubscribeAsync`: send `SubscribeServiceRequest { subscribe: false }`; `ServiceInfoHolder.RemoveServiceInfo(...)`
- `FuzzyWatchAsync(pattern, watcher)`: `NamingFuzzyWatchRequest`; listen for `NamingFuzzyWatchNotifyRequest` push

### 4.5 BeatReactor

- `src/RedNb.Nacos.Http/Naming/BeatReactor.cs` keeps the existing 5s interval default; the interval comes from `Instance.InstanceHeartBeatInterval` if set, otherwise the `HeartBeatInterval` config (no new options field)
- New `_naming.SendHeartbeatAsync(instance, namespaceId)` — internally posts to `/v3/client/ns/instance` with `heartBeat=true` and BeatInfo form
- On `21003` response, call full `RegisterInstanceAsync(instance)` to repopulate metadata
- Dispose → cancel periodic task cleanly

### 4.6 ServiceInfoHolder

- Keep the TTL fix from commit `1b7649f` (`Expired()` check)
- Add push-event consumer: `OnNotify(ServiceInfo)` called by `InstancesChangeNotifier`
- TTL uses `NacosClientOptions.LongPollTimeout` (10s default), no new option field

### 4.7 Untouched files

- `INamingService` (public signature), `Instance`, `ServiceInfo`, `Selector`, listener interfaces

### 4.8 Files to modify

- `src/RedNb.Nacos.Http/Naming/NacosNamingService.cs` — rewrite CRUD to HTTP v3
- `src/RedNb.Nacos.Http/Naming/BeatReactor.cs` — call `SendHeartbeatAsync`
- `src/RedNb.Nacos.Http/Naming/NamingServiceInfoHolder.cs` — accept push events
- `src/RedNb.Nacos.Http/Naming/InstancesChangeNotifier.cs` — listen to push events
- `src/RedNb.Nacos.Grpc/Naming/NacosGrpcNamingService.cs` — handle `SubscribeServiceRequest` / `NotifySubscriberRequest`
- `src/RedNb.Nacos.Grpc/Naming/NamingRpcTransportClient.cs` — bi-stream wrapper

---

## 5. AI Registry alignment

### 5.1 Status

The AIRegistry branch already targets v3 paths for AI services (`v3/{console,admin,client}/ai/...`) and the gRPC MCP/A2A subscribe push is the existing pattern. New `NacosPromptService.cs`, `NacosSkillService.cs`, `NacosAgentSpecService.cs` were just added but never validated against a live Nacos 3.2.4 server.

Section 5 is therefore **verify + lightly adjust**, not rewrite.

### 5.2 Verification checklist (against 3.2.4)

| Service | Method | Verify |
|---|---|---|
| `IPromptService` | `GetPromptAsync` / `GetPromptByLabelAsync` | HTTP 200, body is raw Prompt JSON (not `ApiResult<T>` envelope) on `v3/client/ai/prompt` |
| `IPromptService` | `SubscribePromptAsync` | gRPC push drives `AbstractNacosPromptListener.OnChanged` |
| `IPromptService` | `CreatePromptDraftAsync` | form `promptKey`, `targetVersion`, `template`, `variables` (JSON), `commitMsg?` |
| `IPromptService` | `UpdatePromptLabelsAsync` | form `promptKey`, `labels` (JSON map) |
| `ISkillService` | `DownloadSkillZipAsync` | raw ZIP bytes; honour ETag/MD5 → 304 cache |
| `ISkillService` | `UploadSkillZipAsync` | multipart `file` + form `overwrite`, `targetVersion`, `autoPublishIfNew` |
| `IAgentSpecService` | `GetAgentSpecAsync` | `v3/client/ai/agentspecs` (raw wire) |
| `IAgentSpecService` | `SearchAgentSpecsAsync` | `v3/client/ai/agentspecs/search` returns `ApiResult<PageResult<AgentSpecSummary>>` |
| `IAgentSpecService` | `CreateAgentSpecDraftAsync` | form `agentSpec` (JSON), `basedOnVersion?`, `targetVersion?` |
| `IA2aService` | `GetAgentCardAsync` / `ReleaseAgentCardAsync` | gRPC unary + push |
| `IAiService` (MCP) | `GetMcpServerAsync` / `ReleaseMcpServerAsync` | gRPC unary |
| `IAiService` (MCP) | `RegisterMcpServerEndpointAsync` | form `mcpName`, `address`, `port`, `version?`, `namespaceId?` |
| `IAiService` (MCP) | `SubscribeMcpServerAsync` | gRPC bi-stream |

### 5.3 Likely minor adjustments

- Confirm wire format of `tagsAll` / `bizTags` (CSV vs JSON array)
- Confirm `PageResult` field names (`pageItems`, `totalPages`, `hasNextPage`)
- Confirm admin/console endpoints return `{ code, message, data }` envelope as expected (existing `ApiResult<T>` parser should match)
- Confirm presence of any newer endpoints (e.g., `POST /v3/admin/ai/a2a/update` for `updateAgentCard`) — only add if missing from current client

### 5.4 Untouched files

- All `IAiService` / `IA2aService` / `IPromptService` / `ISkillService` / `IAgentSpecService` public interfaces
- `Ai/Model/**` model classes
- `NacosAiService.cs` main class structure (partial-class split already in place)

### 5.5 Files likely modified

At most a handful of `NacosAgentSpecService.cs` / `NacosPromptService.cs` / `NacosSkillService.cs` path or field corrections based on observed drift.

---

## 6. Deprecated services (Obsolete markers)

### 6.1 Mark `[Obsolete]` with warning only

```csharp
[Obsolete(
    "Nacos 3.2+ removed v1/v2 HTTP endpoints used by this service. " +
    "Calls will return HTTP 404 at runtime. " +
    "Migrate to Nacos 2.x server or use the nacos-api-legacy-adapter JAR. " +
    "Tracked for removal in the next major version.",
    error: false)]
```

- `error: false` → compile warning only, doesn't break builds
- Rationale: minimal publishable strategy requires that 3.2+ server-side Config/Naming/AI run; we don't want to be a hard blocker on existing consumers who still depend on Maintainer / Lock against older servers

### 6.2 Lock service: interface-only Obsolete, implementation kept

- `ILockService` interface: `[Obsolete]`
- `NacosLockService.cs` implementation: **not** marked Obsolete — its underlying `LockConstants.LockAcquirePath = "/nacos/v3/lock/..."` is already v3; existing lock tests may pass on 3.2+
- The interface is still usable; the warning nudges consumers away from it for the long term

### 6.3 Implicit Auth update (not marked Obsolete; required for in-scope services)

Even though `SecurityProxy` is internal and not in our explicit scope, every in-scope call requires a working auth login when `NACOS_AUTH_ENABLE=true`. So:

| File | Change |
|---|---|
| `src/RedNb.Nacos.Http/Http/SecurityProxy.cs` | `LoginAsync` endpoint `POST /v1/auth/login` → `POST /v3/auth/user/login`; response fields `accessToken` / `tokenTtl` unchanged |
| `src/RedNb.Nacos.Http/Http/NacosHttpClient.cs` | unchanged — already sends `accessToken` header |

### 6.4 Obsolete marker list

```csharp
[Obsolete("...")] IMaintainerService, IServiceMaintainer, IInstanceMaintainer,
                INamingMaintainer, IConfigMaintainer, IConfigHistoryMaintainer,
                IBetaConfigMaintainer, IConfigOpsMaintainer, IClientMaintainer,
                ICoreMaintainer

[Obsolete("...")] ILockService  // interface only; implementation kept
```

---

## 7. Test infrastructure changes

### 7.1 Compose upgrade

| File | Change |
|---|---|
| `deploy/docker-compose/docker-compose.yml` | `nacos/nacos-server:v3.1.1` → `v3.2.4` |
| `deploy/docker-compose/docker-compose.mysql.yml` | same |
| `deploy/docker-compose/docker-compose.cluster.yml` | three node images same; `healthcheck` URL `/nacos/v1/console/health/readiness` → `/nacos/v3/health/readiness` |
| `deploy/docker-compose/start.sh` / `start.bat` / `README.md` / `CONTRIBUTING.md` | version strings + healthcheck URL updates |

### 7.2 `NacosServerFixture`

- Switch readiness probe to `/nacos/v3/health/readiness`
- Keep `NACOS_AUTH_ENABLE=false` for tests (auth-on tests out of scope)
- Bump `start_period` 60s → 90s (3.2.4 boots slower — dist module init)

### 7.3 Existing integration tests

All four files must pass on 3.2.4:

| Test class | Coverage |
|---|---|
| `ConfigServiceIntegrationTests` | Get / Publish / Remove / CAS / Listener |
| `NamingServiceIntegrationTests` | Register / Deregister / Heartbeat / List / SelectOne |
| `NamingSelectorIntegrationTests` | Subscribe push + label filter (regression for the bug fixed in `1b7649f`) |
| `AiServiceIntegrationTests` | MCP server CRUD + endpoints + subscribe |

### 7.4 New tests (gRPC push paths)

| Test class | Purpose |
|---|---|
| `ConfigListenerPushTests` | Register listener → server-side publish → client receives OnChanged within 5s (proves gRPC bi-stream push) |
| `NamingSubscribePushTests` | Subscribe → server-side RegisterInstance → client receives OnChanged push |
| `HeartbeatExpiryTests` | Register ephemeral → wait for expiry → observe `21003` → client auto re-registers |

### 7.5 Unit tests

Existing `tests/RedNb.Nacos.Http.Tests/Ai/NacosAgentSpecServiceTests.cs`, `NacosPromptServiceTests.cs`, `NacosSkillServiceTests.cs` continue to pass. If §5 verification reveals field/endpoint drift, the corresponding mock fixtures are updated in the same PR as the source change.

### 7.6 Auth tests

Out of scope this iteration (`NACOS_AUTH_ENABLE=false` remains default for the integration suite). A future PR may add auth-on integration coverage.

---

## 8. Migration / Rollout plan

### 8.1 Sequencing (5 PRs)

| PR | Title | Validation gate |
|---|---|---|
| **PR1** | Foundation: compose upgrade + fixture readiness | `docker compose up -d` succeeds; 25 integration tests fail (expected — v1 endpoints now 404) |
| **PR2** | Auth: `SecurityProxy` → `/v3/auth/user/login` | With `NACOS_AUTH_ENABLE=true`, login succeeds, subsequent calls receive `accessToken` header |
| **PR3** | Config Service: gRPC bi-stream rewrite | `ConfigServiceIntegrationTests` all green + new `ConfigListenerPushTests` pass |
| **PR4** | Naming Service: HTTP v3 + gRPC subscribe | `NamingServiceIntegrationTests` + `NamingSelectorIntegrationTests` green + new `HeartbeatExpiryTests` pass |
| **PR5** | AI verification + `[Obsolete]` markers + docs | `AiServiceIntegrationTests` green; samples still build with warnings; README + new `docs/MIGRATION.md` updated |

### 8.2 Validation gate after each PR

```bash
cd deploy/docker-compose && ./start.sh                                       # bring up Nacos 3.2.4
cd tests/RedNb.Nacos.IntegrationTests && dotnet test -f net8.0               # integration
cd tests/RedNb.Nacos.Http.Tests        && dotnet test -f net8.0               # unit (HTTP)
cd tests/RedNb.Nacos.Tests              && dotnet test -f net8.0               # unit (core)
```

Expected after PR5: all three test projects green.

### 8.3 Risks & mitigations

| Risk | Mitigation |
|---|---|
| gRPC bi-stream reconnect drops listeners | On reconnect, resend `ConnectionSetupRequest` then replay all registered `ConfigBatchListen` / `SubscribeServiceRequest` |
| Heartbeat `instanceHeartBeatInterval` mismatch with server | Read `ServiceInfo.cacheMillis` / use server-returned interval if present |
| `RegisterInstanceAsync` form `groupName` field vs C# parameter `groupName` — already aligned | No public-signature change needed; only form-key rename inside the implementation |
| Sample apps calling `[Obsolete]` Maintainer / Lock produce warnings | Add README pointer in PR5; do not migrate samples this iteration |
| 3.2.4 boots slower than 3.1.1 → CI timeouts | Fixture `start_period` 60s → 90s |

### 8.4 Rollback

- Each PR is independently revertible (inter-PR dependency is "tests fail forward" semantic only)
- Do not publish a NuGet release until PR5 merges cleanly
- CHANGELOG entry: "1.x.x (post-PR5) drops support for Nacos servers below 3.2.0; v1/v2 HTTP endpoints are no longer used by this client"

### 8.5 Documentation

- `README.md`: declare min server 3.2.0; list `[Obsolete]` services
- `docs/MIGRATION.md` (new): v1 → v3 upgrade guide, breaking changes, Maintainer/Lock user migration advice
- `CONTRIBUTING.md`: point devs to `docker compose up` (now 3.2.4)

---

## 9. Out-of-scope / Open questions

- **Nacos 3.2+ auth defaults**: `nacos.core.auth.enabled=true` will be the default for client APIs in 3.3+. We do not test against auth-on this round.
- **Maintainer user migration path**: out of scope. If the legacy adapter JAR proves insufficient, a future migration is needed.
- **Lock interface removal timing**: defer to a future major version (post-deprecation).
- **v4 client API**: not addressed; revisit when Nacos publishes a v4 spec.

---

## 10. References

- Nacos OpenAPI: https://nacos.io/en-us/docs/open-api
- Nacos 3.x Compatibility & Deprecation spec: https://github.com/alibaba/nacos/blob/develop/specs/en/design/compatibility-deprecation-spec.md
- Legacy adapter: https://github.com/nacos-group/nacos-api-legacy-adapter
- Nacos 3.2.4 release notes
- Internal agent research output (v1 endpoint inventory; v3 endpoint spec) — recorded 2026-09-10