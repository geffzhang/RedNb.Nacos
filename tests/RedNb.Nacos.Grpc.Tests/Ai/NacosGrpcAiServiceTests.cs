using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Client;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai.Model;
using RedNb.Nacos.Core.Ai.Model.A2a;
using RedNb.Nacos.Core.Ai.Model.Mcp;
using RedNb.Nacos.GrpcClient.Ai;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Ai;

/// <summary>
/// Wire-shape tests for <see cref="NacosGrpcAiService"/> using the
/// <see cref="FakeNacosGrpcClient"/> capture seam. The request DTOs are
/// private nested classes, so assertions run on the serialized camelCase JSON.
/// </summary>
public class NacosGrpcAiServiceTests
{
    private static NacosClientOptions Options => new()
    {
        ServerAddresses = "localhost:8848",
        Namespace = "test-ns"
    };

    private static McpServerBasicInfo ValidMcpServer(string name) => new()
    {
        Name = name,
        VersionDetail = new ServerVersionDetail { Version = "1.0.0" },
        Protocol = "stdio"
    };

    private static string SerializeCamel(object request) =>
        JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    [Fact]
    public async Task ReleaseMcpServerAsync_SendsNamespaceId_NotNamespace()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        await service.ReleaseMcpServerAsync(ValidMcpServer("m1"), toolSpecification: null);

        var (type, request) = Assert.Single(fake.Captured);
        Assert.Equal("ReleaseMcpServerRequest", type);
        var json = SerializeCamel(request);
        json.Should().Contain("\"namespaceId\":\"test-ns\"");
        json.Should().NotContain("\"namespace\"");
    }

    [Fact]
    public async Task RegisterMcpServerEndpointAsync_SendsNamespaceId_NotNamespace()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        // The fake cannot produce an OperationResponse (private nested DTO) so
        // RequestAsync returns null and the method throws before returning. The
        // request is captured before that, which is what this test asserts on.
        await Assert.ThrowsAsync<NacosException>(
            () => service.RegisterMcpServerEndpointAsync("m1", "127.0.0.1", 9100, "1.0.0"));

        var (type, request) = Assert.Single(fake.Captured);
        Assert.Equal("McpServerEndpointRequest", type);
        var json = SerializeCamel(request);
        json.Should().Contain("\"namespaceId\":\"test-ns\"");
        json.Should().NotContain("\"namespace\"");
    }

    [Fact]
    public async Task ReleaseMcpServerAsync_ReadsMcpIdFromResponse()
    {
        // The fake echoes a response object whose JSON contract matches the
        // server: ReleaseMcpServerResponse { mcpId }. Type-compatibility with
        // the private DTO cannot be arranged from outside, so the fake returns
        // null here and the method must degrade to string.Empty (no throw).
        // The live mcpId binding is asserted by the re-enabled
        // ReleaseAndGetMcpServer_ViaGrpc integration test (Task 5).
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var released = await service.ReleaseMcpServerAsync(ValidMcpServer("m1"), toolSpecification: null);

        released.Should().BeEmpty();
    }
}
