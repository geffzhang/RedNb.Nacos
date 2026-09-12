using System.Text.Json;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Config;
using RedNb.Nacos.Core.Config.FuzzyWatch;
using RedNb.Nacos.GrpcClient;
using RedNb.Nacos.GrpcClient.Config;
using RedNb.Nacos.Utils;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Config;

/// <summary>
/// Tests for <see cref="NacosGrpcConfigService"/>.
///
/// These tests exercise the wire-up between <c>NacosGrpcConfigService</c> and
/// <c>ConfigRpcTransportClient</c> using a <see cref="FakeNacosGrpcClient"/>
/// injected via the test-friendly constructor overload added in Task 3.5.
///
/// The fake's <c>ConnectAsync</c> override is a no-op, so the service's
/// <c>EnsureInitializedAsync</c> path completes immediately without a live
/// gRPC server. Tests assert against captured frames and the registered push
/// handler rather than waiting on the listen-loop background task.
/// </summary>
public class NacosGrpcConfigServiceTests
{
    [Fact]
    public async Task GetConfigAsync_CallsTransportClientQueryConfigAsync()
    {
        // Arrange: fake returns a ConfigQueryResponse so GetConfigAsync will
        // take the success path and dispatch through QueryConfigAsync.
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new ConfigQueryResponse
            {
                Success = true,
                Content = "hello",
                Md5 = "m"
            }
        };
        var service = new NacosGrpcConfigService(options, fake, logger: null);

        // Act
        var content = await service.GetConfigAsync("foo", "G", timeoutMs: 3000, cancellationToken: default);

        // Assert: a ConfigQueryRequest was dispatched with the dataId/group
        // we passed in, and the fake response is echoed back to the caller.
        var captured = Assert.Single(fake.Captured, c => c.request is ConfigQueryRequest);
        Assert.Equal(ConfigRpcPaths.ConfigQueryRequest, captured.type);

        var query = Assert.IsType<ConfigQueryRequest>(captured.request);
        Assert.Equal("foo", query.DataId);
        Assert.Equal("G", query.Group);

        Assert.Equal("hello", content);
    }

    [Fact]
    public async Task PublishConfigAsync_CallsTransportClientPublishConfigAsync()
    {
        // Arrange: fake returns a successful ConfigPublishResponse so the
        // service returns true.
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new ConfigPublishResponse { Success = true }
        };
        var service = new NacosGrpcConfigService(options, fake, logger: null);

        // Act
        var result = await service.PublishConfigAsync("foo", "G", "content", cancellationToken: default);

        // Assert: a ConfigPublishRequest was dispatched with the right dataId/group/content,
        // and the returned bool reflects the success response.
        var captured = Assert.Single(fake.Captured, c => c.request is ConfigPublishRequest);
        Assert.Equal(ConfigRpcPaths.ConfigPublishRequest, captured.type);

        var publish = Assert.IsType<ConfigPublishRequest>(captured.request);
        Assert.Equal("foo", publish.DataId);
        Assert.Equal("G", publish.Group);
        Assert.Equal("content", publish.Content);

        Assert.True(result);
    }

    [Fact]
    public async Task RemoveConfigAsync_CallsTransportClientRemoveConfigAsync()
    {
        // Arrange: fake returns a successful ConfigRemoveResponse.
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new ConfigRemoveResponse { Success = true }
        };
        var service = new NacosGrpcConfigService(options, fake, logger: null);

        // Act
        var result = await service.RemoveConfigAsync("foo", "G", cancellationToken: default);

        // Assert: a ConfigRemoveRequest was dispatched with the dataId/group,
        // and the returned bool reflects the success response.
        var captured = Assert.Single(fake.Captured, c => c.request is ConfigRemoveRequest);
        Assert.Equal(ConfigRpcPaths.ConfigRemoveRequest, captured.type);

        var remove = Assert.IsType<ConfigRemoveRequest>(captured.request);
        Assert.Equal("foo", remove.DataId);
        Assert.Equal("G", remove.Group);

        Assert.True(result);
    }

    [Fact]
    public async Task AddListenerAsync_SendsListenRequestAndNotifiesOnPush()
    {
        // Arrange: fake returns content + md5 from QueryConfigAsync so
        // AddListenerAsync computes the MD5 locally and sends a listen frame.
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new ConfigQueryResponse
            {
                Success = true,
                Content = "value",
                Md5 = "md5-value"
            }
        };
        var service = new NacosGrpcConfigService(options, fake, logger: null);

        var listener = new TestConfigChangeListener();
        await service.AddListenerAsync("foo", "G", listener, CancellationToken.None);

        // Assert: AddListenerAsync first queries the server for the initial
        // content/MD5, then sends a ConfigBatchListenRequest with listen=true.
        Assert.Contains(fake.Captured,
            c => c.request is ConfigQueryRequest q && q.DataId == "foo" && q.Group == "G");

        var listenCall = Assert.Single(fake.StreamCalls, c => c.request is ConfigBatchListenRequest);
        Assert.Equal(ConfigRpcPaths.ConfigBatchListenRequest, listenCall.type);

        var batchListen = Assert.IsType<ConfigBatchListenRequest>(listenCall.request);
        Assert.True(batchListen.Listen);
        var ctx = Assert.Single(batchListen.ConfigListenContexts);
        Assert.Equal("foo", ctx.DataId);
        Assert.Equal("G", ctx.Group);
        // MD5 is locally computed from the fetched content by AddListenerAsync,
        // so it is the MD5 of "value" (not the server-provided "md5-value").
        Assert.False(string.IsNullOrEmpty(ctx.Md5));
        Assert.Equal(NacosUtils.GetMd5("value"), ctx.Md5);

        // Drive the registered push handler with a content push — this
        // exercises the ConfigRpcTransportClient.OnConfigChanged →
        // NacosGrpcConfigService.HandleConfigChangeNotify → listener chain.
        var notify = new ConfigChangeNotifyRequest
        {
            DataId = "foo",
            Group = "G",
            ContentPush = true,
            Content = "new-value",
            Md5 = "md5-new"
        };
        var json = JsonSerializer.Serialize(notify);

        fake.SimulatePush("config", ConfigRpcPaths.ConfigChangeNotifyRequest, json);

        // The listener is notified from a fire-and-forget task — wait for
        // the TaskCompletionSource to be set with a bounded timeout.
        var configInfo = await listener.Received.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("foo", configInfo.DataId);
        Assert.Equal("G", configInfo.Group);
        Assert.Equal("new-value", configInfo.Content);
        Assert.Equal("md5-new", configInfo.Md5);
    }

    [Fact]
    public async Task RemoveListener_SendsUnlistenAfterLastListenerRemoved()
    {
        // Arrange: subscribe a listener so the cache is populated, then
        // remove it. The unlisten should be dispatched via the stream path.
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options)
        {
            Response = new ConfigQueryResponse
            {
                Success = true,
                Content = "value",
                Md5 = "md5-value"
            }
        };
        var service = new NacosGrpcConfigService(options, fake, logger: null);

        var listener = new TestConfigChangeListener();
        await service.AddListenerAsync("foo", "G", listener, CancellationToken.None);

        // Sanity: the listen call was sent.
        Assert.Contains(fake.StreamCalls, c => c.request is ConfigBatchListenRequest b && b.Listen);

        // Act: removing the only listener sends an unlisten (listen=false).
        service.RemoveListener("foo", "G", listener);

        // Wait briefly for the fire-and-forget unlisten task to dispatch.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!fake.StreamCalls.Any(c => c.request is ConfigBatchListenRequest b && !b.Listen))
        {
            if (DateTime.UtcNow > deadline)
            {
                break;
            }
            await Task.Delay(10);
        }

        var unlistenCall = fake.StreamCalls.Last(c => c.request is ConfigBatchListenRequest);
        var unlisten = Assert.IsType<ConfigBatchListenRequest>(unlistenCall.request);
        Assert.False(unlisten.Listen);
        var ctx = Assert.Single(unlisten.ConfigListenContexts);
        Assert.Equal("foo", ctx.DataId);
        Assert.Equal("G", ctx.Group);
    }

    [Fact]
    public async Task FuzzyWatchAsync_CallsTransportClientFuzzyWatchAsync()
    {
        // Arrange
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);
        var service = new NacosGrpcConfigService(options, fake, logger: null);

        // Act
        await service.FuzzyWatchAsync(
            dataIdPattern: "foo.*",
            groupNamePattern: "G.*",
            watcher: new RecordingFuzzyWatcher(),
            cancellationToken: default);

        // Assert: a ConfigFuzzyWatchRequest with watch=true was dispatched
        // through the stream path with the patterns we passed in.
        var call = Assert.Single(fake.StreamCalls, c => c.request is ConfigFuzzyWatchRequest);
        Assert.Equal(ConfigRpcPaths.ConfigFuzzyWatchRequest, call.type);

        var fuzzyWatch = Assert.IsType<ConfigFuzzyWatchRequest>(call.request);
        Assert.True(fuzzyWatch.Watch);
        var ctx = Assert.Single(fuzzyWatch.Contexts);
        Assert.Equal("foo.*", ctx.DataIdPattern);
        Assert.Equal("G.*", ctx.GroupPattern);
    }

    [Fact]
    public async Task DisposeAsync_UnregistersHandlers()
    {
        // Arrange: constructing the service wires ConfigRpcTransportClient,
        // which registers its push handler with the (fake) NacosGrpcClient.
        var options = new NacosClientOptions();
        var fake = new FakeNacosGrpcClient(options);
        var service = new NacosGrpcConfigService(options, fake, logger: null);

        Assert.True(fake.PushHandlers.ContainsKey("config"));

        // Act
        await service.DisposeAsync();

        // Assert: the "config" push handler was unregistered on DisposeAsync,
        // proving the ConfigRpcTransportClient.DisposeAsync → UnregisterPushHandler
        // chain is intact.
        Assert.False(fake.PushHandlers.ContainsKey("config"));
    }

    /// <summary>
    /// Test double for <see cref="IConfigChangeListener"/> that signals a
    /// TaskCompletionSource when its callback fires, so async tests can
    /// await the (fire-and-forget) notification without polling.
    /// </summary>
    private sealed class TestConfigChangeListener : IConfigChangeListener
    {
        private readonly TaskCompletionSource<ConfigInfo> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ConfigInfo> Received => _tcs.Task;

        public void OnReceiveConfigInfo(ConfigInfo configInfo)
        {
            _tcs.TrySetResult(configInfo);
        }
    }

    /// <summary>
    /// Test double for <see cref="IConfigFuzzyWatchEventWatcher"/> that
    /// records invocations. Only constructed in the FuzzyWatchAsync test;
    /// never invoked in this file.
    /// </summary>
    private sealed class RecordingFuzzyWatcher : IConfigFuzzyWatchEventWatcher
    {
        public void OnEvent(ConfigFuzzyWatchChangeEvent @event)
        {
        }

        public TaskScheduler? Scheduler => null;
    }
}
