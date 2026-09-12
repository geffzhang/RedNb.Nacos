# RedNb.Nacos.Sample.AI

Console sample demonstrating the full `IAiService` surface of the RedNb.Nacos
SDK (MCP, A2A, Prompt, Skill, AgentSpec) plus integration with
`Microsoft.Extensions.AI`, the official `ModelContextProtocol` SDK and
`Microsoft.Agents.AI`.

## Prerequisites

- **.NET 10 SDK** (the sample targets `net10.0`)
- A reachable **Nacos server 3.2.x** (the sample is verified on 3.2.4;
  force-publish needs ≥ 3.2.1) — default `localhost:8848`; the gRPC port is
  the HTTP port plus the configured offset, so 8848 → 9848
- No LLM API key required — the default `EchoChatClient` ships with the sample

The sample is written and verified against **Nacos 3.2.4** (see
`deploy/docker-compose/docker-compose.yml`).

## Run

```bash
dotnet run --project samples/RedNb.Nacos.Sample.AI
```

The process prints one line per section, then prints a `--- Summary ---` block
and exits 0; only a connection failure exits 2 — a section-level `Failed` is
reported in the summary block, not in the exit code.

## What it demonstrates

Eight sections run in order:

- `McpSamples` — release an MCP server (Streamable HTTP protocol metadata plus
  a `REF` endpoint spec) → list → get → subscribe/unsubscribe over the HTTP
  long-poll channel → **gRPC-only** endpoint register/deregister → delete.
  MCP tool CRUD is not exposed on Nacos 3.2.4, so the sample omits it.
- `A2aSamples` — release an Agent Card → **gRPC-only** batch endpoint register
  (`RegisterAgentEndpointsAsync`, two endpoints in one RTT) → list → get →
  subscribe → unsubscribe → **gRPC-only** endpoint deregister → delete.
- `PromptSamples` — draft → submit review → **force-publish** → online → client
  read + `Render()` → list → offline → delete.
- `SkillSamples` — build a minimal skill ZIP in memory (`SKILL.md` with YAML
  front matter) → upload → submit review → **force-publish** → online →
  download by version (md5 and entry shape checked) → offline → delete.
- `AgentSpecSamples` — build a minimal AgentSpec ZIP in memory (`manifest.json`
  + `AGENTS.md`) → upload (lands as `0.0.1`) → submit review → **force-publish**
  → online → read back by version and by the `latest` label → offline → delete.
- `Integration/PromptChatIntegrationSample` — publish a throwaway prompt,
  render its template into a system message, and hand it to an `IChatClient`
  (the echo client) — registry template to chat request, closed loop.
- `Integration/McpChatIntegrationSample` — host a real MCP server in-process
  (streamable HTTP on an ephemeral port), release it to the MCP registry and
  register the live endpoint, read the endpoint back **from Nacos**, dial it
  with `ModelContextProtocol.McpClient`, call `get_weather` over the wire, then
  wire the discovered tools into an `IChatClient` (`UseFunctionInvocation`).
- `Integration/AgentsAISamples` — three sub-demos, each with its own outcome:
  1. **MCP** — the same host/release/discover/dial loop, with the discovered
     tools wired into a Microsoft.Agents.AI `ChatClientAgent` running on the
     echo client.
  2. **Skill** — an inline, code-defined `AgentInlineSkill` fed to a
     `ChatClientAgent` through `AgentSkillsProviderBuilder`. Fully offline: no
     Nacos round trip and no model credentials.
  3. **A2A** — release a throwaway card, read the card JSON back out of the
     registry, map the stored 0.3.7 shape onto the A2A 1.0 protocol card, and
     build an `AIAgent` (`AsAIAgent`) plus an `A2AClientFactory` client from it,
     logging the transport metadata. No live A2A round trip: it needs a hosted
     A2A agent, and the hosting packages (`Microsoft.Agents.AI.Hosting.A2A*`)
     are outside this sample's pinned set — a code comment documents this.

### Force-publish and the AI pipeline plugin

Vanilla Nacos 3.2.4 ships the default AI pipeline plugin
(`nacos-default-ai-pipeline-plugin-3.2.4.jar`), which gates the normal publish
behind an approval step the stock server exposes no API for — a plain publish
call fails with `Pipeline not approved` (HTTP 400). Prompt, Skill and AgentSpec
therefore use `ForcePublish*Async` ([since=3.2.1]), the documented unattended
bypass, which has the same `reviewing → online` effect.

### Self-contained integration sections

`PromptChat`, `McpChat` and `AgentsAI` do not depend on artifacts another
section created: each publishes or releases its own timestamped prompt, MCP
server or agent card, and cleans it up in a `finally` (guarded per step, so a
cleanup failure cannot mask the run outcome). Their `MCP`/`A2A` registry reads
are the discovery moment — the endpoint or card is taken from what Nacos
returns, never from the local variable.

## Configuration

`Program.cs` reads `appsettings.json` from the output directory. Edit that file
or override with environment variables prefixed `REDNB_NACOS_` (nested keys use
`__`, e.g. `REDNB_NACOS_Nacos__ServerAddresses`).

| Key | Default | Notes |
|---|---|---|
| `Nacos:ServerAddresses` | `localhost:8848` | Comma-separated `host:port` list |
| `Nacos:GrpcPortOffset` | `1000` | gRPC port = HTTP port + offset (8848 → 9848) |
| `Nacos:Username` / `Password` | `nacos` / `nacos` | |
| `Nacos:Namespace` | (empty = `public`) | |
| `ChatProvider` | `echo` | `echo` or `openai`, resolved by `Chat/ChatClientFactory.cs`; an unknown value or a missing `OpenAI:ApiKey` falls back to echo with a warning |
| `OpenAI:ApiKey` | (empty) | Only read by the `openai` branch |
| `OpenAI:Model` | `gpt-4o-mini` | |
| `Logging:LogLevel:Default` | `Information` | `trace`/`debug`/`information`/`warning`/`error` |

The startup log prints the configured provider, e.g. `ChatProvider: echo` (the
`ChatClientFactory` fallback warning only appears on the config-driven path).

The `openai` branch is only compiled when `OPENAI_PROVIDER` is defined:

```bash
dotnet run --project samples/RedNb.Nacos.Sample.AI -p:DefineConstants=OPENAI_PROVIDER
```

The default build stays lean — the `Microsoft.Extensions.AI.OpenAI` package
reference is gated on that constant and only `EchoChatClient` is used. The
integration sections construct `EchoChatClient` directly, so the run itself
never needs credentials; `ChatClientFactory` is the config-driven path (covered
by `tests/RedNb.Nacos.Sample.AI.Tests`).

## Channel guide

| Operation | Channel |
|---|---|
| Get / List / Release / Upload / Subscribe (MCP, A2A, Prompt, Skill, AgentSpec) | HTTP |
| `RegisterAgentEndpointAsync` / `DeregisterAgentEndpointAsync` / `RegisterAgentEndpointsAsync` | **gRPC only** |
| `RegisterMcpServerEndpointAsync` / `DeregisterMcpServerEndpointAsync` | **gRPC only** |
| MCP tool CRUD (`RefreshMcpToolAsync`, `GetMcpToolAsync`, `DeleteMcpToolAsync`, `UpdateMcpToolAsync`) | Not exposed on Nacos 3.2.4 — sample omits these |
| Prompt / Skill / AgentSpec lifecycle | HTTP only (the gRPC `IAiService` delegates these to the same HTTP sub-service) |

The HTTP `IAiService` throws `NacosException(ServerError)` for the gRPC-only
operations; the sample delegates those calls to the gRPC `IAiService` instead.

## Troubleshooting

- **"Nacos connection failed"** — verify a Nacos 3.x server is running on
  `localhost:8848` (or update `Nacos:ServerAddresses`), that the gRPC port
  (`9848` by default) is reachable, and that auth matches
  `Nacos:Username`/`Nacos:Password`.
- **An integration section reports `Failed`** — those sections are
  self-contained (they publish/release their own artifact), so a failure means
  the registry or the wire path is broken rather than a missing prerequisite;
  there is no "run the CRUD section first" ordering requirement. No section is
  expected to report `Skipped` on Nacos 3.2.4 — the sample omits the MCP
  tool-CRUD calls that used to trigger it, so an all-`Ok` summary is the
  expected result.
- **`Pipeline not approved` (HTTP 400) from your own publish call** — the
  3.2.4 AI pipeline plugin gates normal publish; use the force-publish route
  ([since=3.2.1]), as this sample does.
- **MCP transport** — the sample speaks Streamable HTTP
  (`AiConstants.Mcp.ProtocolStreamable` / `HttpTransportMode.StreamableHttp`).
  A server that only speaks SSE needs a different transport mode in
  `McpChatIntegrationSample` / `AgentsAISamples`.

## Manual verification checklist

After running the sample against a real Nacos 3.2.4:

```
[ ] Mcp: release → list → get → subscribe/unsubscribe → gRPC endpoint register/deregister → delete
[ ] A2a: release card → gRPC batch endpoint register → list → get → subscribe/unsubscribe → gRPC deregister → delete
[ ] Prompt: draft → submit review → force-publish → online → read + render → list → offline → delete
[ ] Skill: upload ZIP → submit review → force-publish → online → download (SKILL.md) → offline → delete
[ ] AgentSpec: upload ZIP (0.0.1) → submit review → force-publish → online → get by version + label → offline → delete
[ ] PromptChat: own prompt → render → echo system-message + user-turn round trip → cleanup
[ ] McpChat: host MCP server → release → gRPC endpoint register → Nacos read-back → MCP handshake → get_weather over the wire → tools wired into IChatClient
[ ] AgentsAI: MCP agent (registry tools → ChatClientAgent), offline Skill sub-demo, A2A card read-back → AsAIAgent + A2AClientFactory
[ ] Summary lists all eight sections as Ok and the process exits 0
```

If a row cannot be checked, that's a regression — fix before shipping.
