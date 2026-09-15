using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedNb.Nacos.Ai.Models.A2a;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Ai.Models.Prompt;
using RedNb.Nacos.Config;
using RedNb.Nacos.Config.FuzzyWatch;
using RedNb.Nacos.Failover;
using RedNb.Nacos.Grpc;
using RedNb.Nacos.Grpc.Ai;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Lock;
using RedNb.Nacos.Http.Administration;
using RedNb.Nacos.Lock;
using RedNb.Nacos.Naming.FuzzyWatch;
using Microsoft.Extensions.Logging.Abstractions;

namespace RedNb.Nacos.Sample.Aot;

internal static class LiveChecks
{
    internal static NacosClientOptions Options(string? ns = null) => new()
    {
        ServerAddresses = Environment.GetEnvironmentVariable("NACOS_TEST_SERVER") ?? "localhost:8848",
        ConsoleAddresses = Environment.GetEnvironmentVariable("NACOS_TEST_CONSOLE") ?? "localhost:8080",
        Username = Environment.GetEnvironmentVariable("NACOS_TEST_USERNAME") ?? "nacos",
        Password = Environment.GetEnvironmentVariable("NACOS_TEST_PASSWORD") ?? "nacos",
        Namespace = ns ?? "", DefaultTimeout = 10000,
        JsonTypeInfoResolver = LiveContext.Default
    };

    internal static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static async Task Until(Func<Task<bool>> condition, string message, int seconds = 45)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds); Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try { if (await condition()) return; } catch (Exception ex) { last = ex; }
            await Task.Delay(250);
        }
        throw new TimeoutException(message, last);
    }

    public static async Task RunAsync(bool recovery)
    {
        var id = "aot-" + Guid.NewGuid().ToString("N")[..16];
        await using var admin = new NacosAdministrationService(Options());
        await admin.CreateNamespaceAsync(id, "NativeAOT validation");
        try
        {
            var options = Options(id);
            await using var writer = await NacosGrpcFactory.CreateConfigServiceAsync(options);
            await using var reader = await NacosGrpcFactory.CreateConfigServiceAsync(options);
            await writer.PublishConfigAsync(id, "DEFAULT_GROUP", "before");
            await Until(async () => await reader.GetConfigAsync(id, "DEFAULT_GROUP", 10000) == "before", "Initial config read");
            var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var deleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var listener = new Listener(info => { if (info.Content == "after") changed.TrySetResult(true); if (info.Content == null) deleted.TrySetResult(true); });
            await reader.AddListenerAsync(id, "DEFAULT_GROUP", listener);
            var md5 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes("before"))).ToLowerInvariant();
            Require(await writer.PublishConfigCasAsync(id, "DEFAULT_GROUP", "after", md5), "Valid CAS rejected");
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Require(!await writer.PublishConfigCasAsync(id, "DEFAULT_GROUP", "bad", md5), "Stale CAS succeeded");
            await writer.RemoveConfigAsync(id, "DEFAULT_GROUP");
            await deleted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            reader.RemoveListener(id, "DEFAULT_GROUP", listener);
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                try { await reader.GetConfigAsync(id, "DEFAULT_GROUP", 10000, cancelled.Token); throw new Exception("Cancellation ignored"); }
                catch (OperationCanceledException) { }
            }
            await writer.PublishConfigAsync(id, "DEFAULT_GROUP", "custom-query");
            await Until(async () => await reader.GetConfigAsync(id, "DEFAULT_GROUP", 10000) == "custom-query", "Config propagation");
            await using (var raw = new NacosGrpcClient(options))
            {
                var response = await raw.RequestAsync<ConfigQueryResponse>("ConfigQueryRequest", new CustomQuery { DataId = id, Group = "DEFAULT_GROUP", Tenant = id });
                Require(response?.Content == "custom-query", "User resolver did not reach the gRPC request path");
                var customResponse = await raw.RequestAsync<CustomQueryResponse>("ConfigQueryRequest", new CustomQuery { DataId = id, Group = "DEFAULT_GROUP", Tenant = id });
                Require(customResponse?.Content == "custom-query", "User response metadata was not used");
                try
                {
                    await raw.RequestAsync<ConfigQueryResponse>("ConfigQueryRequest", new UnregisteredQuery { DataId = id, Group = "DEFAULT_GROUP", Tenant = id });
                    Require(JsonSerializer.IsReflectionEnabledByDefault, "Strict runtime accepted unknown user type");
                }
                catch (NotSupportedException) when (!JsonSerializer.IsReflectionEnabledByDefault) { }
            }
            var configWatcher = new ConfigFuzzyWatchEventWatcher(_ => { });
            var configKeys = await reader.FuzzyWatchWithGroupKeysAsync(id + "*", "DEFAULT_GROUP", configWatcher);
            Require(configKeys.Any(key => key.Contains(id)), "Fuzzy config initial sync/ACK missing");
            await reader.CancelFuzzyWatchAsync(id + "*", "DEFAULT_GROUP", configWatcher);

            await using var naming = await NacosGrpcFactory.CreateNamingServiceAsync(options);
            await naming.RegisterInstanceAsync(id, "127.0.0.1", 19501);
            await Until(async () => (await naming.GetAllInstancesAsync(id, false)).Any(i => i.Port == 19501), "Naming read");
            var namingWatcher = new NamingFuzzyWatchEventWatcher(_ => { });
            var keys = await naming.FuzzyWatchWithGroupKeysAsync(id + "*", "DEFAULT_GROUP", namingWatcher);
            Require(keys.Any(key => key.Contains(id)), "Fuzzy naming initial sync/ACK missing");
            await naming.CancelFuzzyWatchAsync(id + "*", "DEFAULT_GROUP", namingWatcher);

            await using (var one = new NacosGrpcLockService(options))
            await using (var two = new NacosGrpcLockService(options))
            {
                var first = new LockInstance { Key = id, ExpireTime = 30000, Params = new() { ["application"] = new ExtensionValue { Value = 23 } } };
                var second = new LockInstance { Key = id, ExpireTime = 30000 };
                Require(await one.LockAsync(first), "Lock acquire failed");
                try { Require(!await two.LockAsync(second), "Lock mutual exclusion failed"); }
                finally { await one.UnlockAsync(first); }
                Require(await two.LockAsync(second), "Lock release failed");
                await two.UnlockAsync(second);
            }
            await CheckRegistry(id);
            CheckCache(id);
            if (recovery)
            {
                await using var ai = new NacosAiClient(Options("public"));
                var mcp = id + "-mcp"; var backend = id + "-backend";
                await ai.ReleaseMcpServerAsync(new McpServerBasicInfo
                {
                    Name = mcp, Protocol = "mcp-sse",
                    VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
                    RemoteServerConfig = new McpServerRemoteServiceConfig
                    {
                        ServiceRef = new McpServiceRef { NamespaceId = "public", GroupName = "DEFAULT_GROUP", ServiceName = backend }
                    }
                }, null, new McpEndpointSpec
                {
                    Type = "REF", Data = new() { ["namespaceId"] = "public", ["groupName"] = "DEFAULT_GROUP", ["serviceName"] = backend }
                });
                try
                {
                await ai.RegisterMcpServerEndpointAsync(mcp, "127.0.0.1", 19503, "1.0.0");
                var signal = Environment.GetEnvironmentVariable("NACOS_RECOVERY_SIGNAL") ?? throw new Exception("Recovery signal path required");
                var pushed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var recoveryListener = new Listener(info => { if (info.Content == "recovered") pushed.TrySetResult(true); });
                await reader.AddListenerAsync(id, "DEFAULT_GROUP", recoveryListener);
                await File.WriteAllTextAsync(signal + ".ready", "ready");
                await Until(() => Task.FromResult(File.Exists(signal + ".restarted")), "Runner did not restart the local server", 180);
                await using var observer = await NacosGrpcFactory.CreateNamingServiceAsync(options);
                await Until(async () => (await observer.GetAllInstancesAsync(id, false)).Any(i => i.Port == 19501), "Idle naming registration did not recover", 90);
                await using var aiObserver = await NacosGrpcFactory.CreateNamingServiceAsync(Options("public"));
                await Until(async () => (await aiObserver.GetAllInstancesAsync(backend, false)).Any(i => i.Port == 19503), "Idle MCP backend did not recover", 90);
                await using var publisher = await NacosGrpcFactory.CreateConfigServiceAsync(options);
                await publisher.PublishConfigAsync(id, "DEFAULT_GROUP", "recovered");
                await pushed.Task.WaitAsync(TimeSpan.FromSeconds(60));
                reader.RemoveListener(id, "DEFAULT_GROUP", recoveryListener);
                Console.WriteLine("PASS idle config/naming/MCP recovery");
                }
                finally
                {
                    try { await ai.DeregisterMcpServerEndpointAsync(mcp, "127.0.0.1", 19503); }
                    finally { await ai.DeleteMcpServerAsync(mcp); }
                }
            }
            await naming.DeregisterInstanceAsync(id, "127.0.0.1", 19501);
            await writer.RemoveConfigAsync(id, "DEFAULT_GROUP");
            Console.WriteLine("PASS config/CAS/delete/cancel/custom-context/naming/fuzzy/lock");
        }
        finally { await admin.DeleteNamespaceAsync(id); }
    }

    public static void CheckValues()
    {
        var yaml = new RedNb.Nacos.Config.Parser.YamlChangeParser();
        foreach (var explicitMapping in new[] { "value: explicit, <<: [*a, *b]", "<<: [*a, *b], value: explicit" })
        {
            var changes = yaml.Parse(null, "a: &a {value: first, extra: one}\nb: &b {value: second, extra: two}\nservice: {" + explicitMapping + "}\nquoted: {\"<<\": literal, nullValue: null, quotedNull: \"null\"}\n", "yaml");
            Require(changes["service.value"].NewValue == "explicit" && changes["service.extra"].NewValue == "one", "YAML merge precedence");
            Require(changes["quoted.<<"].NewValue == "literal" && changes["quoted.nullValue"].NewValue == "" && changes["quoted.quotedNull"].NewValue == "null", "YAML quoted/null handling");
        }
        Require(yaml.Parse(null, "loop: &loop {self: *loop}", "yaml").Count == 0, "YAML alias cycle returned partial data");
        IDictionary<string, object?> dynamicMap = new System.Dynamic.ExpandoObject();
        dynamicMap["count"] = 3;
        foreach (var (value, expected) in new (object Value, string Expected)[]
        {
            (ulong.MaxValue, "18446744073709551615"), (1e-30, "1E-30"),
            (double.MaxValue, "1.7976931348623157E+308"),
            (new int[] { 1, 2 }, "[1,2]"), (new byte[] { 1, 2 }, "\"AQI=\""),
            (dynamicMap, "{\"count\":3}"),
            (new Dictionary<string, object> { ["items"] = new[] { 1, 2 } }, "{\"items\":[1,2]}"),
            (new PayloadList { 1, 2 }, "{\"count\":2}")
        })
        {
            var json = JsonSerializer.Serialize(new ValueEnvelope { Value = value }, LiveContext.Default.ValueEnvelope);
            using var doc = JsonDocument.Parse(json);
            Require(doc.RootElement.GetProperty("payload").GetRawText() == expected, "Value changed during AOT serialization: " + json);
        }
        var cycle = new Dictionary<string, object>(); cycle["self"] = cycle;
        try { JsonSerializer.Serialize(new ValueEnvelope { Value = cycle }, LiveContext.Default.ValueEnvelope); throw new Exception("Cycle was not rejected"); }
        catch (JsonException) { }
    }

    private static void CheckCache(string id)
    {
        var directory = Path.Combine(Path.GetTempPath(), id);
        var store = new LocalDiskFailoverDataSource<ExtensionValue>(NullLogger.Instance, directory, "switch", LiveContext.Default.ExtensionValue);
        try { store.SaveFailoverData("user", new ExtensionValue { Value = 17 }); Require(store.GetFailoverData()["user"].Data.Value == 17, "Custom cache metadata"); }
        finally { store.DeleteFailoverData("user"); Directory.Delete(directory); }
        var sdkDirectory = Path.Combine(Path.GetTempPath(), id + "-sdk-cache");
        var sdkStore = new DiskNamingFailoverDataSource(NullLogger.Instance, sdkDirectory);
        try
        {
            sdkStore.SaveServiceInfo(new RedNb.Nacos.Naming.ServiceInfo { Name = id });
            sdkStore.SetSwitch(true);
            Require(sdkStore.GetSwitch().Enabled, "SDK failover switch");
            Require(sdkStore.GetFailoverData().Values.Any(entry => entry.Data.Name == id), "SDK failover cache read/write");
        }
        finally { Directory.Delete(sdkDirectory, recursive: true); }
    }

    private static async Task CheckRegistry(string id)
    {
        await using var ai = new NacosAiClient(Options("public"));
        var prompt = id + "-p"; var skill = id + "-s"; var spec = id + "-a"; var agent = id + "-agent";
        await ai.CreatePromptDraftAsync(prompt, "1.0.0", "Hello {{name}}", variables: [new PromptVariable { Name = "name", DefaultValue = "world" }]);
        try
        {
            await ai.SubmitPromptReviewAsync(prompt, "1.0.0"); await ai.ForcePublishPromptAsync(prompt, "1.0.0");
            await Until(async () => await ai.GetPromptAsync(prompt) != null, "Prompt read");
            Require((await ai.GetPromptAsync(prompt))!.Render(new Dictionary<string, string> { ["name"] = "AOT" }) == "Hello AOT", "Prompt render");
        }
        finally { try { await ai.OfflinePromptAsync(prompt, "1.0.0"); } finally { await ai.DeletePromptAsync(prompt); } }
        await ai.UploadSkillZipAsync(Zip(new() { ["SKILL.md"] = "---\nname: " + skill + "\ndescription: AOT check\nversion: 1.0.0\n---\n# Skill\n" }), skill + ".zip", targetVersion: "1.0.0");
        try
        {
            await ai.SubmitSkillReviewAsync(skill, "1.0.0"); await ai.ForcePublishSkillAsync(skill, "1.0.0");
            await Until(async () => (await ai.DownloadSkillZipByVersionAsync(skill, "1.0.0"))?.ZipContent.Length > 0, "Skill download");
        }
        finally { try { await ai.OfflineSkillAsync(skill, "1.0.0"); } finally { await ai.DeleteSkillAsync(skill); } }
        var manifest = "{\"worker\":{\"suggested_name\":\"" + spec + "\"},\"description\":\"AOT test\"}";
        await ai.UploadAgentSpecAsync(Zip(new() { ["manifest.json"] = manifest, ["AGENTS.md"] = "# AOT instructions\n" }), spec + ".zip");
        try
        {
            await ai.SubmitAgentSpecReviewAsync(spec, "0.0.1"); await ai.ForcePublishAgentSpecAsync(spec, "0.0.1");
            await Until(async () => (await ai.GetAgentSpecAsync(spec))?.Resource?.Values.Any(r => r.Name == "AGENTS.md") == true, "AgentSpec resources");
        }
        finally { try { await ai.OfflineAgentSpecAsync(spec, "0.0.1"); } finally { await ai.DeleteAgentSpecAsync(spec); } }
        await ai.ReleaseAgentCardAsync(new AgentCard
        {
            Name = agent, Version = "1.0.0", ProtocolVersion = "0.3.7", PreferredTransport = "jsonrpc", Url = "http://127.0.0.1:19502",
            Capabilities = new AgentCapabilities { Extensions = [new AgentExtension { Uri = "urn:aot-test", Params = new() { ["application"] = new ExtensionValue { Value = 42 } } }] }
        });
        try
        {
            await Until(async () => await ai.GetAgentCardAsync(agent) != null, "A2A read");
            var value = (JsonElement)(await ai.GetAgentCardAsync(agent))!.Capabilities!.Extensions![0].Params!["application"];
            Require(value.GetProperty("application_value").GetInt32() == 42, "HTTP user Context was bypassed");
            var otherOptions = Options("public");
            otherOptions.JsonTypeInfoResolver = new AlternateResolver();
            await using var other = new NacosAiClient(otherOptions);
            var otherAgent = agent + "-two";
            await other.ReleaseAgentCardAsync(new AgentCard
            {
                Name = otherAgent, Version = "1.0.0", ProtocolVersion = "0.3.7", PreferredTransport = "jsonrpc", Url = "http://127.0.0.1:19504",
                Capabilities = new AgentCapabilities { Extensions = [new AgentExtension { Uri = "urn:aot-test", Params = new() { ["application"] = new ExtensionValue { Value = 99 } } }] }
            });
            try
            {
                await Until(async () => await other.GetAgentCardAsync(otherAgent) != null, "Second client Context read");
                var alternate = (JsonElement)(await other.GetAgentCardAsync(otherAgent))!.Capabilities!.Extensions![0].Params!["application"];
                Require(alternate.GetProperty("client_two").GetInt32() == 99, "Second client Context was ignored");
                await ai.ReleaseAgentCardAsync(new AgentCard
                {
                    Name = agent, Version = "1.0.0", ProtocolVersion = "0.3.7", PreferredTransport = "jsonrpc", Url = "http://127.0.0.1:19502",
                    Capabilities = new AgentCapabilities { Extensions = [new AgentExtension { Uri = "urn:aot-test", Params = new() { ["application"] = new ExtensionValue { Value = 43 } } }] }
                });
                value = (JsonElement)(await ai.GetAgentCardAsync(agent))!.Capabilities!.Extensions![0].Params!["application"];
                Require(value.TryGetProperty("application_value", out var original) && original.GetInt32() == 43, "Client Contexts polluted each other");
            }
            finally { await other.DeleteAgentAsync(otherAgent); }
            await ai.RegisterAgentEndpointAsync(agent, "1.0.0", "127.0.0.1", 19502);
            await ai.DeregisterAgentEndpointAsync(agent, "1.0.0", "127.0.0.1", 19502);
        }
        finally { await ai.DeleteAgentAsync(agent); }
        Console.WriteLine("PASS AI HTTP/gRPC and extension payload");
    }

    private static byte[] Zip(Dictionary<string, string> files)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (var file in files) { using var writer = new StreamWriter(zip.CreateEntry(file.Key).Open(), new UTF8Encoding(false)); writer.Write(file.Value); }
        return buffer.ToArray();
    }
    private sealed class Listener(Action<ConfigInfo> action) : IConfigChangeListener { public void OnReceiveConfigInfo(ConfigInfo info) => action(info); }
}

internal sealed class CustomQuery
{
    public string? DataId { get; set; }
    public string? Group { get; set; }
    public string? Tenant { get; set; }
}
internal sealed class UnregisteredQuery
{
    public string? DataId { get; set; }
    public string? Group { get; set; }
    public string? Tenant { get; set; }
}
internal sealed class CustomQueryResponse { public int ResultCode { get; set; } public string? Content { get; set; } }
internal sealed class ExtensionValue { [JsonPropertyName("application_value")] public int Value { get; set; } }
[JsonSerializable(typeof(CustomQuery))]
[JsonSerializable(typeof(CustomQueryResponse))]
[JsonSerializable(typeof(ExtensionValue))]
[JsonSerializable(typeof(ValueEnvelope))]
[JsonSerializable(typeof(PayloadList))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class LiveContext : JsonSerializerContext { }
internal sealed class ValueEnvelope
{
    [JsonPropertyName("payload")]
    [JsonConverter(typeof(RedNb.Nacos.Serialization.ObjectValueConverter))]
    public object? Value { get; set; }
}

[JsonConverter(typeof(PayloadListConverter))]
internal sealed class PayloadList : List<int> { }
internal sealed class PayloadListConverter : JsonConverter<PayloadList>
{
    public override PayloadList? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
    public override void Write(Utf8JsonWriter writer, PayloadList value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WriteNumber("count", value.Count); writer.WriteEndObject();
    }
}

internal sealed class AlternateResolver : System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver
{
    public System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var info = ((System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver)LiveContext.Default).GetTypeInfo(type, options);
        if (type == typeof(ExtensionValue) && info != null) info.Properties[0].Name = "client_two";
        return info;
    }
}
