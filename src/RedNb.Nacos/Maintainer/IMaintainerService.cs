namespace RedNb.Nacos.Core.Maintainer;

/// <summary>
/// Combined maintainer service interface that includes all maintenance operations.
/// </summary>
[Obsolete(
    "Nacos 3.2+ removed v1/v2 HTTP endpoints used by this service. " +
    "Calls will return HTTP 404 at runtime. " +
    "Migrate to Nacos 2.x server or use the nacos-api-legacy-adapter JAR. " +
    "Tracked for removal in the next major version.",
    error: false)]
public interface IMaintainerService :
    // Naming maintainer interfaces
    IServiceMaintainer,
    IInstanceMaintainer,
    INamingMaintainer,
    // Config maintainer interfaces
    IConfigMaintainer,
    IConfigHistoryMaintainer,
    IBetaConfigMaintainer,
    IConfigOpsMaintainer,
    // Client maintainer interface
    IClientMaintainer,
    // Core maintainer interface
    ICoreMaintainer,
    IAsyncDisposable
{
    /// <summary>
    /// Gets the server status.
    /// </summary>
    string GetServerStatus();

    /// <summary>
    /// Shuts down the maintainer service.
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
