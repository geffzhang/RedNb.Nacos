using Microsoft.Extensions.Logging;
using RedNb.Nacos.Lock;
namespace RedNb.Nacos.Http.Lock;
/// <summary>HTTP has no native lock API in Nacos 3.2.4; use NacosGrpcLockService.</summary>
public sealed class NacosLockService : ILockService
{
    public NacosLockService(NacosClientOptions options, ILogger<NacosLockService>? logger = null) { options.Validate(); }
    private static NotSupportedException Unsupported() => new("Nacos 3.2.4 lock operations require NacosGrpcLockService.");
    public Task<bool> LockAsync(LockInstance instance, CancellationToken cancellationToken = default) => throw Unsupported();
    public Task<bool> UnlockAsync(LockInstance instance, CancellationToken cancellationToken = default) => throw Unsupported();
    public Task<bool> TryLockAsync(LockInstance instance, TimeSpan timeout, CancellationToken cancellationToken = default) => throw Unsupported();
    public Task<bool> RemoteTryLockAsync(LockInstance instance, CancellationToken cancellationToken = default) => throw Unsupported();
    public Task<bool> RemoteReleaseLockAsync(LockInstance instance, CancellationToken cancellationToken = default) => throw Unsupported();
    public string GetServerStatus() => "UNSUPPORTED";
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
