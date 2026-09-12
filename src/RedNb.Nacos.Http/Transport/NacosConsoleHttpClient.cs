using Microsoft.Extensions.Logging;
using RedNb.Nacos;

namespace RedNb.Nacos.Http.Transport;

/// <summary>
/// HTTP client for the Nacos <b>console</b> listener (default port 8080). The
/// console listener serves the AI admin/UI API (e.g. <c>/v3/console/ai/**</c>)
/// plus readiness/health probes, and unlike the API port (8848) does not sit
/// under the <c>/nacos</c> context path. Shares its full implementation with
/// <see cref="NacosHttpClient"/> via <see cref="NacosHttpClientBase"/>.
/// </summary>
/// <remarks>
/// <para>
/// Behavior is otherwise identical to <see cref="NacosHttpClient"/>: same retry
/// loop, same body/header handling, same multipart support, same
/// <see cref="SecurityProxy"/>-issued access token. The two intentional
/// differences are:
/// </para>
/// <list type="bullet">
///   <item>The server list is sourced from
///   <see cref="NacosClientOptions.GetConsoleAddressList"/>, which derives
///   from <c>ConsoleAddresses</c> or substitutes port 8080 onto
///   <c>ServerAddresses</c>.</item>
///   <item>Per-request URLs are built with
///   <see cref="NacosClientOptions.GetConsoleBaseUrl"/>, which omits the
///   <c>ContextPath</c>.</item>
/// </list>
/// <para>
/// Because the console address list is required, construction fails fast with
/// <see cref="NacosException"/> when no console address can be resolved.
/// </para>
/// </remarks>
public class NacosConsoleHttpClient : NacosHttpClientBase
{
    /// <summary>
    /// Resolved console base URLs (one per console address, scheme honors
    /// <see cref="NacosClientOptions.EnableTls"/>). Exposed for testability —
    /// callers and tests can assert on the exact set of console endpoints the
    /// client will route to without spinning up a server.
    /// </summary>
    public IReadOnlyList<string> ConsoleBaseUrls { get; }

    public NacosConsoleHttpClient(NacosClientOptions options, ILogger? logger = null)
        : base(options, logger, CreateServerListManager(options))
    {
        ConsoleBaseUrls = options.GetConsoleAddressList()
            .Select(a => options.GetConsoleBaseUrl(a))
            .ToList();
    }

    /// <inheritdoc />
    protected override string BuildBaseUrl(string server) => _options.GetConsoleBaseUrl(server);

    private static ServerListManager CreateServerListManager(NacosClientOptions options)
    {
        var addresses = options.GetConsoleAddressList();
        if (addresses.Count == 0)
        {
            throw new NacosException(
                NacosException.InvalidParam,
                "ConsoleAddresses (or derivable ServerAddresses) is required for NacosConsoleHttpClient");
        }

        return new ServerListManager(addresses);
    }
}
