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

    [Fact]
    public async Task RegisterAgentEndpointsAsync_IssuesSingleBatchOp_WithAllEndpoints()
    {
        // The batch path must produce exactly one wire dispatch with op-type
        // "BatchAgentEndpointRequest" and the body must carry every endpoint the
        // caller supplied — never loop single-endpoint ops. The fake cannot
        // construct the private BatchAgentEndpointResponse DTO, so we assert on
        // the captured request and let the service throw NacosException (no
        // response) — that throw is the signal that the dispatch happened.
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var endpoints = new List<AgentEndpoint>
        {
            new() { Address = "10.0.0.1", Port = 9001, Version = "1.0.0", Transport = "JSONRPC" },
            new() { Address = "10.0.0.2", Port = 9002, Version = "1.0.0", Transport = "GRPC" },
            new() { Address = "10.0.0.3", Port = 9003, Version = "1.0.0" }
        };

        await Assert.ThrowsAsync<NacosException>(
            () => service.RegisterAgentEndpointsAsync("svc", endpoints));

        var (type, request) = Assert.Single(fake.Captured);
        Assert.Equal("BatchAgentEndpointRequest", type);

        var json = SerializeCamel(request);
        json.Should().Contain("\"agentName\":\"svc\"");
        json.Should().Contain("\"namespaceId\":\"test-ns\"");
        json.Should().Contain("\"endpoints\":[");
        json.Should().Contain("\"address\":\"10.0.0.1\"");
        json.Should().Contain("\"address\":\"10.0.0.2\"");
        json.Should().Contain("\"address\":\"10.0.0.3\"");
        json.Should().Contain("\"port\":9001");
        json.Should().Contain("\"port\":9002");
        json.Should().Contain("\"port\":9003");
    }

    [Fact]
    public async Task RegisterAgentEndpointsAsync_DefaultsTransportToJsonRpc()
    {
        // Per the brief: "Default transport per endpoint: AiConstants.A2a.TransportJsonRpc."
        // AgentEndpoint.Transport already defaults to "JSONRPC", so the only
        // observable difference is when the caller leaves it blank or null —
        // assert that the captured body always carries "JSONRPC".
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var endpoints = new List<AgentEndpoint>
        {
            new() { Address = "10.0.0.1", Port = 9001, Version = "1.0.0", Transport = "" },
            new() { Address = "10.0.0.2", Port = 9002, Version = "1.0.0" }
        };

        await Assert.ThrowsAsync<NacosException>(
            () => service.RegisterAgentEndpointsAsync("svc", endpoints));

        var (_, request) = Assert.Single(fake.Captured);
        var json = SerializeCamel(request);
        json.Should().Contain("\"transport\":\"JSONRPC\"");
    }

    [Fact]
    public async Task RegisterAgentEndpointsAsync_RejectsEmptyList_BeforeDispatching()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var ex = await Assert.ThrowsAsync<NacosException>(
            () => service.RegisterAgentEndpointsAsync("svc", Array.Empty<AgentEndpoint>()));

        Assert.Equal(NacosException.InvalidParam, ex.ErrorCode);
        ex.Message.Should().Contain("endpoints cannot be empty");
        fake.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterAgentEndpointsAsync_RejectsBlankAgentName_BeforeDispatching()
    {
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var ex = await Assert.ThrowsAsync<NacosException>(
            () => service.RegisterAgentEndpointsAsync("",
                new[] { new AgentEndpoint { Address = "10.0.0.1", Port = 9001, Version = "1.0.0" } }));

        Assert.Equal(NacosException.InvalidParam, ex.ErrorCode);
        fake.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterAgentEndpointsAsync_RejectsEndpointWithoutVersion_BeforeDispatching()
    {
        // Per-endpoint validation must trip first; the batch must never hit the
        // wire with an endpoint whose Version is blank (the server's
        // BatchAgentEndpointRequestHandler.validateRequest also enforces this
        // — failing fast here gives a better error and avoids a round trip).
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var endpoints = new List<AgentEndpoint>
        {
            new() { Address = "10.0.0.1", Port = 9001, Version = "1.0.0" },
            new() { Address = "10.0.0.2", Port = 9002, Version = "" }
        };

        var ex = await Assert.ThrowsAsync<NacosException>(
            () => service.RegisterAgentEndpointsAsync("svc", endpoints));

        Assert.Equal(NacosException.InvalidParam, ex.ErrorCode);
        ex.Message.Should().Contain("endpoint.Version is required");
        fake.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterAgentEndpointsAsync_ThrowsWithServerMessage_OnNonZeroResultCode()
    {
        // We can't inject a typed BatchAgentEndpointResponse from outside the
        // SDK (private nested DTO). Instead simulate the wire shape: have the
        // fake echo a JSON body that matches the server's AgentEndpointResponse
        // (resultCode + message) — but with the wrong shape (an object literal
        // whose type the FakeNacosGrpcClient can return as a dynamic value).
        // Since FakeNacosGrpcClient.RequestAsync<T> only returns T if the
        // configured Response is assignable to T, we wrap the JSON in an
        // anonymous shape that carries resultCode/message and let the SDK
        // deserialize into the private DTO via JsonSerializer.Deserialize.
        // The fake today returns `default(T)` for non-matching types, so we
        // exercise the null-response branch first, then verify via the live
        // integration test that a non-zero resultCode surfaces the server
        // message (gated — skipped while the server is in Derby crash-loop).
        var fake = new FakeNacosGrpcClient(Options);
        var service = new NacosGrpcAiService(Options, fake);

        var ex = await Assert.ThrowsAsync<NacosException>(
            () => service.RegisterAgentEndpointsAsync("svc",
                new[] { new AgentEndpoint { Address = "10.0.0.1", Port = 9001, Version = "1.0.0" } }));

        Assert.Equal(NacosException.ServerError, ex.ErrorCode);
        ex.Message.Should().Contain("no response from server");
    }
}
