using System.Text.Json;
using FluentAssertions;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests;

/// <summary>
/// Tests for HTTP-based Config Service using WireMock.
/// </summary>
public class ConfigServiceHttpTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly NacosClientOptions _options;
    private readonly NacosFactory _factory;

    public ConfigServiceHttpTests()
    {
        _server = WireMockServer.Start();
        _options = new NacosClientOptions
        {
            ServerAddresses = $"localhost:{_server.Port}",
            Username = "nacos",
            Password = "nacos"
        };
        _factory = new NacosFactory();

        // Setup login endpoint
        SetupLoginEndpoint();
    }

    private void SetupLoginEndpoint()
    {
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/auth/user/login")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"accessToken\":\"test-token\",\"tokenTtl\":18000}"));
    }

    [Fact]
    public async Task GetConfigAsync_Success_ShouldReturnContent()
    {
        // Arrange
        var expectedContent = "key=value\nname=test";
        var envelope = JsonSerializer.Serialize(new
        {
            code = 0,
            message = "success",
            data = new
            {
                content = expectedContent,
                md5 = "d41d8cd98f00b204e9800998ecf8427e",
                contentType = "text"
            }
        });

        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/cs/config")
                .WithParam("dataId", "test-config")
                .WithParam("groupName", "DEFAULT_GROUP")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(envelope));

        var configService = _factory.CreateConfigService(_options);

        // Act
        var result = await configService.GetConfigAsync("test-config", "DEFAULT_GROUP", 5000);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task GetConfigAsync_NotFound_ShouldReturnNull()
    {
        // Arrange
        // Nacos 3.x reports a missing config as HTTP 200 with code 20004.
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/cs/config")
                .WithParam("dataId", "non-existent")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"code\":20004,\"message\":\"config data not exist\",\"data\":null}"));

        var configService = _factory.CreateConfigService(_options);

        // Act
        var result = await configService.GetConfigAsync("non-existent", "DEFAULT_GROUP", 5000);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConfigAsync_ErrorCodeOtherThanNotFound_ShouldThrow()
    {
        // Arrange
        // A non-zero, non-20004 code is a server-side failure, not a missing config.
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/cs/config")
                .WithParam("dataId", "denied-config")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"code\":10001,\"message\":\"access denied\",\"data\":null}"));

        var configService = _factory.CreateConfigService(_options);

        // Act
        var action = async () => await configService.GetConfigAsync("denied-config", "DEFAULT_GROUP", 5000);

        // Assert
        await action.Should().ThrowAsync<NacosException>()
            .WithMessage("*10001*");
    }

#pragma warning disable CS0618 // CAS publish over HTTP is obsolete by design
    [Fact]
    public async Task PublishConfigCasAsync_WithCasMd5_ShouldThrowNotSupported()
    {
        // Arrange
        // The v3 admin endpoint ignores casMd5 (a stale overwrite would otherwise
        // look successful), so the HTTP service must refuse the call outright.
        var configService = _factory.CreateConfigService(_options);

        // Act
        var action = async () => await configService.PublishConfigCasAsync(
            "test-config", "DEFAULT_GROUP", "new content", "deadbeefdeadbeefdeadbeefdeadbeef");

        // Assert
        await action.Should().ThrowAsync<NotSupportedException>();
        _server.LogEntries.Should().BeEmpty("CAS publish must fail before any request is sent");
    }
#pragma warning restore CS0618

    [Fact]
    public async Task PublishConfigAsync_Success_ShouldReturnTrue()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/admin/cs/config")
                .UsingPost()
                .WithBody(body => body != null
                    && body.Contains("dataId=test-config")
                    && body.Contains("groupName=DEFAULT_GROUP")
                    && body.Contains($"content={Uri.EscapeDataString("new content")}")
                    && body.Contains("type=text")))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"code\":0,\"message\":\"success\",\"data\":true}"));

        var configService = _factory.CreateConfigService(_options);

        // Act
        var result = await configService.PublishConfigAsync("test-config", "DEFAULT_GROUP", "new content", "text");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveConfigAsync_Success_ShouldReturnTrue()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/admin/cs/config")
                .WithParam("dataId", "test-config")
                .WithParam("groupName", "DEFAULT_GROUP")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"code\":0,\"message\":\"success\",\"data\":true}"));

        var configService = _factory.CreateConfigService(_options);

        // Act
        var result = await configService.RemoveConfigAsync("test-config", "DEFAULT_GROUP");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetServerStatus_WhenHealthy_ShouldReturnUp()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v1/console/health/readiness")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("UP"));

        var configService = _factory.CreateConfigService(_options);

        // Act
        var status = configService.GetServerStatus();

        // Assert
        status.Should().Be("UP");
    }

    public void Dispose()
    {
        _server?.Stop();
        _server?.Dispose();
    }
}
