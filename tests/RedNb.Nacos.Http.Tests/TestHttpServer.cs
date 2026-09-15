using WireMock.Server;

namespace RedNb.Nacos.Http.Tests;

internal static class TestHttpServer
{
    public static WireMockServer Start()
    {
        var server = WireMockServer.Start();
        try
        {
            // Start() binds the socket before the first request has JIT-compiled
            // WireMock's pipeline. Establish readiness outside SDK timeout tests.
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var response = client.GetAsync($"http://localhost:{server.Port}/__nacos_test_ready").GetAwaiter().GetResult();
            server.Reset();
            return server;
        }
        catch { server.Dispose(); throw; }
    }
}
