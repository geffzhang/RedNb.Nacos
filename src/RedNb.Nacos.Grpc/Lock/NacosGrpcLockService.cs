using Microsoft.Extensions.Logging;
using RedNb.Nacos.Lock;
namespace RedNb.Nacos.Grpc.Lock;

/// <summary>Nacos 3.2.4 native mutex; no fencing or cross-process ownership guarantee is added.</summary>
public sealed class NacosGrpcLockService : ILockService
{
    private readonly NacosClientOptions _options;
    private readonly NacosGrpcClient _client;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly Dictionary<string, (string Owner, DateTime Expires, string? Connection)> _leases = new();
    private bool _disposed;
    public NacosGrpcLockService(NacosClientOptions options, ILogger<NacosGrpcLockService>? logger = null)
    { options.Validate(); _options = options; _client = new NacosGrpcClient(options, logger, "lock"); }
    public Task InitializeAsync(CancellationToken cancellationToken = default) => _client.ConnectAsync(cancellationToken);
    public Task<bool> LockAsync(LockInstance instance, CancellationToken cancellationToken = default) => RemoteTryLockAsync(instance, cancellationToken);
    public Task<bool> UnlockAsync(LockInstance instance, CancellationToken cancellationToken = default) => RemoteReleaseLockAsync(instance, cancellationToken);
    public async Task<bool> TryLockAsync(LockInstance instance, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            if (await RemoteTryLockAsync(instance, cancellationToken)) return true;
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) return false;
            await Task.Delay(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100), cancellationToken);
        } while (DateTime.UtcNow < deadline);
        return false;
    }
    public async Task<bool> RemoteTryLockAsync(LockInstance instance, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance.Key);
        if (instance.Reentrant) throw new NotSupportedException("The native Nacos mutex is not reentrant.");
        await _operations.WaitAsync(cancellationToken);
        try
        {
            await _client.ConnectAsync(cancellationToken);
            var ttl = instance.ExpireTime > 0 ? instance.ExpireTime : LockConstants.DefaultExpireTime;
            var expires = DateTime.UtcNow.AddMilliseconds(ttl);
            var acquired = await Execute(instance, "ACQUIRE", ttl, cancellationToken);
            if (acquired)
            {
                instance.Owner ??= Guid.NewGuid().ToString("N");
                _leases[Key(instance)] = (instance.Owner, expires, _client.ConnectionId);
            }
            return acquired;
        }
        finally { _operations.Release(); }
    }
    public async Task<bool> RemoteReleaseLockAsync(LockInstance instance, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(instance);
        await _operations.WaitAsync(cancellationToken);
        try
        {
            await _client.ConnectAsync(cancellationToken);
            var key = Key(instance);
            if (!_leases.TryGetValue(key, out var lease) || lease.Owner != instance.Owner ||
                lease.Expires <= DateTime.UtcNow || lease.Connection != _client.ConnectionId) return false;
            var released = await Execute(instance, "RELEASE", 0, cancellationToken);
            if (released) _leases.Remove(key);
            return released;
        }
        finally { _operations.Release(); }
    }
    private string Key(LockInstance instance)
        => (string.IsNullOrWhiteSpace(instance.NamespaceId ?? _options.Namespace) ? "public" : instance.NamespaceId ?? _options.Namespace) + "@@" + instance.Key;
    private async Task<bool> Execute(LockInstance instance, string operation, long ttl, CancellationToken ct)
    {
        var request = new LockOperationRequest
        {
            LockOperationEnum = operation,
            LockInstance = new LockOperationInstance
            {
                Key = Key(instance),
                ExpiredTime = ttl,
                LockType = instance.LockType == "nacos" ? "NACOS_LOCK" : instance.LockType,
                Params = instance.Params
            }
        };

        var response = await _client.RequestAsync<LockOperationResponse>("LockOperationRequest", request, ct);
        if (response?.ResultCode != 200)
            throw new NacosException(response?.ErrorCode ?? NacosException.ServerError, response?.Message ?? "Lock operation failed");
        return response.Result;
    }
    public string GetServerStatus() => !_disposed && _client.IsConnected ? "UP" : "DOWN";
    public async Task ShutdownAsync(CancellationToken cancellationToken = default) => await DisposeAsync();
    public async ValueTask DisposeAsync()
    { if (_disposed) return; _disposed = true; await _client.DisposeAsync(); _leases.Clear(); }
}
