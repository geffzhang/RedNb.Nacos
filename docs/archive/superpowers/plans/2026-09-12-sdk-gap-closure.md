# SDK gap-closure plan (2026-09-12)

> For agentic workers: REQUIRED SUB-SKILL: superpowers:subagent-driven-development.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal**: Close three server-supported gaps identified in the 2026-09-12 reconciliation: (A) gRPC factory skips `Validate()`, (B) agent endpoint batch op loops single endpoint, (C) AccessKey/SecretKey never wired into `SecurityProxy`.

**Architecture**: Three independent, vertically-sliced commits — no shared files between tasks. Each task's brief is self-contained.

**Tech Stack**: .NET 8/10, gRPC over bi-stream, Nacos 3.2.4.

**Spec**: `.tmp/audit-reconciliation-2026-09-12.md` §4 (真实缺口清单).

**Base SHA**: `a4b81b6` (parent of all work).

## Global Constraints

- Branch: `AIRegistry`. NO merge, NO push.
- Untracked files stay untracked: `.claude/`, `.tmp/`, `.superpowers/`. Only the task's own files are staged; the rest of the repo (including the untracked `deploy/docker-compose/README.md`, plan files, etc.) is left alone.
- `nacos-server` container MUST NOT be restarted or recreated.
- Commits use conventional prefixes and `Co-Authored-By: Claude Code <noreply@anthropic.com>` trailer.
- Live verification: integration tests against the running 3.2.4 server (currently in a Derby crash-loop — see SDD context below).

## SDD context: server availability

The `nacos-server` container is currently stuck in a Derby `Login timeout exceeded` crash-loop. JVM keeps respawning and dies at `load derby-schema.sql`. Tasks B and C need live HTTP probes against `:8080` for verification; if the server is unreachable when the implementer runs, the gate degrades gracefully:

- Task A: pure unit test (no live probe needed) — always runnable.
- Task B: a single integration test that registers/releases a batch of agent endpoints and asserts server-side state. If `:8080` is unreachable, run unit-level tests only and document the live-verification gap.
- Task C: AK/SK path requires login via `/v3/auth/user/login` or AK/SK signing; same gate — unit-only if server down, live verify if up.

The implementer MUST NOT attempt to restart the container. If they need live verification and the server is down, they write that down in the report and the controller decides next steps.

---

## Task 1 — `NacosGrpcFactory` calls `Validate()`

**Files**:
- Modify: `src/RedNb.Nacos.Grpc/NacosGrpcFactory.cs` (each `*Async` ctor and each sync `Create*` helper)
- Test: `tests/RedNb.Nacos.Grpc.Tests/NacosGrpcFactoryTests.cs` (new) — or add to existing test file if one exists

**Behavior**:
1. Every `NacosGrpcFactory.CreateXxx*(NacosClientOptions options, …)` overload calls `options.Validate()` first, before constructing any service.
2. Throws `NacosException(InvalidParam)` propagated from `Validate()` — same surface as HTTP `NacosFactory`.
3. Non-`Options` overloads (string server address) build a `NacosClientOptions` internally; the same `Validate()` is applied.

**Test coverage**:
- Unit: passing valid `Options` succeeds; `Options` with `Username` set but blank `ServerAddresses` throws `InvalidParam`; `Options` with all-empty addresses throws.
- Existing `NacosGrpcFactory.CreateAiServiceAsync(Options)` smoke test (if exists) still passes.

---

## Task 2 — `BatchAgentEndpointRequest` replaces single-endpoint loop

**Files**:
- Modify: `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs` (replace `RegisterAgentEndpointsAsync` loop)
- Modify: `tests/RedNb.Nacos.Grpc.Tests/Ai/NacosGrpcAiServiceTests.cs` (add unit test for the batch path)
- Possibly: introduce `BatchAgentEndpointRequest` proto type — investigate how the project sources its `.proto`. The audit shows server registers `BatchAgentEndpointRequestHandler`; check whether the SDK's `NamingRpcModels.cs` / `ConfigRpcModels.cs` pattern is the right place, or whether a new `AiRpcModels.cs` partial is needed.

**Behavior**:
1. `IA2aService.RegisterAgentEndpointsAsync(name, endpoints, ct?)` issues a single `BatchAgentEndpointRequest` over the AI gRPC connection, instead of looping `RegisterAgentEndpointAsync`.
2. Each endpoint carries `address`, `port`, `version`, `transport` (no need to set them one by one). Validation moves from per-endpoint to batch-level.
3. Response is `AgentEndpointResponse` — single resultCode. If `resultCode != 0`, throw `NacosException` with the server message (preserve the loop's per-endpoint semantics if the server returns partial success — investigate whether `AgentEndpointResponse` carries a per-endpoint result list; the audit shows it returns the same shape as the single-endpoint case, so we treat batch as all-or-nothing for now).
4. Server-side gate: live integration test (if server is up) that registers 3 endpoints in one batch and verifies all 3 are deregisterable; falls back to wire-shape unit test only.

**Test coverage**:
- Unit: `RegisterAgentEndpointsAsync(name, [3 endpoints])` produces exactly one `BatchAgentEndpointRequest` payload over the fake transport (assert `RequestType == "BatchAgentEndpointRequest"`, body contains all 3 endpoints).
- Unit: response with `resultCode != 0` throws with the server message.
- Live (gated): integration test registering 3 endpoints in one batch + asserting server-side state via `GetAgentCardAsync` + endpoint list — `tests/RedNb.Nacos.IntegrationTests/Ai/GrpcAiServiceIntegrationTests.cs`.

---

## Task 3 — AccessKey/SecretKey wired into `SecurityProxy`

**Files**:
- Modify: `src/RedNb.Nacos.Http/Auth/SecurityProxy.cs` (or wherever the current login path lives)
- Modify: `src/RedNb.Nacos.Grpc/Payload/SecurityProxy.cs` if gRPC has its own (verify)
- Test: `tests/RedNb.Nacos.Http.Tests/Auth/SecurityProxyTests.cs` (new or existing) — covers both username/password and AK/SK branches

**Behavior**:
1. `SecurityProxy.LoginAsync` chooses strategy by inspecting `NacosClientOptions`:
   - If `AccessKey` + `SecretKey` both set: sign via the standard `hmacSha1`-based Nacos signature (`SignatureUtils.SignRequest`), set `accessToken` header from signed token service. The Java client uses `com.alibaba.nacos.client.security.SecurityProxy` → `NacosAuthLoginService` with `LoginIdentityContext` using AK/SK.
   - If `Username` + `Password` set: existing `/v3/auth/user/login` flow.
   - If neither: throw `InvalidParam` (already covered by `Validate()` post-Task A).
2. Token caching + TTL refresh: same as today, agnostic to which login path produced the token.
3. gRPC side: `Payload.Metadata.Headers["accessToken"]` injection already works; verify it picks up the token regardless of how it was obtained.

**Reference** (audit-confirmed): server `default-auth-plugin-3.2.4.jar` exposes `/v3/auth/user` and an additional `/v3/auth/identity/login` or signing endpoint for AK/SK (verify which by inspecting the jar). Java client `NacosAuthLoginServiceImpl` signs requests with `HmacSHA1(secretKey, resource+timestamp)` and sends `?accessKey=…&timestamp=…&signature=…` — the SDK must mirror this.

**Test coverage**:
- Unit: given `AccessKey="ak", SecretKey="sk"`, the request URL contains `accessKey=ak&timestamp=…&signature=…` with the right HMAC.
- Unit: given `Username/Password`, the request body is the existing JSON `{username, password}` (regression).
- Live (gated): integration test using AK/SK logs in and reads a config — same shape as the existing username/password smoke tests in `ConfigServiceIntegrationTests.cs`.

---

## Out of scope (per scope decision 2026-09-12)

- `LockQueryRequest` implementation — deferred to a follow-up; type string stays defined-but-unused.
- `RedoScheduledTask` instance — deferred to a follow-up (involves connection-generation coupling).
- gRPC service-layer business method tests — deferred to be filled in alongside next feature work.
- gRPC AI subscribe push — server doesn't have a handler; cannot be implemented.

---

## Execution

SDD with three implementer dispatches (one per task), two-stage review (spec + quality) per task, scoped re-review on fix rounds, single whole-branch final review at the end.

Model selection:
- Task A: haiku (transcription — one call site + tests).
- Task B: sonnet (proto integration judgment, multi-file).
- Task C: sonnet (auth integration judgment, server-side jar inspection for AK/SK endpoint shape).

Reviewers: sonnet floor per task; final review opus.
