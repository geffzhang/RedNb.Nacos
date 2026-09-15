using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using RedNb.Nacos.AspNetCore.Configuration;
using RedNb.Nacos.AspNetCore.HealthChecks;
using RedNb.Nacos.Config;
using RedNb.Nacos.DependencyInjection;
using RedNb.Nacos.Grpc;
using RedNb.Nacos.Sample.Aot;

Console.WriteLine($"MODE dynamic={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported} reflection={JsonSerializer.IsReflectionEnabledByDefault}");
var name = "aot-web-" + Guid.NewGuid().ToString("N")[..16];
var options = LiveChecks.Options();
await using var writer = await NacosGrpcFactory.CreateConfigServiceAsync(options);
try
{
    await writer.PublishConfigAsync(name, "DEFAULT_GROUP", "{\"ProbeValue\":\"one\",\"OldKey\":\"old\"}", "json");
    await LiveChecks.Until(async () => (await writer.GetConfigAsync(name, "DEFAULT_GROUP", 10000))?.Contains("one") == true, "Seed web configuration");
    var builder = WebApplication.CreateSlimBuilder(args);
    builder.Configuration.AddNacosConfiguration(source =>
    {
        source.Options = options; source.ReloadOnChange = true;
        source.ConfigItems.Add(new NacosConfigurationItem { DataId = name, ConfigType = "json" });
    });
    void Configure(RedNb.Nacos.NacosClientOptions target)
    {
        target.ServerAddresses = options.ServerAddresses; target.ConsoleAddresses = options.ConsoleAddresses;
        target.Username = options.Username; target.Password = options.Password;
        target.Namespace = options.Namespace; target.JsonTypeInfoResolver = options.JsonTypeInfoResolver;
    }
    builder.Services.AddNacos(Configure);
    builder.Services.AddNacos("first", Configure);
    builder.Services.AddNacos("second", Configure);
    builder.Services.AddHealthChecks().AddNacos();
    builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.TypeInfoResolverChain.Insert(0, WebContext.Default));
    await using var app = builder.Build();
    app.Urls.Add("http://127.0.0.1:0");
    app.MapGet("/probe", (IConfiguration config) => TypedResults.Ok(new ProbeValue { Value = config["ProbeValue"], Old = config["OldKey"] }));
    app.MapHealthChecks("/health");
    await app.StartAsync();
    using var http = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    var configService = app.Services.GetRequiredService<IConfigService>();
    await configService.GetConfigAsync(name, "DEFAULT_GROUP", 10000);
    LiveChecks.Require((await http.GetAsync("/health")).IsSuccessStatusCode, "Native health check failed");
    using (var response = JsonDocument.Parse(await http.GetStringAsync("/probe")))
        LiveChecks.Require(response.RootElement.GetProperty("value").GetString() == "one", "Initial native HTTP response");
    await writer.PublishConfigAsync(name, "DEFAULT_GROUP", "{\"ProbeValue\":\"two\"}", "json");
    await LiveChecks.Until(async () =>
    {
        using var json = JsonDocument.Parse(await http.GetStringAsync("/probe"));
        return json.RootElement.GetProperty("value").GetString() == "two" && json.RootElement.GetProperty("old").ValueKind == JsonValueKind.Null;
    }, "Native configuration did not reload/remove keys");
    var first = app.Services.GetRequiredKeyedService<IConfigService>("first");
    var second = app.Services.GetRequiredKeyedService<IConfigService>("second");
    LiveChecks.Require(!ReferenceEquals(first, second), "Named clients shared state");
    await first.DisposeAsync();
    LiveChecks.Require(await second.GetConfigAsync(name, "DEFAULT_GROUP", 10000) != null, "Disposing first client broke the second");
    await writer.RemoveConfigAsync(name, "DEFAULT_GROUP");
    await LiveChecks.Until(async () =>
    {
        using var json = JsonDocument.Parse(await http.GetStringAsync("/probe"));
        return json.RootElement.GetProperty("value").ValueKind == JsonValueKind.Null;
    }, "Native deletion did not reload");
    await app.StopAsync();
    await AckProbeServer.RunAsync();
    Console.WriteLine("PASS native-web DI/named-clients/config-reload/delete/health/stop");
}
finally { await writer.RemoveConfigAsync(name, "DEFAULT_GROUP"); }

internal sealed class ProbeValue
{
    public string? Value { get; set; }
    public string? Old { get; set; }
}
[JsonSerializable(typeof(ProbeValue))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class WebContext : JsonSerializerContext { }
