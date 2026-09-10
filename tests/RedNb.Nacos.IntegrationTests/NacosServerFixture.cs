using Xunit;

namespace RedNb.Nacos.IntegrationTests;

/// <summary>
/// Collection definition for Nacos integration tests.
/// Tests in this collection will not run in parallel.
/// </summary>
[CollectionDefinition("NacosIntegration")]
public class NacosIntegrationCollection : ICollectionFixture<NacosServerFixture>
{
}

/// <summary>
/// Fixture that ensures Nacos server is available before running tests.
/// </summary>
public class NacosServerFixture : IAsyncLifetime
{
    public const string ServerAddress = "localhost:8848";
    public const string Username = "nacos";
    public const string Password = "nacos";

    // Nacos 3.2.4 boots slower than 3.1.x due to dist module initialization,
    // so the readiness probe is given a generous start_period budget (matches
    // the compose healthcheck start_period in deploy/docker-compose/*.yml).
    private const int StartPeriodSeconds = 90;
    private const int PollIntervalSeconds = 3;

    public async Task InitializeAsync()
    {
        // Poll the Nacos 3.x readiness endpoint until it succeeds or the
        // start_period budget elapses.
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        var maxAttempts = StartPeriodSeconds / PollIntervalSeconds;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(
                    $"http://{ServerAddress}/nacos/v3/health/readiness");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Nacos server is ready (attempt {attempt})");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Attempt {attempt}: Failed to connect to Nacos - {ex.Message}");
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds));
            }
        }

        throw new InvalidOperationException(
            $"Nacos server did not become ready within {StartPeriodSeconds}s at {ServerAddress}. " +
            "Please ensure Nacos 3.x is running before executing integration tests.");
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}