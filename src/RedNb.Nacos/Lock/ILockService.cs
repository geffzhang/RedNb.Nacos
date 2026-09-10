namespace RedNb.Nacos.Core.Lock;

/// <summary>
/// Nacos distributed lock service interface.
/// Provides distributed lock capabilities for microservices.
/// </summary>
[Obsolete(
    "ILockService is preserved as a marker for the legacy lock API on Nacos 3.x. " +
    "The underlying /nacos/v3/lock endpoints still function but the interface is " +
    "scheduled for removal. Use a dedicated lock library or a custom integration instead.",
    error: false)]
public interface ILockService : IAsyncDisposable
{
    /// <summary>
    /// Acquires a distributed lock.
    /// </summary>
    /// <param name="instance">The lock instance containing lock key and configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if lock acquired successfully, false otherwise.</returns>
    Task<bool> LockAsync(LockInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a distributed lock.
    /// </summary>
    /// <param name="instance">The lock instance to release.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if lock released successfully, false otherwise.</returns>
    Task<bool> UnlockAsync(LockInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to acquire a lock with timeout.
    /// </summary>
    /// <param name="instance">The lock instance.</param>
    /// <param name="timeout">The maximum time to wait for acquiring the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if lock acquired within timeout, false otherwise.</returns>
    Task<bool> TryLockAsync(LockInstance instance, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to acquire a lock remotely via gRPC.
    /// </summary>
    /// <param name="instance">The lock instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if lock acquired successfully, false otherwise.</returns>
    Task<bool> RemoteTryLockAsync(LockInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a lock remotely via gRPC.
    /// </summary>
    /// <param name="instance">The lock instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if lock released successfully, false otherwise.</returns>
    Task<bool> RemoteReleaseLockAsync(LockInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the server status.
    /// </summary>
    /// <returns>The server status string.</returns>
    string GetServerStatus();

    /// <summary>
    /// Shuts down the lock service and releases resources.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
