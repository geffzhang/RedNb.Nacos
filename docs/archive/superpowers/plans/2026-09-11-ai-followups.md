# AI Follow-up Backlog (Tasks 10/11/12) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining AIRegistry AI dual-channel gaps: AgentVersionInfo rich model, gRPC auth path, response-model wire fixes, fail-loud unsupported gRPC ops, re-enabled gRPC integration tests, console-client dedup, and Validate() credential rule.

**Architecture:** All work lands on the existing AIRegistry branch (no merge/push — user decision 2026-09-11). gRPC auth reuses the proven HTTP `SecurityProxy` (the Grpc project already references RedNb.Nacos.Http — `src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj:26`) by attaching the raw JWT to the protobuf `Payload.Metadata.Headers` map under key `accessToken` (the server's `NacosAuthPluginService` identity names = `["Authorization","accessToken","username","password"]`; Java client sends `{accessToken: rawJWT}`). Unsupported operations fail loudly with `NacosException.ServerNotImplemented` (501) instead of silently swallowing or misreporting.

**Tech Stack:** C# / .NET 8 + 10 multi-target, xunit 2.9.3 + FluentAssertions 8.3.0 (+ WireMock.Net 1.6.12 in Http.Tests), Google.Protobuf gRPC client, live Nacos 3.2.4 container `nacos-server` (client 8848 / gRPC 9848 / console 8080, auth enabled, nacos/nacos).

**Spec:** `docs/superpowers/plans/2026-09-11-ai-live-validation.md` §"Follow-up backlog" (Tasks 10/11/12) + the research dossier `.tmp/ai-followups-research.md` (live-probed wire shapes, javap-verified server contracts, exact line numbers). The spec is the binding authority; this plan is its argument.

## Global Constraints

- Branch `AIRegistry`. **No merge, no push** — user decision "保持分支现状" (2026-09-11). Commit per task with the repo's conventional prefixes (`feat(ai):`, `fix(ai):`, `refactor(ai):`, `test(ai):`, `docs(ai):`).
- Do NOT restart or modify the `nacos-server` container. Live integration tests assume it is up (readiness probe `http://localhost:8080/v3/console/health/readiness`).
- Released public API must not break: `IA2aService.ListAgentVersionsAsync` pre-exists on master (commit 914102a) — keep its signature and meaning.
- Test conventions: xunit + FluentAssertions; integration tests carry `[Trait("Category", "Integration")]` + `[Trait("Module", "AI")]` and join `[Collection("NacosIntegration")]`; unit tests never hit the network (WireMock or fakes only).
- Wire facts (probed/javap-verified, trust over intuition): server `Response` base = `{resultCode,errorCode,message,requestId}` and Jackson emits `"success":true` from public `isSuccess()` — success ⇔ `resultCode==200`. Server registers only 8 AI gRPC handlers (QueryMcpServer, ReleaseMcpServer, McpServerEndpoint, QueryAgentCard, ReleaseAgentCard, AgentEndpoint, BatchAgentEndpoint, QueryPrompt); every other type string has no handler. Console HTTP lives on 8080 under `/v3/console/ai/{mcp,a2a,prompt,skills,agentspecs,import,pipelines}` with **no** `/nacos` prefix; client/admin HTTP on 8848 uses `/nacos/...`.

---

### Task 1: AgentVersionInfo model + ListAgentVersionInfosAsync (rich versions) + re-enabled live test

**Files:**
- Create: `src/RedNb.Nacos/Ai/Model/A2a/AgentVersionInfo.cs`
- Modify: `src/RedNb.Nacos/Ai/IA2aService.cs:234-240` (add new member; keep existing)
- Modify: `src/RedNb.Nacos.Http/Ai/NacosAiService.cs:779-804` (rewrite both methods)
- Modify: `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs:804-819` (gRPC: both methods fail-loud 501; delete `AgentVersionListRequest`/`AgentVersionListResponse` at :1140-1149)
- Create: `tests/RedNb.Nacos.Http.Tests/Ai/NacosAiServiceAgentVersionTests.cs`
- Modify: `tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs:134-154` (un-skip `ListAgentVersions_ReturnsVersions`, settle-aware) + add `ListAgentVersionInfos_ReturnsRichMetadata`

**Interfaces:**
- Consumes: live envelope `{"code":int,"message":string,"data":[{version,createdAt,updatedAt,latest}]}` (research §3.5, verbatim probe); `ApiResult<T>` private wrapper `NacosAiService.cs:1026-1034`; `A2aBasePath = "v3/console/ai/a2a"` (:36); `BuildNamespaceHeaders()` → empty dict (:962-968); class-private test helpers `WaitForAsync<T>` (:356-394) / `DeleteWithRetryAsync` (:401-416) already present in `AiServiceIntegrationTests`.
- Produces: public `AgentVersionInfo` in namespace `RedNb.Nacos.Core.Ai.Model.A2a`; `IA2aService.ListAgentVersionInfosAsync(string, CancellationToken) → Task<List<AgentVersionInfo>>`; gRPC `ListAgentVersionsAsync`/`ListAgentVersionInfosAsync` throwing 501 (Task 4 standardizes the remaining no-handler ops).

- [ ] **Step 1: Create the model**

Create `src/RedNb.Nacos/Ai/Model/A2a/AgentVersionInfo.cs` (mirror `AgentCard.cs` style — explicit `[JsonPropertyName]`, file-scoped namespace):

```csharp
using System.Text.Json.Serialization;

namespace RedNb.Nacos.Core.Ai.Model.A2a;

/// <summary>
/// Metadata for one released version of an agent card, as returned by the
/// console AI version-list endpoint.
/// </summary>
public class AgentVersionInfo
{
    /// <summary>
    /// Gets or sets the version string.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the creation time (ISO 8601).
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update time (ISO 8601).
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this version is the latest one.
    /// </summary>
    [JsonPropertyName("latest")]
    public bool Latest { get; set; }
}
```

- [ ] **Step 2: Extend the interface**

In `src/RedNb.Nacos/Ai/IA2aService.cs`, insert after the existing `ListAgentVersionsAsync` declaration (keep that declaration unchanged — back-compat):

```csharp
    /// <summary>
    /// Lists all versions of an agent card with their metadata (created/updated
    /// timestamps and latest flag), as served by the console AI endpoint.
    /// </summary>
    /// <param name="agentName">Name of the agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of agent version infos.</returns>
    Task<List<AgentVersionInfo>> ListAgentVersionInfosAsync(string agentName, CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Rewrite the HTTP implementation**

In `src/RedNb.Nacos.Http/Ai/NacosAiService.cs`, replace the existing `ListAgentVersionsAsync` body (:779-804) with:

```csharp
    /// <inheritdoc />
    public async Task<List<string>> ListAgentVersionsAsync(string agentName, CancellationToken cancellationToken = default)
    {
        var infos = await ListAgentVersionInfosAsync(agentName, cancellationToken);
        return infos
            .Select(i => i.Version)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<AgentVersionInfo>> ListAgentVersionInfosAsync(string agentName, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);

        var parameters = new Dictionary<string, string?>
        {
            { "agentName", agentName }
        };
        var headers = BuildNamespaceHeaders();

        try
        {
            var response = await _httpClient.GetWithHeadersAsync($"{A2aBasePath}/version/list", parameters, headers, _options.DefaultTimeout, cancellationToken);
            if (string.IsNullOrEmpty(response))
            {
                return new List<AgentVersionInfo>();
            }

            var result = JsonSerializer.Deserialize<ApiResult<List<AgentVersionInfo>>>(response, JsonOptions);
            return result?.Data ?? new List<AgentVersionInfo>();
        }
        catch (NacosException ex) when (ex.ErrorCode == NacosException.NotFound)
        {
            // Server answers 404 {"code":50100,"message":"Agent not found"} for unknown agents.
            return new List<AgentVersionInfo>();
        }
    }
```

(The file already imports `RedNb.Nacos.Core.Ai.Model.A2a` — it uses `AgentCard`.)

- [ ] **Step 4: Fail the gRPC side loudly**

In `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs`, replace `ListAgentVersionsAsync` (:805-819) and add the new member:

```csharp
    /// <inheritdoc />
    public Task<List<string>> ListAgentVersionsAsync(string agentName, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        throw new NacosException(NacosException.ServerNotImplemented,
            "AgentVersionListRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }

    /// <inheritdoc />
    public Task<List<AgentVersionInfo>> ListAgentVersionInfosAsync(string agentName, CancellationToken cancellationToken = default)
    {
        ValidateAgentName(agentName);
        throw new NacosException(NacosException.ServerNotImplemented,
            "AgentVersionListRequest has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
    }
```

Then delete the now-unreferenced private DTOs `AgentVersionListRequest` and `AgentVersionListResponse` (:1140-1149). Verify nothing else references them (`git grep "AgentVersionListRequest\|AgentVersionListResponse" src/` must return only this file's deletion — run it before deleting).

- [ ] **Step 5: Build**

Run: `dotnet build src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj -f net8.0`
Expected: PASS (if a using for `AgentVersionInfo` is missing in NacosGrpcAiService.cs, add `using RedNb.Nacos.Core.Ai.Model.A2a;`).

- [ ] **Step 6: Add WireMock unit tests**

Create `tests/RedNb.Nacos.Http.Tests/Ai/NacosAiServiceAgentVersionTests.cs` (pattern mirror of `NacosPromptServiceTests.cs`; the console client routes `/v3/console/ai/...` with no `/nacos` prefix, and `ConsoleAddresses` must point at the WireMock port because `GetConsoleAddressList()` would otherwise port-substitute to 8080):

```csharp
using FluentAssertions;
using RedNb.Nacos.Client.Ai;
using RedNb.Nacos.Core;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Ai;

/// <summary>
/// Tests for agent-version listing on the console AI channel using WireMock.
/// </summary>
public class NacosAiServiceAgentVersionTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly NacosAiService _aiService;

    public NacosAiServiceAgentVersionTests()
    {
        _server = WireMockServer.Start();
        var options = new NacosClientOptions
        {
            ServerAddresses = "",
            ConsoleAddresses = $"localhost:{_server.Port}"
        };
        _aiService = new NacosAiService(options);
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task ListAgentVersionInfosAsync_DeserializesRichObjects()
    {
        _server
            .Given(Request.Create()
                .WithPath("/v3/console/ai/a2a/version/list")
                .WithParam("agentName", "probe-agent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"code":0,"message":"success","data":[{"version":"1.0.0","createdAt":"2026-09-11T11:44:31Z","updatedAt":"2026-09-11T11:44:31Z","latest":true},{"version":"0.9.0","createdAt":"2026-09-10T10:00:00Z","updatedAt":"2026-09-10T10:00:00Z","latest":false}]}"""));

        var infos = await _aiService.ListAgentVersionInfosAsync("probe-agent");

        infos.Should().HaveCount(2);
        infos[0].Version.Should().Be("1.0.0");
        infos[0].Latest.Should().BeTrue();
        infos[0].CreatedAt.Should().Be(DateTimeOffset.Parse("2026-09-11T11:44:31Z"));
        infos[1].Version.Should().Be("0.9.0");
        infos[1].Latest.Should().BeFalse();
    }

    [Fact]
    public async Task ListAgentVersionsAsync_ProjectsVersionStrings()
    {
        _server
            .Given(Request.Create()
                .WithPath("/v3/console/ai/a2a/version/list")
                .WithParam("agentName", "probe-agent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"code":0,"message":"success","data":[{"version":"1.0.0","createdAt":"2026-09-11T11:44:31Z","updatedAt":"2026-09-11T11:44:31Z","latest":true}]}"""));

        var versions = await _aiService.ListAgentVersionsAsync("probe-agent");

        versions.Should().Equal("1.0.0");
    }
}
```

- [ ] **Step 7: Run unit tests**

Run: `dotnet test tests/RedNb.Nacos.Http.Tests/RedNb.Nacos.Http.Tests.csproj -f net8.0`
Expected: PASS, including the 2 new facts.

- [ ] **Step 8: Re-enable the live version-list test**

In `tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs`, remove the `Skip` attribute from `ListAgentVersions_ReturnsVersions` (:134) and make it settle-aware (the class-private `WaitForAsync`/`DeleteWithRetryAsync` helpers exist at :356-416):

```csharp
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ListAgentVersions_ReturnsVersions()
    {
        var agentName = $"it-agent-ver-{Guid.NewGuid():N}";
        await _aiService!.ReleaseAgentCardAsync(new AgentCard
        {
            Name = agentName,
            Version = "2.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        });

        var versions = await WaitForAsync(
            () => _aiService.ListAgentVersionsAsync(agentName),
            list => list is not null && list.Contains("2.0.0"));
        versions.Should().Contain("2.0.0");

        await DeleteWithRetryAsync(() => _aiService.DeleteAgentAsync(agentName));
    }
```

Then append the new rich-metadata test to the same class:

```csharp
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ListAgentVersionInfos_ReturnsRichMetadata()
    {
        var agentName = $"it-agent-verinfo-{Guid.NewGuid():N}";
        await _aiService!.ReleaseAgentCardAsync(new AgentCard
        {
            Name = agentName,
            Version = "2.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        });

        var infos = await WaitForAsync(
            () => _aiService.ListAgentVersionInfosAsync(agentName),
            list => list is not null && list.Count > 0);

        infos.Should().Contain(i => i.Version == "2.0.0");
        var info = infos.Single(i => i.Version == "2.0.0");
        info.Latest.Should().BeTrue();
        info.CreatedAt.Should().HaveValue();
        info.UpdatedAt.Should().HaveValue();

        await DeleteWithRetryAsync(() => _aiService.DeleteAgentAsync(agentName));
    }
```

- [ ] **Step 9: Run the two live tests**

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 --filter "FullyQualifiedName~ListAgentVersion"`
Expected: both PASS (live probe evidence: a fresh single release serves `latest:true`).

- [ ] **Step 10: Commit**

```bash
git add src/RedNb.Nacos/Ai/Model/A2a/AgentVersionInfo.cs src/RedNb.Nacos/Ai/IA2aService.cs src/RedNb.Nacos.Http/Ai/NacosAiService.cs src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs tests/RedNb.Nacos.Http.Tests/Ai/NacosAiServiceAgentVersionTests.cs tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs
git commit -m "feat(ai): add AgentVersionInfo rich model + ListAgentVersionInfosAsync

- IA2aService.ListAgentVersionInfosAsync returns {version, createdAt, updatedAt, latest}
- ListAgentVersionsAsync now projects .Version from the rich list (no API break)
- gRPC side fails loudly with ServerNotImplemented: the server has no AgentVersionListRequest handler
- re-enable ListAgentVersions_ReturnsVersions live test with settle-aware helpers"
```

---

### Task 2: gRPC auth — SecurityProxy token in Payload.Metadata.Headers

**Files:**
- Modify: `src/RedNb.Nacos.Grpc/NacosGrpcClient.cs` (ctor :130-144, 4 `CreatePayload` call sites :318/:399/:441/:601, `CreatePayload` :664-686 → async, `DisposeAsync` :790)

**Interfaces:**
- Consumes: `SecurityProxy` (`src/RedNb.Nacos.Http/Http/SecurityProxy.cs`, namespace `RedNb.Nacos.Client.Http`): ctor `SecurityProxy(NacosClientOptions, ILogger?)`, `Task<string?> GetAccessTokenAsync(CancellationToken)` — returns `null` when Username/Password blank (no login attempted), else raw JWT. Grpc csproj already references RedNb.Nacos.Http (`RedNb.Nacos.Grpc.csproj:26`).
- Produces: every outgoing gRPC `Payload` carries `Headers["accessToken"] = <raw JWT>` when credentials are configured — the exact Java-client behavior (`SecurityProxy.getIdentityContext` → `{accessToken: token}`; server `NacosAuthPluginService` identity names = `["Authorization","accessToken","username","password"]`). Gate: the 6 pre-existing failing push tests go green.

- [ ] **Step 1: Inject SecurityProxy**

In `src/RedNb.Nacos.Grpc/NacosGrpcClient.cs`, add the field next to the other private fields (top of the class) and wire it in the ctor (:130-144, after `_jsonOptions` init):

```csharp
    private readonly RedNb.Nacos.Client.Http.SecurityProxy _securityProxy;
```

```csharp
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        _securityProxy = new RedNb.Nacos.Client.Http.SecurityProxy(options, logger);
```

- [ ] **Step 2: Make CreatePayload async and attach the token**

Replace `CreatePayload` (:664-686) with:

```csharp
    private async Task<Payload> CreatePayloadAsync(ConnectionGeneration generation, string type, object request, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var body = ByteString.CopyFromUtf8(json);

        var payload = new Payload
        {
            Metadata = new ProtoMetadata
            {
                Type = type,
                ClientIp = GetLocalIp(),
                Headers =
                {
                    { "connectionId", generation.ConnectionId ?? _clientId },
                    { "clientId", _clientId }
                }
            },
            Body = new Any
            {
                Value = body
            }
        };

        // Nacos 3.x gRPC auth travels INSIDE the payload header map (not gRPC
        // call metadata): NacosAuthPluginService resolves identity from the
        // Authorization/accessToken headers, and the official Java client sends
        // the raw JWT under "accessToken" on every payload.
        var token = await _securityProxy.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            payload.Metadata.Headers["accessToken"] = token;
        }

        return payload;
    }
```

- [ ] **Step 3: Await it at all 4 call sites**

For each of the 4 `CreatePayload(generation, ...)` call sites (:318, :399, :441, :601), change to `await CreatePayloadAsync(generation, ..., cancellationToken)` — using the `CancellationToken` variable in scope at each site (all four enclosing methods are async and receive a cancellation token; the connection handshake at :399/:441, the push ack at :601, and the unary request at :318 all get the token header, matching the Java client which attaches identity to every payload). Compiler errors will name any site whose token variable name differs — adapt.

- [ ] **Step 4: Dispose the proxy**

In `DisposeAsync` (:790), alongside the existing cleanup, add:

```csharp
        _securityProxy.Dispose();
```

- [ ] **Step 5: Build + unit regression**

Run: `dotnet build src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj -f net8.0` then `dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net8.0`
Expected: build PASS; unit PASS (fakes use blank-credential options → `GetAccessTokenAsync` returns null → no login attempted, no network).

- [ ] **Step 6: Gate — the 6 push tests**

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 --filter "FullyQualifiedName~ConfigListenerPushTests|FullyQualifiedName~NamingSubscribePushTests"`
Expected: all 6 PASS (previously failed with 401 `resultCode:500, errorCode:403` — the missing `accessToken` header was the blocker; live login against `localhost:8848/nacos/v3/auth/user/login` happens inside the proxy with a 2-minute token refresh window).

- [ ] **Step 7: Full integration matrix sanity**

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0`
Expected: 0 FAIL (6 previously-failing push tests now pass; the 7 skips remain skipped — Task 5 re-enables 4 of the gRPC AI ones).

- [ ] **Step 8: Commit**

```bash
git add src/RedNb.Nacos.Grpc/NacosGrpcClient.cs
git commit -m "feat(grpc): attach accessToken to payload headers via SecurityProxy

Reproduces the Java client identity context: the raw JWT travels inside
Payload.Metadata.Headers under 'accessToken', which NacosAuthPluginService
resolves. Fixes the 401s on the 6 config/naming push integration tests and
unblocks the AI gRPC channel."
```

---

### Task 3: Response DTO wire fixes — mcpId binding + namespaceId payload key

**Files:**
- Modify: `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs` (private DTOs + all `Namespace = _namespaceId` sites; `McpServerReleaseResponse` :1015-1018 + read site :122; add test-friendly ctor at :46)
- Create: `tests/RedNb.Nacos.Grpc.Tests/Ai/NacosGrpcAiServiceTests.cs`

**Interfaces:**
- Consumes: `FakeNacosGrpcClient : NacosGrpcClient` pattern (`tests/RedNb.Nacos.Grpc.Tests/FakeNacosGrpcClient.cs` — overrides `ConnectAsync`/`RequestAsync<TResponse>`, captures `(type, request)` tuples in `Captured`, echoes `Response` when type-compatible); server-side facts: `AbstractMcpRequest` reads `namespaceId` (camelCase serialization of `NamespaceId` binds it; `Namespace` today serializes to `"namespace"` which the server ignores), `ReleaseMcpServerResponse.mcpId` (SDK read `McpServerId` → always empty).
- Produces: `NacosGrpcAiService(NacosClientOptions, NacosGrpcClient, ILogger<NacosGrpcAiService>?)` test ctor — consumed by Task 4's unit tests.
- Ruling (baked in): `OperationResponse {Success, Message}` is KEPT as-is. Its shape is correct on the wire — Jackson serializes the server `Response` base's public `isSuccess()` as `"success":true` (javap §2.3: success ⇔ `resultCode==200`), and the client's camelCase + case-insensitive options bind it to `Success`. The earlier "shape mismatch" skip reasons were a misdiagnosis; the real blocker was Task 2's missing auth. Live confirmation comes from Task 5's re-enabled endpoint test asserting no throw.

- [ ] **Step 1: Rename McpServerId → McpId**

In `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs`, change the private DTO (:1015-1018):

```csharp
    private class McpServerReleaseResponse
    {
        public string? McpId { get; set; }
    }
```

and the single read site (:122):

```csharp
        return response?.McpId ?? string.Empty;
```

(`McpId` + camelCase policy binds the server's `"mcpId"` field — verified from `ReleaseMcpServerResponse` javap.)

- [ ] **Step 2: Rename Namespace → NamespaceId on the wire**

In the same file, run: `grep -n "Namespace = _namespaceId" src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs` — rename **every** occurrence to `NamespaceId = _namespaceId`. Then rename the `Namespace` property to `NamespaceId` in **every** private request DTO that declares it (the live ones today: `McpServerReleaseRequest` :1006, `McpEndpointRequest`, `McpServerQueryRequest`, `AgentCardReleaseRequest`; any additional one the grep surfaces — e.g. subscribe/delete/list DTOs — gets the same mechanical rename; dead DTOs are deleted in Task 4 regardless). Verify with: `grep -n "public string? Namespace" src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs` → must return zero hits.

Server contract (javap): `AbstractMcpRequest`/`AbstractAgentRequest` read `namespaceId`; `"namespace"` is silently ignored and falls back to public.

- [ ] **Step 3: Add the test-friendly constructor**

Replace the existing public ctor (:46) with a delegating pair (mirrors the `NacosGrpcConfigService` test-ctor pattern):

```csharp
    public NacosGrpcAiService(NacosClientOptions options, ILogger<NacosGrpcAiService>? logger = null)
        : this(options, new NacosGrpcClient(options, logger), logger)
    {
    }

    /// <summary>
    /// Test-friendly overload: injects the gRPC client so tests can capture
    /// requests with a <c>FakeNacosGrpcClient</c> instead of a live channel.
    /// </summary>
    public NacosGrpcAiService(NacosClientOptions options, NacosGrpcClient grpcClient, ILogger<NacosGrpcAiService>? logger = null)
    {
        _options = options;
        _logger = logger;
        _grpcClient = grpcClient;
        _namespaceId = options.Namespace ?? string.Empty;

        // Prompt / Skill / AgentSpec APIs are HTTP-only on the Nacos server side,
        // so they are served by dedicated HTTP services sharing one client.
        _registryHttpClient = new RedNb.Nacos.Client.Http.NacosHttpClient(options, logger);
        _promptService = new RedNb.Nacos.Client.Ai.NacosPromptService(_registryHttpClient, options, logger);
        _skillService = new RedNb.Nacos.Client.Ai.NacosSkillService(_registryHttpClient, options, logger);
        _agentSpecService = new RedNb.Nacos.Client.Ai.NacosAgentSpecService(_registryHttpClient, options, logger);

        // Register push handler
        _grpcClient.RegisterPushHandler(HandlePushMessage);
    }
```

- [ ] **Step 4: Add wire-shape unit tests**

Create `tests/RedNb.Nacos.Grpc.Tests/Ai/NacosGrpcAiServiceTests.cs` (the captured request objects are private nested DTOs — assert on their camelCase JSON instead of naming the types):

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai.Model;
using RedNb.Nacos.Core.Ai.Model.A2a;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Ai;

/// <summary>
/// Wire-shape tests for <see cref="NacosGrpcAiService"/> using the
/// <see cref="FakeNacosGrpcClient"/> capture seam. The request DTOs are
/// private nested classes, so assertions run on the serialized camelCase JSON.
/// </summary>
public class NacosGrpcAiServiceTests
{
    private static NacosClientOptions Options => new()
    {
        ServerAddresses = "localhost:8848",
        Namespace = "test-ns"
    };

    private static McpServerBasicInfo ValidMcpServer(string name) => new()
    {
        Name = name,
        VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
        Protocol = "stdio"
    };

    private static string SerializeCamel(object request) =>
        JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    [Fact]
    public async Task ReleaseMcpServerAsync_SendsNamespaceId_NotNamespace()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        await service.ReleaseMcpServerAsync(ValidMcpServer("m1"), toolSpecification: null);

        var (type, request) = Assert.Single(fake.Captured);
        Assert.Equal("ReleaseMcpServerRequest", type);
        var json = SerializeCamel(request);
        json.Should().Contain("\"namespaceId\":\"test-ns\"");
        json.Should().NotContain("\"namespace\"");
    }

    [Fact]
    public async Task RegisterMcpServerEndpointAsync_SendsNamespaceId_NotNamespace()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        await service.RegisterMcpServerEndpointAsync("m1", "127.0.0.1", 9100, "1.0.0");

        var (type, request) = Assert.Single(fake.Captured);
        Assert.Equal("McpServerEndpointRequest", type);
        var json = SerializeCamel(request);
        json.Should().Contain("\"namespaceId\":\"test-ns\"");
        json.Should().NotContain("\"namespace\"");
    }

    [Fact]
    public async Task ReleaseMcpServerAsync_ReadsMcpIdFromResponse()
    {
        // The fake echoes a response object whose JSON contract matches the
        // server: ReleaseMcpServerResponse { mcpId }. Type-compatibility with
        // the private DTO cannot be arranged from outside, so the fake returns
        // null here and the method must degrade to string.Empty (no throw).
        // The live mcpId binding is asserted by the re-enabled
        // ReleaseAndGetMcpServer_ViaGrpc integration test (Task 5).
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var released = await service.ReleaseMcpServerAsync(ValidMcpServer("m1"), toolSpecification: null);

        released.Should().BeEmpty();
    }
}
```

Adjust the `using` block to whatever the compiler names for `McpServerBasicInfo`/`ServerVersionDetail`/`NacosGrpcAiService`/`NacosClientOptions` (mirror the usings of `tests/RedNb.Nacos.Grpc.Tests/Config/NacosGrpcConfigServiceTests.cs` and the integration fixture).

- [ ] **Step 5: Run unit tests**

Run: `dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net8.0`
Expected: PASS including the 3 new facts (fakes never hit the network).

- [ ] **Step 6: Regression — AI HTTP integration suite still green**

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 --filter "FullyQualifiedName~AiServiceIntegrationTests"`
Expected: PASS (HTTP channel untouched; gRPC AI tests remain skipped until Task 5).

- [ ] **Step 7: Commit**

```bash
git add src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs tests/RedNb.Nacos.Grpc.Tests/Ai/NacosGrpcAiServiceTests.cs
git commit -m "fix(ai): bind mcpId and send namespaceId on the gRPC AI wire

- McpServerReleaseResponse.McpServerId -> McpId (server sends mcpId)
- request DTOs send namespaceId instead of the silently-ignored namespace
- add NacosGrpcAiService test ctor + wire-shape unit tests"
```

---

### Task 4: Fail loudly — ServerNotImplemented(501) for the 12 gRPC ops with no server handler

**Files:**
- Modify: `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs` (12 methods)
- Create: `tests/RedNb.Nacos.Grpc.Tests/Ai/NacosGrpcAiServiceUnsupportedTests.cs`

**Interfaces:**
- Consumes: test ctor from Task 3; `FakeNacosGrpcClient`; `NacosException.ServerNotImplemented = 501` (`src/RedNb.Nacos/Exceptions/NacosException.cs:1403` — currently unused in the AI gRPC path). Task 1 already flipped `ListAgentVersionsAsync` (not repeated here).
- Produces: every unsupported gRPC AI op throws `NacosException` with `ErrorCode == 501` **before** dispatching, and the dead private request DTOs are removed.
- Ruling (baked in): server registers only 8 AI gRPC handlers (QueryMcpServer, ReleaseMcpServer, McpServerEndpoint, QueryAgentCard, ReleaseAgentCard, AgentEndpoint, BatchAgentEndpoint, QueryPrompt). Anything else must fail loudly — today it either silently swallows the server's "unknown type" response or misreports `ServerError`(500). MCP **tool** ops have no HTTP console endpoint either, so their message says "neither channel"; the rest point to the HTTP console channel.

- [ ] **Step 1: Replace the 12 method bodies**

In `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs`, keep each method's existing argument validation, then replace everything from the request construction through the return with a 501 throw.

**Group A — ops that exist on the HTTP console channel** (`SubscribeMcpServerAsync` both overloads :185/:191, `DeleteMcpServerAsync` :254, `ListMcpServersAsync` :281, `ValidateImportAsync` :321, `ImportMcpServersAsync` :356, `SubscribeAgentCardAsync` both overloads :673/:679, `DeleteAgentAsync` :742, `ListAgentCardsAsync` :769):

```csharp
        throw new NacosException(NacosException.ServerNotImplemented,
            "<TypeString> has no Nacos 3.2.4 gRPC handler; use the HTTP console channel (IAiService via NacosFactory with ConsoleAddresses) instead");
```

with `<TypeString>` the literal the old body dispatched (e.g. `McpServerDeleteRequest`, `McpServerListRequest`, `McpServerSubscribeRequest`, `McpServerValidateImportRequest`, `McpServerImportRequest`, `AgentCardSubscribeRequest`, `AgentDeleteRequest`, `AgentListRequest`).

**Group B — MCP tool ops with no channel at all** (`RefreshMcpToolAsync` :398, `GetMcpToolAsync` :422, `DeleteMcpToolAsync` :446, `UpdateMcpToolAsync` :475):

```csharp
        throw new NacosException(NacosException.ServerNotImplemented,
            "MCP tool management has no Nacos 3.2.4 gRPC handler and no HTTP console endpoint — operation unavailable on this server");
```

Notes:
- `ValidateImportAsync`/`ImportMcpServersAsync`: keep the existing `if (request == null) throw new NacosException(InvalidParam, "request is required");` guard, then throw 501.
- `SubscribeMcpServerAsync`/`SubscribeAgentCardAsync`: the overloads that delegate to the fuller overloads stay untouched; apply the throw only in the fullest overload of each pair (the delegating overload propagates it). Also keep the existing `listener == null`-style guards if present, before the throw.
- `UnsubscribeMcpServerAsync`/`UnsubscribeAgentCardAsync` are NOT in scope — they remove local listeners without dispatching.

- [ ] **Step 2: Delete the dead private DTOs**

For each type string eliminated in Step 1, remove the now-unreferenced private DTO classes (and sibling response DTOs, **except** any type still named in a method signature — e.g. `McpServerImportResponse` must stay because `ImportMcpServersAsync` returns it). Verify before deleting:

Run: `git grep -n "McpServerSubscribeRequest\|McpServerDeleteRequest\|McpServerListRequest\|McpServerValidateImportRequest\|McpServerImportGrpcRequest\|McpToolRefreshRequest\|McpToolQueryRequest\|McpToolDeleteRequest\|McpToolUpdateRequest\|AgentCardSubscribeRequest\|AgentDeleteRequest\|AgentListRequest" src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs`
Expected: only the class declarations remain. Delete each declaration whose name has zero other hits (keep any response type referenced by a remaining method signature).

- [ ] **Step 3: Build**

Run: `dotnet build src/RedNb.Nacos.Grpc/RedNb.Nacos.Grpc.csproj -f net8.0`
Expected: PASS.

- [ ] **Step 4: Add fail-loud unit tests**

Create `tests/RedNb.Nacos.Grpc.Tests/Ai/NacosGrpcAiServiceUnsupportedTests.cs`:

```csharp
using FluentAssertions;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai.Model;
using RedNb.Nacos.Core.Ai.Listener;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Ai;

/// <summary>
/// The Nacos 3.2.4 server registers only 8 AI gRPC handlers. Every other op
/// must fail loudly with ServerNotImplemented (501) BEFORE dispatching —
/// never silently swallow the server's unknown-type response.
/// </summary>
public class NacosGrpcAiServiceUnsupportedTests
{
    private static NacosClientOptions Options => new() { ServerAddresses = "localhost:8848" };

    private static McpServerBasicInfo ValidMcpServer(string name) => new()
    {
        Name = name,
        VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
        Protocol = "stdio"
    };

    [Fact]
    public async Task UnsupportedOps_Throw501_BeforeDispatching()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var operations = new (string Name, Func<Task> Invoke)[]
        {
            ("SubscribeMcpServer", () => service.SubscribeMcpServerAsync("m1", new TestMcpListener()).AsTask()),
            ("DeleteMcpServer", () => service.DeleteMcpServerAsync("m1")),
            ("ListMcpServers", () => service.ListMcpServersAsync().AsTask()),
            ("RefreshMcpTool", () => service.RefreshMcpToolAsync("m1", "t1").AsTask()),
            ("GetMcpTool", () => service.GetMcpToolAsync("m1", "t1").AsTask()),
            ("DeleteMcpTool", () => service.DeleteMcpToolAsync("m1", "t1")),
            ("UpdateMcpTool", () => service.UpdateMcpToolAsync("m1", new McpToolSpec { Name = "t1" })),
            ("SubscribeAgentCard", () => service.SubscribeAgentCardAsync("a1", new TestAgentCardListener()).AsTask()),
            ("DeleteAgent", () => service.DeleteAgentAsync("a1")),
            ("ListAgentCards", () => service.ListAgentCardsAsync().AsTask()),
        };

        foreach (var op in operations)
        {
            var ex = await Assert.ThrowsAsync<NacosException>(op.Invoke);
            Assert.Equal(NacosException.ServerNotImplemented, ex.ErrorCode);
            fake.Captured.Should().BeEmpty($"{op.Name} must throw before dispatching");
        }
    }

    [Fact]
    public async Task ImportOps_WithNullRequest_KeepInvalidParam()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var ex1 = await Assert.ThrowsAsync<NacosException>(() => service.ValidateImportAsync(null!, CancellationToken.None));
        Assert.Equal(NacosException.InvalidParam, ex1.ErrorCode);

        var ex2 = await Assert.ThrowsAsync<NacosException>(() => service.ImportMcpServersAsync(null!, CancellationToken.None));
        Assert.Equal(NacosException.InvalidParam, ex2.ErrorCode);

        fake.Captured.Should().BeEmpty();
    }

    private sealed class TestMcpListener : AbstractNacosMcpServerListener
    {
        public override void OnEvent(NacosMcpServerEvent evt) { }
    }

    private sealed class TestAgentCardListener : AbstractNacosAgentCardListener
    {
        public override void OnEvent(NacosAgentCardEvent evt) { }
    }
}
```

Adjust usings to compile (mirror `tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs` for the listener types and `NacosGrpcConfigServiceTests.cs` for the rest). If `McpToolSpec` requires properties beyond `Name`, satisfy the minimal valid state the old `ValidateToolName` path accepted. If `Assert.ThrowsAsync` needs `using Xunit;` it is included above.

- [ ] **Step 5: Run unit tests**

Run: `dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net8.0`
Expected: PASS including both new facts.

- [ ] **Step 6: Integration regression — HTTP AI suite + push suite**

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 --filter "FullyQualifiedName~AiServiceIntegrationTests|FullyQualifiedName~ConfigListenerPushTests|FullyQualifiedName~NamingSubscribePushTests"`
Expected: PASS (HTTP channel untouched; push tests keep Task 2's green state; the 4 gRPC AI tests are still skipped — Task 5).

- [ ] **Step 7: Commit**

```bash
git add src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs tests/RedNb.Nacos.Grpc.Tests/Ai/NacosGrpcAiServiceUnsupportedTests.cs
git commit -m "refactor(ai): fail loudly with ServerNotImplemented on unsupported gRPC ops

The 3.2.4 server registers only 8 AI gRPC handlers; the 12 other ops now
throw 501 before dispatching instead of silently swallowing, and the dead
private request DTOs are removed."
```

---

### Task 5: Re-enable the 4 gRPC AI integration tests with settle-aware teardown

**Files:**
- Create: `tests/RedNb.Nacos.IntegrationTests/TestRetryHelpers.cs`
- Modify: `tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs` (rewire helpers to the shared class)
- Modify: `tests/RedNb.Nacos.IntegrationTests/Ai/GrpcAiServiceIntegrationTests.cs` (remove 4 Skip attributes, settle-aware bodies, new mcpId assertion)

**Interfaces:**
- Consumes: Tasks 2+3 (auth on the wire; mcpId binding); the existing private helpers `WaitForAsync<T>` (:356-394) / `DeleteWithRetryAsync` (:401-416) in `AiServiceIntegrationTests`; fixture wiring in `GrpcAiServiceIntegrationTests.InitializeAsync` (:14-50, unchanged — cleanup stays on the HTTP console channel because the server has no delete handlers).
- Produces: `internal static class TestRetryHelpers` in `tests/RedNb.Nacos.IntegrationTests/` with `WaitForAsync<T>(ITestOutputHelper, Func<Task<T>>, Func<T,bool>, int, int)` and `DeleteWithRetryAsync(ITestOutputHelper, Func<Task>, int)` — consumed by both AI integration test classes.

- [ ] **Step 1: Extract shared retry helpers**

Create `tests/RedNb.Nacos.IntegrationTests/TestRetryHelpers.cs` — move the two helper bodies verbatim from `AiServiceIntegrationTests.cs:356-416`, taking `ITestOutputHelper` as the first parameter (replace `_output.WriteLine` with `output.WriteLine`):

```csharp
using RedNb.Nacos.Core;
using Xunit.Abstractions;

namespace RedNb.Nacos.IntegrationTests;

/// <summary>
/// Shared settle-aware retry helpers for integration tests. The console API
/// can lag a released write by up to ~1s; polls tolerate that lag while a
/// plain Task.Delay either flakes or wastes time.
/// </summary>
internal static class TestRetryHelpers
{
    internal static async Task<T> WaitForAsync<T>(
        ITestOutputHelper output,
        Func<Task<T>> read,
        Func<T, bool> isSettled,
        int timeoutMs = 2000,
        int pollMs = 200)
        where T : class?
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        NacosException? lastServerError = null;

        while (true)
        {
            try
            {
                var value = await read();
                if (value is not null && isSettled(value))
                {
                    return value;
                }
            }
            catch (NacosException ex)
            {
                lastServerError = ex; // transient while the release settles
            }

            if (DateTime.UtcNow >= deadline)
            {
                if (lastServerError is not null)
                {
                    throw lastServerError;
                }

                throw new TimeoutException($"The console read did not settle within {timeoutMs} ms.");
            }

            output.WriteLine($"WaitForAsync: read not settled yet; retrying in {pollMs} ms.");
            await Task.Delay(pollMs);
        }
    }

    internal static async Task DeleteWithRetryAsync(ITestOutputHelper output, Func<Task> delete, int attempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await delete();
                return;
            }
            catch (NacosException ex) when (attempt < attempts)
            {
                output.WriteLine($"DeleteWithRetryAsync: {ex.Message}; retrying.");
                await Task.Delay(200);
            }
        }
    }
}
```

- [ ] **Step 2: Rewire AiServiceIntegrationTests**

In `tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs`: delete the two private helpers (:356-416), add `using static RedNb.Nacos.IntegrationTests.TestRetryHelpers;`, and at every existing call site insert the `_output` argument (`WaitForAsync(_output, ...)`, `DeleteWithRetryAsync(_output, ...)`). The test class already stores `_output` (ITestOutputHelper).

- [ ] **Step 3: Rewrite the 4 gRPC tests**

In `tests/RedNb.Nacos.IntegrationTests/Ai/GrpcAiServiceIntegrationTests.cs`, add `using static RedNb.Nacos.IntegrationTests.TestRetryHelpers;` and replace the 4 skipped tests with (keep the class fixture :14-56 unchanged):

```csharp
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseAndGetMcpServer_ViaGrpc()
    {
        var mcpName = $"grpc-mcp-{Guid.NewGuid():N}";
        var spec = new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        };

        var released = await _grpcAi!.ReleaseMcpServerAsync(spec, toolSpecification: null);
        released.Should().NotBeNullOrEmpty();

        var got = await WaitForAsync(_output, () => _grpcAi.GetMcpServerAsync(mcpName), s => s is not null);
        got!.Name.Should().Be(mcpName);

        await DeleteWithRetryAsync(_output, () => _httpAi!.DeleteMcpServerAsync(mcpName));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task RegisterAndDeregisterMcpEndpoint_ViaGrpc()
    {
        var mcpName = $"grpc-mcp-ep-{Guid.NewGuid():N}";
        var released = await _grpcAi!.ReleaseMcpServerAsync(new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        }, toolSpecification: null);
        released.Should().NotBeNullOrEmpty();

        await WaitForAsync(_output, () => _grpcAi.GetMcpServerAsync(mcpName), s => s is not null);

        await _grpcAi.RegisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100, "1.0.0");
        var got = await WaitForAsync(_output, () => _grpcAi.GetMcpServerAsync(mcpName), s => s is not null);
        got.Should().NotBeNull();

        await _grpcAi.DeregisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100);
        await DeleteWithRetryAsync(_output, () => _httpAi!.DeleteMcpServerAsync(mcpName));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseAndGetAgentCard_ViaGrpc()
    {
        var agentName = $"grpc-agent-{Guid.NewGuid():N}";
        var card = new AgentCard
        {
            Name = agentName,
            Version = "1.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        };

        await _grpcAi!.ReleaseAgentCardAsync(card);

        var got = await WaitForAsync(_output, () => _grpcAi.GetAgentCardAsync(agentName), s => s is not null);
        got!.Name.Should().Be(agentName);

        await DeleteWithRetryAsync(_output, () => _httpAi!.DeleteAgentAsync(agentName));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task CrossChannel_ReleaseViaGrpc_VisibleViaHttpList()
    {
        var mcpName = $"cross-mcp-{Guid.NewGuid():N}";
        var released = await _grpcAi!.ReleaseMcpServerAsync(new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        }, toolSpecification: null);
        released.Should().NotBeNullOrEmpty();

        try
        {
            var page = await WaitForAsync(
                _output,
                () => _httpAi!.ListMcpServersAsync(pageSize: 100),
                p => p is not null && p.PageItems.Any(i => i.Name == mcpName));
            page.PageItems.Should().Contain(i => i.Name == mcpName);
        }
        finally
        {
            await DeleteWithRetryAsync(_output, () => _httpAi!.DeleteMcpServerAsync(mcpName));
        }
    }
```

(The skip-attribute texts about auth/mcpId/OperationResponse were all proven wrong by Tasks 2/3 — delete the attributes outright. If `GetMcpServerAsync`'s returned `McpServerDetailInfo` maps the server's response differently than expected, the test failure message will say so; fix the binding, don't re-skip.)

- [ ] **Step 4: Run the 4 tests live ×3**

Run (3 times to prove stability):
`dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 --filter "FullyQualifiedName~GrpcAiServiceIntegrationTests"`
Expected: 4/4 PASS on every run, no flakes (settle polls absorb the console's ~1s write lag).

- [ ] **Step 5: Full suite**

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0`
Expected: 0 FAIL, 2 SKIP (the two HTTP subscription tests), everything else PASS — record the exact counts for Task 8.

- [ ] **Step 6: Commit**

```bash
git add tests/RedNb.Nacos.IntegrationTests/TestRetryHelpers.cs tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs tests/RedNb.Nacos.IntegrationTests/Ai/GrpcAiServiceIntegrationTests.cs
git commit -m "test(grpc-ai): re-enable 4 gRPC AI integration tests with settle-aware teardown

Auth (accessToken in payload headers) and the mcpId binding land first; the
tests now assert a non-empty released mcpId and poll for console settle
instead of sleeping fixed delays. Shared retry helpers extracted."
```

---

### Task 6: Dedup NacosHttpClient/NacosConsoleHttpClient via NacosHttpClientBase

**Files:**
- Create: `src/RedNb.Nacos.Http/Http/NacosHttpClientBase.cs`
- Modify: `src/RedNb.Nacos.Http/Http/NacosHttpClient.cs` (418 lines → thin subclass)
- Modify: `src/RedNb.Nacos.Http/Http/NacosConsoleHttpClient.cs` (460 lines → thin subclass keeping `ConsoleBaseUrls` + fail-fast)

**Interfaces:**
- Consumes: research §4.1-4.3 — the two clients are byte-identical apart from 5 diffs (class decl + console XML doc; `ConsoleBaseUrls` test hook; ctor server-list resolver + fail-fast; `GetConsoleBaseUrl` vs `GetBaseUrl` in the two private request methods). `ServerListManager` has both `(NacosClientOptions)` (:14-19) and `(IList<string>)` (:21-31) ctors.
- Produces: `public abstract class NacosHttpClientBase : IDisposable` in `src/RedNb.Nacos.Http/Http/` with the full shared public surface (`GetAsync`, `GetWithHeadersAsync`, `PostAsync`, `PostWithHeadersAsync`, `PutAsync`, `PutWithHeadersAsync`, `DeleteAsync`, `DeleteWithHeadersAsync`, `GetRawAsync`, `PostMultipartAsync`, `PostMultipartWithHeadersAsync`, `Dispose`) and `protected virtual string BuildBaseUrl(string server)`. Public surface of both concrete classes is preserved exactly (methods become inherited).
- Ruling (baked in): base ctor takes the `ServerListManager` itself (not an address list) so `NacosHttpClient` keeps its options-based manager construction 100% unchanged (`serverListManager ?? new ServerListManager(options)`); only the console client passes a pre-built one.

- [ ] **Step 1: Create the base class**

Create `src/RedNb.Nacos.Http/Http/NacosHttpClientBase.cs` by copying the **entire current content of `NacosHttpClient.cs`**, then:
1. Rename `public class NacosHttpClient` → `public abstract class NacosHttpClientBase`.
2. Add the XML summary:

```csharp
/// <summary>
/// Shared implementation of the Nacos server HTTP transport. The concrete
/// clients differ only in how a base URL is derived from a server address
/// (<see cref="BuildBaseUrl"/> — the console listener has no /nacos context
/// path), in construction-time server resolution, and in the console client's
/// fail-fast check.
/// </summary>
```

3. Change the ctor to:

```csharp
    protected NacosHttpClientBase(NacosClientOptions options, ILogger? logger = null, ServerListManager? serverListManager = null)
    {
        _options = options;
        _logger = logger;
        _serverListManager = serverListManager ?? new ServerListManager(options);
        _securityProxy = new SecurityProxy(options, logger);
        // ... remainder of the original ctor body unchanged ...
    }
```

4. Add the seam right after the ctor:

```csharp
    /// <summary>
    /// Derives the base URL for a server address. <see cref="NacosConsoleHttpClient"/>
    /// overrides this to drop the /nacos context path.
    /// </summary>
    protected virtual string BuildBaseUrl(string server) => _options.GetBaseUrl(server);
```

5. In the two private request methods, replace the base-URL lines (`var baseUrl = _options.GetBaseUrl(server);` at current :176-178 and :292-293) with `var baseUrl = BuildBaseUrl(server);`.
6. Make the shared private fields `protected` (`_options`, `_logger`, `_serverListManager`, `_securityProxy`, `_httpClient`, `_disposed`) so subclasses can read them.
7. Keep every public method, `AddAuthHeadersAsync`, `BuildUrl`, retry loop and `Dispose` exactly as copied.

- [ ] **Step 2: Shrink NacosHttpClient**

Replace the entire `src/RedNb.Nacos.Http/Http/NacosHttpClient.cs` content with:

```csharp
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core;

namespace RedNb.Nacos.Client.Http;

/// <summary>
/// HTTP transport for the core Nacos server API (client/admin port 8848,
/// /nacos context path). Shares its full implementation with
/// <see cref="NacosConsoleHttpClient"/> via <see cref="NacosHttpClientBase"/>.
/// </summary>
public class NacosHttpClient : NacosHttpClientBase
{
    public NacosHttpClient(NacosClientOptions options, ILogger? logger = null)
        : base(options, logger)
    {
    }
}
```

(Confirm the namespace + using names match what the file currently declares — copy them from the old file header; the file keeps the `using` lines that the now-moved body needed only if still used, otherwise drop them and let the compiler complain.)

- [ ] **Step 3: Shrink NacosConsoleHttpClient**

Replace the entire `src/RedNb.Nacos.Http/Http/NacosConsoleHttpClient.cs` content with (preserving the exact fail-fast message and the `ConsoleBaseUrls` test hook semantics):

```csharp
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core;

namespace RedNb.Nacos.Client.Http;

/// <summary>
/// HTTP transport for the Nacos console listener (port 8080): no /nacos
/// context path, derived console addresses, fail-fast when no console address
/// resolves. Shares its full implementation with <see cref="NacosHttpClient"/>
/// via <see cref="NacosHttpClientBase"/>.
/// </summary>
public class NacosConsoleHttpClient : NacosHttpClientBase
{
    /// <summary>
    /// Resolved console base URLs (one per console address, scheme honors
    /// <see cref="NacosClientOptions.EnableTls"/>). Exposed for testability —
    /// callers and tests can assert on the exact set of console endpoints the
    /// client will route to without spinning up a server.
    /// </summary>
    public IReadOnlyList<string> ConsoleBaseUrls { get; }

    public NacosConsoleHttpClient(NacosClientOptions options, ILogger? logger = null)
        : base(options, logger, CreateServerListManager(options))
    {
        ConsoleBaseUrls = options.GetConsoleAddressList()
            .Select(a => options.GetConsoleBaseUrl(a))
            .ToList();
    }

    /// <inheritdoc />
    protected override string BuildBaseUrl(string server) => _options.GetConsoleBaseUrl(server);

    private static ServerListManager CreateServerListManager(NacosClientOptions options)
    {
        var addresses = options.GetConsoleAddressList();
        if (addresses.Count == 0)
        {
            throw new NacosException(
                NacosException.InvalidParam,
                "ConsoleAddresses (or derivable ServerAddresses) is required for NacosConsoleHttpClient");
        }

        return new ServerListManager(addresses);
    }
}
```

(Keep the 11-line console-specific `<remarks>` XML doc from the old file if it documents behavior beyond what the summary above says — merge it into the new doc comment rather than losing it.)

- [ ] **Step 4: Build + full unit suite**

Run: `dotnet build RedNb.Nacos.sln -f net8.0` (or the repo's normal build entry), then `dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net8.0 && dotnet test tests/RedNb.Nacos.Http.Tests/RedNb.Nacos.Http.Tests.csproj -f net8.0 && dotnet test tests/RedNb.Nacos.Grpc.Tests/RedNb.Nacos.Grpc.Tests.csproj -f net8.0`
Expected: all PASS, including `NacosConsoleHttpClientTests` (3 facts asserting `ConsoleBaseUrls`) and `NacosClientOptionsTests`.

- [ ] **Step 5: Integration regression — both HTTP channels**

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0`
Expected: same matrix as end of Task 5 (0 FAIL; HTTP AI suite exercises the console client, config/naming/lock/maintainer exercise the core client).

- [ ] **Step 6: Commit**

```bash
git add src/RedNb.Nacos.Http/Http/NacosHttpClientBase.cs src/RedNb.Nacos.Http/Http/NacosHttpClient.cs src/RedNb.Nacos.Http/Http/NacosConsoleHttpClient.cs
git commit -m "refactor(http): extract NacosHttpClientBase shared transport

NacosHttpClient and NacosConsoleHttpClient were byte-identical apart from
base-URL derivation, server-list resolution, and the console fail-fast. The
base class hosts the shared implementation; the only seam is the virtual
BuildBaseUrl. Public surfaces are preserved."
```

---

### Task 7: Validate() — credentials require ServerAddresses

**Files:**
- Modify: `src/RedNb.Nacos/NacosClientOptions.cs` (`Validate()` :219-233)
- Modify: `tests/RedNb.Nacos.Tests/Config/NacosClientOptionsTests.cs`

**Interfaces:**
- Consumes: `SecurityProxy.LoginAsync` iterates ONLY `GetServerAddressList()` (research §5.3) — with `ServerAddresses=""` + credentials it throws `NacosException(NoRight, "Failed to login to Nacos server: ", null)` even when the console URL is reachable. Blank credentials short-circuit to `null` token (no login), so console-only configurations without credentials remain valid.
- Produces: `Validate()` throws `InvalidParam` when `Username` or `Password` is non-blank and `ServerAddresses` is blank; console-only + no-credentials still validates (existing test `Validate_AllowsConsoleAddressesAlone` must keep passing).
- Ruling (baked in): this is a config-time guard for the runtime hole above, NOT a new login path for the console port.

- [ ] **Step 1: Extend Validate()**

In `src/RedNb.Nacos/NacosClientOptions.cs`, after the existing three-way check in `Validate()` (:219-233), add:

```csharp
        var hasCredentials = !string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password);
        if (hasCredentials && !hasCore)
        {
            throw new NacosException(
                NacosException.InvalidParam,
                "Username/Password require ServerAddresses: login is performed against the server API port (8848), so a console-only configuration cannot authenticate");
        }
```

(`Username`/`Password` are `string?` at :32/:37; `hasCore` is already computed above.)

- [ ] **Step 2: Add unit tests**

In `tests/RedNb.Nacos.Tests/Config/NacosClientOptionsTests.cs`, add after `Validate_AllowsConsoleAddressesAlone`:

```csharp
    [Fact]
    public void Validate_Throws_WhenCredentialsSetWithoutServerAddresses()
    {
        var opts = new NacosClientOptions
        {
            ServerAddresses = "",
            ConsoleAddresses = "localhost:8080",
            Username = "nacos",
            Password = "nacos"
        };
        Action act = () => opts.Validate();
        act.Should().Throw<NacosException>()
            .WithMessage("*Username/Password require ServerAddresses*");
    }

    [Fact]
    public void Validate_AllowsServerAddressesWithCredentials()
    {
        var opts = new NacosClientOptions
        {
            ServerAddresses = "localhost:8848",
            Username = "nacos",
            Password = "nacos"
        };
        Action act = () => opts.Validate();
        act.Should().NotThrow();
    }
```

- [ ] **Step 3: Run unit tests**

Run: `dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net8.0`
Expected: PASS — both new facts, and `Validate_AllowsConsoleAddressesAlone` (console-only, no credentials) still passes.

- [ ] **Step 4: Commit**

```bash
git add src/RedNb.Nacos/NacosClientOptions.cs tests/RedNb.Nacos.Tests/Config/NacosClientOptionsTests.cs
git commit -m "feat(core): Validate() rejects credentials without ServerAddresses

SecurityProxy only logs in against the server API port, so a console-only
configuration with credentials was guaranteed to fail at runtime; make it a
config-time InvalidParam instead. Console-only without credentials stays valid."
```

---

### Task 8: Docs closure — honest matrix, fixed-gap notes, backlog strike-through

**Files:**
- Modify: `docs/SDK_COMPLETENESS_REPORT.md`
- Modify: `docs/superpowers/plans/2026-09-11-ai-live-validation.md` (Follow-up backlog section only)
- Modify: any `docs/*.md` migration note that lists AI gaps (locate via `git grep -li "migration" -- docs/` and `git grep -l "AgentVersion\|gRPC.*auth\|待完善" -- docs/`)

**Interfaces:**
- Consumes: final verified test matrix from this plan's gates.
- Produces: docs that match the branch's actual state. No code.

- [ ] **Step 1: Run the full suites and record honest counts**

Run:
`dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net8.0` and
`dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0`

Expected (projection): unit 382 + new facts all green; integration 41 = **39 pass · 2 skip · 0 fail** (the 6 push tests fixed by Task 2, `ListAgentVersions_ReturnsVersions` un-skipped by Task 1, 1 new rich-metadata test, 4 gRPC AI tests re-enabled by Task 5, the 2 HTTP subscription tests remain skipped). If reality differs, record reality — never invent numbers.

- [ ] **Step 2: Update the completeness report**

In `docs/SDK_COMPLETENESS_REPORT.md`:
1. **Test matrix (§零)**: update the integration counts to the verified numbers from Step 1; flip the 6 push tests from "environment-blocked/known-failing" to passing.
2. **§五 (待完善)**: mark fixed and describe the fix for: gRPC auth path (accessToken in payload headers via SecurityProxy), `mcpId` binding, `namespaceId` payload key, `OperationResponse` (add the note that the earlier shape-mismatch diagnosis was wrong — success is on the wire via Jackson `isSuccess()`, resultCode 200), the 501 fail-loud list for no-handler ops, console-client dedup, and the Validate() credential rule.
3. **Feature surface**: add `ListAgentVersionInfosAsync` → `List<AgentVersionInfo>` and note `ListAgentVersionsAsync` now projects from it.

- [ ] **Step 3: Strike through the backlog**

In `docs/superpowers/plans/2026-09-11-ai-live-validation.md` §"Follow-up backlog": for each of Tasks 10/11/12, mark the header with `✅ 已完成（2026-09-11，实施计划 docs/superpowers/plans/2026-09-11-ai-followups.md）` and strike through (`~~...~~`) the now-resolved item bodies. Leave the section in place as the record of what was found.

- [ ] **Step 4: Migration notes**

Locate migration docs: `git grep -li "migration" -- docs/` plus `git grep -l "待完善\|gRPC.*(401\|AgentVersion" -- docs/`. For each hit that mentions AI gRPC gaps or agent-version shape, update it to the fixed state (auth works with credentials; version list returns rich objects projected to strings). If a doc mentions nothing stale, leave it untouched.

- [ ] **Step 5: Commit**

```bash
git add docs/SDK_COMPLETENESS_REPORT.md docs/superpowers/plans/2026-09-11-ai-live-validation.md
git add docs/   # only if Step 4 found migration docs to update — otherwise add them explicitly
git commit -m "docs(ai): reflect fixed gRPC gaps, honest test matrix, strike through backlog"
```

---

## Execution notes

- SDD workspace: `.superpowers/sdd/2026-09-11-ai-followups/` (ledger `progress.md`).
- Model selection: Tasks 1/3/4/7/8 are transcription-level (haiku); Tasks 2/5/6 need integration judgment (sonnet). Task reviewers: sonnet floor. Final whole-branch review: most capable model.
- The `.tmp/ai-followups-research.md` dossier stays untracked scratch; the plan file itself is untracked (do not commit it unless the user asks).
- At the end, run superpowers:finishing-a-development-branch with the user's standing decision already made: **Option 3, keep AIRegistry as-is** (no merge, no push).
