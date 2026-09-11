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
