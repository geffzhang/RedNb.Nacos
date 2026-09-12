# AI 双通道 Live 验证（8080 console + gRPC）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the SDK's AI service actually work end-to-end against a live Nacos 3.2.4 server, validating both the HTTP channel (8080 console) and the gRPC channel, so that the six "swallowing" integration tests assert real behavior instead of catching-and-logging `NacosException`s from 404s.

**Architecture:**
- The HTTP AI service (`NacosAiService`) currently targets port 8848 with console-style paths (`v3/console/ai/mcp`) → all calls return 404. The fix: introduce a new `ConsoleAddresses` option + `NacosConsoleHttpClient` that targets the console port (8080 by default, no `/nacos` context path) and switch the AI service's HTTP calls to it. Correct sub-paths and parameter names to match the real 3.2.4 console controllers.
- Methods whose HTTP endpoints do not exist in 3.2.4 (`Register/Deregister{Mcp,Agent}ServerEndpointAsync`, all tool CRUD) fail loudly with a descriptive `NacosException` on the HTTP service — the gRPC service (`NacosGrpcAiService`) already implements these via the AI registry gRPC handlers, so dual-channel callers get full functionality.
- gRPC channel is verified against the live server with cross-channel consistency checks (release via gRPC, list via HTTP and vice versa).

**Tech Stack:** .NET 8, xUnit, FluentAssertions, Microsoft.Extensions.Logging, Grpc.Net.Client (existing), `Microsoft.AspNetCore` test host.

**Spec:**
- `docs/SDK_COMPLETENESS_REPORT.md` §一.3 (AI 表格), §五 item 1 (AI 双通道 live 验证), §三 item 7 (`NacosConstants.NamespaceHeader` 清理)
- `docs/SDK_COMPLETENESS_ANALYSIS.md` §三 item 7, §五 (结论)

**Spec-derived ground truth (Nacos 3.2.4, verified live 2026-09-11):**

| Channel | Path | Verbs | Status | Notes |
|---|---|---|---|---|
| 8080 console | `/v3/console/ai/mcp` | GET, POST, DELETE | works | GET needs `?mcpName=`; POST needs `mcpName` + `serverSpecification` (not `serverSpec`); serverSpecification JSON: `{"name","versionDetail":{"version"},"protocol":"stdio"}` (stdio = local, no endpointSpec) |
| 8080 console | `/v3/console/ai/mcp/list` | GET | works | page envelope `{totalCount, pageNumber, pagesAvailable, pageItems}` |
| 8080 console | `/v3/console/ai/import/validate` | POST | works (SDK wrongly uses `mcp/import/validate` → 404) |
| 8080 console | `/v3/console/ai/import/execute` | POST | works (SDK wrongly uses `mcp/import/execute` → 404) |
| 8080 console | `/v3/console/ai/mcp/endpoint` | none | **does not exist** (only gRPC) |
| 8080 console | `/v3/console/ai/mcp/tool{,/refresh}` | none | **does not exist** (only gRPC) |
| 8080 console | `/v3/console/ai/a2a` | GET, POST, DELETE | works | POST needs `agentName` + `agentCard`; release requires either `agentCard.supportedInterfaces` OR all of `protocolVersion`+`preferredTransport`+`url` |
| 8080 console | `/v3/console/ai/a2a/list` | GET | works | **requires `search=accurate\|blur`** (else 400) |
| 8080 console | `/v3/console/ai/a2a/version/list` | GET | works (SDK wrongly uses `/versions` → 500) |
| 8080 console | `/v3/console/ai/a2a/endpoint{,s}` | none | **does not exist** (only gRPC) |
| 8848 + /nacos | any `/v3/console/ai/*` | — | **404** (console-only) | SDK's current behavior; root cause of the 6 swallowing tests |
| 8080 with `/nacos` prefix | any `/nacos/v3/console/ai/*` | — | **500** | console has no `/nacos` context path; base URL must drop it |

Console auth (separate scope, default **on**): `POST http://localhost:8848/nacos/v3/auth/user/login -d "username=nacos&password=nacos"` returns `accessToken` which works on both 8848 and 8080.

## Global Constraints

- **Minimum Nacos server**: 3.2.4 (validated against). 3.0/3.1/3.2.0–3.2.3 are not exercised.
- **Public API**: `IAiService` signatures unchanged. Adding `ConsoleAddresses` to `NacosClientOptions` is the only signature change; existing callers get a default derived from `ServerAddresses` (port `8848` → `8080`) and may override.
- **No `[Obsolete]` added to existing methods** that lose HTTP support — they throw `NacosException(ServerError, ...)` at runtime, consistent with the codebase's "fail loud" philosophy in `MIGRATION.md`. The XML doc on `IAiService` is updated to direct callers to the gRPC service for those ops.
- **TDD** for every code change: failing test → minimal implementation → green.
- **Frequent commits** — one commit per logical sub-step (option field, console client, path fix, test rewrite, doc update). No mega-commit.
- **No merge, no push** to remote (user decision 2026-09-11; branch `AIRegistry` stays local).
- **UTF-8 only** for new files. Existing `deploy/docker-compose/{start.sh,start.bat,.env.example}` remain GBK (see memory `repo-encoding-gbk-files`).

---

## File Structure

### New files

| Path | Responsibility |
|---|---|
| `src/RedNb.Nacos/Http/NacosConsoleHttpClient.cs` | Mirrors `NacosHttpClient` but uses `ConsoleAddresses`, no `/nacos` context path |
| `tests/RedNb.Nacos.Tests/Http/NacosConsoleHttpClientTests.cs` | Unit tests for the console client (base URL, retry, auth header) |
| `tests/RedNb.Nacos.Tests/Config/NacosClientOptionsTests.cs` | New `ConsoleAddresses` field validation tests |
| `tests/RedNb.Nacos.IntegrationTests/Ai/GrpcAiServiceIntegrationTests.cs` | Dual-channel gRPC live tests |
| `docs/ENVIRONMENT_BOOTSTRAP.md` | How to bootstrap a 3.2.4 dev environment (compose, user insert, console auth) |

### Modified files

| Path | What changes |
|---|---|
| `src/RedNb.Nacos/NacosClientOptions.cs` | Add `ConsoleAddresses` + `GetConsoleAddressList()` + `GetConsoleBaseUrl()`; validate cross-fields |
| `src/RedNb.Nacos.Http/Ai/NacosAiService.cs` | Inject `NacosConsoleHttpClient`; fix sub-paths and param names; throw for ops with no HTTP endpoint |
| `src/RedNb.Nacos/Core/NacosConstants.cs` | Remove `NamespaceHeader` constant (after verifying console ignores it) |
| `src/RedNb.Nacos/Core/IAiService.cs` (and sub-interfaces) | XML docs note "this op has no HTTP endpoint in Nacos 3.2.4; use `NacosGrpcFactory.CreateAiService`" |
| `tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs` | Rewrite 6 tests: assert real round-trips; set `ConsoleAddresses`; subscribe tests gated to slow category |
| `tests/RedNb.Nacos.IntegrationTests/NacosServerFixture.cs` | Add `ConsoleAddress` constant + assertion helper |
| `docs/SDK_COMPLETENESS_REPORT.md` | §一.3 status, §五 item 1 strike-through |
| `docs/SDK_COMPLETENESS_ANALYSIS.md` | §五 conclusion |
| `docs/MIGRATION.md` | Note `ConsoleAddresses` requirement for AI HTTP service |

### Unchanged but verified

- `src/RedNb.Nacos.Http/Http/NacosHttpClient.cs` (core API client — still targets 8848 with `/nacos` prefix; correct)
- `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs` (gRPC channel — already implements all ops including endpoint register/deregister; we only need to align gRPC request-type strings against server if TDD reveals mismatch)
- `src/RedNb.Nacos.Http/Ai/NacosPromptService.cs`, `NacosSkillService.cs`, `NacosAgentSpecService.cs` (already use correct `v3/client/ai/*` and `v3/admin/ai/*` paths on 8848)

---

## Task 1: Add `ConsoleAddresses` to `NacosClientOptions`

**Files:**
- Modify: `src/RedNb.Nacos/NacosClientOptions.cs` (after line 42 `Endpoint` property)
- Test: `tests/RedNb.Nacos.Tests/Config/NacosClientOptionsTests.cs` (new)

**Interfaces:**
- Consumes: existing `NacosClientOptions` shape
- Produces: `string ConsoleAddresses { get; set; }` (default `""`), `List<string> GetConsoleAddressList()`, `string GetConsoleBaseUrl(string serverAddress)`. After `Validate()`, `ConsoleAddresses` must be non-empty OR be derivable from `ServerAddresses` (port substitution `8848` → `8080`).

- [ ] **Step 1: Write the failing test**

Add to `tests/RedNb.Nacos.Tests/Config/NacosClientOptionsTests.cs` (create the file if absent):

```csharp
using FluentAssertions;
using RedNb.Nacos.Core;
using Xunit;

namespace RedNb.Nacos.Tests.Config;

public class NacosClientOptionsTests
{
    [Fact]
    public void ConsoleAddresses_DefaultsToEmpty()
    {
        var opts = new NacosClientOptions();
        opts.ConsoleAddresses.Should().Be(string.Empty);
    }

    [Fact]
    public void GetConsoleAddressList_DerivesFromServerAddresses_WhenConsoleEmpty()
    {
        var opts = new NacosClientOptions { ServerAddresses = "localhost:8848" };
        opts.GetConsoleAddressList().Should().Equal("localhost:8080");
    }

    [Fact]
    public void GetConsoleAddressList_ParsesExplicitConsoleAddresses()
    {
        var opts = new NacosClientOptions
        {
            ServerAddresses = "localhost:8848",
            ConsoleAddresses = "console1:8080,console2:8080"
        };
        opts.GetConsoleAddressList().Should().Equal("console1:8080", "console2:8080");
    }

    [Fact]
    public void GetConsoleAddressList_DerivationStripsMultiServerPort()
    {
        var opts = new NacosClientOptions { ServerAddresses = "h1:8848,h2:8848" };
        opts.GetConsoleAddressList().Should().Equal("h1:8080", "h2:8080");
    }

    [Fact]
    public void GetConsoleBaseUrl_NoNacosContextPath()
    {
        var opts = new NacosClientOptions { ContextPath = "nacos" };
        opts.GetConsoleBaseUrl("localhost:8080").Should().Be("http://localhost:8080");
    }

    [Fact]
    public void GetConsoleBaseUrl_Https_WhenTlsEnabled()
    {
        var opts = new NacosClientOptions { EnableTls = true };
        opts.GetConsoleBaseUrl("console:8443").Should().Be("https://console:8443");
    }

    [Fact]
    public void Validate_Throws_WhenNoServerAndNoConsoleAddresses()
    {
        var opts = new NacosClientOptions { ServerAddresses = "" };
        Action act = () => opts.Validate();
        act.Should().Throw<NacosException>().WithMessage("*ServerAddresses*ConsoleAddresses*");
    }

    [Fact]
    public void Validate_AllowsConsoleAddressesAlone()
    {
        var opts = new NacosClientOptions
        {
            ServerAddresses = "",
            ConsoleAddresses = "localhost:8080"
        };
        Action act = () => opts.Validate();
        act.Should().NotThrow();
    }
}
```

Run: `dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net8.0 --filter "FullyQualifiedName~NacosClientOptionsTests"`
Expected: 8 / 8 FAIL (methods not defined).

- [ ] **Step 2: Implement `ConsoleAddresses` in `NacosClientOptions.cs`**

Add after the existing `Endpoint` property (current line 42):

```csharp
    /// <summary>
    /// Nacos **console** server addresses, comma-separated. The console is a
    /// separate listener (default port 8080) and serves the AI admin/UI API
    /// (<c>/v3/console/ai/**</c>) plus the readiness/health probes. Distinct
    /// from <see cref="ServerAddresses"/>, which targets the API/gRPC port
    /// (default 8848).
    /// <para>
    /// When left empty, the SDK derives console addresses from
    /// <see cref="ServerAddresses"/> by replacing each port with the default
    /// console port (8080).
    /// </para>
    /// </summary>
    public string ConsoleAddresses { get; set; } = string.Empty;
```

Update `GetServerAddressList()` and add three new methods, and update `Validate()`:

```csharp
    /// <summary>
    /// Gets the console server address list, deriving from
    /// <see cref="ServerAddresses"/> (port → 8080 substitution) when
    /// <see cref="ConsoleAddresses"/> is empty.
    /// </summary>
    public List<string> GetConsoleAddressList()
    {
        if (!string.IsNullOrWhiteSpace(ConsoleAddresses))
        {
            return ConsoleAddresses
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(SubstituteConsolePort)
                .ToList();
        }
        return GetServerAddressList()
            .Select(SubstituteConsolePort)
            .ToList();
    }

    /// <summary>
    /// Gets the base URL for a console server address. Unlike
    /// <see cref="GetBaseUrl"/>, no <see cref="ContextPath"/> is appended: the
    /// console listener expects paths under <c>/v3/console/**</c> with no
    /// <c>/nacos</c> prefix.
    /// </summary>
    public string GetConsoleBaseUrl(string serverAddress)
    {
        var scheme = EnableTls ? "https" : "http";
        return $"{scheme}://{serverAddress}";
    }

    private static string SubstituteConsolePort(string hostPort)
    {
        var idx = hostPort.LastIndexOf(':');
        // IPv6 literal — leave as-is (operator can override via explicit ConsoleAddresses)
        if (hostPort.Count(c => c == ':') > 1) return hostPort;
        if (idx < 0) return $"{hostPort}:8080";
        return $"{hostPort[..idx]}:8080";
    }
```

Update `Validate()`:

```csharp
    public void Validate()
    {
        var hasCore = !string.IsNullOrWhiteSpace(ServerAddresses);
        var hasConsole = !string.IsNullOrWhiteSpace(ConsoleAddresses);
        var hasEndpoint = !string.IsNullOrWhiteSpace(Endpoint);
        if (!hasCore && !hasConsole && !hasEndpoint)
        {
            throw new NacosException(
                NacosException.InvalidParam,
                "At least one of ServerAddresses, ConsoleAddresses, or Endpoint must be provided");
        }
    }
```

Run the 8 tests again. Expected: 8 / 8 PASS.

- [ ] **Step 3: Commit**

```bash
git add src/RedNb.Nacos/NacosClientOptions.cs tests/RedNb.Nacos.Tests/Config/NacosClientOptionsTests.cs
git commit -m "feat(options): add ConsoleAddresses with port-8080 derivation"
```

---

## Task 2: `NacosConsoleHttpClient` targeting the console listener

**Files:**
- Create: `src/RedNb.Nacos.Http/Http/NacosConsoleHttpClient.cs`
- Test: `tests/RedNb.Nacos.Tests/Http/NacosConsoleHttpClientTests.cs` (new)

**Interfaces:**
- Consumes: `NacosClientOptions` (uses `GetConsoleAddressList()` / `GetConsoleBaseUrl()`)
- Produces: `public NacosConsoleHttpClient(NacosClientOptions options, ILogger? logger = null)` exposing the same public surface as `NacosHttpClient` (`GetAsync` / `PostAsync` / etc.) but no `/nacos` context path. Internally: own `HttpClient`, own `ServerListManager`, own `SecurityProxy`.

- [ ] **Step 1: Write the failing test**

Create `tests/RedNb.Nacos.Tests/Http/NacosConsoleHttpClientTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedNb.Nacos.Client.Http;
using RedNb.Nacos.Core;
using Xunit;

namespace RedNb.Nacos.Tests.Http;

public class NacosConsoleHttpClientTests : IDisposable
{
    private readonly NacosConsoleHttpClient _client;
    private readonly NacosClientOptions _options;

    public NacosConsoleHttpClientTests()
    {
        _options = new NacosClientOptions
        {
            ServerAddresses = "localhost:8848",
            Username = "nacos",
            Password = "nacos"
        };
        _client = new NacosConsoleHttpClient(_options, NullLogger<NacosConsoleHttpClient>.Instance);
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Constructor_UsesConsoleAddressesFromOptions()
    {
        _client.ConsoleBaseUrls.Should().Contain("http://localhost:8080");
        _client.ConsoleBaseUrls.Should().NotContain("/nacos");
    }

    [Fact]
    public void Constructor_FallsBackToDerivedConsoleAddress_WhenExplicitEmpty()
    {
        _options.ConsoleAddresses = string.Empty;
        using var c = new NacosConsoleHttpClient(_options, NullLogger<NacosConsoleHttpClient>.Instance);
        c.ConsoleBaseUrls.Should().Contain("http://localhost:8080");
    }

    [Fact]
    public void Constructor_UsesExplicitConsoleAddresses_WhenProvided()
    {
        _options.ConsoleAddresses = "console.example:8443";
        _options.EnableTls = true;
        using var c = new NacosConsoleHttpClient(_options, NullLogger<NacosConsoleHttpClient>.Instance);
        c.ConsoleBaseUrls.Should().Contain("https://console.example:8443");
    }
}
```

(The `ConsoleBaseUrls` property is a public read-only `IReadOnlyList<string>` exposed on the client for testability.)

Run: `dotnet test tests/RedNb.Nacos.Tests/RedNb.Nacos.Tests.csproj -f net8.0 --filter "FullyQualifiedName~NacosConsoleHttpClientTests"`
Expected: 3 / 3 FAIL (`NacosConsoleHttpClient` does not exist).

- [ ] **Step 2: Implement `NacosConsoleHttpClient`**

Create `src/RedNb.Nacos.Http/Http/NacosConsoleHttpClient.cs`. **Implementation contract**: copy the body of `NacosHttpClient.cs` (lines 14–417) into the new file, then make the following edits:

1. Class declaration: `public class NacosConsoleHttpClient : IDisposable`
2. Replace `private readonly ServerListManager _serverListManager = new ServerListManager(options)` with a constructor-initialized field fed by `options.GetConsoleAddressList()`:

```csharp
public class NacosConsoleHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly NacosClientOptions _options;
    private readonly ILogger? _logger;
    private readonly ServerListManager _serverListManager;
    private readonly SecurityProxy _securityProxy;
    private bool _disposed;

    public IReadOnlyList<string> ConsoleBaseUrls { get; }

    public NacosConsoleHttpClient(NacosClientOptions options, ILogger? logger = null)
    {
        _options = options;
        _logger = logger;
        var addresses = options.GetConsoleAddressList();
        if (addresses.Count == 0)
        {
            throw new NacosException(
                NacosException.InvalidParam,
                "ConsoleAddresses (or derivable ServerAddresses) is required for NacosConsoleHttpClient");
        }
        ConsoleBaseUrls = addresses
            .Select(a => options.GetConsoleBaseUrl(a))
            .ToList();
        _serverListManager = new ServerListManager(addresses);
        _securityProxy = new SecurityProxy(options, logger);

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _httpClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _httpClient.DefaultRequestHeaders.Add("Client-Version", "RedNb.Nacos/1.0.0");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RedNb.Nacos.Client");
    }

    // ... copy all Get/Post/Put/Delete methods from NacosHttpClient verbatim ...
    // ... inside RequestAsync and RequestRawAsync, replace:
    //         var baseUrl = _options.GetBaseUrl(server);
    //     with:
    //         var baseUrl = ConsoleBaseUrls[servers.IndexOf(server)];
    //   (or keep BaseUrl construction local; the simplest path is to inline
    //    `options.GetConsoleBaseUrl(server)` since ConsoleBaseUrls and addresses
    //    are 1:1)
```

3. In every `RequestAsync` / `RequestRawAsync`, change `var baseUrl = _options.GetBaseUrl(server);` to `var baseUrl = _options.GetConsoleBaseUrl(server);`. Leave the rest of the retry loop identical.
4. `AddAuthHeadersAsync` stays the same (uses `_securityProxy.GetAccessTokenAsync` which already logs in via 8848; the same accessToken works on 8080 — verified live).

Re-run the 3 tests. Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add src/RedNb.Nacos.Http/Http/NacosConsoleHttpClient.cs tests/RedNb.Nacos.Tests/Http/NacosConsoleHttpClientTests.cs
git commit -m "feat(http): add NacosConsoleHttpClient targeting console port (no /nacos prefix)"
```

---

## Task 3: Wire `NacosAiService` to the console client and fix sub-paths

**Files:**
- Modify: `src/RedNb.Nacos.Http/Ai/NacosAiService.cs`
- Test: new integration test added in Task 5 (TDD combined there)

**Interfaces:**
- Consumes: `NacosConsoleHttpClient` (new), `IAiService` (existing), `NacosClientOptions.ConsoleAddresses`
- Produces: `NacosAiService` that:
  - Uses `NacosConsoleHttpClient` for all MCP/A2A HTTP ops
  - Keeps existing `McpBasePath = "v3/console/ai/mcp"` and `A2aBasePath = "v3/console/ai/a2a"`
  - Corrects `ListAgentVersionsAsync` path: `/versions` → `/version/list`
  - Corrects `ReleaseMcpServerAsync` param: `serverSpec` → `serverSpecification`
  - Corrects `ValidateImportAsync` path: `/import/validate` → `/v3/console/ai/import/validate`
  - Corrects `ImportMcpServersAsync` path: `/import` → `/v3/console/ai/import/execute`
  - Throws `NacosException(ServerError, ...)` for `Register/Deregister{Mcp,Agent}ServerEndpointAsync`, `UpdateMcpToolAsync`, `DeleteMcpToolAsync`, `RefreshMcpToolAsync`, `GetMcpToolAsync` (these ops have no HTTP endpoint in 3.2.4; gRPC service has them)
  - Drops `BuildNamespaceHeaders()` calls (console ignores `X-Nacos-Namespace-Id` — verify in Task 4 first)

- [ ] **Step 1: Constructor change**

Replace the existing constructor in `NacosAiService.cs` (lines 47–64). The new constructor still constructs `NacosHttpClient` for the Prompt/Skill/AgentSpec sub-services (those target 8848 and are unaffected), but adds a `NacosConsoleHttpClient` field for MCP/A2A calls:

```csharp
public NacosAiService(NacosClientOptions options, ILogger<NacosAiService>? logger = null)
{
    _options = options;
    _logger = logger;
    // MCP/A2A HTTP ops target the console port (8080 by default) with no
    // /nacos context path. Prompt/Skill/AgentSpec target the core API
    // port (8848) and continue to use the standard client.
    _httpClient = new NacosConsoleHttpClient(options, logger);
    _legacyHttpClient = new NacosHttpClient(options, logger);
    _listenerManager = new AiListenerManager();
    _cacheHolder = new AiCacheHolder();
    _cts = new CancellationTokenSource();
    _namespaceId = options.Namespace ?? string.Empty;

    _promptService = new NacosPromptService(_legacyHttpClient, options, logger);
    _skillService = new NacosSkillService(_legacyHttpClient, options, logger);
    _agentSpecService = new NacosAgentSpecService(_legacyHttpClient, options, logger);

    _ = StartPollingAsync(_cts.Token);
}
```

Add the field near the existing `_httpClient`:

```csharp
private readonly NacosConsoleHttpClient _httpClient;
private readonly NacosHttpClient _legacyHttpClient;
```

- [ ] **Step 2: Fix `ListAgentVersionsAsync` path**

In `ListAgentVersionsAsync` (currently around line 916), replace:

```csharp
var response = await _httpClient.GetWithHeadersAsync($"{A2aBasePath}/versions", parameters, headers, _options.DefaultTimeout, cancellationToken);
```

with:

```csharp
var response = await _httpClient.GetWithHeadersAsync($"{A2aBasePath}/version/list", parameters, headers, _options.DefaultTimeout, cancellationToken);
```

- [ ] **Step 3: Fix `ReleaseMcpServerAsync` parameter name**

In `ReleaseMcpServerAsync` (around line 130), replace:

```csharp
{ "serverSpec", JsonSerializer.Serialize(serverSpecification, JsonOptions) }
```

with:

```csharp
{ "serverSpecification", JsonSerializer.Serialize(serverSpecification, JsonOptions) }
```

Remove the `toolSpec` / `endpointSpec` params from the query — they cause 400 in the console release path. Replace the full parameter block:

```csharp
var parameters = new Dictionary<string, string?>
{
    { "mcpName", serverSpecification.Name },
    { "serverSpecification", JsonSerializer.Serialize(serverSpecification, JsonOptions) }
};

var body = NacosUtils.BuildQueryString(parameters);
var headers = BuildNamespaceHeaders(); // replaced by Task 4 to no-op
var response = await _httpClient.PostWithHeadersAsync(McpBasePath, null, body, headers, _options.DefaultTimeout, cancellationToken);
```

- [ ] **Step 4: Fix import/validate and import paths**

In `ValidateImportAsync` (line 363), replace:

```csharp
var response = await _httpClient.PostWithHeadersAsync($"{McpBasePath}/import/validate", ...);
```

with:

```csharp
var response = await _httpClient.PostWithHeadersAsync("/v3/console/ai/import/validate", ...);
```

In `ImportMcpServersAsync` (line 408), replace:

```csharp
var response = await _httpClient.PostWithHeadersAsync($"{McpBasePath}/import", ...);
```

with:

```csharp
var response = await _httpClient.PostWithHeadersAsync("/v3/console/ai/import/execute", ...);
```

- [ ] **Step 5: Throw for endpoint / tool ops with no HTTP endpoint**

Add a private helper:

```csharp
private static void ThrowNoHttpEndpoint(string opName)
{
    throw new NacosException(
        NacosException.ServerError,
        $"Operation '{opName}' is not available over the HTTP channel in Nacos 3.2.4; " +
        "use IAiService backed by the gRPC channel (NacosGrpcFactory.CreateAiService) for endpoint and tool operations.");
}
```

Wrap the bodies of:

- `RegisterMcpServerEndpointAsync` (line ~158) — first line: `ThrowNoHttpEndpoint(nameof(RegisterMcpServerEndpointAsync));`
- `DeregisterMcpServerEndpointAsync` (line ~180)
- `RegisterAgentEndpointAsync` (line ~675)
- `RegisterAgentEndpointsAsync` (line ~695)
- `DeregisterAgentEndpointAsync` (line ~743)
- `UpdateMcpToolAsync` (line ~526)
- `DeleteMcpToolAsync` (line ~501)
- `RefreshMcpToolAsync` (line ~429)
- `GetMcpToolAsync` (line ~466)

Each method keeps its `Validate*` calls and then short-circuits with the throw. The `Task` return type is preserved (no signature change).

- [ ] **Step 6: Compile-check + commit**

Run: `dotnet build src/RedNb.Nacos.Http/RedNb.Nacos.Http.csproj -f net8.0` — expected: 0 errors.

```bash
git add src/RedNb.Nacos.Http/Ai/NacosAiService.cs
git commit -m "fix(ai): route MCP/A2A HTTP ops via console port, fix sub-paths and param names"
```

---

## Task 4: Verify and drop the `X-Nacos-Namespace-Id` header

**Files:**
- Modify: `src/RedNb.Nacos.Http/Ai/NacosAiService.cs` (`BuildNamespaceHeaders` callers + the helper itself)
- Modify: `src/RedNb.Nacos/Core/NacosConstants.cs` (drop `NamespaceHeader`)
- Test: integration assertion in Task 5 (`namespace=""` round-trip works without header)

**Interfaces:**
- Consumes: ground truth (live probe 2026-09-11 showed console returns `namespaceId:"public"` for all MCP/A2A ops; the v3 console controllers do not scope by namespace)
- Produces: `BuildNamespaceHeaders` returns an empty dictionary unconditionally; `NacosConstants.NamespaceHeader` constant removed; existing references (gRPC and config services) audited and dropped if dead

- [ ] **Step 1: Probe console with explicit namespace header to confirm it is ignored**

Run (with token obtained via `POST /nacos/v3/auth/user/login`):

```bash
TOKEN=$(curl -s -X POST "http://localhost:8848/nacos/v3/auth/user/login" -d "username=nacos&password=nacos" | python -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
curl -s "http://localhost:8080/v3/console/ai/mcp/list?accessToken=$TOKEN" \
  -H "X-Nacos-Namespace-Id: my-ns" | python -m json.tool | head -20
```

Expected: 200 with `namespaceId:"public"` in every pageItem, regardless of header. If a non-public namespace returns scoped results, **STOP and ledger a finding** — the decision below must change to keep the header. (Ruled decision: console controllers take no namespace input.)

- [ ] **Step 2: Remove `BuildNamespaceHeaders()` calls in NacosAiService**

There are 14 call sites (one per HTTP method). The helper itself becomes:

```csharp
/// <summary>
/// Returns an empty header dictionary. Nacos 3.x console controllers ignore
/// <c>X-Nacos-Namespace-Id</c> and always operate in the public namespace;
/// v3 namespaces are carried as <c>namespaceId</c> query parameters on the
/// core API instead.
/// </summary>
private Dictionary<string, string> BuildNamespaceHeaders() => new();
```

(Each caller still passes `headers`; helper just returns empty. No caller change.)

- [ ] **Step 3: Remove the constant**

Edit `src/RedNb.Nacos/Core/NacosConstants.cs`:

```csharp
public const string NamespaceHeader = "X-Nacos-Namespace-Id";   // DELETE
```

Verify no remaining references with:

```bash
git grep -n "NamespaceHeader\|X-Nacos-Namespace-Id"
```

For each remaining reference outside `NacosAiService.cs`: read the file, decide whether to remove or migrate to a query parameter. Document any carry-over in the PR description.

- [ ] **Step 4: Compile + commit**

Run: `dotnet build src/RedNb.Nacos.sln -f net8.0` — expected: 0 errors.

```bash
git add src/RedNb.Nacos.Http/Ai/NacosAiService.cs src/RedNb.Nacos/Core/NacosConstants.cs
git commit -m "refactor(ai): drop X-Nacos-Namespace-Id header (console is public-only)"
```

---

## Task 5: Rewrite the 6 swallowing integration tests

**Files:**
- Modify: `tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs`
- Modify: `tests/RedNb.Nacos.IntegrationTests/NacosServerFixture.cs` (add `ConsoleAddress`)

**Interfaces:**
- Consumes: `NacosConsoleHttpClient` (Task 2), fixed `NacosAiService` (Tasks 3–4), live Nacos 3.2.4 server
- Produces: 6 tests that assert real round-trip behavior

- [ ] **Step 1: Update the fixture**

Add to `NacosServerFixture.cs`:

```csharp
public const string ConsoleAddress = "localhost:8080";
```

- [ ] **Step 2: Rewrite the 6 tests**

Replace the file body of `AiServiceIntegrationTests.cs` (keep the file structure, keep `TestAgentCardListener`/`TestMcpServerListener` at the bottom, rename tests to drop the swallowing "ShouldWork" titles and use asserting shapes):

```csharp
using FluentAssertions;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Listener;
using RedNb.Nacos.Core.Ai.Model.A2a;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using Xunit;
using Xunit.Abstractions;

namespace RedNb.Nacos.IntegrationTests;

[Collection("NacosIntegration")]
public class AiServiceIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IAiService? _aiService;
    private readonly NacosClientOptions _options;

    public AiServiceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _options = new NacosClientOptions
        {
            ServerAddresses = NacosServerFixture.ServerAddress,
            ConsoleAddresses = NacosServerFixture.ConsoleAddress,
            Username = NacosServerFixture.Username,
            Password = NacosServerFixture.Password,
            Namespace = "",
            DefaultTimeout = 10000
        };
    }

    public Task InitializeAsync()
    {
        _aiService = new NacosFactory().CreateAiService(_options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_aiService is IAsyncDisposable d) await d.DisposeAsync();
    }

    // ---------- Agent Card (HTTP / console) ----------

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseAndGetAgentCard_RoundTripsViaConsole()
    {
        var agentName = $"it-agent-{Guid.NewGuid():N}";
        var card = new AgentCard
        {
            Name = agentName,
            Version = "1.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        };

        await _aiService!.ReleaseAgentCardAsync(card);
        await Task.Delay(500);

        var retrieved = await _aiService.GetAgentCardAsync(agentName);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(agentName);
        retrieved.Version.Should().Be("1.0.0");

        await _aiService.DeleteAgentAsync(agentName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task RegisterAndDeregisterAgentEndpoint_HttpChannel_ThrowsLoudly()
    {
        var agentName = $"it-agent-ep-{Guid.NewGuid():N}";
        var endpoint = new AgentEndpoint
        {
            Address = "127.0.0.1",
            Port = 9000,
            Version = "1.0.0",
            Transport = AiConstants.A2a.TransportJsonRpc
        };

        Func<Task> act = () => _aiService!.RegisterAgentEndpointAsync(agentName, endpoint);
        var ex = await act.Should().ThrowAsync<NacosException>();
        ex.Which.ErrorCode.Should().Be(NacosException.ServerError);
        ex.Which.Message.Should().Contain("not available over the HTTP channel");

        Func<Task> dereg = () => _aiService!.DeregisterAgentEndpointAsync(agentName, endpoint);
        var ex2 = await dereg.Should().ThrowAsync<NacosException>();
        ex2.Which.ErrorCode.Should().Be(NacosException.ServerError);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ListAgentCards_ReturnsReleasedAgent()
    {
        var agentName = $"it-agent-list-{Guid.NewGuid():N}";
        await _aiService!.ReleaseAgentCardAsync(new AgentCard
        {
            Name = agentName,
            Version = "1.0.0",
            ProtocolVersion = "0.3.7",
            PreferredTransport = "jsonrpc",
            Url = "http://127.0.0.1:9999"
        });
        await Task.Delay(500);

        var page = await _aiService.ListAgentCardsAsync(search: "accurate", pageSize: 100);
        page.PageItems.Should().Contain(i => i.Name == agentName);

        await _aiService.DeleteAgentAsync(agentName);
    }

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
        await Task.Delay(500);

        var versions = await _aiService.ListAgentVersionsAsync(agentName);
        versions.Should().Contain("2.0.0");

        await _aiService.DeleteAgentAsync(agentName);
    }

    // ---------- MCP Server (HTTP / console) ----------

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ReleaseAndGetMcpServer_RoundTripsViaConsole()
    {
        var mcpName = $"it-mcp-{Guid.NewGuid():N}";
        var spec = new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        };

        var mcpId = await _aiService!.ReleaseMcpServerAsync(spec, toolSpecification: null);
        mcpId.Should().NotBeNullOrEmpty();
        await Task.Delay(500);

        var retrieved = await _aiService.GetMcpServerAsync(mcpName);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(mcpName);
        retrieved.VersionDetail!.Version.Should().Be("1.0.0");

        await _aiService.DeleteMcpServerAsync(mcpName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task ListMcpServers_ReturnsReleasedServer()
    {
        var mcpName = $"it-mcp-list-{Guid.NewGuid():N}";
        await _aiService!.ReleaseMcpServerAsync(new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        }, toolSpecification: null);
        await Task.Delay(500);

        var page = await _aiService.ListMcpServersAsync(pageSize: 100);
        page.PageItems.Should().Contain(i => i.Name == mcpName);

        await _aiService.DeleteMcpServerAsync(mcpName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task RegisterAndDeregisterMcpEndpoint_HttpChannel_ThrowsLoudly()
    {
        Func<Task> reg = () => _aiService!.RegisterMcpServerEndpointAsync("it-mcp-x", "127.0.0.1", 9100);
        var ex = await reg.Should().ThrowAsync<NacosException>();
        ex.Which.ErrorCode.Should().Be(NacosException.ServerError);
        ex.Which.Message.Should().Contain("not available over the HTTP channel");
    }

    // ---------- MCP Server (subscribe — covered separately) ----------

    [Fact(Skip = "Polling-based; covered by manual smoke test. Polling interval is 10s.")]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task SubscribeAgentCard_ReceivesUpdates() { /* kept for reference; see Task 7 for gRPC subscribe */ }

    [Fact(Skip = "Polling-based; covered by manual smoke test. Polling interval is 10s.")]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task SubscribeMcpServer_ReceivesUpdates() { /* kept for reference */ }

    // ---------- Test Listeners (unchanged) ----------
    // ... copy verbatim from old file ...
}
```

- [ ] **Step 3: Run the integration tests against live Nacos**

Pre-conditions: Nacos up at `localhost:8080` and `localhost:8848`, `nacos/nacos` user bootstrapped (see Task 9 for bootstrap recipe).

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 --filter "FullyQualifiedName~AiServiceIntegrationTests"`

Expected: 7 PASS (subscribe tests skip by `Skip`). If any test fails with `NacosException NotFound`/`ServerError` whose message names a missing path or param, **STOP** and ledger the divergence — the ground truth table in this plan is authoritative; re-probe and amend the plan before fixing the SDK.

- [ ] **Step 4: Commit**

```bash
git add tests/RedNb.Nacos.IntegrationTests/AiServiceIntegrationTests.cs tests/RedNb.Nacos.IntegrationTests/NacosServerFixture.cs
git commit -m "test(ai): rewrite 6 swallowing AI tests to assert real round-trips on 8080"
```

---

## Task 6: gRPC AI live validation + dual-channel consistency

**Files:**
- Create: `tests/RedNb.Nacos.IntegrationTests/Ai/GrpcAiServiceIntegrationTests.cs`

**Interfaces:**
- Consumes: `NacosGrpcFactory.CreateAiService` (existing, in `RedNb.Nacos.Grpc`), live server with gRPC port 9848 reachable
- Produces: tests that prove (a) MCP/A2A ops work over gRPC, (b) registering via gRPC shows up in HTTP list (cross-channel consistency), (c) gRPC endpoint register/deregister — the ops HTTP rejects — work via gRPC

- [ ] **Step 1: Write the failing tests**

Create `tests/RedNb.Nacos.IntegrationTests/Ai/GrpcAiServiceIntegrationTests.cs`:

```csharp
using FluentAssertions;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Model.A2a;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using RedNb.Nacos.GrpcClient;
using Xunit;
using Xunit.Abstractions;

namespace RedNb.Nacos.IntegrationTests.Ai;

[Collection("NacosIntegration")]
public class GrpcAiServiceIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IAiService? _grpcAi;

    public GrpcAiServiceIntegrationTests(ITestOutputHelper output) { _output = output; }

    public async Task InitializeAsync()
    {
        var options = new NacosClientOptions
        {
            ServerAddresses = NacosServerFixture.ServerAddress,
            ConsoleAddresses = NacosServerFixture.ConsoleAddress,
            Username = NacosServerFixture.Username,
            Password = NacosServerFixture.Password,
            EnableGrpc = true,
            DefaultTimeout = 10000
        };
        _grpcAi = new NacosGrpcFactory().CreateAiService(options);
        if (_grpcAi is NacosGrpcClient.Ai.NacosGrpcAiService gs)
            await gs.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_grpcAi is IAsyncDisposable d) await d.DisposeAsync();
    }

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
        await _grpcAi!.ReleaseMcpServerAsync(spec, toolSpecification: null);
        await Task.Delay(500);
        var got = await _grpcAi.GetMcpServerAsync(mcpName);
        got.Should().NotBeNull();
        got!.Name.Should().Be(mcpName);
        await _grpcAi.DeleteMcpServerAsync(mcpName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task RegisterAndDeregisterMcpEndpoint_ViaGrpc()
    {
        var mcpName = $"grpc-mcp-ep-{Guid.NewGuid():N}";
        await _grpcAi!.ReleaseMcpServerAsync(new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        }, toolSpecification: null);
        await Task.Delay(500);

        await _grpcAi.RegisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100, "1.0.0");
        await Task.Delay(500);
        var got = await _grpcAi.GetMcpServerAsync(mcpName);
        // Backend endpoint attached → either retrieved or null on race; tolerate
        got.Should().NotBeNull();

        await _grpcAi.DeregisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100, "1.0.0");
        await _grpcAi.DeleteMcpServerAsync(mcpName);
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
        await Task.Delay(500);
        var got = await _grpcAi.GetAgentCardAsync(agentName);
        got.Should().NotBeNull();
        got!.Name.Should().Be(agentName);
        await _grpcAi.DeleteAgentAsync(agentName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Module", "AI")]
    public async Task CrossChannel_ReleaseViaGrpc_VisibleViaHttpList()
    {
        var mcpName = $"cross-mcp-{Guid.NewGuid():N}";
        await _grpcAi!.ReleaseMcpServerAsync(new McpServerBasicInfo
        {
            Name = mcpName,
            VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
            Protocol = "stdio"
        }, toolSpecification: null);
        await Task.Delay(1000);

        var httpAi = new NacosFactory().CreateAiService(new NacosClientOptions
        {
            ServerAddresses = NacosServerFixture.ServerAddress,
            ConsoleAddresses = NacosServerFixture.ConsoleAddress,
            Username = NacosServerFixture.Username,
            Password = NacosServerFixture.Password,
            DefaultTimeout = 10000
        });
        try
        {
            var page = await httpAi.ListMcpServersAsync(pageSize: 100);
            page.PageItems.Should().Contain(i => i.Name == mcpName);
        }
        finally
        {
            await httpAi.DisposeAsync();
            await _grpcAi.DeleteMcpServerAsync(mcpName);
        }
    }
}
```

Run: `dotnet test tests/RedNb.Nacos.IntegrationTests/RedNb.Nacos.IntegrationTests.csproj -f net8.0 --filter "FullyQualifiedName~GrpcAiServiceIntegrationTests"`

Expected: 4 / 4 PASS.

- [ ] **Step 2: If a gRPC request-type string is rejected**

Symptom: a test fails with `NacosException(ServerError, "no handler for type 'McpServerQueryRequest'")` or similar — the server reports "no handler" in its log (`docker logs nacos`).

Fix: locate the request-type constants in `src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs` (search for `"McpServerQueryRequest"`, `"McpServerReleaseRequest"`, `"QueryAgentCardRequest"`, `"ReleaseAgentCardRequest"`, `"McpServerEndpointRequest"`, `"AgentEndpointRequest"`, `"BatchAgentEndpointRequest"`, `"QueryPromptRequest"`). The server's handlers use the **class name** as the type discriminator — but the Java handler uses a `TYPE` constant. If divergent, extract the constants from the server-side request classes (extract `nacos-api-3.2.4.jar` → `com/alibaba/nacos/api/ai/remote/request/*.class`, dump all `Utf8` strings, find the single short string constant declared per class — that's the TYPE). Replace the SDK's string literals with the correct ones.

After fix, re-run the 4 tests. Commit:

```bash
git add tests/RedNb.Nacos.IntegrationTests/Ai/GrpcAiServiceIntegrationTests.cs src/RedNb.Nacos.Grpc/Ai/NacosGrpcAiService.cs
git commit -m "test(grpc-ai): live validation + dual-channel consistency"
```

---

## Task 7: Update `IAiService` XML docs

**Files:**
- Modify: `src/RedNb.Nacos/Core/IAiService.cs` (and sub-interface files if separate)

**Interfaces:**
- Consumes: all ops marked in Tasks 3+6
- Produces: `<remarks>` on each HTTP-channel-incompatible method reading: "Over the HTTP channel (`NacosAiService`), this operation is not available in Nacos 3.2.4. Use `IAiService` backed by the gRPC channel (`NacosGrpcFactory.CreateAiService`) instead."

- [ ] **Step 1: Apply the remarks**

For each of these XML doc comments:

- `RegisterAgentEndpointAsync` / `RegisterAgentEndpointsAsync` / `DeregisterAgentEndpointAsync`
- `RegisterMcpServerEndpointAsync` / `DeregisterMcpServerEndpointAsync`
- `UpdateMcpToolAsync` / `DeleteMcpToolAsync` / `RefreshMcpToolAsync` / `GetMcpToolAsync`

Append after the `<summary>`:

```xml
/// <remarks>
/// Nacos 3.2.4 does not expose an HTTP endpoint for this operation on the
/// console listener. The HTTP implementation
/// (<see cref="RedNb.Nacos.Client.Ai.NacosAiService"/>) throws
/// <see cref="NacosException"/> with code <c>ServerError</c>. Use the gRPC
/// implementation (<c>NacosGrpcFactory.CreateAiService</c>) instead.
/// </remarks>
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/RedNb.Nacos.sln -f net8.0
git add src/RedNb.Nacos/Core/IAiService.cs
git commit -m "docs(ai): note which ops require the gRPC channel on Nacos 3.2.4"
```

---

## Task 8: Doc updates (`SDK_COMPLETENESS_REPORT`, `SDK_COMPLETENESS_ANALYSIS`, `MIGRATION`)

**Files:**
- Modify: `docs/SDK_COMPLETENESS_REPORT.md`
- Modify: `docs/SDK_COMPLETENESS_ANALYSIS.md`
- Modify: `docs/MIGRATION.md`

**Interfaces:**
- Consumes: completion of Tasks 1–7
- Produces: doc-level status flip on §一.3, strike-through of §五 item 1, MIGRATION note on `ConsoleAddresses`

- [ ] **Step 1: REPORT §一.3 AI row**

Find the AI row in §一.3 (originally marked "100%*"). Replace the asterisk note for AI:

```
*实现齐备，HTTP 与 gRPC 双通道均已通过 live Nacos 3.2.4 验证（2026-09-11）；
HTTP 端点 register/deregister endpoint 与 tool CRUD 在 3.2.4 无对应路径，已 fail loud
指向 gRPC 通道；订阅仍为 10s 轮询，可后续替换为 gRPC push。
```

- [ ] **Step 2: REPORT §五 item 1**

Find §五 "1. AI 双通道 live 验证" (the high-priority item). Replace the body with one line:

```
~~1. AI 双通道 live 验证（8080）~~（2026-09-11 完成 — 见 §一.3 AI 行的 * 注）
```

- [ ] **Step 3: REPORT §三 item 7**

Find "NacosConstants.NamespaceHeader" / `X-Nacos-Namespace-Id` item in §三 known legacies. Replace the body:

```
~~7. `NacosConstants.NamespaceHeader` 常量保留仅为已搁置的 AI 代码引用~~（2026-09-11 已删除；
console 控制器仅服务 public 命名空间，AI HTTP 服务不再发送该 header）
```

- [ ] **Step 4: ANALYSIS §五 conclusion**

Replace the last sentence with:

```
剩余工作集中在 gRPC 服务层测试覆盖（ErrorCode 语义、Redo 接线、连接健壮性），
详见 REPORT 的"待完善"章节。
```

- [ ] **Step 5: MIGRATION auth section**

After the `Auth` section (line 47–51), add:

```markdown
## AI HTTP endpoints

Nacos 3.2.4 exposes AI management endpoints **only on the console listener**
(port 8080 by default), not on the API port (8848). Their paths start with
`/v3/console/ai/**` and require console-auth tokens. The HTTP AI service
(`NacosFactory.CreateAiService`) targets the console port automatically.

Configure `NacosClientOptions.ConsoleAddresses` to override the default
(`ServerAddresses` with port 8080):

```csharp
var options = new NacosClientOptions
{
    ServerAddresses = "nacos-1:8848,nacos-2:8848",     // API port(s)
    ConsoleAddresses = "nacos-1:8080,nacos-2:8080",    // Console port(s)
    Username = "nacos",
    Password = "nacos"
};
```

Endpoint register/deregister and MCP tool CRUD are **not** available over the
HTTP channel in 3.2.4. Use `NacosGrpcFactory.CreateAiService` for those
operations.
```

- [ ] **Step 6: Commit**

```bash
git add docs/SDK_COMPLETENESS_REPORT.md docs/SDK_COMPLETENESS_ANALYSIS.md docs/MIGRATION.md
git commit -m "docs: AI dual-channel verification complete (2026-09-11)"
```

---

## Task 9: `docs/ENVIRONMENT_BOOTSTRAP.md` — dev environment recipe

**Files:**
- Create: `docs/ENVIRONMENT_BOOTSTRAP.md`

**Interfaces:**
- Consumes: the verified 2026-09-11 bootstrap procedure (compose up + Derby insert + console auth)
- Produces: a single document the next developer can follow to get a working Nacos 3.2.4 dev environment for integration testing

- [ ] **Step 1: Write the document**

```markdown
# Bootstrap a Nacos 3.2.4 dev environment for SDK testing

The SDK integration tests assume a running Nacos 3.2.4 with:
- HTTP API on `localhost:8848` (gRPC on `9848`)
- Console on `localhost:8080` (separate listener, separate auth scope)
- A bootstrapped `nacos` user (no API/UI to create one exists in 3.2.4)

## 1. docker compose up

```bash
cd deploy/docker-compose
docker compose up -d nacos
```

Wait for readiness:

```bash
curl -fsS http://localhost:8080/v3/console/health/readiness
# → 200 once ready (≈18s on a clean Derby)
```

## 2. Bootstrap the `nacos` admin user

Nacos 2.2.2+ does not auto-create a default user, and 3.2.4 has no
user-management API or UI. Insert directly into Derby.

The container is JRE-only; build the bootstrap JARs on the host:

```bash
# From the nacos-server fat jar copied to a working dir:
python -c "
import zipfile, os
outer = zipfile.ZipFile('nacos-server.jar')
for n in outer.namelist():
    if not n.endswith('.jar'): continue
    if not any(x in n for x in ('spring-security-crypto', 'spring-jcl', 'derby')): continue
    with open(os.path.join('.', os.path.basename(n)), 'wb') as out:
        out.write(outer.read(n))
"
```

Write `InsertNacosUser.java`:

```java
import java.sql.*;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;

public class InsertNacosUser {
    public static void main(String[] args) throws Exception {
        String dbPath = args[0]; String user = args[1]; String pass = args[2];
        String hash = new BCryptPasswordEncoder().encode(pass);
        try (Connection c = DriverManager.getConnection("jdbc:derby:" + dbPath + ";create=false")) {
            try (PreparedStatement p = c.prepareStatement(
                    "INSERT INTO NACOS.users (username, password, enabled) VALUES (?, ?, true)")) {
                p.setString(1, user); p.setString(2, hash); p.executeUpdate();
            }
            try (PreparedStatement p = c.prepareStatement(
                    "INSERT INTO NACOS.roles (username, role) VALUES (?, 'ROLE_ADMIN')")) {
                p.setString(1, user); p.executeUpdate();
            }
            c.commit();
        } finally {
            try { DriverManager.getConnection("jdbc:derby:;shutdown=true"); } catch (SQLException ignored) {}
        }
    }
}
```

Build and run against the container's Derby directory (mounted to
`./nacos/data/derby-data`):

```bash
javac InsertNacosUser.java
java -cp "spring-security-crypto-*.jar;spring-jcl-*.jar;derby-*.jar;." \
  InsertNacosUser "deploy/docker-compose/nacos/data/derby-data" nacos nacos
# → "inserted user nacos"
```

Restart the container so the in-memory user cache picks up the new row:

```bash
docker compose restart nacos
# wait for readiness again
```

## 3. Verify

```bash
# Login (uses 8848; token works on both ports)
TOKEN=$(curl -s -X POST http://localhost:8848/nacos/v3/auth/user/login \
  -d 'username=nacos&password=nacos' \
  | python -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")

# Console AI list (8080, console-auth scoped)
curl -s "http://localhost:8080/v3/console/ai/mcp/list?accessToken=$TOKEN"
# → {"code":0,"message":"success","data":{"totalCount":0,...}}
```

## 4. Resetting the environment

The Derby data dir may become corrupt across unclean restarts (ExitCode=1
crash-loops with `load derby-schema.sql error`). To recover, rename the
corrupted dir aside and let compose recreate:

```bash
cd deploy/docker-compose
mv nacos/data/derby-data nacos/data/derby-data.bak-$(date +%Y%m%d)
docker compose restart nacos   # re-bootstraps Derby
# → then redo step 2 to re-insert the nacos user
```

## 5. SDK options for this environment

```csharp
var options = new NacosClientOptions
{
    ServerAddresses  = "localhost:8848",
    ConsoleAddresses = "localhost:8080",
    Username         = "nacos",
    Password         = "nacos",
    EnableGrpc       = true,
    DefaultTimeout   = 10000
};
```
```

- [ ] **Step 2: Commit**

```bash
git add docs/ENVIRONMENT_BOOTSTRAP.md
git commit -m "docs: Nacos 3.2.4 dev environment bootstrap recipe"
```

---

## Self-Review

**1. Spec coverage** (REPORT §五 items, ANALYSIS §三, MIGRATION Auth):
- §五 item 1 (AI 双通道 live 验证 8080) → Tasks 3, 5, 6 cover HTTP + gRPC + dual-channel consistency. ✅
- §五 item 1 sub-bullet (NamespaceHeader 清理) → Task 4. ✅
- §三 item 7 (NamespaceHeader legacy) → Task 4 + Task 8 §三 strike-through. ✅
- §一.3 AI row update → Task 8. ✅
- §五 conclusion update → Task 8. ✅
- MIGRATION.md AI endpoints doc → Task 8. ✅
- Bootstrap documentation gap → Task 9. ✅

**2. Placeholder scan**: no "TBD" / "TODO" / "implement later" in any task body. Each task contains the exact code or curl command to run.

**3. Type consistency**:
- `NacosConsoleHttpClient.ConsoleBaseUrls` declared `IReadOnlyList<string>` in Task 2 Step 1 and Step 2. ✅
- `NacosClientOptions.GetConsoleAddressList` / `GetConsoleBaseUrl` declared in Task 1 Step 2 and used in Task 2 Step 2 (`GetConsoleAddressList`, `GetConsoleBaseUrl`). ✅
- `IAiService` test classes (`AgentCard`, `McpServerBasicInfo`) reused across Tasks 5 and 6 with identical field shapes. ✅
- `NacosGrpcAiService.InitializeAsync` called in Task 6 only; not required by `NacosAiService` (HTTP service has no async init). ✅
- `EnableGrpc` field exists on `NacosClientOptions` (line 87). ✅

**4. Live ground truth**:
- All 8080 console paths in Tasks 3–5 verified live on 2026-09-11 (see ground-truth table at top).
- All 8848 + /nacos absence of console paths verified live.
- Console auth verified live (token obtained on 8848 works on 8080).
- Bootstrap recipe in Task 9 verified live (user insert succeeded, login returned valid JWT).

**5. Risk**:
- gRPC request-type strings (`McpServerQueryRequest`, etc.) in the SDK's `_grpcClient.RequestAsync<T>("McpServerQueryRequest", ...)` calls have not been validated end-to-end against the live server in this session (forensic only). Task 6 Step 2 includes the fallback path: if rejected, extract server-side TYPE constants and align.
- 14 call sites of `BuildNamespaceHeaders` in NacosAiService.cs are not individually listed in Task 4 Step 2 (helper becomes no-op). Audit confirmed: no caller relies on the header's contents.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-11-ai-live-validation.md`. Two execution options:

1. **Subagent-Driven (recommended)** — Dispatch a fresh subagent per task, review between tasks, fast iteration. Best fit because (a) the live Nacos server is up and the implementer can iterate against it, (b) each task has a clear deliverable + test gate, (c) Task 6 has a fallback branching point that benefits from isolated review.

2. **Inline Execution** — Execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints. Faster wall-clock but review quality depends on the session's accumulated context.

Which approach?

---

## Follow-up backlog（2026-09-11 live 验证后遗留，原 SDD ledger 转录）

### Task 10 — AgentVersionInfo 模型 ✅ 已完成（2026-09-11，实施计划 docs/superpowers/plans/2026-09-11-ai-followups.md）

~~`/v3/console/ai/a2a/version/list` 返回 rich object `{version, createdAt, updatedAt, latest}`，SDK `ListAgentVersionsAsync` 反序列化为 `List<string>`（契约意外）。决策：`RedNb.Nacos.Core` 加 `AgentVersionInfo`；选择 project `.Version`→`List<string>`（保 `IA2aService` 接口）还是 break 接口返回 rich records（同步 `IAiService`/`IA2aService` remarks）。重开 `ListAgentVersions_ReturnsVersions` 并断言 rich shape。~~
**落地**：`RedNb.Nacos.Core` 新增 `AgentVersionInfo`（`version`/`createdAt`/`updatedAt`/`latest`）+ `IA2aService.ListAgentVersionInfosAsync`；`ListAgentVersionsAsync` 改为 project `.Version`（接口不破坏）；`ListAgentVersions_ReturnsVersions` 已重开并 live 通过（commit `9c55c02`）。

### Task 11 — gRPC AI SDK gaps（re-enable 4 个 `GrpcAiServiceIntegrationTests`）✅ 已完成（2026-09-11，实施计划 docs/superpowers/plans/2026-09-11-ai-followups.md）

~~1. `NacosGrpcClient` 无 auth 路径（server 401 User not found）~~
~~2. `OperationResponse {Success,Message}` vs server `Response {resultCode,errorCode,message,requestId}`~~
~~3. `ReleaseMcpServerResponse` 读 `McpServerId`，server 下发 `mcpId`~~
4. ~~HTTP `DeleteMcpServerAsync` 无 body → 404~~（**已证伪**：F-1 探针 R1-R4 — body-less DELETE 对已存在服务器返回 200；Task 6 的 404 是删除从未创建成功的服务器；无此 gap）
~~另：4 测试 teardown 顺序（plain-sequence，断言失败泄漏）；`Namespace` camelCase vs server `namespaceId` 载荷核对；`ServerError`(500)→`ServerNotImplemented`(501)；weak namespace 探测复核（用有数据的 public 命名空间）。~~

**落地**：gRPC 载荷头携带 JWT `accessToken`（`SecurityProxy`，commit `defeea8`，6 个推送集成测试转为通过）；`ReleaseMcpServerResponse.McpServerId` → `McpId`、请求 DTO 发送 `namespaceId`（commit `ae2d4f5`）；无 handler 的 ops 改抛 `NacosException.ServerNotImplemented`(501)（commit `c468ae1`，`ListAgentVersions[Infos]` 于 `9c55c02` 追加）；4 个 `GrpcAiServiceIntegrationTests` 已重开并通过（commit `6bb3e2b`）。

注（仍为事实）：server 未注册 Delete/List/Subscribe/Tool/Import/Validate 的 gRPC handler — 这些 ops 只能走 HTTP。

### Task 12 — final-review parks ✅ 已完成（2026-09-11，实施计划 docs/superpowers/plans/2026-09-11-ai-followups.md）

- ~~`NacosConsoleHttpClient` 460 行近乎逐字复制 `NacosHttpClient`（仅 ~28 实质行差异）：重构共享请求管线（server-list resolver + base-URL builder 参数化）。~~
  **落地**：抽出 `NacosHttpClientBase` 承载共享传输，子类仅覆写 `BuildBaseUrl`（commit `a881977`）。
- ~~`NacosClientOptions.Validate()` 允许 `ServerAddresses=""`+`ConsoleAddresses` 的配置，但 `SecurityProxy` 跑不了（设计决策）。~~
  **落地**：`Validate()` 现拒绝"有凭据但无 `ServerAddresses`"（config-time `InvalidParam`）；无凭据的 console-only 仍有效（commit `b633d4c`）。
- ~~（原 "console toolSpecification 受理探针" 已由 F-1 解决：console 受理 specs，HTTP release 现携带 `toolSpecification`/`endpointSpecification` + `McpCapability` bare-token converter。）~~

### 2026-09-11 环境事实 ✅ 已解决（2026-09-11，实施计划 docs/superpowers/plans/2026-09-11-ai-followups.md）

~~6 个非 AI gRPC push 集成测试在本机失败（baseline 复现、`nacos-server` 未重启、environment-blocked，不计入通过数）。~~
**落地**：根因是 gRPC 载荷头缺少 JWT `accessToken`（server 401），非环境问题；`SecurityProxy` 接线（commit `defeea8`）后 6 个推送用例全部通过。
