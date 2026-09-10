using System.Text.Json;
using RedNb.Nacos.GrpcClient.Config;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Config;

/// <summary>
/// Tests for <see cref="ConfigRpcTransportClient"/>.
///
/// NOTE: This file is the TDD-red scaffold for Task 3.2. The single test below is
/// expected to FAIL TO COMPILE until Task 3.3 introduces a fake bi-stream capture
/// mechanism (and, if needed, exposes the necessary internals to the test project).
/// Do not "fix" the compile errors by stubbing NacosGrpcClient here -- that work
/// belongs to Task 3.3.
/// </summary>
public class ConfigRpcTransportClientTests
{
    [Fact]
    public async Task QueryConfigAsync_BuildsRequestWithDataIdGroupTenant()
    {
        // Arrange: a fake bi-stream capture that records every (type, body) frame
        // sent by ConfigRpcTransportClient so the assertions below can verify
        // that the correct ConfigQueryRequest was emitted.
        // TODO Task 3.3: introduce a fake bi-stream capture mechanism.
        var captured = new List<(string type, byte[] body)>();
        var client = new ConfigRpcTransportClient(/* fake bi-stream */);

        // Act
        var result = await client.QueryConfigAsync(
            dataId: "foo",
            group: "G",
            tenant: "",
            cancellationToken: default);

        // Assert: a ConfigQueryRequest frame was emitted with the expected payload.
        Assert.Contains(captured, c => c.type == ConfigRpcPaths.ConfigQueryRequest);
        var payload = JsonSerializer.Deserialize<ConfigQueryRequest>(
            captured.Single(c => c.type == ConfigRpcPaths.ConfigQueryRequest).body)!;
        Assert.Equal("foo", payload.DataId);
        Assert.Equal("G", payload.Group);
    }
}
