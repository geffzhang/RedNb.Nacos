# AI Samples Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a runnable `RedNb.Nacos.Sample.AI` console demonstrating the full `IAiService` surface (MCP / A2A / Prompt / Skill / AgentSpec, both HTTP and gRPC channels) plus integration with `Microsoft.Extensions.AI` and `ModelContextProtocol`; fix the AI quick-start snippets in both READMEs; bump `deploy/` to Nacos 3.x so the sample runs end-to-end.

**Architecture:** New console project under `samples/RedNb.Nacos.Sample.AI/` with one file per AI subsystem plus a small `Integration/` and `Chat/` subfolder. A separate test project under `tests/RedNb.Nacos.Sample.AI.Tests/` holds unit tests for the `EchoChatClient` and `ChatClientFactory` only — section files are integration demos against a real Nacos 3.x. README fixes are in `README.md` and `README.en.md`. Deploy scripts under `deploy/` (GBK-encoded `start.sh` / `start.bat` are edited with byte-level `sed`, not `Edit`).

**Tech Stack:** .NET 8, `Microsoft.Extensions.AI` (current 9.x stable), `ModelContextProtocol` (current 0.x stable), xUnit for the test project. Nacos server 3.2.4 (Docker).

**Spec:** [docs/superpowers/specs/2026-09-12-ai-samples-design.md](../specs/2026-09-12-ai-samples-design.md) — every task below argues from that spec, so the plan and spec travel together.

## Global Constraints

Carried verbatim from the spec:

- Target framework: `net8.0` (matches existing samples).
- SDK references: `RedNb.Nacos.All` if the aggregate is non-placeholder; otherwise reference `RedNb.Nacos` + `RedNb.Nacos.Http` + `RedNb.Nacos.Grpc` + `RedNb.Nacos.DependencyInjection` directly. Inspect `src/RedNb.Nacos.All/Placeholder.cs` at Task 1.
- `Microsoft.Extensions.AI` 9.x stable. `ModelContextProtocol` 0.x stable (verify Nacos 3.2.4 MCP wire compatibility at Task 11; flag mismatch, do not silently bump).
- `Microsoft.Extensions.AI.OpenAI` and `.AzureAIInference` are gated behind `#if OPENAI_PROVIDER` / `#if AZURE_PROVIDER` — the default build must not pull them in.
- Both new csproj files set `<IsPackable>false</IsPackable>`; the test project sets `<IsTestProject />`.
- Section files are typed against `IAiService` for both HTTP and gRPC — they must NOT depend on the gRPC concrete class.
- `McpClientTool` → `AIFunction` conversion uses the ModelContextProtocol SDK's `AsAIFunction()` / `AITool` extension if present in the bound SDK version; otherwise wrap manually via `AIFunctionFactory.Create(...)` against the tool's parameter schema (implementation decision in Task 11).
- Default chat backend is `EchoChatClient` — no API key required. OpenAI/Azure are opt-in.
- Nacos 3.2.4 server: endpoints 8848 (HTTP) + 9848 (gRPC). The HTTP `IAiService` throws `NacosException(ServerError)` for endpoint reg/deregister, batch endpoint register, and MCP tool CRUD — those operations must go through the gRPC `IAiService`, never attempted via HTTP.
- `deploy/start.sh` and `deploy/start.bat` are GBK-encoded per repo memory. Edits use byte-level `sed`, not `Edit`. Verify with `file` before and after.
- No silent retries, no fake-data fallbacks. Failures surface immediately. Per-call timeout defaults to 5 s.
- Per repo memory (`airegistry-ai-followups`): README/plan files normally stay uncommitted unless the user asks. The plan below uses commits for traceability during implementation, but **the final commit for Tasks 13–16 should be undone** (or amended) at the end of the plan if the user prefers the historical uncommitted-doc policy.

## File Structure

**Created** (`samples/RedNb.Nacos.Sample.AI/`):
- `RedNb.Nacos.Sample.AI.csproj` — net8.0, refs SDK projects + `Microsoft.Extensions.AI` + `ModelContextProtocol`, `#if`-gated provider packages, `<IsPackable>false</IsPackable>`
- `SampleResult.cs` — `SampleOutcome` enum + `SampleResult` record
- `Program.cs` — orchestrator: factory wiring (HTTP + gRPC), DI, section runner, summary table
- `appsettings.json` — `ChatProvider` selector + provider config
- `Chat/EchoChatClient.cs` — `IChatClient` impl that never throws
- `Chat/ChatClientFactory.cs` — `CreateFromConfig(IConfiguration)` with `#if`-gated OpenAI/Azure branches
- `Listeners/DemoMcpListener.cs` — `AbstractNacosMcpServerListener` impl
- `Listeners/DemoAgentCardListener.cs` — `AbstractNacosAgentCardListener` impl
- `McpSamples.cs` — MCP CRUD (HTTP) + endpoint reg/dereg (gRPC)
- `A2aSamples.cs` — A2A CRUD (HTTP) + batch endpoint register (gRPC)
- `PromptSamples.cs` — Prompt lifecycle (draft → review → publish → online)
- `SkillSamples.cs` — Skill ZIP upload/download + lifecycle
- `AgentSpecSamples.cs` — AgentSpec lifecycle
- `Integration/PromptChatIntegrationSample.cs` — Nacos Prompt → render → `IChatClient` system message
- `Integration/McpChatIntegrationSample.cs` — Nacos → `McpClient` → `IChatClient` function wiring (Flow A + Flow B)
- `README.md` — six subsections (Prerequisites / Run / What it demonstrates / Configuration / Channel guide / Troubleshooting) + manual verification checklist

**Created** (`tests/RedNb.Nacos.Sample.AI.Tests/`):
- `RedNb.Nacos.Sample.AI.Tests.csproj` — xUnit, refs sample + `Microsoft.Extensions.AI`, `<IsPackable>false</IsPackable>`, `<IsTestProject />`
- `EchoChatClientTests.cs`
- `ChatClientFactoryTests.cs`

**Modified:**
- `RedNb.Nacos.sln` — add the two new csproj
- `README.md` — fix AI quick-start snippet block (lines 195–264), add Samples subsection under AI service section, update project structure tree (lines 696–739)
- `README.en.md` — mirror the same three edits
- `samples/README.md` — add new project to project table, add AI Sample subsection, update docker run snippet to `v3.2.4`
- `deploy/docker-compose.yml` — `nacos-server:v2.3.0` → `v3.2.4`
- `deploy/.env.example` — mirror version bump
- `deploy/start.sh` — image bump via byte-level `sed` (GBK)
- `deploy/start.bat` — image bump via byte-level `sed` (GBK)

---

## Task 1: Sample skeleton, test project scaffold, sln registration

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
- Create: `samples/RedNb.Nacos.Sample.AI/Program.cs` (placeholder)
- Create: `tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj`
- Modify: `RedNb.Nacos.sln` — add both projects

**Interfaces:**
- Consumes: nothing yet (first task)
- Produces:
  - `samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj` builds clean via `dotnet build`
  - `tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj` builds clean via `dotnet build`
  - `RedNb.Nacos.sln` builds clean via `dotnet build RedNb.Nacos.sln`

**Inspect `src/RedNb.Nacos.All/Placeholder.cs` first.** If it is a single-class file with only `namespace RedNb.Nacos { internal static class Placeholder { } }` and no real `RedNb.Nacos.All.csproj`, reference the SDK projects directly. If it is a real aggregate, reference `RedNb.Nacos.All` instead.

- [ ] **Step 1: Read `src/RedNb.Nacos.All/Placeholder.cs` and `src/RedNb.Nacos.All/RedNb.Nacos.All.csproj`**

Run: `cat src/RedNb.Nacos.All/Placeholder.cs` (use Read tool)
Expected: Either a placeholder file (reference SDK projects directly) or a real aggregate csproj (reference `RedNb.Nacos.All`).

- [ ] **Step 2: Look at existing `samples/RedNb.Nacos.Sample.Console/RedNb.Nacos.Sample.Console.csproj` to copy the SDK-reference pattern**

Run: `cat samples/RedNb.Nacos.Sample.Console/RedNb.Nacos.Sample.Console.csproj` (use Read tool)
Expected: A csproj referencing `RedNb.Nacos.Client` and `RedNb.Nacos.Core` (or similar).

- [ ] **Step 3: Create `samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>RedNb.Nacos.Sample.AI</RootNamespace>
    <AssemblyName>RedNb.Nacos.Sample.AI</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <UserSecretsId>rednb-nacos-sample-ai</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <!-- Pick one of the two blocks below based on Step 1 -->
    <!-- (a) RedNb.Nacos.All is real: -->
    <ProjectReference Include="..\..\src\RedNb.Nacos.All\RedNb.Nacos.All.csproj" />
    <!-- (b) RedNb.Nacos.All is placeholder — uncomment instead: -->
    <!--
    <ProjectReference Include="..\..\src\RedNb.Nacos\RedNb.Nacos.csproj" />
    <ProjectReference Include="..\..\src\RedNb.Nacos.Http\RedNb.Nacos.Http.csproj" />
    <ProjectReference Include="..\..\src\RedNb.Nacos.Grpc\RedNb.Nacos.Grpc.csproj" />
    <ProjectReference Include="..\..\src\RedNb.Nacos.DependencyInjection\RedNb.Nacos.DependencyInjection.csproj" />
    -->
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.AI" Version="9.*" />
    <PackageReference Include="ModelContextProtocol" Version="0.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.*" />
  </ItemGroup>

  <ItemGroup Condition="'$(DefineConstants.Contains(`OPENAI_PROVIDER`))' == 'True'">
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.*" />
  </ItemGroup>

  <ItemGroup Condition="'$(DefineConstants.Contains(`AZURE_PROVIDER`))' == 'True'">
    <PackageReference Include="Microsoft.Extensions.AI.AzureAIInference" Version="9.*" />
  </ItemGroup>

</Project>
```

Use the (a) block if `RedNb.Nacos.All.csproj` is real, the (b) block if it is a placeholder. Delete the unused block.

- [ ] **Step 4: Create `samples/RedNb.Nacos.Sample.AI/Program.cs` placeholder**

```csharp
// Placeholder — full orchestrator arrives in Task 4.
Console.WriteLine("RedNb.Nacos.Sample.AI — skeleton ready.");
```

- [ ] **Step 5: Create `tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>RedNb.Nacos.Sample.AI.Tests</RootNamespace>
    <AssemblyName>RedNb.Nacos.Sample.AI.Tests</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject />
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Microsoft.Extensions.AI" Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Memory" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\samples\RedNb.Nacos.Sample.AI\RedNb.Nacos.Sample.AI.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 6: Add both projects to `RedNb.Nacos.sln`**

Use `dotnet sln add`:

```bash
dotnet sln RedNb.Nacos.sln add samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj
dotnet sln RedNb.Nacos.sln add tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj
```

Expected: both print "Project added to the solution."

- [ ] **Step 7: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`. Warnings about pinned `9.*` / `0.*` package versions resolving to a specific prerelease are acceptable; errors are not.

- [ ] **Step 8: Verify the test project builds**

Run: `dotnet build tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj`
Expected: `Build succeeded`.

- [ ] **Step 9: Verify the full solution builds**

Run: `dotnet build RedNb.Nacos.sln`
Expected: `Build succeeded`. No new errors introduced by the two new projects.

- [ ] **Step 10: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj \
        samples/RedNb.Nacos.Sample.AI/Program.cs \
        tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj \
        RedNb.Nacos.sln
git commit -m "feat(samples): scaffold RedNb.Nacos.Sample.AI + test project"
```

---

## Task 2: EchoChatClient (TDD)

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/Chat/EchoChatClient.cs`
- Create: `tests/RedNb.Nacos.Sample.AI.Tests/EchoChatClientTests.cs`

**Interfaces:**
- Consumes: `Microsoft.Extensions.AI` (`IChatClient`, `ChatMessage`, `ChatRole`, `ChatOptions`, `ChatResponse`, `ChatClientMetadata`, `ChatResponseUpdate`)
- Produces: `public sealed class EchoChatClient : IChatClient` with `Metadata`, `GetResponseAsync`, `GetStreamingResponseAsync`, `GetService`, `Dispose`. Never throws. Returns assistant message containing the last user text plus `(echo, {N} tool(s) wired from Nacos)` when `ChatOptions.Tools` is non-empty.

- [ ] **Step 1: Write the failing test**

Create `tests/RedNb.Nacos.Sample.AI.Tests/EchoChatClientTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using RedNb.Nacos.Sample.AI.Chat;
using Xunit;

namespace RedNb.Nacos.Sample.AI.Tests;

public class EchoChatClientTests
{
    [Fact]
    public void Metadata_DeclaresEchoIdentifier()
    {
        var client = new EchoChatClient();
        Assert.Equal("echo", client.Metadata.ProviderName);
    }

    [Fact]
    public async Task GetResponseAsync_ReturnsAssistantRole()
    {
        var client = new EchoChatClient();
        var response = await client.GetResponseAsync(new[]
        {
            new ChatMessage(ChatRole.User, "hello")
        });

        Assert.NotNull(response);
        Assert.NotEmpty(response.Messages);
        Assert.Equal(ChatRole.Assistant, response.Messages[0].Role);
    }

    [Fact]
    public async Task GetResponseAsync_EchoesTheUserText()
    {
        var client = new EchoChatClient();
        var response = await client.GetResponseAsync(new[]
        {
            new ChatMessage(ChatRole.User, "Paris weather please")
        });

        var text = response.Messages[0].Text;
        Assert.Contains("Paris weather please", text);
    }

    [Fact]
    public async Task GetResponseAsync_AnnotatesToolCountWhenToolsProvided()
    {
        var client = new EchoChatClient();
        var options = new ChatOptions
        {
            Tools = new List<AITool>
            {
                new TestTool("get_weather"),
                new TestTool("get_time")
            }
        };

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "go") }, options);

        Assert.Contains("2 tool", response.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_DoesNotThrowOnEmptyMessages()
    {
        var client = new EchoChatClient();
        var response = await client.GetResponseAsync(Array.Empty<ChatMessage>());
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetResponseAsync_DoesNotThrowOnNullOptions()
    {
        var client = new EchoChatClient();
        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") }, options: null);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_YieldsAtLeastOneUpdate()
    {
        var client = new EchoChatClient();
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "stream me") }))
        {
            updates.Add(update);
        }
        Assert.NotEmpty(updates);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var client = new EchoChatClient();
        client.Dispose();
        client.Dispose(); // must not throw
    }

    // Minimal AITool for tests; AITool is abstract in current SDK, so derive a
    // throwaway concrete subclass for the unit test only.
    private sealed class TestTool : AITool
    {
        public TestTool(string name) { Name = name; }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj --filter FullyQualifiedName~EchoChatClientTests`
Expected: All 8 tests FAIL with `EchoChatClient` not found (CS0246 / FileNotFound).

- [ ] **Step 3: Implement `EchoChatClient`**

Create `samples/RedNb.Nacos.Sample.AI/Chat/EchoChatClient.cs`:

```csharp
using Microsoft.Extensions.AI;

namespace RedNb.Nacos.Sample.AI.Chat;

/// <summary>
/// Default IChatClient implementation for the AI sample. Echoes the last
/// user message back and annotates the response with the count of tools it
/// was handed. Never throws — it is the safety net for the no-API-key case.
/// </summary>
public sealed class EchoChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = new("echo");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var userText = messages?
            .LastOrDefault(m => m.Role == ChatRole.User)?.Text
            ?? string.Empty;

        var toolCount = options?.Tools?.Count ?? 0;
        var suffix = toolCount > 0
            ? $" (echo, {toolCount} tool(s) wired from Nacos)"
            : string.Empty;

        var reply = new ChatMessage(
            ChatRole.Assistant,
            $"[echo]{suffix} {userText}".TrimStart());

        return Task.FromResult(new ChatResponse(reply));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { /* no-op */ }
}
```

If the SDK version in use exposes `AITool.Name` differently (the current 9.x has `Name` as a settable property on the abstract base — confirm at implementation time), adjust the `TestTool` helper in the test file accordingly. If `Name` is read-only, use a constructor that takes the name parameter via the base type's protected setter or a constructor overload.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj --filter FullyQualifiedName~EchoChatClientTests`
Expected: All 8 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/Chat/EchoChatClient.cs \
        tests/RedNb.Nacos.Sample.AI.Tests/EchoChatClientTests.cs
git commit -m "feat(samples): add EchoChatClient with unit tests"
```

---

## Task 3: ChatClientFactory (TDD)

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/Chat/ChatClientFactory.cs`
- Create: `tests/RedNb.Nacos.Sample.AI.Tests/ChatClientFactoryTests.cs`

**Interfaces:**
- Consumes: `IConfiguration`, `ILogger`, `Microsoft.Extensions.AI` (`IChatClient`), `EchoChatClient` (Task 2)
- Produces: `public static class ChatClientFactory` with `CreateFromConfig(IConfiguration, ILogger?)` returning `IChatClient`. Reads `ChatProvider` key. Default is echo. OpenAI/Azure branches gated behind `#if OPENAI_PROVIDER` / `#if AZURE_PROVIDER`. Redacts API keys from log output.

- [ ] **Step 1: Write the failing test**

Create `tests/RedNb.Nacos.Sample.AI.Tests/ChatClientFactoryTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedNb.Nacos.Sample.AI.Chat;
using Xunit;

namespace RedNb.Nacos.Sample.AI.Tests;

public class ChatClientFactoryTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(values);
        return builder.Build();
    }

    [Fact]
    public void CreateFromConfig_DefaultProvider_ReturnsEcho()
    {
        var config = BuildConfig(new Dictionary<string, string?>()); // empty
        IChatClient client = ChatClientFactory.CreateFromConfig(config, NullLogger.Instance);
        Assert.IsType<EchoChatClient>(client);
    }

    [Fact]
    public void CreateFromConfig_EchoExplicitly_ReturnsEcho()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ChatProvider"] = "echo"
        });
        IChatClient client = ChatClientFactory.CreateFromConfig(config, NullLogger.Instance);
        Assert.IsType<EchoChatClient>(client);
    }

    [Fact]
    public void CreateFromConfig_UnknownProvider_FallsBackToEchoAndLogsWarning()
    {
        var logger = new TestLogger();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ChatProvider"] = "made-up-provider"
        });
        IChatClient client = ChatClientFactory.CreateFromConfig(config, logger);
        Assert.IsType<EchoChatClient>(client);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void CreateFromConfig_OpenAiMissingApiKey_FallsBackToEcho()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ChatProvider"] = "openai"
            // no OpenAI:ApiKey
        });
        IChatClient client = ChatClientFactory.CreateFromConfig(config, NullLogger.Instance);
        Assert.IsType<EchoChatClient>(client);
    }

    private sealed class TestLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
```

The OpenAI-success branch test is intentionally omitted from the default suite — it only compiles when `OPENAI_PROVIDER` is defined. See the `#if`-gated optional test at the bottom of this file (Step 3) added by the implementer.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj --filter FullyQualifiedName~ChatClientFactoryTests`
Expected: 4 tests FAIL with `ChatClientFactory` not found.

- [ ] **Step 3: Implement `ChatClientFactory`**

Create `samples/RedNb.Nacos.Sample.AI/Chat/ChatClientFactory.cs`:

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RedNb.Nacos.Sample.AI.Chat;

/// <summary>
/// Resolves an <see cref="IChatClient"/> based on the "ChatProvider" key in
/// configuration. Default provider is <see cref="EchoChatClient"/>. OpenAI and
/// Azure branches are only compiled when the matching symbol is defined, so
/// the default build does not pull in their provider packages.
/// </summary>
public static class ChatClientFactory
{
    public static IChatClient CreateFromConfig(IConfiguration config, ILogger? logger)
    {
        var provider = config["ChatProvider"];
        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(provider, "echo", StringComparison.OrdinalIgnoreCase))
        {
            return new EchoChatClient();
        }

#if OPENAI_PROVIDER
        if (string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = config["OpenAI:ApiKey"];
            var model = config["OpenAI:Model"] ?? "gpt-4o-mini";
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger?.LogWarning(
                    "ChatProvider=openai but OpenAI:ApiKey is missing — falling back to EchoChatClient.");
                return new EchoChatClient();
            }

            var openAi = new OpenAI.OpenAIClient(apiKey);
            return openAi.AsChatClient(model);
        }
#endif

#if AZURE_PROVIDER
        if (string.Equals(provider, "azure", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = config["AzureOpenAI:Endpoint"];
            var apiKey = config["AzureOpenAI:ApiKey"];
            var deployment = config["AzureOpenAI:DeploymentName"];
            if (string.IsNullOrWhiteSpace(endpoint) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(deployment))
            {
                logger?.LogWarning(
                    "ChatProvider=azure but AzureOpenAI:* config is incomplete — falling back to EchoChatClient.");
                return new EchoChatClient();
            }

            var azure = new Azure.AI.Inference.AzureOpenAIClient(
                new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
            return azure.AsChatClient(deployment);
        }
#endif

        logger?.LogWarning(
            "Unknown ChatProvider '{Provider}' — falling back to EchoChatClient.", provider);
        return new EchoChatClient();
    }
}
```

If `OpenAIClient` and `AsChatClient` extension live in `Microsoft.Extensions.AI.OpenAI` and require `Azure.AI.Inference` for the Azure branch, those `using`s must be added inside the `#if` blocks (not at file top) so the default build compiles without those packages. Update the file accordingly.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RedNb.Nacos.Sample.AI.Tests/RedNb.Nacos.Sample.AI.Tests.csproj --filter FullyQualifiedName~ChatClientFactoryTests`
Expected: 4 tests PASS.

- [ ] **Step 5: Verify the default build still has no OpenAI/Azure references**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`. No OpenAI/Azure package restore warnings.

- [ ] **Step 6: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/Chat/ChatClientFactory.cs \
        tests/RedNb.Nacos.Sample.AI.Tests/ChatClientFactoryTests.cs
git commit -m "feat(samples): add ChatClientFactory with unit tests"
```

---

## Task 4: Shared types + Program.cs factory wiring

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/SampleResult.cs`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs`

**Interfaces:**
- Consumes: `RedNb.Nacos.Core` (`NacosClientOptions`), `RedNb.Nacos.Client` (`NacosFactory`), `RedNb.Nacos.Grpc` (`NacosGrpcFactory`), `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Logging`
- Produces:
  - `public enum SampleOutcome { Ok, Skipped, Failed }` and `public sealed record SampleResult(SampleOutcome Outcome, string? Message = null)`
  - `Program.cs` constructs both HTTP and gRPC `IAiService` instances, sets up logging, links cancellation, runs zero sections yet (sections land in Tasks 5–11), prints a placeholder summary line.

- [ ] **Step 1: Create `samples/RedNb.Nacos.Sample.AI/SampleResult.cs`**

```csharp
namespace RedNb.Nacos.Sample.AI;

public enum SampleOutcome
{
    Ok,
    Skipped,
    Failed
}

public sealed record SampleResult(SampleOutcome Outcome, string? Message = null);
```

- [ ] **Step 2: Replace `Program.cs` with the full orchestrator scaffold**

Replace `samples/RedNb.Nacos.Sample.AI/Program.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Grpc;

namespace RedNb.Nacos.Sample.AI;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables(prefix: "REDNB_NACOS_")
            .Build();

        var serverAddresses = config["Nacos:ServerAddresses"] ?? "localhost:8848";
        var grpcPort = int.TryParse(config["Nacos:GrpcPort"], out var p) ? p : 9848;
        var username = config["Nacos:Username"] ?? "nacos";
        var password = config["Nacos:Password"] ?? "nacos";

        var options = new NacosClientOptions
        {
            ServerAddresses = serverAddresses,
            Username = username,
            Password = password,
            Namespace = config["Nacos:Namespace"] ?? string.Empty,
            EnableGrpc = true,
            GrpcPort = grpcPort,
            DefaultTimeout = 5000
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(ParseLogLevel(config["Logging:LogLevel:Default"]));
        });
        var logger = loggerFactory.CreateLogger("RedNb.Nacos.Sample.AI");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        NacosFactory? httpFactory = null;
        NacosGrpcFactory? grpcFactory = null;
        IAiService? httpAi = null;
        IAiService? grpcAi = null;
        var results = new List<(string Section, SampleResult Result)>();

        try
        {
            httpFactory = new NacosFactory(loggerFactory);
            grpcFactory = new NacosGrpcFactory(loggerFactory);

            logger.LogInformation("Connecting to Nacos at {Server} (HTTP) and gRPC port {Port}",
                serverAddresses, grpcPort);
            httpAi = httpFactory.CreateAiService(options);
            grpcAi = grpcFactory.CreateAiService(options);

            logger.LogInformation("Connected. Section runner arrives in Tasks 5-11.");

            // TODO(Tasks 5-11): dispatch McpSamples / A2aSamples / PromptSamples /
            // SkillSamples / AgentSpecSamples / PromptChatIntegrationSample /
            // McpChatIntegrationSample in order, appending each result.

            results.Add(("Scaffold", new SampleResult(SampleOutcome.Ok)));
        }
        catch (NacosException nex)
        {
            logger.LogError(nex,
                "Nacos connection failed (code={Code}): {Message}. " +
                "Is Nacos 3.x running on {Server}? auth ok?",
                nex.ErrorCode, nex.Message, serverAddresses);
            return 2;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Cancelled by user.");
        }
        finally
        {
            if (httpAi is IAsyncDisposable d1) await d1.DisposeAsync();
            if (grpcAi is IAsyncDisposable d2) await d2.DisposeAsync();
        }

        PrintSummary(results, logger);
        return 0;
    }

    private static void PrintSummary(
        List<(string Section, SampleResult Result)> results, ILogger logger)
    {
        logger.LogInformation("--- Summary ---");
        foreach (var (section, result) in results)
        {
            var msg = result.Message is null ? string.Empty : $" — {result.Message}";
            logger.LogInformation("{Section}: {Outcome}{Msg}", section, result.Outcome, msg);
        }
    }

    private static LogLevel ParseLogLevel(string? value) => value?.ToLowerInvariant() switch
    {
        "trace" => LogLevel.Trace,
        "debug" => LogLevel.Debug,
        "information" or "info" => LogLevel.Information,
        "warning" or "warn" => LogLevel.Warning,
        "error" => LogLevel.Error,
        _ => LogLevel.Information
    };
}
```

Note: `IAiService` lives in `RedNb.Nacos.Core.Ai` per the spec. Add the using at the top if needed:
```csharp
using RedNb.Nacos.Core.Ai;
```
The actual namespace depends on the SDK's namespace layout — verify at compile time and adjust.

If `NacosGrpcFactory` exposes `CreateAiService` returning the concrete `NacosGrpcAiService` (not `IAiService`), assign to `IAiService grpcAi = grpcFactory.CreateAiService(options)` and the section file signatures from spec §4.2 still work (assignment to interface is implicit).

- [ ] **Step 3: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`. No new errors. The expected TODO comment is fine.

- [ ] **Step 4: Verify the sample runs and reports a connection error against a missing Nacos**

Run: `dotnet run --project samples/RedNb.Nacos.Sample.AI`
Expected: Logs `Connecting to Nacos at localhost:8848 ...`, then either a `NacosException` (server not running) — which the catch block converts into a clear error message and exits with code 2 — or (if a Nacos 3.x is reachable) prints `Scaffold: Ok` and exits 0. Either outcome is acceptable for this task.

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/SampleResult.cs \
        samples/RedNb.Nacos.Sample.AI/Program.cs
git commit -m "feat(samples): add SampleResult + Program.cs factory wiring"
```

---

## Task 5: Listeners (DemoMcpListener, DemoAgentCardListener)

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/Listeners/DemoMcpListener.cs`
- Create: `samples/RedNb.Nacos.Sample.AI/Listeners/DemoAgentCardListener.cs`

**Interfaces:**
- Consumes: `RedNb.Nacos.Core.Ai.Listener` (`AbstractNacosMcpServerListener`, `NacosMcpServerEvent`, `AbstractNacosAgentCardListener`, `NacosAgentCardEvent`)
- Produces:
  - `public sealed class DemoMcpListener : AbstractNacosMcpServerListener` — overrides `OnEvent`, prints payload fields via `ILogger`
  - `public sealed class DemoAgentCardListener : AbstractNacosAgentCardListener` — same shape

- [ ] **Step 1: Read the event payload shapes**

Run:
- `cat src/RedNb.Nacos/Ai/Listener/NacosMcpServerEvent.cs`
- `cat src/RedNb.Nacos/Ai/Listener/NacosAgentCardEvent.cs`
- `cat src/RedNb.Nacos/Ai/Listener/AbstractNacosMcpServerListener.cs`
- `cat src/RedNb.Nacos/Ai/Listener/AbstractNacosAgentCardListener.cs`

Expected: Each event exposes fields like `McpName` / `Version` / `AgentName`. Use Read tool. If the field names differ, adjust the implementations below.

- [ ] **Step 2: Create `samples/RedNb.Nacos.Sample.AI/Listeners/DemoMcpListener.cs`**

```csharp
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai.Listener;

namespace RedNb.Nacos.Sample.AI.Listeners;

public sealed class DemoMcpListener : AbstractNacosMcpServerListener
{
    private readonly ILogger _logger;

    public DemoMcpListener(ILogger logger) => _logger = logger;

    public override void OnEvent(NacosMcpServerEvent @event)
    {
        _logger.LogInformation(
            "[MCP event] mcpName={McpName} version={Version} type={EventType}",
            @event.McpName, @event.Version, @event.GetType().Name);
    }
}
```

- [ ] **Step 3: Create `samples/RedNb.Nacos.Sample.AI/Listeners/DemoAgentCardListener.cs`**

```csharp
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai.Listener;

namespace RedNb.Nacos.Sample.AI.Listeners;

public sealed class DemoAgentCardListener : AbstractNacosAgentCardListener
{
    private readonly ILogger _logger;

    public DemoAgentCardListener(ILogger logger) => _logger = logger;

    public override void OnEvent(NacosAgentCardEvent @event)
    {
        _logger.LogInformation(
            "[A2A event] agentName={AgentName} version={Version} type={EventType}",
            @event.AgentName, @event.Version, @event.GetType().Name);
    }
}
```

- [ ] **Step 4: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/Listeners/
git commit -m "feat(samples): add demo MCP and AgentCard listeners"
```

---

## Task 6: McpSamples + A2aSamples (gRPC-required operations)

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/McpSamples.cs`
- Create: `samples/RedNb.Nacos.Sample.AI/A2aSamples.cs`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs` — wire the two sections into the runner

**Interfaces:**
- Consumes: `IAiService` (both HTTP and gRPC), `DemoMcpListener`, `DemoAgentCardListener`, `McpServerBasicInfo`, `McpToolSpecification`, `McpEndpointSpec`, `AgentCard`, `AgentEndpoint`, `McpServerDetailInfo`, `AgentCardDetailInfo`, `PageResult<>`
- Produces: two `public static class` types with a single `public static async Task<SampleResult> RunAsync(IAiService httpAi, IAiService grpcAi, ILogger logger, CancellationToken ct)` each.

- [ ] **Step 1: Read the model types to confirm field names**

Use Read tool:
- `src/RedNb.Nacos/Ai/Model/Mcp/McpServerBasicInfo.cs`
- `src/RedNb.Nacos/Ai/Model/Mcp/McpToolSpecification.cs`
- `src/RedNb.Nacos/Ai/Model/Mcp/McpEndpointSpec.cs`
- `src/RedNb.Nacos/Ai/Model/Mcp/McpServerDetailInfo.cs`
- `src/RedNb.Nacos/Ai/Model/A2a/AgentCard.cs`
- `src/RedNb.Nacos/Ai/Model/A2a/AgentEndpoint.cs`
- `src/RedNb.Nacos/Ai/Model/A2a/AgentCardDetailInfo.cs`

Expected: each exposes the fields referenced below. Adjust if names differ.

- [ ] **Step 2: Create `samples/RedNb.Nacos.Sample.AI/McpSamples.cs`**

```csharp
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using RedNb.Nacos.Sample.AI.Listeners;

namespace RedNb.Nacos.Sample.AI;

public static class McpSamples
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi,
        ILogger logger,
        CancellationToken ct)
    {
        var mcpName = $"weather-mcp-{DateTime.UtcNow:yyyyMMddHHmmss}";

        try
        {
            // 1. Release via HTTP (works for reads + releases on Nacos 3.2.4)
            var spec = new McpServerBasicInfo
            {
                Name = mcpName,
                Version = "1.0.0",
                // set other required fields per McpServerBasicInfo definition
            };
            var toolSpec = new McpToolSpecification();
            var endpointSpec = new McpEndpointSpec
            {
                // required fields per the model
            };

            logger.LogInformation("[MCP] releasing {Name}", mcpName);
            var id = await httpAi.ReleaseMcpServerAsync(spec, toolSpec, endpointSpec, ct);
            logger.LogInformation("[MCP] released id={Id}", id);

            // 2. List and get
            var page = await httpAi.ListMcpServersAsync(mcpName: mcpName, pageNo: 1, pageSize: 10, ct);
            logger.LogInformation("[MCP] list count={Count}", page.TotalCount);

            var detail = await httpAi.GetMcpServerAsync(mcpName, ct);
            if (detail is null)
            {
                return new SampleResult(SampleOutcome.Failed, "GetMcpServer returned null after release");
            }

            // 3. Subscribe via HTTP long-poll (works on Nacos 3.2.4)
            var listener = new DemoMcpListener(logger);
            var subscribed = await httpAi.SubscribeMcpServerAsync(mcpName, listener, ct);
            if (subscribed is null)
            {
                return new SampleResult(SampleOutcome.Failed, "SubscribeMcpServer returned null");
            }
            await httpAi.UnsubscribeMcpServerAsync(mcpName, listener, ct);

            // 4. Register an endpoint via gRPC (HTTP throws ServerError on Nacos 3.2.4)
            logger.LogInformation("[MCP] registering endpoint via gRPC");
            await grpcAi.RegisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100, "1.0.0", ct);
            await grpcAi.DeregisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100, ct);

            // 5. Cleanup
            await httpAi.DeleteMcpServerAsync(mcpName, ct);

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (NacosException nex) when (
            nex.ErrorCode == NacosErrorCode.ServerError &&
            nex.Message.Contains("MCP tool", StringComparison.OrdinalIgnoreCase))
        {
            // MCP tool CRUD is not exposed on Nacos 3.2.4 by either channel — skip rather than fail
            logger.LogWarning("[MCP] skipping — tool CRUD not exposed on Nacos 3.2.4");
            return new SampleResult(SampleOutcome.Skipped,
                "MCP tool CRUD not exposed on Nacos 3.2.4 (ServerError)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MCP] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }
}
```

Notes:
- The exact required fields of `McpServerBasicInfo`, `McpToolSpecification`, and `McpEndpointSpec` must be filled in based on what Task 6 Step 1 reveals. The structure shown above is the skeleton.
- `NacosErrorCode.ServerError` constant may live in a different namespace — verify and add the using.
- The `catch` filter uses string-contains check on `nex.Message` for the MCP-tool-CRUD gap, matching the spec §5.3 and §6.

- [ ] **Step 3: Create `samples/RedNb.Nacos.Sample.AI/A2aSamples.cs`**

```csharp
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Ai.Model.A2a;
using RedNb.Nacos.Sample.AI.Listeners;

namespace RedNb.Nacos.Sample.AI;

public static class A2aSamples
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi,
        ILogger logger,
        CancellationToken ct)
    {
        var agentName = $"travel-agent-{DateTime.UtcNow:yyyyMMddHHmmss}";

        try
        {
            // 1. Release Agent Card via HTTP
            var card = new AgentCard
            {
                Name = agentName,
                Version = "1.0.0",
                // set other required fields per AgentCard definition
            };
            logger.LogInformation("[A2A] releasing {Name}", agentName);
            await httpAi.ReleaseAgentCardAsync(card, ct);

            // 2. Batch register endpoints via gRPC (HTTP throws ServerError)
            var endpoints = new[]
            {
                new AgentEndpoint { Address = "127.0.0.1", Port = 9201, Transport = "HTTP+JSON" },
                new AgentEndpoint { Address = "127.0.0.1", Port = 9202, Transport = "JSONRPC" }
            };
            logger.LogInformation("[A2A] batch registering {Count} endpoints via gRPC", endpoints.Length);
            await grpcAi.RegisterAgentEndpointsAsync(agentName, endpoints, ct);

            // 3. List and get
            var page = await httpAi.ListAgentCardsAsync(agentName: agentName, pageNo: 1, pageSize: 10, ct);
            logger.LogInformation("[A2A] list count={Count}", page.TotalCount);

            var detail = await httpAi.GetAgentCardAsync(agentName, ct);
            if (detail is null)
            {
                return new SampleResult(SampleOutcome.Failed, "GetAgentCard returned null after release");
            }

            // 4. Subscribe + unsubscribe
            var listener = new DemoAgentCardListener(logger);
            var subscribed = await httpAi.SubscribeAgentCardAsync(agentName, listener, ct);
            if (subscribed is null)
            {
                return new SampleResult(SampleOutcome.Failed, "SubscribeAgentCard returned null");
            }
            await httpAi.UnsubscribeAgentCardAsync(agentName, listener, ct);

            // 5. Deregister endpoints (gRPC) and delete (HTTP)
            foreach (var ep in endpoints)
            {
                await grpcAi.DeregisterAgentEndpointAsync(agentName, ep, ct);
            }
            await httpAi.DeleteAgentAsync(agentName, ct);

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[A2A] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }
}
```

Notes:
- `AgentEndpoint.Transport` value strings must match what `IA2aService.RegisterAgentEndpointAsync` accepts ("JSONRPC" / "GRPC" / "HTTP+JSON"). Verify and adjust if the property is an enum.
- `AgentCard` likely requires more fields (description, skills, etc.). Fill in the minimum needed to make the call succeed; empty optional lists are acceptable.

- [ ] **Step 4: Wire both sections into `Program.cs`**

In `Program.cs`, replace the `TODO(Tasks 5-11)` comment with:

```csharp
            results.Add(("Mcp", await McpSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
            results.Add(("A2a", await A2aSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
```

(Additional section calls will be appended in Tasks 7–11.)

- [ ] **Step 5: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/McpSamples.cs \
        samples/RedNb.Nacos.Sample.AI/A2aSamples.cs \
        samples/RedNb.Nacos.Sample.AI/Program.cs
git commit -m "feat(samples): add McpSamples and A2aSamples with gRPC endpoint ops"
```

---

## Task 7: PromptSamples (lifecycle)

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/PromptSamples.cs`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs`

**Interfaces:**
- Consumes: `IAiService` (HTTP), `IPromptService` methods (`CreatePromptDraftAsync`, `SubmitPromptReviewAsync`, `PublishPromptAsync`, `OnlinePromptAsync`, `GetPromptAsync`, `ListPromptsAsync`, `DeletePromptAsync`)
- Produces: `public static class PromptSamples` with `RunAsync(...)` exercising the full lifecycle.

- [ ] **Step 1: Read `src/RedNb.Nacos/Ai/IPromptService.cs`**

Use Read tool. Expected: ~25 methods covering the lifecycle listed in spec §2.1.

- [ ] **Step 2: Create `samples/RedNb.Nacos.Sample.AI/PromptSamples.cs`**

```csharp
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;

namespace RedNb.Nacos.Sample.AI;

public static class PromptSamples
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi, // unused here but matches the section signature
        ILogger logger,
        CancellationToken ct)
    {
        var promptKey = $"code-review-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var version = "1.0.0";

        try
        {
            logger.LogInformation("[Prompt] draft {Key}@{Version}", promptKey, version);
            await httpAi.CreatePromptDraftAsync(promptKey, version, "Review {{language}} code");

            await httpAi.SubmitPromptReviewAsync(promptKey, version, ct);
            logger.LogInformation("[Prompt] submitted review");

            await httpAi.PublishPromptAsync(promptKey, version, ct);
            logger.LogInformation("[Prompt] published");

            await httpAi.OnlinePromptAsync(promptKey, version, ct);
            logger.LogInformation("[Prompt] online");

            var prompt = await httpAi.GetPromptAsync(promptKey, version, ct);
            if (prompt is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    "GetPrompt returned null after publish");
            }

            var rendered = prompt.Render(
                new Dictionary<string, string> { ["language"] = "C#" });
            logger.LogInformation("[Prompt] rendered: {Snippet}",
                rendered.Length > 80 ? rendered[..80] + "..." : rendered);

            var page = await httpAi.ListPromptsAsync(promptKey: promptKey, pageNo: 1, pageSize: 10, ct);
            logger.LogInformation("[Prompt] list count={Count}", page.TotalCount);

            await httpAi.OfflinePromptAsync(promptKey, version, ct);
            await httpAi.DeletePromptAsync(promptKey, version, ct);

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Prompt] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }
}
```

Notes:
- The exact signature of `CreatePromptDraftAsync` may differ (e.g., take a `PromptDetail` object instead of three positional args). Verify against the interface read in Step 1 and adjust.
- The prompt lifecycle may require additional steps (e.g., approve review) on a real Nacos 3.2.4. If `SubmitPromptReviewAsync` blocks waiting for a manual approve, the section will time out — adjust to log a `Skipped` rather than fail.

- [ ] **Step 3: Wire the section into `Program.cs`**

In `Program.cs`, after the existing section calls, add:

```csharp
            results.Add(("Prompt", await PromptSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
```

- [ ] **Step 4: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/PromptSamples.cs \
        samples/RedNb.Nacos.Sample.AI/Program.cs
git commit -m "feat(samples): add PromptSamples lifecycle demo"
```

---

## Task 8: SkillSamples

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/SkillSamples.cs`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs`

**Interfaces:**
- Consumes: `IAiService` (HTTP), `ISkillService` methods
- Produces: `public static class SkillSamples` exercising ZIP upload/download + lifecycle.

- [ ] **Step 1: Read `src/RedNb.Nacos/Ai/ISkillService.cs`**

Use Read tool. Expected: ZIP upload, ZIP download (md5/version headers), draft/review/publish/online lifecycle.

- [ ] **Step 2: Build a tiny ZIP artifact for upload**

The SDK expects a real ZIP package. Create `samples/RedNb.Nacos.Sample.AI/SkillSamples.cs` that builds an in-memory ZIP in code:

```csharp
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;

namespace RedNb.Nacos.Sample.AI;

public static class SkillSamples
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi, // unused
        ILogger logger,
        CancellationToken ct)
    {
        var skillName = $"doc-writer-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var version = "1.0.0";

        try
        {
            // Build a minimal ZIP in memory (one SKILL.md file)
            byte[] zipBytes;
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = zip.CreateEntry("SKILL.md");
                    await using var s = entry.Open();
                    var bytes = Encoding.UTF8.GetBytes("# Sample Skill\n\nGenerated for the AI sample.");
                    await s.WriteAsync(bytes, ct);
                }
                zipBytes = ms.ToArray();
            }

            logger.LogInformation("[Skill] uploading {Bytes} bytes for {Name}", zipBytes.Length, skillName);
            await httpAi.UploadSkillZipAsync(zipBytes, $"{skillName}.zip", ct);

            await httpAi.SubmitSkillReviewAsync(skillName, version, ct);
            await httpAi.PublishSkillAsync(skillName, version, ct);
            await httpAi.OnlineSkillAsync(skillName, version, scope: "public", ct);
            logger.LogInformation("[Skill] published + online");

            var downloaded = await httpAi.DownloadSkillZipAsync(skillName, version, ct);
            logger.LogInformation("[Skill] downloaded {Bytes} bytes md5={Md5}",
                downloaded.ZipBytes.Length, downloaded.Md5);

            await httpAi.OfflineSkillAsync(skillName, version, ct);
            await httpAi.DeleteSkillAsync(skillName, version, ct);

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Skill] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }
}
```

Notes:
- `UploadSkillZipAsync`, `DownloadSkillZipAsync`, and the lifecycle method signatures must be verified against `ISkillService` and adjusted. The `SkillZipPackage` return type likely has properties `ZipBytes`, `Md5`, `Version` (or similar).
- `scope` parameter on `OnlineSkillAsync` may be a different name. Verify.

- [ ] **Step 3: Wire the section into `Program.cs`**

Add after the previous section call:

```csharp
            results.Add(("Skill", await SkillSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
```

- [ ] **Step 4: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/SkillSamples.cs \
        samples/RedNb.Nacos.Sample.AI/Program.cs
git commit -m "feat(samples): add SkillSamples ZIP upload + lifecycle demo"
```

---

## Task 9: AgentSpecSamples

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/AgentSpecSamples.cs`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs`

**Interfaces:**
- Consumes: `IAiService` (HTTP), `IAgentSpecService` methods
- Produces: `public static class AgentSpecSamples` exercising multipart upload + lifecycle.

- [ ] **Step 1: Read `src/RedNb.Nacos/Ai/IAgentSpecService.cs`**

Use Read tool. Expected: multipart upload, draft/review/publish/online lifecycle, version + label retrieval.

- [ ] **Step 2: Create `samples/RedNb.Nacos.Sample.AI/AgentSpecSamples.cs`**

```csharp
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;

namespace RedNb.Nacos.Sample.AI;

public static class AgentSpecSamples
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi, // unused
        ILogger logger,
        CancellationToken ct)
    {
        var agentName = $"travel-agent-spec-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var version = "1.0.0";

        try
        {
            // Build a minimal multipart payload (single JSON file)
            var json = "{}"; // placeholder — AgentSpec schema TBD by implementer
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            logger.LogInformation("[AgentSpec] uploading {Bytes} bytes for {Name}", bytes.Length, agentName);
            await httpAi.UploadAgentSpecAsync(bytes, $"{agentName}.json", ct);

            await httpAi.SubmitAgentSpecReviewAsync(agentName, version, ct);
            await httpAi.PublishAgentSpecAsync(agentName, version, ct);
            await httpAi.OnlineAgentSpecAsync(agentName, version, ct);
            logger.LogInformation("[AgentSpec] published + online");

            var spec = await httpAi.GetAgentSpecAsync(agentName, version, ct);
            if (spec is null)
            {
                return new SampleResult(SampleOutcome.Failed,
                    "GetAgentSpec returned null after publish");
            }

            await httpAi.OfflineAgentSpecAsync(agentName, version, ct);
            await httpAi.DeleteAgentSpecAsync(agentName, version, ct);

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AgentSpec] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }
}
```

Notes:
- The actual `AgentSpec` upload payload shape (multipart parts, schema) is implementation-specific. Read `IAgentSpecService.UploadAgentSpecAsync` overloads at Step 1 and adjust — the placeholder JSON `{}` will fail at the server; the implementer must construct a valid AgentSpec payload matching the server's expected schema. This is the highest-risk piece of the section files.
- If `UploadAgentSpecAsync` takes an `AgentSpecDetail` object instead of raw bytes, refactor accordingly.

- [ ] **Step 3: Wire the section into `Program.cs`**

Add after the previous section call:

```csharp
            results.Add(("AgentSpec", await AgentSpecSamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
```

- [ ] **Step 4: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`. Runtime errors against a real server are expected and are the implementer's job to track in the manual verification checklist.

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/AgentSpecSamples.cs \
        samples/RedNb.Nacos.Sample.AI/Program.cs
git commit -m "feat(samples): add AgentSpecSamples lifecycle demo"
```

---

## Task 10: PromptChatIntegrationSample

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/Integration/PromptChatIntegrationSample.cs`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs`

**Interfaces:**
- Consumes: `IAiService` (HTTP), `IChatClient` (EchoChatClient from Task 2), `Microsoft.Extensions.AI` (`ChatMessage`, `ChatRole`)
- Produces: `public static class PromptChatIntegrationSample` showing Nacos Prompt → render → `IChatClient` system message round trip.

- [ ] **Step 1: Create `samples/RedNb.Nacos.Sample.AI/Integration/PromptChatIntegrationSample.cs`**

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Sample.AI.Chat;

namespace RedNb.Nacos.Sample.AI.Integration;

public static class PromptChatIntegrationSample
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi, // unused
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            // Look up a previously published Prompt from PromptSamples (or by a
            // fixed key that the reader can publish via Nacos console).
            var promptKey = "code-review";
            var prompt = await httpAi.GetPromptAsync(promptKey, version: null, ct);
            if (prompt is null)
            {
                return new SampleResult(SampleOutcome.Skipped,
                    $"Prompt '{promptKey}' not found — run PromptSamples first or publish it via Nacos console");
            }

            var sysMsg = prompt.Render(new Dictionary<string, string>
            {
                ["language"] = "C#",
                ["code"] = "var x = 1 + 1;" // tiny snippet
            });
            logger.LogInformation("[PromptChat] rendered system prompt ({Length} chars)", sysMsg.Length);

            IChatClient chat = new EchoChatClient();
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, sysMsg),
                new(ChatRole.User, "Review this code")
            };

            var response = await chat.GetResponseAsync(messages, cancellationToken: ct);
            var assistantText = response.Messages.LastOrDefault()?.Text ?? string.Empty;
            logger.LogInformation("[PromptChat] echo reply: {Reply}",
                assistantText.Length > 120 ? assistantText[..120] + "..." : assistantText);

            if (!assistantText.Contains("[echo]"))
            {
                return new SampleResult(SampleOutcome.Failed,
                    "EchoChatClient did not annotate with [echo]");
            }

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PromptChat] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }
}
```

Notes:
- If the `Prompt` model class returned by `GetPromptAsync` has a different render method (e.g., `RenderTemplate` or extension method `Render` lives elsewhere), adjust the call site. The current interface XML docs do not confirm the exact member; verify against `src/RedNb.Nacos/Ai/Model/Prompt/*`.

- [ ] **Step 2: Wire the section into `Program.cs`**

Add after the previous section call:

```csharp
            results.Add(("PromptChat",
                await Integration.PromptChatIntegrationSample.RunAsync(httpAi, grpcAi, logger, cts.Token)));
```

- [ ] **Step 3: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/Integration/PromptChatIntegrationSample.cs \
        samples/RedNb.Nacos.Sample.AI/Program.cs
git commit -m "feat(samples): add PromptChatIntegrationSample (Nacos prompt + IChatClient)"
```

---

## Task 11: McpChatIntegrationSample (MCP SDK + IChatClient wiring)

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/Integration/McpChatIntegrationSample.cs`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs`

**Interfaces:**
- Consumes: `IAiService` (HTTP), `ModelContextProtocol` (`McpClient`, `McpClientTool`, `HttpClientTransport`), `Microsoft.Extensions.AI` (`AIFunction`, `AIFunctionFactory`, `IChatClient`, `ChatOptions`, `ChatMessage`, `ChatRole`), `EchoChatClient` (Task 2)
- Produces: `public static class McpChatIntegrationSample` demonstrating Flow A (registry → McpClient → tool call) and Flow B (tools wired into `IChatClient`).

- [ ] **Step 1: Inspect the bound ModelContextProtocol SDK surface**

```bash
dotnet list samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj package --include-transitive | grep -i modelcontext
```

Note the resolved `ModelContextProtocol` version. Confirm it provides `McpClient.CreateAsync(...)`, `McpClient.ListToolsAsync()`, `McpClient.CallToolAsync(...)`, `McpClientTool`, and an `AsAIFunction()` (or `AsAITool()`) extension. If `AsAIFunction()` is missing in the bound version, use `AIFunctionFactory.Create(...)` against the tool's parameter JSON schema (extracted from `McpClientTool.JsonSchema` or similar).

If no ModelContextProtocol SDK version is compatible with Nacos 3.2.4's MCP wire format, **stop and surface the incompatibility back to the user** per spec §9 Risks. Do not silently pick a wrong version. Document the failed attempt in `samples/RedNb.Nacos.Sample.AI/README.md` under "Known limitations" and skip Flow A in this task; Flow B still demonstrates the wiring concept with an empty tool list.

- [ ] **Step 2: Create `samples/RedNb.Nacos.Sample.AI/Integration/McpChatIntegrationSample.cs`**

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Sample.AI.Chat;

namespace RedNb.Nacos.Sample.AI.Integration;

public static class McpChatIntegrationSample
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi, // unused
        ILogger logger,
        CancellationToken ct)
    {
        // Discover a previously released MCP server (from McpSamples).
        var mcpName = "weather-mcp";
        var detail = await httpAi.GetMcpServerAsync(mcpName, ct);
        if (detail is null)
        {
            return new SampleResult(SampleOutcome.Skipped,
                $"MCP server '{mcpName}' not found — run McpSamples first");
        }

        // Resolve an endpoint from the detail payload.
        var endpoint = detail.LatestVersion?.EndpointSpec
                       ?? detail.LatestVersion?.ServerEndpoints?.FirstOrDefault();
        if (endpoint is null)
        {
            return new SampleResult(SampleOutcome.Skipped,
                $"MCP server '{mcpName}' has no endpoint registered — run McpSamples first");
        }

        var endpointUri = new Uri($"http://{endpoint.Address}:{endpoint.Port}");

        try
        {
            // --- Flow A: Nacos discovery -> McpClient -> tool call ---
            logger.LogInformation("[McpChat] connecting to {Uri}", endpointUri);
            await using var mcpClient = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpointUri }),
                cancellationToken: ct);

            var tools = await mcpClient.ListToolsAsync(cancellationToken: ct);
            logger.LogInformation("[McpChat] discovered {Count} tools", tools.Count);

            if (tools.Count > 0)
            {
                var firstTool = tools[0];
                // Call with empty arguments — let the server default-handle
                var result = await mcpClient.CallToolAsync(
                    firstTool.Name,
                    arguments: new Dictionary<string, object?>(),
                    cancellationToken: ct);
                logger.LogInformation("[McpChat] tool {Tool} returned: {Snippet}",
                    firstTool.Name,
                    result.Content.Count > 0 ? result.Content[0].ToString() : "(empty)");
            }

            // --- Flow B: tools wired into an IChatClient ---
            var aiFunctions = new List<AIFunction>();
            foreach (var tool in tools)
            {
                // If the SDK exposes AsAIFunction(), use it; otherwise wrap manually.
                if (tool is AIFunction af)
                {
                    aiFunctions.Add(af);
                }
                else
                {
                    aiFunctions.Add(AIFunctionFactory.Create(
                        async (Dictionary<string, object?> args) =>
                        {
                            var r = await mcpClient.CallToolAsync(tool.Name, args, cancellationToken: ct);
                            return r.Content.Count > 0 ? r.Content[0].ToString() : string.Empty;
                        },
                        tool.Name,
                        tool.Description ?? string.Empty));
                }
            }

            IChatClient chat = new EchoChatClient()
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, $"What's available via {mcpName}?")
            };
            var options = new ChatOptions { Tools = aiFunctions };

            var response = await chat.GetResponseAsync(messages, options, ct);
            var reply = response.Messages.LastOrDefault()?.Text ?? string.Empty;
            logger.LogInformation("[McpChat] echo reply: {Reply}",
                reply.Length > 120 ? reply[..120] + "..." : reply);

            if (!reply.Contains($"{aiFunctions.Count} tool"))
            {
                return new SampleResult(SampleOutcome.Failed,
                    "EchoChatClient did not annotate with the wired tool count");
            }

            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[McpChat] failed");
            return new SampleResult(SampleOutcome.Failed, ex.Message);
        }
    }
}
```

Notes:
- The `McpServerDetailInfo.LatestVersion.EndpointSpec` / `ServerEndpoints` shapes are placeholders — read `src/RedNb.Nacos/Ai/Model/Mcp/McpServerDetailInfo.cs` and `src/RedNb.Nacos/Ai/Model/Mcp/McpEndpointSpec.cs` to confirm the actual property names and types, and adjust the endpoint resolution code.
- The ModelContextProtocol SDK class names (`McpClient`, `HttpClientTransport`, `HttpClientTransportOptions`, `CallToolAsync` argument shape, `McpClientTool`) and the `AIFunctionFactory.Create` delegate signature vary between SDK versions. Match the API of the version pinned in Task 1.
- `EchoChatClient.AsBuilder()` is the `IChatClient` extension in `Microsoft.Extensions.AI`. It exists in current 9.x. If unavailable in the pinned version, omit `.UseFunctionInvocation()` and document the omission in the README.

- [ ] **Step 3: Wire the section into `Program.cs`**

Add after the previous section call:

```csharp
            results.Add(("McpChat",
                await Integration.McpChatIntegrationSample.RunAsync(httpAi, grpcAi, logger, cts.Token)));
```

- [ ] **Step 4: Verify the sample builds**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`. If MCP SDK surface differs from the code above, iterate until it builds. Do not change the SDK version silently — if no version works, follow Step 1's fallback path.

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/Integration/McpChatIntegrationSample.cs \
        samples/RedNb.Nacos.Sample.AI/Program.cs
git commit -m "feat(samples): add McpChatIntegrationSample (Nacos + MCP SDK + IChatClient)"
```

---

## Task 12: appsettings.json + sample README

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/appsettings.json`
- Create: `samples/RedNb.Nacos.Sample.AI/README.md`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs` — load `appsettings.json` (already done in Task 4) and surface the resolved `ChatProvider` in the startup log

**Interfaces:**
- Consumes: spec §8.3
- Produces:
  - `appsettings.json` with `Nacos`, `ChatProvider`, `OpenAI`, `AzureOpenAI`, `Logging` sections
  - `README.md` with all six subsections + manual verification checklist

- [ ] **Step 1: Create `samples/RedNb.Nacos.Sample.AI/appsettings.json`**

```json
{
  "Nacos": {
    "ServerAddresses": "localhost:8848",
    "GrpcPort": 9848,
    "Username": "nacos",
    "Password": "nacos",
    "Namespace": ""
  },
  "ChatProvider": "echo",
  "OpenAI": {
    "ApiKey": "",
    "Model": "gpt-4o-mini"
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "ApiKey": "",
    "DeploymentName": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

Add to the sample csproj so the file is copied to output:

In `samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`, inside `<ItemGroup>` (create one if absent), add:

```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 2: Surface the resolved chat provider in `Program.cs`**

In `Program.cs`, immediately after the `logger.LogInformation("Connecting ...")` line, add:

```csharp
        var chatProvider = config["ChatProvider"] ?? "echo";
        logger.LogInformation("ChatProvider: {Provider}", chatProvider);
```

- [ ] **Step 3: Create `samples/RedNb.Nacos.Sample.AI/README.md`**

```markdown
# RedNb.Nacos.Sample.AI

Console sample demonstrating the full `IAiService` surface of the RedNb.Nacos
SDK (MCP, A2A, Prompt, Skill, AgentSpec) plus integration with
`Microsoft.Extensions.AI` and the official `ModelContextProtocol` SDK.

## Prerequisites

- .NET 8 SDK
- A reachable **Nacos server 3.0+** (default `localhost:8848`, gRPC `9848`)
- No LLM API key required — the default `EchoChatClient` ships with the sample

The sample targets Nacos 3.2.4 (see `deploy/docker-compose.yml`).

## Run

```bash
cd samples/RedNb.Nacos.Sample.AI
dotnet run
```

## What it demonstrates

- `McpSamples` — release, list, get, subscribe, then gRPC-only endpoint
  register / deregister, then delete
- `A2aSamples` — release Agent Card, gRPC-only batch endpoint register,
  list, subscribe, deregister, delete
- `PromptSamples` — draft → submit review → publish → online → render →
  list → offline → delete
- `SkillSamples` — ZIP upload → submit review → publish → online →
  download → offline → delete
- `AgentSpecSamples` — upload → submit review → publish → online → get →
  offline → delete
- `Integration/PromptChatIntegrationSample` — discover a Prompt via Nacos,
  render its template, send to `IChatClient` (EchoChatClient) as the
  system message
- `Integration/McpChatIntegrationSample` — discover an MCP server via
  Nacos, connect with `ModelContextProtocol.McpClient`, list tools, call
  one, then wire the tools into an `IChatClient` (EchoChatClient +
  `UseFunctionInvocation` middleware)

## Configuration

Edit `appsettings.json` (or set environment variables prefixed
`REDNB_NACOS_`):

| Key | Default | Notes |
|---|---|---|
| `Nacos:ServerAddresses` | `localhost:8848` | Comma-separated host:port list |
| `Nacos:GrpcPort` | `9848` | gRPC port |
| `Nacos:Username` / `Password` | `nacos` / `nacos` | |
| `ChatProvider` | `echo` | One of `echo`, `openai`, `azure` |
| `OpenAI:ApiKey` | (empty) | Required only when `ChatProvider=openai` |
| `OpenAI:Model` | `gpt-4o-mini` | |
| `AzureOpenAI:Endpoint` / `ApiKey` / `DeploymentName` | (empty) | Required only when `ChatProvider=azure` |

To enable the OpenAI or Azure provider branches, build with
`-p:DefineConstants=OPENAI_PROVIDER` or `-p:DefineConstants=AZURE_PROVIDER`
respectively. The default build is lean and uses only `EchoChatClient`.

## Channel guide

| Operation | Channel |
|---|---|
| Get / List / Release / Upload / Subscribe (MCP, A2A, Prompt, Skill, AgentSpec) | HTTP |
| `RegisterAgentEndpointAsync` / `DeregisterAgentEndpointAsync` / `RegisterAgentEndpointsAsync` | **gRPC only** |
| `RegisterMcpServerEndpointAsync` / `DeregisterMcpServerEndpointAsync` | **gRPC only** |
| MCP tool CRUD (`RefreshMcpToolAsync`, `GetMcpToolAsync`, `DeleteMcpToolAsync`, `UpdateMcpToolAsync`) | Not exposed on Nacos 3.2.4 — sample omits these |

The HTTP `IAiService` throws `NacosException(ServerError)` for the gRPC-only
operations. The sample delegates those calls to the gRPC `IAiService`
instead.

## Troubleshooting

- **"Nacos connection failed"** — verify a Nacos 3.0+ server is running on
  `localhost:8848` (or update `Nacos:ServerAddresses`) and that the gRPC
  port (`9848` by default) is reachable.
- **Integration samples report `Skipped`** — the prerequisite artifact
  (MCP server, Prompt) was not found. Run the corresponding CRUD sample
  first (`McpSamples`, `PromptSamples`), or publish via the Nacos console.
- **`ModelContextProtocol` SDK wire mismatch** — Nacos 3.2.4's MCP wire
  format may not match the SDK's expected protocol version. Pin a
  known-good `ModelContextProtocol` version in the sample csproj, or
  report the mismatch as a follow-up.

## Manual verification checklist

After running the sample against a real Nacos 3.2.4:

```
[ ] McpSamples: release → list → get → subscribe (gRPC endpoint reg) → delete
[ ] A2aSamples: release → batch endpoint register (gRPC) → list → delete
[ ] PromptSamples: draft → submit review → publish → online → list
[ ] SkillSamples: upload zip → download zip → publish → online
[ ] AgentSpecSamples: upload → publish → online
[ ] McpChatIntegrationSample: Nacos discovery → MCP connect → tool call → echo with tool count
[ ] PromptChatIntegrationSample: Nacos prompt → render → echo system message
```

If a row cannot be checked, that's a regression — fix before shipping.
```

- [ ] **Step 4: Verify the sample builds and runs**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj`
Expected: `Build succeeded`.

Run: `dotnet run --project samples/RedNb.Nacos.Sample.AI`
Expected (no Nacos running): clear "Nacos connection failed" error, exits with code 2. The startup log includes `ChatProvider: echo`.

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/appsettings.json \
        samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj \
        samples/RedNb.Nacos.Sample.AI/Program.cs \
        samples/RedNb.Nacos.Sample.AI/README.md
git commit -m "feat(samples): add sample README, appsettings, ChatProvider logging"
```

---

## Task 13: README.md fixes (Chinese)

**Files:**
- Modify: `README.md` — three edit regions per spec §8.1

- [ ] **Step 1: Read the current AI snippet block**

Run: Read `README.md` lines 195–264.

- [ ] **Step 2: Fix the AI snippet signatures**

Replace each snippet per spec §8.1. The corrected block must use signatures matching `IAiService`:

- `aiService.ReleaseMcpServerAsync(serverSpec, toolSpec, endpointSpec)`
- `aiService.RegisterMcpServerEndpointAsync(mcpName, "127.0.0.1", 9100, version)`
- `aiService.RegisterAgentEndpointAsync(agentName, endpoint, transport)` where `transport` is a string ("HTTP+JSON" / "JSONRPC" / "GRPC")
- `aiService.ReleaseAgentCardAsync(agentCard)` — note the additional `(agentCard, registrationType, setAsLatest)` overloads exist
- Keep the Prompt / Skill / AgentSpec lifecycle snippets — they match.

Append a footer line to the snippet block:

```markdown
> 📦 Full sample: see [`samples/RedNb.Nacos.Sample.AI/`](samples/RedNb.Nacos.Sample.AI/).
```

- [ ] **Step 3: Add a "🧪 示例" subsection under the AI service section (after the existing feature tables, around line 575)**

Insert:

```markdown
#### 🧪 AI 示例

| 示例 | 内容 |
|---|---|
| [`RedNb.Nacos.Sample.AI`](samples/RedNb.Nacos.Sample.AI/) | AI 服务全套演示：MCP/A2A/Prompt/Skill/AgentSpec CRUD，gRPC 通道的端点注册/批量注册，Nacos 作为注册中心 + Microsoft.Extensions.AI / ModelContextProtocol 集成的端到端示例 |
```

- [ ] **Step 4: Update the project structure tree (around line 730)**

Insert `│   └── RedNb.Nacos.Sample.AI/      # AI 服务示例` after the WebApi line. Keep alignment with the existing tree.

- [ ] **Step 5: Verify no broken Markdown**

Run: `git diff README.md | head -200`
Expected: only the intended changes. No malformed tables or broken fence blocks.

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs(readme): fix AI quick-start snippets; link to new AI sample"
```

Note: per repo memory (`airegistry-ai-followups`), the user prefers README files to stay uncommitted. **Revert this commit** with `git reset HEAD~1` and leave the changes as uncommitted working-tree edits at the end of the plan if the user so prefers.

---

## Task 14: README.en.md fixes (English)

**Files:**
- Modify: `README.en.md` — mirror Task 13

- [ ] **Step 1: Mirror Task 13 against `README.en.md`**

Apply the same three edits to the English README:
- Fix AI snippet signatures
- Add a "🧪 Samples" subsection under the AI service section
- Update the project structure tree

The English equivalents must match the spec §8.1 wording and use English sample-description text.

- [ ] **Step 2: Verify**

Run: `git diff README.en.md | head -200`
Expected: only the intended changes.

- [ ] **Step 3: Commit (or revert per Task 13 Step 6 note)**

```bash
git add README.en.md
git commit -m "docs(readme.en): fix AI quick-start snippets; link to new AI sample"
```

---

## Task 15: samples/README.md update

**Files:**
- Modify: `samples/README.md`

- [ ] **Step 1: Add the new project to the project table**

Insert a new row into the table near the top:

```markdown
| [RedNb.Nacos.Sample.AI](RedNb.Nacos.Sample.AI/) | Console application demonstrating the full `IAiService` surface plus `Microsoft.Extensions.AI` and `ModelContextProtocol` integration |
```

- [ ] **Step 2: Add an "AI Sample" subsection after the existing Console/WebApi subsections**

Insert:

```markdown
### AI Sample

```bash
cd RedNb.Nacos.Sample.AI
dotnet run
```

This sample demonstrates:
- MCP / A2A / Prompt / Skill / AgentSpec CRUD lifecycles
- gRPC-only endpoint register / batch endpoint register / endpoint deregister
- Nacos-as-registry + `Microsoft.Extensions.AI` `IChatClient` + `ModelContextProtocol` `McpClient` integration
- An `EchoChatClient` for the default zero-API-key path; OpenAI and Azure OpenAI are opt-in via `-p:DefineConstants`

Requires **Nacos 3.0+**. The Docker quick-start below uses Nacos 3.2.4.
```

- [ ] **Step 3: Update the docker run snippet (around line 33)**

Change `nacos/nacos-server:v2.3.0` → `nacos/nacos-server:v3.2.4`.

- [ ] **Step 4: Verify**

Run: `git diff samples/README.md | head -200`
Expected: only the intended changes.

- [ ] **Step 5: Commit (or revert per Task 13 Step 6 note)**

```bash
git add samples/README.md
git commit -m "docs(samples-readme): add AI sample row, subsection, Nacos 3.2.4"
```

---

## Task 16: Deploy bump to Nacos 3.2.4

**Files:**
- Modify: `deploy/docker-compose.yml`
- Modify: `deploy/.env.example`
- Modify: `deploy/start.sh` (GBK-encoded — byte-level `sed`)
- Modify: `deploy/start.bat` (GBK-encoded — byte-level `sed`)
- Modify: `samples/README.md` already handled in Task 15 Step 3.

- [ ] **Step 1: Verify encoding of `deploy/start.sh` and `deploy/start.bat`**

Run:
```bash
file deploy/start.sh deploy/start.bat
```

Expected: `ISO-8859 text` or `Non-ISO extended-ASCII text` (this is GBK being reported as Latin-1 by `file`). If UTF-8, the byte-level sed constraint doesn't apply — use Edit. Adjust subsequent steps accordingly.

- [ ] **Step 2: Update `deploy/docker-compose.yml`**

Use Edit (UTF-8). Change every `nacos/nacos-server:v2.3.0` to `nacos/nacos-server:v3.2.4`. Verify both ports `8848` and `9848` are exposed; add `9849` only if the implementer's local 3.2.4 server actually uses it.

- [ ] **Step 3: Update `deploy/.env.example`**

Use Edit. Change `nacos/nacos-server:v2.3.0` → `nacos/nacos-server:v3.2.4` (and any other version reference).

- [ ] **Step 4: Update `deploy/start.sh` with byte-level `sed`**

If Step 1 reported GBK / non-UTF-8:

```bash
sed -i 's|nacos/nacos-server:v2.3.0|nacos/nacos-server:v3.2.4|g' deploy/start.sh
```

Then verify encoding unchanged:

```bash
file deploy/start.sh
```

If UTF-8, use Edit instead.

- [ ] **Step 5: Update `deploy/start.bat` with byte-level `sed`**

If GBK:

```bash
sed -i 's|nacos/nacos-server:v2.3.0|nacos/nacos-server:v3.2.4|g' deploy/start.bat
file deploy/start.bat
```

If UTF-8, use Edit.

- [ ] **Step 6: Verify the deploy scripts still parse**

Run:
```bash
bash -n deploy/start.sh
```

Expected: no syntax error.

For `.bat`, no portable syntax checker; rely on visual inspection of the diff and `file` encoding confirmation.

- [ ] **Step 7: Commit**

```bash
git add deploy/docker-compose.yml deploy/.env.example deploy/start.sh deploy/start.bat
git commit -m "chore(deploy): bump Nacos image to v3.2.4"
```

---

## Self-Review

Run before handing off for execution.

**1. Spec coverage** — every requirement in the spec has at least one task:

| Spec section | Task |
|---|---|
| §2 Goals 1 (one focused AI sample) | Task 1 |
| §2 Goals 2 (fix READMEs) | Tasks 13, 14 |
| §2 Goals 3 (bump deploy to Nacos 3.x) | Task 16 |
| §2 Goals 4 (don't touch Console/WebApi) | Confirmed in Tasks 6–11 scope notes |
| §2 Goals 5 (zero API keys default) | Tasks 2, 3, 12 |
| §3 Architecture / layout | Task 1 |
| §4.1 Program.cs | Task 4 |
| §4.2 Section files | Tasks 6–9 |
| §4.3 McpChatIntegrationSample | Task 11 |
| §4.4 PromptChatIntegrationSample | Task 10 |
| §4.5 Listeners | Task 5 |
| §4.6 EchoChatClient | Task 2 |
| §4.7 ChatClientFactory | Task 3 |
| §5 Data Flow | Implicit in Tasks 5–11 wiring |
| §6 Error Handling | Task 4 (`NacosException` catch), Tasks 6/7/9 Skipped/Failed paths |
| §7.1 Unit tests | Tasks 2, 3 |
| §7.2 Solution hygiene | Task 1 |
| §7.3 Manual checklist | Task 12 |
| §8.1 README.md fixes | Task 13 |
| §8.1 README.en.md fixes | Task 14 |
| §8.2 samples/README.md | Task 15 |
| §8.3 samples/RedNb.Nacos.Sample.AI/README.md | Task 12 |
| §8.4 deploy bump | Task 16 |
| §8.5 sln | Task 1 |
| §9 Risks (MCP SDK wire mismatch) | Task 11 Step 1 |
| §9 Risks (`RedNb.Nacos.All` placeholder) | Task 1 Step 1 |
| §9 Risks (GBK files) | Task 16 Step 1 |

No gaps.

**2. Placeholder scan** — searched for "TBD", "TODO", "implement later". Found exactly one intentional `TODO(Tasks 5-11)` comment in Task 4's `Program.cs` that is removed by Task 6 Step 4 wiring. No other placeholders.

**3. Type consistency** —
- `SampleResult` defined in Task 4, consumed by Tasks 5–11. Consistent.
- `IAiService httpAi, IAiService grpcAi, ILogger logger, CancellationToken ct` — same signature across all section files (Tasks 5–11) and the two integration samples. Consistent.
- `EchoChatClient` defined in Task 2, consumed by Tasks 3 (factory fallback), 10, 11, 12 (README). Consistent.
- `ChatClientFactory.CreateFromConfig(IConfiguration, ILogger?)` defined in Task 3, consumed by Program.cs in Task 12 Step 2. Consistent.

No type drift.

### Task 17: AgentsAISamples (Microsoft.Agents.AI — MCP + skill + A2A)

> Added mid-execution (2026-09-12) per user instruction: "增加 Microsoft.Agents.AI 支持mcp和skill 以及a2a". Packages already pinned in the sample csproj by Task 4: `Microsoft.Agents.AI` 1.21.0, `Microsoft.Agents.AI.A2A` 1.21.0-preview.260911.1, `ModelContextProtocol` 2.2.0, `Microsoft.Extensions.AI` 10.10.0. Dispatch AFTER Task 11 (needs its MCP-surface knowledge and Task 6's registered endpoints).

**Files:**
- Create: `samples/RedNb.Nacos.Sample.AI/Integration/AgentsAISamples.cs`
- Modify: `samples/RedNb.Nacos.Sample.AI/Program.cs` (one `results.Add` line, after Task 11's McpChat line)

**Interfaces:**
- Consumes: `IAiService` (both channels, for Nacos discovery of MCP/A2A endpoints), `Microsoft.Agents.AI` 1.21.0 (`ChatClientAgent`, `ChatClientAgentOptions`, `AgentResponse`, `AgentSkill`, `AgentSkillsProviderBuilder`), `Microsoft.Agents.AI.A2A` 1.21.0-preview.260911.1 (`A2ACardResolver`, `AgentCard.AsAIAgent()` extension), `ModelContextProtocol.Client` 2.2.0 (`McpClient`, `HttpClientTransport`), `Microsoft.Extensions.AI` 10.10.0 (`AIFunction`, `AsBuilder()`, `UseFunctionInvocation()`), `EchoChatClient` (Task 2), `SampleResult` (Task 4)
- Produces: `public static class AgentsAISamples` with `RunAsync(IAiService httpAi, IAiService grpcAi, ILogger logger, CancellationToken ct)` returning `SampleResult`. One overall result; each of the three sub-demos runs in its own try/catch and logs its own outcome. Any sub-demo whose prerequisite is absent (Nacos down, no MCP server, no A2A endpoint) logs a `Skipped` note and the remaining demos still run.

**Pattern reference (authoritative, user-provided):** the local Agent Framework repo clone at `E:\GitHub\agent-framework\dotnet\samples` (branch main). Key files to consult for API shapes:
- MCP agent: `02-agents\ModelContextProtocol\Agent_MCP_LongRunningTask_Client\Program.cs`
- Inline skills: `02-agents\AgentSkills\` (SEP-2640 `skill://` convention; `AgentSkillsProviderBuilder` core package; `UseSkills(params AgentSkill[])` is in the CORE `Microsoft.Agents.AI` package)
- A2A client: `02-agents\A2A\A2AAgent_Skills\Program.cs` (`A2ACardResolver(new Uri(...))` → `GetAgentCardAsync()` → `agentCard.AsAIAgent()` → `RunAsync(...)`)
- `ChatClientAgent` over an arbitrary `IChatClient` (the offline Echo pattern) — see `01-get-started` agents for `ChatClientAgentOptions`.

NOTE: the samples target net10.0. Our sample is net8.0 — Step 1 verifies the installed packages support net8.0 (they do if the build succeeds; if NOT, stop and surface to the controller — do not retarget).

- [ ] **Step 1: Verify the installed 1.21.0 surface against the reference samples**

Run: `dotnet list samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj package` and confirm the resolved versions. Read the reference sample files listed above and the XML docs of the restored packages under `C:\Users\<user>\.nuget\packages\microsoft.agents.ai\1.21.0\` (`lib\**\Microsoft.Agents.AI.xml`). Confirm the exact names: `ChatClientAgent`, `ChatClientAgentOptions` (does it expose `ChatClient` + `AIContextProviders` + `Name`?), `AgentResponse.Text`, `AgentSkill` (constructor/init shape), `AgentSkillsProviderBuilder.UseSkills`, and in `microsoft.agents.ai.a2a\1.21.0-preview.260911.1`: the `A2ACardResolver` / `AsAIAgent` extension (namespace!). If a name differs from this brief, use the real one and record it in the report.

- [ ] **Step 2: Create `samples/RedNb.Nacos.Sample.AI/Integration/AgentsAISamples.cs`**

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Sample.AI.Chat;

namespace RedNb.Nacos.Sample.AI.Integration;

public static class AgentsAISamples
{
    public static async Task<SampleResult> RunAsync(
        IAiService httpAi,
        IAiService grpcAi,
        ILogger logger,
        CancellationToken ct)
    {
        var failures = new List<string>();

        await TryAsync("MCP", () => RunMcpAgentAsync(httpAi, logger, ct), failures, logger);
        await TryAsync("Skill", () => RunSkillAgentAsync(logger, ct), failures, logger);
        await TryAsync("A2A", () => RunA2aAgentAsync(httpAi, logger, ct), failures, logger);

        return failures.Count == 0
            ? new SampleResult(SampleOutcome.Ok)
            : new SampleResult(SampleOutcome.Failed, string.Join("; ", failures));

        static async Task TryAsync(
            string name, Func<Task<SampleResult>> demo,
            List<string> failures, ILogger logger)
        {
            try
            {
                var r = await demo();
                logger.LogInformation("[AgentsAI/{Name}] {Outcome}: {Message}",
                    name, r.Outcome, r.Message ?? "ok");
                if (r.Outcome == SampleOutcome.Failed) failures.Add($"{name}: {r.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AgentsAI/{Name}] crashed", name);
                failures.Add($"{name}: {ex.Message}");
            }
        }
    }

    // --- Demo 1: MCP tools wired into a ChatClientAgent (Echo, offline) ---
    private static async Task<SampleResult> RunMcpAgentAsync(
        IAiService httpAi, ILogger logger, CancellationToken ct)
    {
        // Ruling 12: Task 6's McpSamples releases `weather-mcp-<timestamp>` —
        // do NOT use a fixed-name get. Reuse Task 11's verified discovery code
        // (prefix list + client-side StartsWith("weather-mcp-") filter).
        var detail = await DiscoverWeatherMcpAsync(httpAi, ct);
        if (detail is null)
            return new SampleResult(SampleOutcome.Skipped,
                "no released 'weather-mcp-*' server found — run McpSamples first");

        // Resolve the endpoint exactly as McpChatIntegrationSample does
        // (see Task 11's verified endpoint-resolution code; reuse its shape).
        var endpoint = detail.LatestVersion?.EndpointSpec
                       ?? detail.LatestVersion?.ServerEndpoints?.FirstOrDefault();
        if (endpoint is null)
            return new SampleResult(SampleOutcome.Skipped,
                "MCP server 'weather-mcp' has no endpoint registered");

        var endpointUri = new Uri($"http://{endpoint.Address}:{endpoint.Port}");
        await using var mcpClient = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpointUri }),
            cancellationToken: ct);

        var tools = await mcpClient.ListToolsAsync(cancellationToken: ct);
        if (tools.Count == 0)
            return new SampleResult(SampleOutcome.Skipped,
                "MCP server exposed no tools");

        // Wire the MCP tools into an IChatClient-backed agent.
        var functions = new List<AIFunction>();
        foreach (var tool in tools)
        {
            // MCP SDK 2.2.0 exposes AsAIFunction()/AsAITool() on McpClientTool —
            // verify in Step 1; fall back to AIFunctionFactory.Create wrapping
            // CallToolAsync, same fallback as Task 11.
            functions.Add(tool is AIFunction af
                ? af
                : AIFunctionFactory.Create(
                    async (Dictionary<string, object?> args) =>
                    {
                        var r = await mcpClient.CallToolAsync(tool.Name, args, cancellationToken: ct);
                        return r.Content.Count > 0 ? r.Content[0].ToString() : string.Empty;
                    },
                    tool.Name,
                    tool.Description ?? string.Empty));
        }

        IChatClient chat = new EchoChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        AIAgent agent = new ChatClientAgent(new ChatClientAgentOptions
        {
            Name = "NacosMcpAgent",
            ChatClient = chat,
        });

        AgentResponse response = await agent.RunAsync(
            $"What tools are available via the MCP server '{tools.Count}' tools?",
            cancellationToken: ct);

        logger.LogInformation("[AgentsAI/MCP] agent reply: {Text}",
            response.Text.Length > 120 ? response.Text[..120] + "..." : response.Text);
        return new SampleResult(SampleOutcome.Ok);
    }

    // --- Demo 2: inline skill via AgentSkillsProviderBuilder (offline) ---
    private static async Task<SampleResult> RunSkillAgentAsync(ILogger logger, CancellationToken ct)
    {
        // An inline SEP-2640-style skill. The provider feeds it to capable
        // models as context; with the Echo backend the wiring is demonstrated
        // and the skill count is logged.
        var skill = new AgentSkill
        {
            Name = "unit-converter",
            Description = "Convert between common units using a multiplication factor.",
            Instructions = "miles→kilometers ×1.60934; pounds→kilograms ×0.453592."
        };

        var skillsProvider = new AgentSkillsProviderBuilder()
            .UseSkills(skill)
            .Build();

        var agent = new ChatClientAgent(new ChatClientAgentOptions
        {
            Name = "SkillAgent",
            ChatClient = new EchoChatClient(),
            AIContextProviders = [skillsProvider],
        });

        AgentResponse response = await agent.RunAsync(
            "How many kilometers is a marathon?",
            cancellationToken: ct);

        logger.LogInformation("[AgentsAI/Skill] agent reply: {Text}", response.Text);
        return new SampleResult(SampleOutcome.Ok);
    }

    // --- Demo 3: A2A client against a Nacos-registered agent card ---
    private static async Task<SampleResult> RunA2aAgentAsync(
        IAiService httpAi, ILogger logger, CancellationToken ct)
    {
        // Task 6's A2aSamples registers an agent card whose endpoint URL we
        // resolve here. Adjust the lookup to Task 6's verified IA2aService
        // surface (see its report): if the SDK exposes GetAgentCardAsync or
        // an AgentEndpoint model, use it; otherwise read A2A:AgentCardUrl
        // from config via the logger factory's provider if available.
        // If no endpoint can be resolved, return Skipped (an A2A server must
        // be running for this demo — hosting needs the extra
        // Microsoft.Agents.AI.Hosting.A2A packages, out of scope; the sample
        // README documents this).
        var cardUri = ResolveAgentCardUri(httpAi, ct);
        if (cardUri is null)
            return new SampleResult(SampleOutcome.Skipped,
                "No A2A agent card endpoint — run A2aSamples / start an A2A server");

        try
        {
            var card = await new A2ACardResolver(cardUri).GetAgentCardAsync();
            AIAgent remote = card.AsAIAgent();
            AgentResponse response = await remote.RunAsync(
                "Hello from the RedNb.Nacos AI sample!",
                cancellationToken: ct);
            logger.LogInformation("[AgentsAI/A2A] remote agent reply: {Text}", response.Text);
            return new SampleResult(SampleOutcome.Ok);
        }
        catch (Exception ex)
        {
            return new SampleResult(SampleOutcome.Skipped,
                $"A2A call failed (is an A2A server running?): {ex.Message}");
        }
    }

    private static Uri? ResolveAgentCardUri(IAiService httpAi, CancellationToken ct)
    {
        // Best-effort: Task 6 registered endpoints via the gRPC channel;
        // if the IA2aService surface exposes a query, use it here. Otherwise
        // return null (Skipped) — do NOT fabricate an endpoint.
        return null;
    }
}
```

Notes:
- `AgentSkill` / `ChatClientAgentOptions.AIContextProviders` / `A2ACardResolver` constructor shapes are verified in Step 1 against the installed 1.21.0 packages; adjust property/ctor names to the real surface (the reference samples show the exact usage).
- `ResolveAgentCardUri` is a stub by design: wire it to the REAL `IA2aService` query surface verified in Task 6 (check Task 6's report for the verified method/property names). If Task 6's report shows no way to fetch a registered card's URL, keep the stub and the demo returns Skipped — do not invent API calls.
- The MCP demo's endpoint resolution must mirror Task 11's VERIFIED code (same `McpServerDetailInfo` shapes). If Task 11 ended with Flow A skipped (SDK mismatch), mirror that decision here (Skipped + README note) — do not silently pick another wire format.

- [ ] **Step 3: Wire the section into `Program.cs`**

Add after Task 11's `McpChat` line:

```csharp
            results.Add(("AgentsAI",
                await Integration.AgentsAISamples.RunAsync(httpAi, grpcAi, logger, cts.Token)));
```

- [ ] **Step 4: Verify the sample builds and runs offline**

Run: `dotnet build samples/RedNb.Nacos.Sample.AI/RedNb.Nacos.Sample.AI.csproj` → `Build succeeded` (iterate on API names until green — the installed packages are authoritative).
Run: `dotnet run --project samples/RedNb.Nacos.Sample.AI` → with no Nacos server the earlier sections fail gracefully; the `AgentsAI/Skill` demo must still run and print its reply (offline Echo). The MCP/A2A demos print Skipped notes. Exit code 0.
Also run the 12 existing tests via `dotnet "C:\Program Files\dotnet\sdk\10.0.401\vstest.console.dll" tests\RedNb.Nacos.Sample.AI.Tests\bin\Debug\net8.0\RedNb.Nacos.Sample.AI.Tests.dll` → 12/12 (no new tests in this task; regression check only).

- [ ] **Step 5: Commit**

```bash
git add samples/RedNb.Nacos.Sample.AI/Integration/AgentsAISamples.cs \
        samples/RedNb.Nacos.Sample.AI/Program.cs
git commit -m "feat(samples): add AgentsAISamples (Microsoft.Agents.AI MCP + skill + A2A)"
```

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-12-ai-samples.md`. Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?