# AI Samples & README Alignment — Design Spec

- **Date:** 2026-09-12
- **Status:** Draft — pending review
- **Owner:** SDK maintainers
- **Scope:** New `RedNb.Nacos.Sample.AI` console project, README fixes, deploy bump to Nacos 3.x

## 1. Problem

The SDK ships a substantial AI surface (`IAiService : IA2aService, IPromptService, ISkillService, IAgentSpecService`, ~70 methods covering MCP, A2A, Prompt, Skill, AgentSpec) but only two demo projects, neither of which touch AI:

- `samples/RedNb.Nacos.Sample.Console` — Config + Naming only.
- `samples/RedNb.Nacos.Sample.WebApi` — Config + Naming only.

`README.md` and `README.en.md` describe the AI API in tables and quick-start snippets, but the snippets use method signatures that do not match the real `IAiService`. They reference methods such as `ReleaseMcpServerAsync(name, spec)` and `RegisterMcpServerEndpointAsync(name, version, endpoint)` that the interface does not expose; copying them verbatim produces non-compiling code. The READMEs also never link to a runnable sample for the AI surface.

A second gap: `deploy/docker-compose.yml` and the GBK-encoded `start.sh` / `start.bat` ship `nacos/nacos-server:v2.3.0`. AI services require Nacos 3.0+; on 2.x the new `IAiService` methods are unreachable, so the new sample would fail before exercising any code.

A third opportunity: `Microsoft.Extensions.AI` (`IChatClient`, function invocation middleware) and the official `ModelContextProtocol` SDK (`McpClient`) are the natural runtime layer that consumes artifacts Nacos discovers. Without a sample that wires the two together, the most compelling use case of `IAiService` — *"Nacos as the AI registry, the AI SDKs as the runtime"* — is invisible.

## 2. Goals & Non-Goals

### 2.1 Goals

1. Ship one focused console sample (`RedNb.Nacos.Sample.AI`) that demonstrates the full `IAiService` surface and two integration flows (`Microsoft.Extensions.AI` + `ModelContextProtocol`).
2. Fix the AI quick-start snippets in `README.md` and `README.en.md` so they match the real interface and link to the new sample.
3. Bump the `deploy/` scripts and `docker-compose.yml` to a Nacos 3.x image so the new sample can be exercised end-to-end with `docker compose up`.
4. Keep the existing `Sample.Console` and `Sample.WebApi` untouched — they remain the "classic config + naming" demos.
5. Keep the sample runnable with **zero API keys** by default. Real LLM providers are opt-in via configuration.

### 2.2 Non-Goals

- Splitting AI into multiple sample projects (one per subsystem) — adds maintenance cost without proportional reader benefit.
- Replacing the IAiService CRUD demos with pure integration demos — readers still need a "what does the API look like?" reference.
- Updating `docs/MIGRATION.md` or `docs/SDK_COMPLETENESS_REPORT.md` unless they specifically reference the snippets being corrected.
- Adding the new sample to any CI pipeline beyond the existing solution build.
- Touching `RedNb.Nacos.Sample.Console` or `RedNb.Nacos.Sample.WebApi`.

## 3. Architecture

### 3.1 Project layout

```text
samples/RedNb.Nacos.Sample.AI/
├── RedNb.Nacos.Sample.AI.csproj          # net8.0; refs RedNb.Nacos.*, M.E.AI, MCP SDK
├── Program.cs                            # factory wiring (HTTP + gRPC), DI, section runner
├── appsettings.json                      # ChatProvider selector + provider config
├── McpSamples.cs                         # IAiService MCP CRUD + endpoint reg/dereg (gRPC)
├── A2aSamples.cs                         # IAiService A2A CRUD + batch endpoint register (gRPC)
├── PromptSamples.cs                      # Prompt lifecycle (draft → review → publish → online)
├── SkillSamples.cs                       # Skill ZIP upload/download + lifecycle
├── AgentSpecSamples.cs                   # AgentSpec lifecycle
├── Integration/
│   ├── McpChatIntegrationSample.cs       # Nacos → McpClient → IChatClient function wiring
│   └── PromptChatIntegrationSample.cs    # Nacos Prompt → render → IChatClient system message
├── Listeners/
│   ├── DemoMcpListener.cs                # AbstractNacosMcpServerListener impl
│   └── DemoAgentCardListener.cs          # AbstractNacosAgentCardListener impl
├── Chat/
│   ├── EchoChatClient.cs                 # default IChatClient (no API key)
│   └── ChatClientFactory.cs              # resolves IChatClient from appsettings
└── README.md
```

Each subsystem file has one job. `Integration/` is its own subfolder so the "Nacos-as-registry + AI runtime" story reads as a discrete layer above the CRUD demos. `Chat/` is isolated because the chat backend choice is a configuration concern, not a sample-shape concern. Channel markers (`// HTTP:` / `// gRPC:`) stay inline as comments — no channel-split folders, which would be over-engineering.

### 3.2 Project references & NuGet packages

- **SDK references**: `RedNb.Nacos.All` if the aggregate is non-placeholder; otherwise `RedNb.Nacos` + `RedNb.Nacos.Http` + `RedNb.Nacos.Grpc` + `RedNb.Nacos.DependencyInjection` directly. Resolve at implementation time by inspecting `src/RedNb.Nacos.All/Placeholder.cs`.
- **`Microsoft.Extensions.AI`** (current 9.x stable).
- **`ModelContextProtocol`** (current 0.x stable — verify Nacos 3.2.4 MCP wire compatibility at implementation time; flag if mismatched rather than silently bumping).
- **`Microsoft.Extensions.AI.OpenAI`**, **`Microsoft.Extensions.AI.AzureAIInference`**: gated behind `#if OPENAI_PROVIDER` / `#if AZURE_PROVIDER` so the default build does not pull them in.

### 3.3 Test project

```text
tests/RedNb.Nacos.Sample.AI.Tests/
├── RedNb.Nacos.Sample.AI.Tests.csproj    # xunit; refs sample + Microsoft.Extensions.AI
├── EchoChatClientTests.cs
└── ChatClientFactoryTests.cs
```

Both csproj files set `<IsPackable>false</IsPackable>`. The test project sets `<IsTestProject />`.

## 4. Components

### 4.1 `Program.cs` (orchestrator)

- Constructs `NacosClientOptions` with `EnableGrpc = true` and the gRPC port (default 9848).
- Creates both `IAiService` instances: one via `NacosFactory.CreateAiService(options)` (HTTP), one via `NacosGrpcFactory.CreateAiService(options)` (gRPC).
- Sets up `Microsoft.Extensions.Logging` via `LoggerFactory.Create(...)`, level `Information`, overridable via env var `LOG_LEVEL`.
- Links a top-level `CancellationTokenSource` to `Console.CancelKeyPress`.
- Runs the seven section files in order, passing the right `IAiService` overload (HTTP or gRPC) into each. Tallies `SampleResult` outcomes.
- Prints a final summary table ("MCP: ok / A2A: ok / Prompt: skipped (artifact missing) / Integration: failed (mcp endpoint unreachable)").
- `finally` disposes both `IAiService` instances and any open `McpClient`.

### 4.2 Section files (`McpSamples` / `A2aSamples` / `PromptSamples` / `SkillSamples` / `AgentSpecSamples`)

Each exposes the same signature — both `httpAi` and `grpcAi` are typed as `IAiService` so section files stay decoupled from the gRPC concrete class:

```csharp
public static async Task<SampleResult> RunAsync(
    IAiService httpAi,
    IAiService grpcAi,
    ILogger logger,
    CancellationToken ct);
```

`SampleResult` is a small record:

```csharp
public enum SampleOutcome { Ok, Skipped, Failed }
public sealed record SampleResult(SampleOutcome Outcome, string? Message = null);
```

Each section:

- Releases or uploads a uniquely named artifact (timestamp-suffixed to avoid collisions across runs).
- Reads it back to prove the round trip.
- Exercises at least one subscription or listener to demonstrate change events.
- Cleans up its own artifacts on success; on `Skipped` or `Failed`, leaves them for inspection.

### 4.3 `Integration/McpChatIntegrationSample.cs`

Two flows, both observable with the `EchoChatClient`:

**Flow A — Registry → MCP SDK → tool invocation.** `IAiService.GetMcpServerAsync("weather-mcp")` over HTTP → resolve endpoint → `McpClient.CreateAsync(new HttpClientTransport(new() { Endpoint = new Uri($"http://{address}:{port}") }))` → `McpClient.ListToolsAsync()` → `McpClient.CallToolAsync("get_weather", new() { ["city"] = "Paris" })` → print result content + round-trip latency.

**Flow B — Tools wired into an `IChatClient`.** Convert each `McpClientTool` to `AIFunction` (via the ModelContextProtocol SDK's `AsAIFunction()` / `AITool` conversion; if absent in the bound SDK version, wrap manually via `AIFunctionFactory.Create` against the tool's parameter schema — implementation decision). Build `new EchoChatClient().AsBuilder().UseFunctionInvocation().Build()`. Send a message with `ChatOptions { Tools = tools }`. The echo client annotates its response with the count of tools it received, so the reader sees "the chat client was handed N MCP tools sourced from Nacos" without needing a real LLM to loop tool calls.

### 4.4 `Integration/PromptChatIntegrationSample.cs`

1. `var prompt = await aiService.GetPromptByLabelAsync("code-review", "stable")`.
2. `var sysMsg = prompt!.Render(new Dictionary<string,string> { ["language"] = "C#", ["code"] = snippet })`.
3. Send `[new ChatMessage(ChatRole.System, sysMsg), new ChatMessage(ChatRole.User, "Review this code")]` to the echo client.
4. Echo returns the rendered system prompt back, demonstrating the prompt-as-template handoff.

### 4.5 `Listeners/`

- `DemoMcpListener : AbstractNacosMcpServerListener` — prints incoming `NacosMcpServerEvent` payloads.
- `DemoAgentCardListener : AbstractNacosAgentCardListener` — prints incoming `NacosAgentCardEvent` payloads.

### 4.6 `Chat/EchoChatClient`

Implements `IChatClient`:

- `Metadata` returns `new ChatClientMetadata("echo")`.
- `GetResponseAsync` returns an assistant message echoing the last user text, with a `"(echo, {N} tool(s) wired from Nacos)"` annotation when `ChatOptions.Tools` is non-empty.
- `GetStreamingResponseAsync` yields the same content as a stream of `ChatResponseUpdate`.
- `GetService` is pass-through for known service types, otherwise returns null.
- `Dispose` is a no-op.
- **Never throws** — it is the safety net for the no-API-key case.

### 4.7 `Chat/ChatClientFactory`

`CreateFromConfig(IConfiguration)` reads `ChatProvider`:

- `"echo"` (default) → `EchoChatClient`.
- `"openai"` with `OpenAI:ApiKey` set → `new OpenAIClient(apiKey).AsChatClient("gpt-4o-mini")` via `Microsoft.Extensions.AI.OpenAI` (gated by `#if OPENAI_PROVIDER`).
- `"azure"` with `AzureOpenAI:Endpoint`/`ApiKey`/`DeploymentName` set → `new AzureOpenAIClient(...).AsChatClient(deployment)` via `Microsoft.Extensions.AI.AzureAIInference` (gated by `#if AZURE_PROVIDER`).
- Missing config → falls back to Echo and logs a warning via the injected `ILogger`. Sensitive values (API key, password) are redacted from log output.

## 5. Data Flow

### 5.1 Per-section flow

```text
Program.cs
  └─ HttpAi.RunAsync / GrpcAi.RunAsync
       └─ <Section>.RunAsync(httpAi, grpcAi, logger, ct)
            ├─ Release / upload artifact         (HTTP for reads, gRPC for endpoint ops)
            ├─ Get / list / subscribe            (HTTP)
            └─ Delete / cleanup                  (HTTP or gRPC as appropriate)
                  └─ SampleResult(Ok | Skipped | Failed)
```

### 5.2 Integration flow (`McpChatIntegrationSample`)

```text
GetMcpServerAsync("weather-mcp")            [IAiService, HTTP]
   └─ McpServerDetailInfo
        └─ endpoint (address:port)
             └─ McpClient.CreateAsync(HttpClientTransport)   [ModelContextProtocol]
                  └─ mcpClient
                       ├─ ListToolsAsync()                   [McpClient]
                       │    └─ IList<McpClientTool>
                       │         └─ Convert to IList<AIFunction>   [MS.Extensions.AI]
                       │              └─ ChatOptions.Tools
                       └─ CallToolAsync("get_weather", {...}) [McpClient]
                            └─ PrintContent
EchoChatClient.GetResponseAsync(messages, ChatOptions{Tools})  [MS.Extensions.AI]
   └─ Assistant message annotating tool count   [IChatClient, no API key]
```

### 5.3 Channel split

| Operation | Channel |
|---|---|
| `GetMcpServerAsync`, `GetAgentCardAsync`, `GetPromptAsync`, `GetSkill*Async`, `GetAgentSpecAsync` | HTTP |
| `ReleaseMcpServerAsync`, `ReleaseAgentCardAsync`, `CreatePromptDraftAsync`, `UploadSkillZipAsync`, `UploadAgentSpecAsync` | HTTP |
| `List*Async` (paginated) | HTTP |
| `Subscribe*Async` | HTTP (long-poll) |
| `RegisterAgentEndpointAsync`, `DeregisterAgentEndpointAsync`, `RegisterAgentEndpointsAsync` (batch) | **gRPC only** |
| `RegisterMcpServerEndpointAsync`, `DeregisterMcpServerEndpointAsync` | **gRPC only** |
| `RefreshMcpToolAsync`, `GetMcpToolAsync`, `DeleteMcpToolAsync`, `UpdateMcpToolAsync` | Neither channel works on Nacos 3.2.4 — sample omits these and notes the gap in README |

Sections that need gRPC-only ops receive `grpcAi` and label those calls `// gRPC:`. Sections that hit the "neither channel works" gap skip the call and log a `Skipped` result with a "ServerError: MCP tool CRUD not exposed on Nacos 3.2.4" message.

## 6. Error Handling

| Category | Source | Handling |
|---|---|---|
| Connection / auth | wrong creds, server down, Nacos version <3.0 | `Program.cs` catches `NacosException` whose message contains "ServerError" and prints a remediation hint: "is Nacos 3.x running on {host}:{port}? auth ok? actual exception: {code} {message}". Exits cleanly. |
| gRPC-only op on HTTP | endpoint reg/deregister, batch register via HTTP `IAiService` | Section files wrap each gRPC-only call in a helper that delegates to `grpcAi` and prints an inline runtime note. The HTTP path is never attempted for these ops. |
| Missing prerequisite artifact | MCP server / Prompt not yet released | Section returns `SampleResult.Skipped("weather-mcp not found; run McpSamples first to release it")`. Other sections continue. |
| MCP transport failure | wrong address/port, SSE not exposed, MCP server down | Caught locally; prints "couldn't reach {endpoint}: {message} — is the MCP server listening? did RegisterMcpServerEndpointAsync run?" |
| Chat backend failure | OpenAI/Azure auth, rate limit, network | Errors propagate to the user. Echo mode never throws. |
| Cancellation | Ctrl+C / token cancel | A single top-level `CancellationTokenSource` linked to `Console.CancelKeyPress`; passed to every async call; the `finally` block disposes both `IAiService` instances and any open `McpClient`. |

**No silent retries.** Failures surface immediately. **Per-call timeout** defaults to 5 s. **No fallback to fake data** — if Nacos is unreachable, the sample exits with a clear error rather than pretending success.

## 7. Testing

### 7.1 Unit tests

Scope is the logic-bearing helpers only:

- **`EchoChatClientTests`**
  - Returns a response with role `Assistant`.
  - Includes the user text in the echoed content.
  - Annotates tool count when `ChatOptions.Tools` is non-empty.
  - Does not throw on empty messages or null options.

- **`ChatClientFactoryTests`**
  - Reads `ChatProvider` correctly: `"echo"` → `EchoChatClient`; `"openai"` with key → `OpenAI`-backed `IChatClient` (skipped when compiled without `#if OPENAI_PROVIDER`); missing config → Echo + expected warning.
  - Redacts API keys from log output.

`IAiService`, `McpClient`, and the section files are **not** unit-tested. SDK-level coverage lives in `tests/RedNb.Nacos.IntegrationTests/` already; duplicating it with mocks would be low value.

### 7.2 Solution hygiene

- Add `RedNb.Nacos.Sample.AI.csproj` and `RedNb.Nacos.Sample.AI.Tests.csproj` to `RedNb.Nacos.sln`.
- `<IsPackable>false</IsPackable>` on both.
- `<IsTestProject />` on the test project.
- Sample csproj pins `Microsoft.Extensions.AI` and `ModelContextProtocol` to versions verified compatible with Nacos 3.2.4's MCP wire format. Mismatch is flagged back, not silently bumped.

### 7.3 Manual verification checklist (in `samples/RedNb.Nacos.Sample.AI/README.md`)

```text
[ ] McpSamples: release → list → get → subscribe (gRPC endpoint reg) → delete
[ ] A2aSamples: release → batch endpoint register (gRPC) → list → delete
[ ] PromptSamples: draft → submit review → publish → online → list
[ ] SkillSamples: upload zip → download zip → publish → online
[ ] AgentSpecSamples: upload → publish → online
[ ] McpChatIntegrationSample: Nacos discovery → MCP connect → tool call → echo with tool count
[ ] PromptChatIntegrationSample: Nacos prompt → render → echo system message
```

If a row cannot be checked, that is a regression — fix before shipping.

### 7.4 What this section does NOT propose

- Snapshot tests over `Console.WriteLine` output — brittle.
- WireMock / in-process fake Nacos — out of proportion for a sample.
- Adding the new sample to any CI pipeline beyond the existing solution build.

## 8. README & Deploy Changes

### 8.1 `README.md` and `README.en.md` (parallel changes)

**Fix the AI quick-start snippet block** (`README.md` lines 195–264, equivalent block in en):
- `aiService.ReleaseMcpServerAsync("my-mcp-server", mcpServerSpec)` → `(McpServerBasicInfo, McpToolSpecification?)`.
- `aiService.RegisterMcpServerEndpointAsync("my-mcp-server", "1.0.0", endpoint)` → `(mcpName, address, port, version)`.
- `aiService.RegisterAgentEndpointAsync("my-agent", endpoint, TransportProtocol.Http)` → `(agentName, AgentEndpoint, transport)`.
- `aiService.ReleaseAgentCardAsync("my-agent", agentCard)` → real `(AgentCard)` overload; mention the additional `(registrationType, setAsLatest)` overloads exist.
- `CreatePromptDraftAsync("code-review", "1.1.0", "Review {{language}} code")` → keep (matches signature).
- Append a footer line: `📦 Full sample: see [samples/RedNb.Nacos.Sample.AI/](samples/RedNb.Nacos.Sample.AI/)`.

**Add a "🧪 示例 / Samples" subsection** under `### 🤖 AI 服务 (IAiService) - Nacos 3.0`:

| 示例 | 内容 |
|---|---|
| [`RedNb.Nacos.Sample.AI`](samples/RedNb.Nacos.Sample.AI/) | AI 服务全套演示：MCP/A2A/Prompt/Skill/AgentSpec CRUD，gRPC 通道的端点注册/批量注册，Nacos 作为注册中心 + Microsoft.Extensions.AI / ModelContextProtocol 集成的端到端示例 |

**Update the project structure tree** (lines 696–739) to include `samples/RedNb.Nacos.Sample.AI/`.

### 8.2 `samples/README.md`

Add the new project to the existing project table. Add a one-paragraph "AI Sample" subsection: prerequisites = Nacos 3.x; what it covers; chat backend options.

### 8.3 `samples/RedNb.Nacos.Sample.AI/README.md` (new)

Sections:
- **Prerequisites** — .NET 8, Nacos 3.0+ (default `localhost:8848` + gRPC `9848`), no API key required (Echo default).
- **Run** — `dotnet run`.
- **What it demonstrates** — bulleted list of the 7 section files plus the 2 integration flows.
- **Configuration** — `ChatProvider` values (`echo` | `openai` | `azure`) and the `appsettings.json` keys.
- **Channel guide** — table of which operations require the gRPC channel.
- **Troubleshooting** — Nacos 3.x required, gRPC port reachable, MCP endpoint must be registered before integration flows can connect.
- **Manual verification checklist** (from §7.3).

### 8.4 `deploy/`

- **`docker-compose.yml`** — `nacos/nacos-server:v2.3.0` → `nacos/nacos-server:v3.2.4`. Keep both ports `8848` and `9848`. Add `9849` if the 3.2.4 release server uses it for cluster gRPC; otherwise leave as is.
- **`.env.example`** — mirror the version bump.
- **`start.sh` / `start.bat`** — GBK-encoded per repo memory; edits use byte-level `sed`, not `Edit`. Only change is the image version.
- **`samples/README.md` line ~33** — `nacos/nacos-server:v2.3.0` → `nacos/nacos-server:v3.2.4` for parity.

### 8.5 `RedNb.Nacos.sln`

Add `samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj` and `tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj` so both projects are part of the solution build.

### 8.6 Out of scope

- `docs/MIGRATION.md`, `docs/SDK_COMPLETENESS_REPORT.md` — touched only if they reference the snippets being corrected.
- Any README sections unrelated to AI / project structure / samples.

## 9. Risks & Open Items

| Risk | Mitigation |
|---|---|
| `ModelContextProtocol` SDK 0.x is unstable; Nacos 3.2.4 MCP wire format may not match the SDK's expected protocol version. | Pin a known-good version at implementation time; if no version works, file a follow-up spec and ship the sample without Flow A's `McpClient.CreateAsync` call (replace with a manual SSE probe so the registration step is still observable). |
| `Microsoft.Extensions.AI.OpenAI` and `.AzureAIInference` carry transitive deps that may conflict with the rest of the solution. | Gate behind `#if` symbols so the default build is lean. Verify with `dotnet restore` after the change. |
| `RedNb.Nacos.All` may still be a placeholder. | Implementation agent inspects `src/RedNb.Nacos.All/Placeholder.cs`; if placeholder, reference the underlying projects directly. |
| GBK-encoded `start.sh` / `start.bat` break on Edit. | Use byte-level `sed`; verify file encoding with `file` before/after. |
| OpenAI / Azure provider packages break the sample build on machines without those keys. | `#if` gating + the default Echo path keeps the default build green. |

## 10. Definition of Done

- [ ] `samples/RedNb.Nacos.Sample.AI/` builds and runs end-to-end against Nacos 3.2.4 with the Echo chat backend; all 7 manual verification rows pass.
- [ ] `tests/RedNb.Nacos.Sample.AI.Tests/` builds and all unit tests pass on the default solution build (no `#if` flags required).
- [ ] `RedNb.Nacos.sln` builds clean.
- [ ] `README.md` and `README.en.md` AI snippets compile against the real `IAiService`. Both files link to the new sample under the AI section and in the project structure tree.
- [ ] `samples/RedNb.Nacos.Sample.AI/README.md` exists with all six subsections from §8.3.
- [ ] `deploy/docker-compose.yml` and `samples/README.md` use `nacos-server:v3.2.4`.
- [ ] `deploy/start.sh` and `deploy/start.bat` remain GBK-valid (verified with `file`) and point at the 3.x image.
- [ ] No silent retries, no fake-data fallbacks. Failures are loud.
- [ ] No untracked files outside the listed scope.