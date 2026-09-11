using FluentAssertions;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Naming;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests;

/// <summary>
/// Tests for HTTP-based Naming Service using WireMock.
/// </summary>
public class NamingServiceHttpTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly NacosClientOptions _options;
    private readonly NacosFactory _factory;

    public NamingServiceHttpTests()
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

    /// <summary>
    /// Builds a v3 instance-list response envelope whose <c>data</c> is the flat
    /// instance array returned by <c>/v3/client/ns/instance/list</c>.
    /// </summary>
    private static string BuildInstanceListEnvelope(params object[] instances)
    {
        return JsonSerializer.Serialize(new
        {
            code = 0,
            message = "success",
            data = instances
        });
    }

    [Fact]
    public async Task RegisterInstanceAsync_Success_ShouldNotThrow()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ns/instance")
                .WithParam("serviceName", "test-service")
                .WithParam("groupName", "DEFAULT_GROUP")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"code\":0,\"message\":\"success\",\"data\":\"ok\"}"));

        var namingService = _factory.CreateNamingService(_options);
        var instance = new Instance
        {
            Ip = "192.168.1.100",
            Port = 8080,
            Weight = 1.0,
            Healthy = true
        };

        // Act
        var action = async () => await namingService.RegisterInstanceAsync("test-service", instance);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterInstanceAsync_ErrorEnvelope_ShouldThrow()
    {
        // Arrange
        // Nacos v3 reports a rejected registration in the envelope with HTTP 200;
        // reporting success here would start a heartbeat for an unregistered instance.
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ns/instance")
                .WithParam("serviceName", "test-service")
                .WithParam("groupName", "DEFAULT_GROUP")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"code\":10000,\"message\":\"parameter missing\"," +
                    "\"data\":\"Required parameter 'serviceName' is not present\"}"));

        var namingService = _factory.CreateNamingService(_options);
        var instance = new Instance
        {
            Ip = "192.168.1.100",
            Port = 8080,
            Weight = 1.0,
            Healthy = true
        };

        // Act
        var action = async () => await namingService.RegisterInstanceAsync("test-service", instance);

        // Assert
        await action.Should().ThrowAsync<NacosException>()
            .WithMessage("*10000*");
    }

    [Fact]
    public async Task DeregisterInstanceAsync_Success_ShouldNotThrow()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ns/instance")
                .WithParam("serviceName", "test-service")
                .WithParam("groupName", "DEFAULT_GROUP")
                .WithParam("ip", "192.168.1.100")
                .WithParam("port", "8080")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"code\":0,\"message\":\"success\",\"data\":\"ok\"}"));

        var namingService = _factory.CreateNamingService(_options);

        // Act
        var action = async () => await namingService.DeregisterInstanceAsync(
            "test-service", "192.168.1.100", 8080);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAllInstancesAsync_Success_ShouldReturnInstances()
    {
        // Arrange
        var envelope = BuildInstanceListEnvelope(
            new
            {
                ip = "192.168.1.100",
                port = 8080,
                weight = 1.0,
                healthy = true,
                enabled = true,
                ephemeral = true,
                clusterName = "DEFAULT",
                serviceName = "DEFAULT_GROUP@@test-service"
            },
            new
            {
                ip = "192.168.1.101",
                port = 8080,
                weight = 1.0,
                healthy = true,
                enabled = true,
                ephemeral = true,
                clusterName = "DEFAULT",
                serviceName = "DEFAULT_GROUP@@test-service"
            });

        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ns/instance/list")
                .WithParam("serviceName", "test-service")
                .WithParam("groupName", "DEFAULT_GROUP")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(envelope));

        var namingService = _factory.CreateNamingService(_options);

        // Act
        var instances = await namingService.GetAllInstancesAsync("test-service");

        // Assert
        instances.Should().HaveCount(2);
        instances.Should().Contain(i => i.Ip == "192.168.1.100");
        instances.Should().Contain(i => i.Ip == "192.168.1.101");
    }

    [Fact]
    public async Task GetAllInstancesAsync_EmptyService_ShouldReturnEmptyList()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ns/instance/list")
                .WithParam("serviceName", "empty-service")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(BuildInstanceListEnvelope()));

        var namingService = _factory.CreateNamingService(_options);

        // Act
        var instances = await namingService.GetAllInstancesAsync("empty-service");

        // Assert
        instances.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllInstancesAsync_RefusedEnvelope_ShouldThrow()
    {
        // Arrange
        // Nacos v3 reports a refused instance-list query (e.g. access denied) in the
        // envelope with HTTP 200. Returning an empty list here would be read as
        // "the service has no instances" and silently drop all traffic.
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ns/instance/list")
                .WithParam("serviceName", "forbidden-service")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"code\":403,\"message\":\"unknown user!\",\"data\":null}"));

        var namingService = _factory.CreateNamingService(_options);

        // Act
        var action = async () => await namingService.GetAllInstancesAsync(
            "forbidden-service", subscribe: false);

        // Assert
        await action.Should().ThrowAsync<NacosException>()
            .WithMessage("*403*");
    }

    [Fact]
    public async Task SelectInstancesAsync_HealthyOnly_ShouldFilterUnhealthy()
    {
        // Arrange
        var envelope = BuildInstanceListEnvelope(
            new
            {
                ip = "192.168.1.100",
                port = 8080,
                weight = 1.0,
                healthy = true,
                enabled = true,
                ephemeral = true,
                clusterName = "DEFAULT",
                serviceName = "DEFAULT_GROUP@@test-service"
            },
            new
            {
                ip = "192.168.1.101",
                port = 8080,
                weight = 1.0,
                healthy = false,
                enabled = true,
                ephemeral = true,
                clusterName = "DEFAULT",
                serviceName = "DEFAULT_GROUP@@test-service"
            });

        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/client/ns/instance/list")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(envelope));

        var namingService = _factory.CreateNamingService(_options);

        // Act
        var instances = await namingService.SelectInstancesAsync("test-service", healthy: true);

        // Assert
        instances.Should().HaveCount(1);
        instances[0].Ip.Should().Be("192.168.1.100");
        instances[0].Healthy.Should().BeTrue();
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

        var namingService = _factory.CreateNamingService(_options);

        // Act
        var status = namingService.GetServerStatus();

        // Assert
        status.Should().Be("UP");
    }

    public void Dispose()
    {
        _server?.Stop();
        _server?.Dispose();
    }
}
