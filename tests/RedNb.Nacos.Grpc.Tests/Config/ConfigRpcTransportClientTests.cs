using RedNb.Nacos.Core;
using RedNb.Nacos.GrpcClient;
using RedNb.Nacos.GrpcClient.Config;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Config;

/// <summary>
/// Tests for <see cref="ConfigRpcTransportClient"/>.
///
/// Uses a hand-rolled <see cref="FakeNacosGrpcClient"/> that overrides
/// <see cref="NacosGrpcClient.RequestAsync{TResponse}"/> to capture the
/// (type, request) pairs dispatched by the transport client without requiring
/// a live gRPC server.
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

    /// <summary>
    /// Hand-rolled test double: subclasses <see cref="NacosGrpcClient"/> and
    /// overrides the single <c>virtual</c> seam method
    /// <see cref="RequestAsync{TResponse}"/> to capture every dispatched frame
    /// and return a configurable fake response. No real gRPC connection is
    /// established.
    /// </summary>
    private class FakeNacosGrpcClient : NacosGrpcClient
    {
        public List<(string type, object request)> Captured { get; } = new();

        /// <summary>
        /// When non-null, returned from <see cref="RequestAsync{TResponse}"/>
        /// if the requested <typeparamref name="TResponse"/> matches the
        /// configured response type. Otherwise <c>null</c> is returned.
        /// </summary>
        public object? Response { get; set; }

        public FakeNacosGrpcClient(NacosClientOptions options)
            : base(options)
        {
        }

        public override Task<TResponse?> RequestAsync<TResponse>(string type, object request,
            CancellationToken cancellationToken = default) where TResponse : class
        {
            Captured.Add((type, request));

            if (Response is TResponse typed)
            {
                return Task.FromResult<TResponse?>(typed);
            }

            return Task.FromResult<TResponse?>(null);
        }
    }
}
