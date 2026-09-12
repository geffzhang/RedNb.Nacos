namespace RedNb.Nacos.Administration;

/// <summary>
/// Combined maintainer service interface that includes all maintenance operations.
/// </summary>
[Obsolete("Legacy maintenance contract. Use IAdministrationService, IConfigService or INamingService on Nacos 3.2.4.")]
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
