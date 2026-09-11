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
