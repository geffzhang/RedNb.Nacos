using System.Text.Json;
using RedNb.Nacos;
using RedNb.Nacos.Grpc;
using RedNb.Nacos.Grpc.Config;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Config;

/// <summary>
/// Tests for <see cref="ConfigRpcTransportClient"/>.
///
/// Uses a hand-rolled <see cref="FakeNacosGrpcClient"/> that overrides
/// the virtual seam methods (<see cref="RequestAsync{TResponse}"/>,
/// <see cref="SendRequestAsync"/>, <see cref="SendRequestWithResponseAsync{TResponse}"/>,
/// and <see cref="RegisterPushHandler"/>) to capture dispatched frames
/// and the registered push handler, so the listen / fuzzy-watch / push-dispatch
/// paths can be exercised without a live gRPC server.
/// </summary>
public class ConfigRpcTransportClientTests
{
    [Fact]
    public async Task QueryConfigAsync_BuildsRequestWithDataIdGroupTenant()
    {
        // Arrange: a fake gRPC client that captures the dispatched request and
        // returns a hand-built response, so we can assert on the outgoing frame
        // without needing a live server.
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new ConfigQueryResponse { Content = "bar" }
        };

        var client = new ConfigRpcTransportClient(fake, options);

        // Act
        var result = await client.QueryConfigAsync(
            dataId: "foo",
            group: "G",
            tenant: null,
            cancellationToken: default);

        // Assert: exactly one frame was dispatched with the expected type,
        // and the request DTO carried the dataId/group we passed in.
        Assert.Single(fake.Captured);
        var captured = fake.Captured[0];

        Assert.Equal(ConfigRpcPaths.ConfigQueryRequest, captured.type);

        var payload = Assert.IsType<ConfigQueryRequest>(captured.request);
        Assert.Equal("foo", payload.DataId);
        Assert.Equal("G", payload.Group);

        // The fake response is echoed back through the transport client.
        Assert.NotNull(result);
        Assert.Equal("bar", result!.Content);
    }

    [Fact]
    public async Task BatchListenAsync_SendsConfigBatchListenRequestWithListenTrue()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);
        var client = new ConfigRpcTransportClient(fake, options);

        var ctx = new ConfigListenContext { DataId = "foo", Group = "G", Tenant = "", Md5 = "x" };

        // Act
        await client.BatchListenAsync(new List<ConfigListenContext> { ctx }, listen: true, cancellationToken: default);

        // Assert: exactly one frame was dispatched through the stream-with-response
        // seam, with the expected type and a populated request body.
        Assert.Single(fake.StreamCalls);
        var captured = fake.StreamCalls[0];

        Assert.Equal(ConfigRpcPaths.ConfigBatchListenRequest, captured.type);

        var payload = Assert.IsType<ConfigBatchListenRequest>(captured.request);
        Assert.True(payload.Listen);
        Assert.Single(payload.ConfigListenContexts);
        Assert.Equal("foo", payload.ConfigListenContexts[0].DataId);
    }

    [Fact]
    public async Task SendBatchListenAsync_SendsConfigBatchListenRequestWithListenFalse()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);
        var client = new ConfigRpcTransportClient(fake, options);

        var ctx = new ConfigListenContext { DataId = "foo", Group = "G", Tenant = "", Md5 = "x" };

        // Act
        await client.SendBatchListenAsync(new List<ConfigListenContext> { ctx }, listen: false, cancellationToken: default);

        // Assert: fire-and-forget stream call captured with listen == false.
        Assert.Single(fake.StreamCalls);
        var captured = fake.StreamCalls[0];

        Assert.Equal(ConfigRpcPaths.ConfigBatchListenRequest, captured.type);

        var payload = Assert.IsType<ConfigBatchListenRequest>(captured.request);
        Assert.False(payload.Listen);
        Assert.Single(payload.ConfigListenContexts);
        Assert.Equal("foo", payload.ConfigListenContexts[0].DataId);
    }

    [Fact]
    public async Task FuzzyWatchAsync_SendsConfigFuzzyWatchRequestWithWatchTrue()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);
        var client = new ConfigRpcTransportClient(fake, options);

        var ctx = new ConfigFuzzyListenContext { DataIdPattern = "foo.*", GroupPattern = "G.*" };

        // Act
        await client.FuzzyWatchAsync(new List<ConfigFuzzyListenContext> { ctx }, watch: true, cancellationToken: default);

        // Assert
        Assert.Single(fake.StreamCalls);
        var captured = fake.StreamCalls[0];

        Assert.Equal(ConfigRpcPaths.ConfigFuzzyWatchRequest, captured.type);

        var payload = Assert.IsType<ConfigFuzzyWatchRequest>(captured.request);
        Assert.True(payload.Watch);
        Assert.Single(payload.Contexts);
        Assert.Equal("foo.*", payload.Contexts[0].DataIdPattern);
        Assert.Equal("G.*", payload.Contexts[0].GroupPattern);
    }

    [Fact]
    public async Task SendFuzzyWatchAsync_SendsConfigFuzzyWatchRequestWithWatchFalse()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);
        var client = new ConfigRpcTransportClient(fake, options);

        var ctx = new ConfigFuzzyListenContext { DataIdPattern = "foo.*", GroupPattern = "G.*" };

        // Act
        await client.SendFuzzyWatchAsync(new List<ConfigFuzzyListenContext> { ctx }, watch: false, cancellationToken: default);

        // Assert: fire-and-forget stream call captured with watch == false.
        Assert.Single(fake.StreamCalls);
        var captured = fake.StreamCalls[0];

        Assert.Equal(ConfigRpcPaths.ConfigFuzzyWatchRequest, captured.type);

        var payload = Assert.IsType<ConfigFuzzyWatchRequest>(captured.request);
        Assert.False(payload.Watch);
        Assert.Single(payload.Contexts);
        Assert.Equal("foo.*", payload.Contexts[0].DataIdPattern);
        Assert.Equal("G.*", payload.Contexts[0].GroupPattern);
    }

    [Fact]
    public void ConfigRpcTransportClient_RegistersPushHandlerInConstructor()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);

        // Act: constructing the transport client should register the push handler.
        var client = new ConfigRpcTransportClient(fake, options);

        // Assert: the constructor called RegisterPushHandler("config", HandlePushMessage).
        Assert.True(fake.PushHandlers.ContainsKey("config"));

        // Subscribe to OnConfigChanged and simulate a server push through the captured
        // handler. The full HandlePushMessage -> HandleConfigChangeNotify ->
        // OnConfigChanged chain should fire.
        var fired = false;
        client.OnConfigChanged += _ => fired = true;

        var notify = new ConfigChangeNotifyRequest { DataId = "foo", Group = "G" };
        var json = JsonSerializer.Serialize(notify);

        fake.SimulatePush("config", ConfigRpcPaths.ConfigChangeNotifyRequest, json);

        Assert.True(fired);
    }
}
