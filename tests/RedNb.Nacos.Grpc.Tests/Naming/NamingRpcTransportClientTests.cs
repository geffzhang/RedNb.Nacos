using System.Text.Json;
using RedNb.Nacos.Core;
using RedNb.Nacos.GrpcClient;
using RedNb.Nacos.GrpcClient.Naming;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Naming;

/// <summary>
/// Tests for <see cref="NamingRpcTransportClient"/>.
///
/// Uses a hand-rolled <see cref="FakeNacosGrpcClient"/> that overrides
/// the virtual seam methods (<see cref="NacosGrpcClient.RequestAsync{TResponse}"/>,
/// <see cref="NacosGrpcClient.SendStreamRequestAsync"/>, and
/// <see cref="NacosGrpcClient.RegisterPushHandler"/>) to capture dispatched
/// frames and the registered push handler, so the query / subscribe /
/// unsubscribe / fuzzy-watch / service-change-push paths can be exercised
/// without a live gRPC server.
/// </summary>
public class NamingRpcTransportClientTests
{
    [Fact]
    public async Task QueryServiceAsync_BuildsRequestWithNamespaceServiceGroup()
    {
        // Arrange: a fake gRPC client that captures the dispatched request and
        // returns a hand-built response, so we can assert on the outgoing frame
        // without needing a live server.
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new ServiceQueryResponse
            {
                ServiceInfo = new NamingServiceInfo
                {
                    Name = "svc",
                    GroupName = "G",
                    Hosts = new List<NamingInstance>()
                }
            }
        };

        var client = new NamingRpcTransportClient(fake, options);

        // Act
        var result = await client.QueryServiceAsync(
            serviceName: "svc",
            groupName: "G",
            ns: "default",
            clusters: null,
            healthyOnly: false,
            cancellationToken: default);

        // Assert: exactly one frame was dispatched with the expected type,
        // and the request DTO carried the serviceName/group/namespace we passed in.
        Assert.Single(fake.Captured);
        var captured = fake.Captured[0];

        Assert.Equal(ServiceQueryRequest.TYPE, captured.type);

        var payload = Assert.IsType<ServiceQueryRequest>(captured.request);
        Assert.Equal("svc", payload.ServiceName);
        Assert.Equal("G", payload.GroupName);
        Assert.Equal("default", payload.Namespace);

        // The fake response is echoed back through the transport client.
        Assert.NotNull(result);
        Assert.Equal("svc", result!.Name);
    }

    [Fact]
    public async Task SubscribeServiceAsync_SendsSubscribeServiceRequestWithSubscribeTrue()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new SubscribeServiceResponse
            {
                Success = true,
                ServiceInfo = new NamingServiceInfo
                {
                    Name = "svc",
                    GroupName = "G",
                    Hosts = new List<NamingInstance>()
                }
            }
        };

        var client = new NamingRpcTransportClient(fake, options);

        // Act
        await client.SubscribeServiceAsync(
            serviceName: "svc",
            groupName: "G",
            ns: "default",
            clusters: null,
            cancellationToken: default);

        // Assert: exactly one unary frame was dispatched with the expected type,
        // and the request DTO carried Subscribe = true.
        Assert.Single(fake.Captured);
        var captured = fake.Captured[0];

        Assert.Equal(SubscribeServiceRequest.TYPE, captured.type);

        var payload = Assert.IsType<SubscribeServiceRequest>(captured.request);
        Assert.True(payload.Subscribe);
        Assert.Equal("svc", payload.ServiceName);
        Assert.Equal("G", payload.GroupName);
        Assert.Equal("default", payload.Namespace);
    }

    [Fact]
    public async Task UnsubscribeServiceAsync_SendsSubscribeServiceRequestWithSubscribeFalse()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new SubscribeServiceResponse { Success = true }
        };

        var client = new NamingRpcTransportClient(fake, options);

        // Act
        var ok = await client.UnsubscribeServiceAsync(
            serviceName: "svc",
            groupName: "G",
            ns: "default",
            clusters: null,
            cancellationToken: default);

        // Assert: unsubscribe dispatches the same SubscribeServiceRequest TYPE
        // but with Subscribe = false, and the response success flag is echoed.
        Assert.Single(fake.Captured);
        var captured = fake.Captured[0];

        Assert.Equal(SubscribeServiceRequest.TYPE, captured.type);

        var payload = Assert.IsType<SubscribeServiceRequest>(captured.request);
        Assert.False(payload.Subscribe);
        Assert.Equal("svc", payload.ServiceName);
        Assert.Equal("G", payload.GroupName);

        Assert.True(ok);
    }

    [Fact]
    public async Task FuzzyWatchAsync_SendsNamingFuzzyWatchRequest()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new NamingFuzzyWatchResponse()
        };

        var client = new NamingRpcTransportClient(fake, options);

        // Act: call FuzzyWatchAsync with default "*" / "*" patterns to confirm
        // those defaults make it onto the outgoing NamingFuzzyWatchRequest.
        await client.FuzzyWatchAsync(
            ns: "default",
            serviceNamePattern: "*",
            groupNamePattern: "*",
            receivedGroupKeys: null,
            initializing: true,
            cancellationToken: default);

        // Assert: exactly one frame was dispatched through the unary seam.
        Assert.Single(fake.Captured);
        var captured = fake.Captured[0];

        Assert.Equal(NamingFuzzyWatchRequest.TYPE, captured.type);

        var payload = Assert.IsType<NamingFuzzyWatchRequest>(captured.request);
        Assert.Equal("*", payload.ServiceNamePattern);
        Assert.Equal("*", payload.GroupNamePattern);
        Assert.Equal("default", payload.Namespace);
        Assert.True(payload.Initializing);
    }

    [Fact]
    public async Task SendFuzzyWatchAsync_SendsNamingFuzzyWatchRequestViaStream()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);
        var client = new NamingRpcTransportClient(fake, options);

        // Act: fire-and-forget stream variant should land in StreamCalls,
        // not in Captured (the unary seam).
        await client.SendFuzzyWatchAsync(
            ns: "default",
            serviceNamePattern: "svc.*",
            groupNamePattern: "G.*",
            receivedGroupKeys: null,
            initializing: false,
            cancellationToken: default);

        // Assert
        Assert.Single(fake.StreamCalls);
        Assert.Empty(fake.Captured);

        var captured = fake.StreamCalls[0];
        Assert.Equal(NamingFuzzyWatchRequest.TYPE, captured.type);

        var payload = Assert.IsType<NamingFuzzyWatchRequest>(captured.request);
        Assert.Equal("svc.*", payload.ServiceNamePattern);
        Assert.Equal("G.*", payload.GroupNamePattern);
        Assert.False(payload.Initializing);
    }

    [Fact]
    public void NamingRpcTransportClient_RegistersPushHandlerInConstructor()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);

        // Act: constructing the transport client should register the push handler.
        var client = new NamingRpcTransportClient(fake, options);

        // Assert: the constructor called RegisterPushHandler("naming", HandlePushMessage).
        Assert.True(fake.PushHandlers.ContainsKey("naming"));

        // Subscribe to OnServiceChanged and simulate a server push through the
        // captured handler. The full HandlePushMessage -> HandleNotifySubscriber
        // -> OnServiceChanged chain should fire.
        var fired = false;
        client.OnServiceChanged += _ => fired = true;

        var notify = new NotifySubscriberRequest
        {
            ServiceName = "svc",
            GroupName = "G",
            ServiceInfo = new NamingServiceInfo
            {
                Name = "svc",
                GroupName = "G",
                Hosts = new List<NamingInstance>()
            }
        };
        var json = JsonSerializer.Serialize(notify);

        fake.SimulatePush("naming", NotifySubscriberRequest.TYPE, json);

        Assert.True(fired);
    }
}
