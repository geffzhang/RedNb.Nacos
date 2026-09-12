using Microsoft.Extensions.Logging;
using RedNb.Nacos;

namespace RedNb.Nacos.Http.Transport;

/// <summary>
/// HTTP transport for the core Nacos server API (client/admin port 8848,
/// /nacos context path). Shares its full implementation with
/// <see cref="NacosConsoleHttpClient"/> via <see cref="NacosHttpClientBase"/>.
/// </summary>
public class NacosHttpClient : NacosHttpClientBase
{
    public NacosHttpClient(NacosClientOptions options, ILogger? logger = null)
        : base(options, logger)
    {
    }
}
