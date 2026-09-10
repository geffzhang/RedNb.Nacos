# Nacos 3.x v1→v3 Client Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate `RedNb.Nacos` C# client from Nacos v1 HTTP endpoints (removed in 3.2.0) to v3 HTTP + gRPC bi-stream, get all 25 integration tests + 3 new gRPC-push tests green against Nacos 3.2.4.

**Architecture:** `ConfigService` goes fully gRPC bi-stream (no v1 HTTP long-poll exists in 3.x). `NamingService` stays on HTTP v3 form-encoded for register/deregister/list/heartbeat (heartbeat merged into the register endpoint via `heartBeat=true`), but subscribe push moves to gRPC bi-stream. AI registry endpoints are already on v3 paths — verify + minor drift fixes. `Maintainer`/`Lock` interfaces are marked `[Obsolete]` (compile warnings only); `SecurityProxy` switches to `POST /v3/auth/user/login` as a foundational change.

**Tech Stack:** .NET 8/10, ASP.NET-style HttpClient (form-encoded), gRPC with custom bi-stream (`BiRequestStream`), xUnit, docker-compose for integration test fixture.

**Spec:** [docs/superpowers/specs/2026-09-10-nacos-v3-migration-design.md](../specs/2026-09-10-nacos-v3-migration-design.md)

## Global Constraints

- Min Nacos server: **3.2.0** (target 3.2.4)
- All HTTP v3 wire format: `application/x-www-form-urlencoded` body, `{code, message, data}` envelope on Admin/Console endpoints
- Config listener: **no HTTP long-poll exists in Nacos 3.x** — must use gRPC bi-stream
- Naming heartbeat: **merged into register endpoint** with `heartBeat=true` flag; expired instance error code is `21003`
- gRPC metadata.type dispatch strings: `ConfigQueryRequest`, `ConfigPublishRequest`, `ConfigRemoveRequest`, `ConfigBatchListenRequest`, `ConfigFuzzyWatchRequest`, `ConfigChangeNotifyRequest`, `ConfigFuzzyWatchChangeNotifyRequest`, `ConnectionSetupRequest`, `HealthCheckRequest`, `SubscribeServiceRequest`, `NotifySubscriberRequest`, `NamingFuzzyWatchRequest`, `NamingFuzzyWatchNotifyRequest`
- `ServiceInfoHolder` must keep the TTL `Expired()` check from commit `1b7649f` and add a push-event consumer
- BeatReactor default interval stays at 5s; uses `Instance.InstanceHeartBeatInterval` if set
- `NacosClientOptions`: no new fields; `EnableGrpc=true` by default; reuse `LongPollTimeout` (10s) for `NamingPushCacheMillis`
- `NacosFactory` / DI signatures unchanged; services internally select transport
- Auth: `accessToken` header (already in `NacosHttpClient`); login via `POST /v3/auth/user/login`
- `[Obsolete]` markers: `error: false` (warning only, not compile error)
- Compose healthcheck URL: `/nacos/v3/health/readiness`; `start_period` 60s → 90s for 3.2.4 boot
- Tests run with `NACOS_AUTH_ENABLE=false`; auth-on integration tests out of scope
- Branch: `AIRegistry` (current)

---

## Phase 1 — Foundation (PR1)

### Task 1.1: Update compose files to Nacos 3.2.4

**Files:**
- Modify: `deploy/docker-compose/docker-compose.yml:11` (image tag)
- Modify: `deploy/docker-compose/docker-compose.mysql.yml:11` (image tag)
- Modify: `deploy/docker-compose/docker-compose.cluster.yml:37,81,125` (three node image tags)

**Step 1:** Replace `nacos/nacos-server:v3.1.1` with `nacos/nacos-server:v3.2.4` in all three files. Use `replace_all: true` per file (each file has only one tag at the top, but cluster has 3 — use replace_all to make sure no stray references).

**Step 2:** Run a sanity check that no `3.1.1` reference remains.

```bash
cd E:/GitHub/RedNb.Nacos && grep -rn "3\.1\.1" deploy/
```

Expected: zero matches.

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add deploy/docker-compose/docker-compose.yml deploy/docker-compose/docker-compose.mysql.yml deploy/docker-compose/docker-compose.cluster.yml
git commit -m "chore(deploy): bump Nacos compose images to v3.2.4"
```

---

### Task 1.2: Update compose healthcheck URL to v3

**Files:**
- Modify: `deploy/docker-compose/docker-compose.cluster.yml:72,116,160`

**Step 1:** Replace all three occurrences of `http://localhost:8848/nacos/v1/console/health/readiness` with `http://localhost:8848/nacos/v3/health/readiness`. Use `replace_all: true`.

**Step 2:** Sanity check

```bash
cd E:/GitHub/RedNb.Nacos && grep -rn "v1/console/health" deploy/
```

Expected: zero matches.

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add deploy/docker-compose/docker-compose.cluster.yml
git commit -m "chore(deploy): update cluster healthcheck to Nacos 3.x endpoint"
```

---

### Task 1.3: Update compose scripts and docs to 3.2.4

**Files:**
- Modify: `deploy/docker-compose/start.sh` (Chinese comments use GBK encoding — read with caution)
- Modify: `deploy/docker-compose/start.bat`
- Modify: `deploy/docker-compose/README.md`
- Modify: `CONTRIBUTING.md`
- Modify: `deploy/docker-compose/docker-compose.yml` (line 3 `# Version: 3.1.1` header comment AND any stale `v1/console/health` URL)
- Modify: `deploy/docker-compose/docker-compose.mysql.yml` (line 3 `# Version: 3.1.1` header comment AND any stale `v1/console/health` URL)
- Modify: `deploy/docker-compose/docker-compose.cluster.yml` (line 3 `# Version: 3.1.1` header comment)

> **Amendment (2026-09-10):** The 3 YAML header comments above are part of Task 1.3's scope. They were originally missed by the pre-flight scan and are added here as a plan defect fix. **Second amendment:** Task 1.2's brief left `docker-compose.yml:31` and `docker-compose.mysql.yml:69` with stale `v1/console/health` URLs (cluster.yml is the only file Task 1.2's brief updated). These are added to Task 1.3's scope so the post-Task-1.3 grep target of zero matches is achievable. Ledger entries: "Task 1.1 ruling" and "Task 1.2 ruling" in `.superpowers/sdd/2026-09-10-nacos-v3-migration/progress.md`.

**Step 1:** In each file, replace `3.1.1` with `3.2.4` in version references only. **Be careful with `start.sh` and `start.bat`** — they are GBK-encoded; use `Read` first to inspect, then `Edit` with the exact bytes you saw. If `Edit` corrupts encoding, run:

```bash
cd E:/GitHub/RedNb.Nacos
git diff deploy/docker-compose/start.sh deploy/docker-compose/start.bat
```

…and if corrupted, restore via:

```bash
cd E:/GitHub/RedNb.Nacos
git checkout HEAD -- deploy/docker-compose/start.sh deploy/docker-compose/start.bat
```

Then re-apply edits one file at a time and verify `git diff` shows only the version bump.

**Step 2:** Verify

```bash
cd E:/GitHub/RedNb.Nacos && grep -rn "3\.1\.1" deploy/ CONTRIBUTING.md
```

Expected: zero matches.

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add deploy/docker-compose/start.sh deploy/docker-compose/start.bat deploy/docker-compose/README.md CONTRIBUTING.md
git commit -m "chore(deploy): update compose scripts and docs to Nacos 3.2.4"
```

---

### Task 1.4: Update NacosServerFixture to v3 readiness probe

**Files:**
- Modify: `tests/RedNb.Nacos.IntegrationTests/NacosServerFixture.cs`

**Step 1:** Find the readiness probe URL (currently `/nacos/v1/console/health/readiness` per existing patterns).

**Step 2:** Replace it with `/nacos/v3/health/readiness`. Locate any other v1 healthcheck references and replace.

**Step 3:** Bump the readiness wait timeout. Find the `start_period` constant (60s) and bump to 90s. If the constant is not present, find the readiness polling logic and adjust the maximum wait from ~60s to ~90s.

**Step 4:** Build and validate

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0
```

Expected: build succeeds (no API breakage from the URL string change).

**Step 5:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add tests/RedNb.Nacos.IntegrationTests/NacosServerFixture.cs
git commit -m "test(fixture): use Nacos 3.x readiness endpoint, extend start_period"
```

---

### Task 1.5: Validate PR1 — Nacos 3.2.4 starts, tests fail forward

**Step 1:** Bring up Nacos 3.2.4

```bash
cd E:/GitHub/RedNb.Nacos/deploy/docker-compose
docker-compose down -v
docker-compose up -d
# wait ~90s for startup
docker-compose logs -f nacos 2>&1 | head -50
```

Expected: container starts; logs show "Nacos started successfully in stand-alone mode" or equivalent; no error.

**Step 2:** Verify readiness via curl

```bash
curl -sf http://localhost:8848/nacos/v3/health/readiness && echo OK
```

Expected: `OK`.

**Step 3:** Run the existing integration suite to confirm it fails forward (every v1 endpoint returns 404)

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 --logger "console;verbosity=normal"
```

Expected: ~25 tests fail with HTTP 404 or connection errors on the old v1 endpoints. **This is the expected "fail forward" state** — record the failure count in the PR description.

**Step 4:** Tear down

```bash
cd E:/GitHub/RedNb.Nacos/deploy/docker-compose
docker-compose down -v
```

PR1 complete. Tag the commit:

```bash
cd E:/GitHub/RedNb.Nacos
git tag phase1-foundation
```

---

## Phase 2 — Auth (PR2)

### Task 2.1: Update SecurityProxy to /v3/auth/user/login

**Files:**
- Modify: `src/RedNb.Nacos.Http/Http/SecurityProxy.cs:90` (login endpoint)

**Step 1:** Read the current `LoginAsync` method and identify the endpoint string (`/v1/auth/login`).

**Step 2:** Replace the endpoint with `/v3/auth/user/login`. Verify response fields (`accessToken`, `tokenTtl`, `globalAdmin`, `username`) are unchanged — v3 keeps the same response shape.

**Step 3:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj
```

Expected: build succeeds.

**Step 4:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Http/SecurityProxy.cs
git commit -m "feat(http): switch SecurityProxy login to Nacos 3.x /v3/auth/user/login"
```

---

### Task 2.2: Validate PR2 — Auth flow works against 3.2.4

**Step 1:** Bring up Nacos 3.2.4 with auth enabled

```bash
cd E:/GitHub/RedNb.Nacos/deploy/docker-compose
NACOS_AUTH_ENABLE=true docker-compose up -d
# wait ~90s
```

**Step 2:** Manually verify login via curl

```bash
curl -s -X POST 'http://localhost:8848/nacos/v3/auth/user/login' \
  -d 'username=nacos&password=nacos' | python -m json.tool
```

Expected: JSON with `data.accessToken` present (the response is `{code, message, data: {accessToken, tokenTtl, ...}}`).

**Step 3:** Bring down with auth disabled for the next phase

```bash
cd E:/GitHub/RedNb.Nacos/deploy/docker-compose
docker-compose down -v
docker-compose up -d
# wait ~90s; default config has NACOS_AUTH_ENABLE=false
```

PR2 complete. Tag:

```bash
cd E:/GitHub/RedNb.Nacos
git tag phase2-auth
```

---

## Phase 3 — Config Service (PR3)

### Task 3.1: Add ConfigRpcPaths constants

**Files:**
- Create: `src/RedNb.Nacos.Grpc/Config/ConfigRpcPaths.cs`

**Step 1:** Create the file with the gRPC `Metadata.type` strings used by Config RPCs:

```csharp
namespace RedNb.Nacos.Grpc.Config;

/// <summary>
/// gRPC Metadata.type strings used by Config bi-stream requests and responses.
/// Matches the values the Nacos 3.x Java gRPC client dispatches on.
/// </summary>
internal static class ConfigRpcPaths
{
    public const string ConfigQueryRequest = "ConfigQueryRequest";
    public const string ConfigQueryResponse = "ConfigQueryResponse";
    public const string ConfigPublishRequest = "ConfigPublishRequest";
    public const string ConfigPublishResponse = "ConfigPublishResponse";
    public const string ConfigRemoveRequest = "ConfigRemoveRequest";
    public const string ConfigRemoveResponse = "ConfigRemoveResponse";
    public const string ConfigBatchListenRequest = "ConfigBatchListenRequest";
    public const string ConfigBatchListenResponse = "ConfigBatchListenResponse";
    public const string ConfigChangeNotifyRequest = "ConfigChangeNotifyRequest";
    public const string ConfigFuzzyWatchRequest = "ConfigFuzzyWatchRequest";
    public const string ConfigFuzzyWatchResponse = "ConfigFuzzyWatchResponse";
    public const string ConfigFuzzyWatchChangeNotifyRequest = "ConfigFuzzyWatchChangeNotifyRequest";
    public const string ConnectionSetupRequest = "ConnectionSetupRequest";
    public const string ConnectionSetupResponse = "ConnectionSetupResponse";
    public const string HealthCheckRequest = "HealthCheckRequest";
}
```

**Step 2:** Build to confirm the file is reachable

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj
```

Expected: build succeeds.

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Grpc/Config/ConfigRpcPaths.cs
git commit -m "feat(grpc): add ConfigRpcPaths constants for gRPC bi-stream dispatch"
```

---

### Task 3.2: Write failing test for ConfigRpcTransportClient.QueryAsync

**Files:**
- Test: `tests/RedNb.Nacos.Grpc.Tests/Config/ConfigRpcTransportClientTests.cs` (create the test project if absent; mirror the existing `tests/RedNb.Nacos.Http.Tests` structure)

**Step 1:** If `tests/RedNb.Nacos.Grpc.Tests/` does not exist, create it as an xUnit test project:

```bash
cd E:/GitHub/RedNb.Nacos/tests
dotnet new xunit -n RedNb.Nacos.Grpc.Tests -f net8.0
dotnet add RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj reference ../src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj
dotnet sln add tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj
```

**Step 2:** Write the failing test:

```csharp
using System.Text.Json;
using RedNb.Nacos.Grpc.Config;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Config;

public class ConfigRpcTransportClientTests
{
    [Fact]
    public async Task QueryAsync_BuildsRequestWithDataIdGroupTenant()
    {
        // Arrange: a fake bi-stream capture
        var captured = new List<(string type, byte[] body)>();
        var client = new ConfigRpcTransportClient(/* fake bi-stream */);

        // Act
        var result = await client.QueryAsync(dataId: "foo", group: "G", tenant: "", ct: default);

        // Assert: request was sent
        Assert.Contains(captured, c => c.type == ConfigRpcPaths.ConfigQueryRequest);
        var payload = JsonSerializer.Deserialize<ConfigQueryRequestRequest>(captured.Single(c => c.type == ConfigRpcPaths.ConfigQueryRequest).body)!;
        Assert.Equal("foo", payload.DataId);
        Assert.Equal("G", payload.Group);
    }
}
```

**Step 3:** Run test, verify it fails (the fake infra doesn't compile, which is the point)

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net8.0 --filter "FullyQualifiedName~QueryAsync_BuildsRequestWithDataIdGroupTenant"
```

Expected: **compile error** or test failure (the fake infrastructure doesn't exist yet — that's TDD).

**Step 4:** Commit the failing test scaffold

```bash
cd E:/GitHub/RedNb.Nacos
git add tests/RedNb.Nacos.Grpc.Tests/
git commit -m "test(grpc): scaffold ConfigRpcTransportClient test with failing QueryAsync"
```

---

### Task 3.3: Implement ConfigRpcTransportClient bi-stream wrapper skeleton

**Files:**
- Modify: `src/RedNb.Nacos.Grpc/Config/ConfigRpcTransportClient.cs`

**Step 1:** Read the current `ConfigRpcTransportClient` to understand its constructor and existing helpers.

**Step 2:** Add a `QueryAsync` method that:
1. Builds a `ConfigQueryRequestRequest` with the dataId/group/tenant
2. Serializes it via the same JSON helper the existing transport uses
3. Wraps in a `Payload { Metadata = { type = ConfigQueryRequest }, Body = { value = json } }`
4. Sends via `RequestAsync` and awaits the response
5. Deserializes the response into `ConfigQueryResponseResponse`
6. Returns the response (or null if `Content` empty)

**Step 3:** Add a corresponding `PublishAsync` and `RemoveAsync` that follow the same pattern.

**Step 4:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj
```

Expected: build succeeds.

**Step 5:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Grpc/Config/ConfigRpcTransportClient.cs
git commit -m "feat(grpc): ConfigRpcTransportClient Query/Publish/Remove over bi-stream"
```

---

### Task 3.4: Implement ConfigRpcTransportClient listen push consumer

**Files:**
- Modify: `src/RedNb.Nacos.Grpc/Config/ConfigRpcTransportClient.cs`

**Step 1:** Add a `SubscribeAsync(configListenContexts)` method that sends a `ConfigBatchListenRequest { listen: true, configListenContexts }` and registers a push handler for `ConfigChangeNotifyRequest`.

**Step 2:** Add an `UnsubscribeAsync(configListenContexts)` mirroring with `Listen: false`.

**Step 3:** Add a `FuzzyWatchAsync(...)` method for fuzzy pattern watch that registers a handler for `ConfigFuzzyWatchChangeNotifyRequest`.

**Step 4:** Add a `RegisterListener(changedConfigs, handler)` event-style callback that `ConfigListenerManager` can subscribe to.

**Step 5:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj
```

**Step 6:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Grpc/Config/ConfigRpcTransportClient.cs
git commit -m "feat(grpc): ConfigRpcTransportClient listen/fuzzy watch push handlers"
```

---

### Task 3.5: Rewrite NacosConfigService to use ConfigRpcTransportClient

**Files:**
- Modify: `src/RedNb.Nacos.Http/Config/NacosConfigService.cs` (refactor to partial)
- Create: `src/RedNb.Nacos.Http/Config/NacosConfigService.Grpc.cs` (new partial holding CRUD via gRPC)

**Step 1:** Convert `NacosConfigService` into a partial class. Remove all HTTP-based method bodies from the original, leaving only the constructor and shared state.

**Step 2:** In the new `NacosConfigService.Grpc.cs`, implement `GetConfigAsync`, `PublishConfigAsync`, `PublishConfigCasAsync`, `RemoveConfigAsync`, `FuzzyWatchAsync`, `CancelFuzzyWatchAsync`, `GetServerStatus`, `ShutdownAsync` — each delegating to the corresponding `ConfigRpcTransportClient` method.

**Step 3:** Wire the new partial into the existing constructor; ensure `_configRpcTransport` is initialized in `NacosFactory`.

**Step 4:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj
```

Expected: build succeeds.

**Step 5:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Config/NacosConfigService.cs src/RedNb.Nacos.Http/Config/NacosConfigService.Grpc.cs
git commit -m "feat(config): rewrite NacosConfigService to use gRPC bi-stream"
```

---

### Task 3.6: Rewrite ConfigListenerManager to consume gRPC push

**Files:**
- Modify: `src/RedNb.Nacos.Http/Config/ConfigListenerManager.cs`

**Step 1:** Identify the long-poll POST and the listener scheduler. Replace the `PostWithHeadersAsync` long-poll with a subscription on `ConfigRpcTransportClient.RegisterListener(changedConfigs, handler)` events.

**Step 2:** Maintain the existing `(dataId, group, md5)` cache and listener map; on push, iterate listeners and invoke `IConfigChangeListener.OnChanged(configInfo)`.

**Step 3:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj
```

**Step 4:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Config/ConfigListenerManager.cs
git commit -m "feat(config): switch ConfigListenerManager from HTTP long-poll to gRPC push"
```

---

### Task 3.7: Add ConfigListenerPushTests integration test

**Files:**
- Create: `tests/RedNb.Nacos.IntegrationTests/Config/ConfigListenerPushTests.cs`

**Step 1:** Write the test using `NacosServerFixture`:

```csharp
using RedNb.Nacos.Core.Config;
using RedNb.Nacos.Client.Config;
using Xunit;
using Xunit.Abstractions;

namespace RedNb.Nacos.IntegrationTests.Config;

public class ConfigListenerPushTests : IClassFixture<NacosServerFixture>
{
    private readonly NacosServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ConfigListenerPushTests(NacosServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task PublishAfterRegister_ListenerReceivesChangeWithin5Seconds()
    {
        var config = await _fixture.GetConfigServiceAsync();
        var dataId = $"test-{Guid.NewGuid():N}";
        var group = "DEFAULT_GROUP";
        var receivedContent = new TaskCompletionSource<string>();

        // Register listener
        config.AddListener(dataId, group, new TestListener(receivedContent));

        // Wait briefly for the listener registration to flush
        await Task.Delay(500);

        // Publish initial content
        await config.PublishConfigAsync(dataId, group, "v1", ct: default);

        // Update content
        await config.PublishConfigAsync(dataId, group, "v2", ct: default);

        // Wait for the push
        var winner = await Task.WhenAny(receivedContent.Task, Task.Delay(5000));
        Assert.True(winner == receivedContent.Task, "Listener did not receive push within 5s");
        var pushedContent = await receivedContent.Task;
        _output.WriteLine($"Listener received: {pushedContent}");
    }

    private sealed class TestListener : IConfigChangeListener
    {
        private readonly TaskCompletionSource<string> _tcs;
        public TestListener(TaskCompletionSource<string> tcs) => _tcs = tcs;
        public void OnChanged(ConfigResponse configInfo)
        {
            _tcs.TrySetResult(configInfo.Content ?? string.Empty);
        }
    }
}
```

**Step 2:** Run the test — expect **PASS** (this is the first green test against 3.2.4)

```bash
cd E:/GitHub/RedNb.Nacos
# Make sure Nacos 3.2.4 is up
cd deploy/docker-compose && docker-compose up -d && cd ../..
dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 \
  --filter "FullyQualifiedName~ConfigListenerPushTests"
```

Expected: PASS.

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add tests/RedNb.Nacos.IntegrationTests/Config/ConfigListenerPushTests.cs
git commit -m "test(config): add ConfigListenerPushTests for gRPC push validation"
```

---

### Task 3.8: Run full integration + unit tests for PR3

**Step 1:** Run the integration suite

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0
```

Expected: all `ConfigServiceIntegrationTests` + new `ConfigListenerPushTests` PASS; Naming and AI tests still fail (those come in PR4/PR5). AI tests may also pass if their endpoints already work; that's a bonus.

**Step 2:** Run unit tests

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net8.0
dotnet test tests/RedNb.Nacos.Http.Tests/RedNb.Nacos.Http.Tests.csproj -f net8.0
dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net8.0
```

Expected: all green.

**Step 3:** Tag

```bash
cd E:/GitHub/RedNb.Nacos
git tag phase3-config
```

PR3 complete.

---

## Phase 4 — Naming Service (PR4)

### Task 4.1: Add NamingApiPaths constants

**Files:**
- Create: `src/RedNb.Nacos.Http/Naming/NamingApiPaths.cs`

**Step 1:** Create the file:

```csharp
namespace RedNb.Nacos.Http.Naming;

/// <summary>
/// Nacos 3.x HTTP v3 endpoint paths for Naming Service.
/// All paths are appended to NacosClientOptions.ContextPath (default "nacos").
/// </summary>
internal static class NamingApiPaths
{
    public const string RegisterInstance = "v3/client/ns/instance";
    public const string DeregisterInstance = "v3/client/ns/instance";
    public const string ListInstances = "v3/client/ns/instance/list";
    public const string ListServices = "v3/console/ns/service/list";

    /// <summary>HTTP method for register/heartbeat (heartbeat sets the form key "heartBeat=true").</summary>
    public const string HttpPost = "POST";
    public const string HttpDelete = "DELETE";
    public const string HttpGet = "GET";
}
```

**Step 2:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj
```

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Naming/NamingApiPaths.cs
git commit -m "feat(http): add NamingApiPaths constants for v3 endpoints"
```

---

### Task 4.2: Rewrite NacosNamingService register/deregister/list to HTTP v3

**Files:**
- Modify: `src/RedNb.Nacos.Http/Naming/NacosNamingService.cs`

**Step 1:** Update `RegisterInstanceAsync` to POST `/v3/client/ns/instance` with form-encoded body containing: `serviceName`, `ip`, `port`, `namespaceId`, `groupName`, `clusterName`, `healthy`, `weight`, `enabled`, `metadata` (JSON), `ephemeral`. Parse `{code, message, data}` response.

**Step 2:** Update `DeregisterInstanceAsync` to DELETE `/v3/client/ns/instance` with the same query keys; same response parsing.

**Step 3:** Update `GetAllInstancesAsync`, `SelectInstancesAsync`, `SelectOneHealthyInstanceAsync` to GET `/v3/client/ns/instance/list`; response is `{code, message, data: ServiceInfo}`.

**Step 4:** Update `GetServicesOfServerAsync` to GET `/v3/console/ns/service/list`; response is `{code, message, data: {count, pageItems: string[]}}`. Map `pageItems` → `ListView<string>.Data` (existing C# property).

**Step 5:** Update `IsServiceAvailableAsync` to use `GetAllInstancesAsync` with `healthyOnly=true`; service available when `Hosts.Count >= 1`.

**Step 6:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj
```

**Step 7:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Naming/NacosNamingService.cs
git commit -m "feat(naming): rewrite Naming CRUD to Nacos 3.x HTTP v3 endpoints"
```

---

### Task 4.3: Update BeatReactor to use SendHeartbeatAsync (merged endpoint)

**Files:**
- Modify: `src/RedNb.Nacos.Http/Naming/BeatReactor.cs`

**Step 1:** Replace the existing `SendBeatAsync` PUT call with a new `SendHeartbeatAsync` that POSTs `/v3/client/ns/instance` with form fields: `serviceName`, `ip`, `port`, `namespaceId`, `groupName`, `clusterName`, `ephemeral`, `heartBeat=true`, and `beat=<BeatInfo JSON>`.

**Step 2:** On error code `21003` (instance expired), trigger full `RegisterInstanceAsync` via the parent `NacosNamingService` to repopulate metadata.

**Step 3:** Keep the existing 5s interval default and the `Instance.InstanceHeartBeatInterval` override behaviour.

**Step 4:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj
```

**Step 5:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Naming/BeatReactor.cs
git commit -m "feat(naming): BeatReactor uses merged /v3/client/ns/instance heartbeat"
```

---

### Task 4.4: Wire NamingRpcTransportClient for subscribe push

**Files:**
- Modify: `src/RedNb.Nacos.Grpc/Naming/NamingRpcTransportClient.cs`

**Step 1:** Add a `SubscribeAsync(serviceName, groupName, clusters)` method that sends `SubscribeServiceRequest { serviceName, groupName, subscribe: true }` and registers a push handler for `NotifySubscriberRequest`.

**Step 2:** Add `UnsubscribeAsync(...)` mirroring with `subscribe: false`.

**Step 3:** Add `FuzzyWatchAsync(serviceNamePattern, groupNamePattern)` and `CancelFuzzyWatchAsync(...)` that listen for `NamingFuzzyWatchNotifyRequest` push.

**Step 4:** Add `RegisterPushHandler(changedType, handler)` for the consumer side.

**Step 5:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj
```

**Step 6:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Grpc/Naming/NamingRpcTransportClient.cs
git commit -m "feat(grpc): NamingRpcTransportClient subscribe push over bi-stream"
```

---

### Task 4.5: Update NamingServiceInfoHolder for push events

**Files:**
- Modify: `src/RedNb.Nacos.Http/Naming/NamingServiceInfoHolder.cs`

**Step 1:** Keep the TTL `Expired()` check from commit `1b7649f` (do not remove).

**Step 2:** Add a public `OnNotify(ServiceInfo)` method invoked by `InstancesChangeNotifier` when the gRPC push fires.

**Step 3:** In `OnNotify`, call `ProcessServiceInfo` with the new payload (already exists in the file).

**Step 4:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj
```

**Step 5:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Naming/NamingServiceInfoHolder.cs
git commit -m "feat(naming): ServiceInfoHolder consumes gRPC push via OnNotify"
```

---

### Task 4.6: Update InstancesChangeNotifier for gRPC subscribe

**Files:**
- Modify: `src/RedNb.Nacos.Http/Naming/InstancesChangeNotifier.cs`

**Step 1:** Replace any internal long-poll wiring with a subscription to `NamingRpcTransportClient.RegisterPushHandler(...)`.

**Step 2:** On push, fetch the latest `ServiceInfo` via `NacosNamingService.GetServiceInfoAsync(...)` (HTTP v3 call from Task 4.2) and call `ServiceInfoHolder.OnNotify(info)`.

**Step 3:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj
```

**Step 4:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Naming/InstancesChangeNotifier.cs
git commit -m "feat(naming): InstancesChangeNotifier consumes gRPC subscribe push"
```

---

### Task 4.7: Add NamingSubscribePushTests integration test

**Files:**
- Create: `tests/RedNb.Nacos.IntegrationTests/Naming/NamingSubscribePushTests.cs`

**Step 1:** Write the test:

```csharp
using RedNb.Nacos.Core.Naming;
using RedNb.Nacos.Client.Naming;
using Xunit;
using Xunit.Abstractions;

namespace RedNb.Nacos.IntegrationTests.Naming;

public class NamingSubscribePushTests : IClassFixture<NacosServerFixture>
{
    private readonly NacosServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public NamingSubscribePushTests(NacosServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task RegisterAfterSubscribe_ListenerReceivesChangeWithin5Seconds()
    {
        var naming = await _fixture.GetNamingServiceAsync();
        var serviceName = $"svc-{Guid.NewGuid():N}";
        var groupName = "DEFAULT_GROUP";

        var seenChange = new TaskCompletionSource<List<Instance>>();
        naming.Subscribe(serviceName, groupName, new TestListener(seenChange));

        // Allow subscription registration to flush
        await Task.Delay(500);

        await naming.RegisterInstanceAsync(serviceName, groupName,
            new Instance { Ip = "10.0.0.1", Port = 8080, Ephemeral = true },
            ct: default);

        var winner = await Task.WhenAny(seenChange.Task, Task.Delay(5000));
        Assert.True(winner == seenChange.Task, "Naming subscribe did not receive push within 5s");
        var hosts = await seenChange.Task;
        Assert.NotEmpty(hosts);
    }

    private sealed class TestListener : IEventListener
    {
        private readonly TaskCompletionSource<List<Instance>> _tcs;
        public TestListener(TaskCompletionSource<List<Instance>> tcs) => _tcs = tcs;
        public void OnChanged(EventEventInfo eventInfo)
        {
            if (eventInfo.ServiceName != null && eventInfo.Instances != null)
                _tcs.TrySetResult(eventInfo.Instances);
        }
    }
}
```

(Adjust `IEventListener` / `EventInfo` / `Instances` to match the actual public API exposed by `INamingService.Subscribe`.`)

**Step 2:** Run

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 \
  --filter "FullyQualifiedName~NamingSubscribePushTests"
```

Expected: PASS.

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add tests/RedNb.Nacos.IntegrationTests/Naming/NamingSubscribePushTests.cs
git commit -m "test(naming): add NamingSubscribePushTests for gRPC subscribe validation"
```

---

### Task 4.8: Add HeartbeatExpiryTests integration test

**Files:**
- Create: `tests/RedNb.Nacos.IntegrationTests/Naming/HeartbeatExpiryTests.cs`

**Step 1:** Write the test:

```csharp
using RedNb.Nacos.Core.Naming;
using RedNb.Nacos.Client.Naming;
using Xunit;

namespace RedNb.Nacos.IntegrationTests.Naming;

public class HeartbeatExpiryTests : IClassFixture<NacosServerFixture>
{
    private readonly NacosServerFixture _fixture;

    public HeartbeatExpiryTests(NacosServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RegisterEphemeral_WithShortIpDeleteTimeout_ClientReRegistersOn21003()
    {
        var naming = await _fixture.GetNamingServiceAsync();
        var serviceName = $"svc-{Guid.NewGuid():N}";
        var groupName = "DEFAULT_GROUP";
        var instance = new Instance
        {
            Ip = "10.0.0.2",
            Port = 8080,
            Ephemeral = true,
            InstanceHeartBeatTimeOut = 3000,   // 3s expiry for the test
            InstanceHeartBeatInterval = 1000, // 1s heartbeat
        };

        await naming.RegisterInstanceAsync(serviceName, groupName, instance, ct: default);

        // Stop heartbeats by setting interval to a huge value and waiting past expiry.
        instance.InstanceHeartBeatInterval = 60_000;
        await Task.Delay(5000); // wait past 3s expiry

        // Client should now observe 21003 and re-register
        var all = await naming.GetAllInstancesAsync(serviceName, groupName, ct: default);
        Assert.NotEmpty(all);
    }
}
```

(Adjust `InstanceHeartBeatTimeOut` / `InstanceHeartBeatInterval` to match the real property names on `Instance`.`)

**Step 2:** Run

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 \
  --filter "FullyQualifiedName~HeartbeatExpiryTests"
```

Expected: PASS (proves the 21003 → re-register path).

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add tests/RedNb.Nacos.IntegrationTests/Naming/HeartbeatExpiryTests.cs
git commit -m "test(naming): add HeartbeatExpiryTests for 21003 auto-re-register"
```

---

### Task 4.9: Run full test suite for PR4

**Step 1:** Bring Nacos 3.2.4 up

```bash
cd E:/GitHub/RedNb.Nacos/deploy/docker-compose
docker-compose up -d
# wait ~90s
```

**Step 2:** Run all four integration suites

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0
```

Expected: `ConfigServiceIntegrationTests` + `NamingServiceIntegrationTests` + `NamingSelectorIntegrationTests` + `AiServiceIntegrationTests` + the three new push/expiry tests **all pass** (or only AI tests fail — that's PR5).

**Step 3:** Run unit tests

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net8.0
dotnet test tests/RedNb.Nacos.Http.Tests/RedNb.Nacos.Http.Tests.csproj -f net8.0
dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net8.0
```

Expected: all green.

**Step 4:** Tag

```bash
cd E:/GitHub/RedNb.Nacos
git tag phase4-naming
```

PR4 complete.

---

## Phase 5 — AI + Obsolete + Docs (PR5)

### Task 5.1: Validate AI endpoints against 3.2.4 and fix drift

**Files:**
- Possibly modify: `src/RedNb.Nacos.Http/Ai/NacosPromptService.cs`, `NacosSkillService.cs`, `NacosAgentSpecService.cs`, `NacosAiService.cs`

**Step 1:** With Nacos 3.2.4 up, run the AI suite

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.Http.Tests/RedNb.Nacos.Http.Tests.csproj -f net8.0 \
  --filter "FullyQualifiedName~Ai"
dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 \
  --filter "FullyQualifiedName~AiService"
```

Expected: most pass; some may fail due to wire-format drift.

**Step 2:** For any failing test, examine the failure mode (404, JSON parse error, missing field). Fix the corresponding endpoint in the appropriate `NacosXxxService.cs`. Common adjustments:
- `tagsAll` / `bizTags` field encoding (CSV vs JSON array)
- `PageResult` field name corrections
- Endpoint path corrections (e.g., `v3/admin/ai/a2a/update` if `UpdateAgentCard` is missing)

**Step 3:** Re-run after each fix until all AI tests pass.

**Step 4:** Commit each discovered fix in a separate commit for traceability

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos.Http/Ai/<file>
git commit -m "fix(ai): <short description of the drift fix>"
```

---

### Task 5.2: Mark [Obsolete] on Maintainer interfaces

**Files:**
- Modify: `src/RedNb.Nacos/Maintainer/IMaintainerService.cs`
- Modify: `src/RedNb.Nacos/Maintainer/IServiceMaintainer.cs`
- Modify: `src/RedNb.Nacos/Maintainer/IInstanceMaintainer.cs`
- Modify: `src/RedNb.Nacos/Maintainer/INamingMaintainer.cs`
- Modify: `src/RedNb.Nacos/Maintainer/IConfigMaintainer.cs`
- Modify: `src/RedNb.Nacos/Maintainer/IConfigHistoryMaintainer.cs`
- Modify: `src/RedNb.Nacos/Maintainer/IBetaConfigMaintainer.cs`
- Modify: `src/RedNb.Nacos/Maintainer/IConfigOpsMaintainer.cs`
- Modify: `src/RedNb.Nacos/Maintainer/IClientMaintainer.cs`
- Modify: `src/RedNb.Nacos/Maintainer/ICoreMaintainer.cs`

**Step 1:** Add to each interface file:

```csharp
[Obsolete(
    "Nacos 3.2+ removed v1/v2 HTTP endpoints used by this service. " +
    "Calls will return HTTP 404 at runtime. " +
    "Migrate to Nacos 2.x server or use the nacos-api-legacy-adapter JAR. " +
    "Tracked for removal in the next major version.",
    error: false)]
```

above the interface declaration. Use `replace_all: true` is unnecessary — there's only one declaration per file.

**Step 2:** Build and verify warning fires (but build succeeds)

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos/RedNb.Nacos.csproj
```

Expected: build succeeds with `warning CS0618: 'IMaintainerService' is obsolete` etc.

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos/Maintainer/
git commit -m "chore(maintainer): mark Maintainer interfaces [Obsolete] (Nacos 3.x v1/v2 removed)"
```

---

### Task 5.3: Mark [Obsolete] on ILockService interface

**Files:**
- Modify: `src/RedNb.Nacos/Lock/ILockService.cs`

**Step 1:** Add to `ILockService.cs`:

```csharp
[Obsolete(
    "ILockService is preserved as a marker for the legacy lock API on Nacos 3.x. " +
    "The underlying /nacos/v3/lock endpoints still function but the interface is " +
    "scheduled for removal. Use a dedicated lock library or a custom integration instead.",
    error: false)]
```

above the interface declaration.

**Step 2:** Build

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build src/RedNb.Nacos/RedNb.Nacos.csproj
```

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add src/RedNb.Nacos/Lock/ILockService.cs
git commit -m "chore(lock): mark ILockService [Obsolete] for removal in next major"
```

---

### Task 5.4: Update README with min server version and Obsolete notice

**Files:**
- Modify: `README.md`

**Step 1:** Find the section that lists "Supported Nacos versions" or similar. Add:

```markdown
## ⚠️ Breaking: Nacos 3.2.0+ Required

This client (post-v1.x release) targets Nacos 3.2.0+ exclusively. v1/v2 HTTP
endpoints are no longer used; `Maintainer` and `Lock` interfaces are marked
`[Obsolete]` and will be removed in the next major version. See
`docs/MIGRATION.md` for upgrade details.
```

**Step 2:** Build (sanity — README is markdown, no build step, but verify the existing samples still build)

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build
```

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add README.md
git commit -m "docs(readme): declare Nacos 3.2.0+ requirement, point to MIGRATION.md"
```

---

### Task 5.5: Create docs/MIGRATION.md

**Files:**
- Create: `docs/MIGRATION.md`

**Step 1:** Create the file with the following content:

```markdown
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
```

**Step 2:** Sanity — verify the markdown renders sensibly (just open it in your editor; no build).

**Step 3:** Commit

```bash
cd E:/GitHub/RedNb.Nacos
git add docs/MIGRATION.md
git commit -m "docs: add MIGRATION.md for Nacos 3.x upgrade"
```

---

### Task 5.6: Final full test pass + tag

**Step 1:** Bring Nacos 3.2.4 up

```bash
cd E:/GitHub/RedNb.Nacos/deploy/docker-compose
docker-compose down -v
docker-compose up -d
# wait ~90s
curl -sf http://localhost:8848/nacos/v3/health/readiness && echo OK
```

**Step 2:** Run the full test matrix

```bash
cd E:/GitHub/RedNb.Nacos
dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net8.0
dotnet test tests/RedNb.Nacos.Http.Tests/RedNb.Nacos.Http.Tests.csproj -f net8.0
dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net8.0
dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0
```

Expected: **all green**. ~28-30 integration tests + 3 unit test projects.

**Step 3:** Build the full solution

```bash
cd E:/GitHub/RedNb.Nacos
dotnet build
```

Expected: succeeds with `[Obsolete]` warnings on Maintainer/Lock consumers (those are intentional).

**Step 4:** Tear down

```bash
cd E:/GitHub/RedNb.Nacos/deploy/docker-compose
docker-compose down -v
```

**Step 5:** Tag and push branch

```bash
cd E:/GitHub/RedNb.Nacos
git tag nacos-v3-migration-complete
git push origin AIRegistry
git push origin nacos-v3-migration-complete
```

PR5 complete. The migration is done.

---

## Self-Review Notes (author's checklist, not for executor)

1. **Spec coverage:** Each spec section maps to one or more tasks:
   - §1 Goals & Scope → Tasks 1.1–1.5, 5.2, 5.3
   - §2 Architecture → implied across Tasks 3.5, 4.2, 4.4 (no single task — the architecture is the sum of the service rewrites)
   - §3 Config Service → Tasks 3.1–3.8
   - §4 Naming Service → Tasks 4.1–4.9
   - §5 AI Registry → Task 5.1
   - §6 Deprecated → Tasks 5.2, 5.3
   - §7 Test infrastructure → Tasks 1.4, 3.7, 4.7, 4.8
   - §8 Migration plan → the phasing itself
2. **Placeholder scan:** No TBD / TODO / "implement later". Every step has either an explicit file/command or a code block.
3. **Type consistency:** `ConfigRpcPaths` strings are referenced consistently. `NamingApiPaths` strings match the spec. `IEventListener`/`EventInfo`/`Instances` names in the new test files are marked "adjust to actual API" — this is the only concession; the executor must verify against the real `INamingService.Subscribe` listener signature before writing the test body.
4. **TDD discipline:** Tasks 3.2 (write failing test) → 3.3 (impl) → 3.4 (push handler) → 3.5 (service rewrite) follow TDD. Tasks 4.7, 4.8 are end-to-end tests written post-rewrite — acceptable for integration-level validation. Tasks 5.1 is exploratory (fix drift); not pure TDD because the fix isn't known until the test runs.