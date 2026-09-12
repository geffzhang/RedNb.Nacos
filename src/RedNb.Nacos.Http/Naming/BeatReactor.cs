using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RedNb.Nacos;
using RedNb.Nacos.Naming;

namespace RedNb.Nacos.Http.Naming;

/// <summary>
/// Handles heartbeat sending for registered instances.
/// </summary>
public class BeatReactor : IDisposable, IAsyncDisposable
{
    private readonly NacosNamingService _namingService;
    private readonly NacosClientOptions _options;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, BeatInfo> _beatInfoMap = new();
    private readonly CancellationTokenSource _cts;
    private bool _disposed;
    private readonly object _startLock = new();
    private Task? _beatTask;

    public BeatReactor(NacosNamingService namingService, NacosClientOptions options, ILogger? logger = null)
    {
        _namingService = namingService;
        _options = options;
        _logger = logger;
        _cts = new CancellationTokenSource();

        // Start the beat task
    }

    /// <summary>
    /// Adds beat info for an instance.
    /// </summary>
    public void AddBeatInfo(string serviceName, string groupName, Instance instance)
    {
        var key = GetKey(serviceName, groupName, instance);
        var beatInfo = new BeatInfo
        {
            ServiceName = serviceName,
            GroupName = groupName,
            Instance = instance,
            Period = instance.GetInstanceHeartBeatInterval()
        };

        _beatInfoMap[key] = beatInfo;
        lock (_startLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _beatTask ??= Task.Run(() => BeatTaskAsync(_cts.Token));
        }
        _logger?.LogDebug("Added beat info for {Key}", key);
    }

    /// <summary>
    /// Removes beat info for an instance.
    /// </summary>
    public void RemoveBeatInfo(string serviceName, string groupName, Instance instance)
    {
        var key = GetKey(serviceName, groupName, instance);
        _beatInfoMap.TryRemove(key, out _);
        _logger?.LogDebug("Removed beat info for {Key}", key);
    }

    private async Task BeatTaskAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting beat reactor");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var kvp in _beatInfoMap)
                {
                    var beatInfo = kvp.Value;

                    if (now - beatInfo.LastBeatTime >= beatInfo.Period)
                    {
                        try
                        {
                            await _namingService.SendBeatAsync(
                                beatInfo.ServiceName,
                                beatInfo.GroupName,
                                beatInfo.Instance,
                                cancellationToken);

                            beatInfo.LastBeatTime = now;
                            _logger?.LogDebug("Sent heartbeat for {Key}", kvp.Key);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to send heartbeat for {Key}", kvp.Key);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in beat task");
            }
        }

        _logger?.LogInformation("Beat reactor stopped");
    }

    private static string GetKey(string serviceName, string groupName, Instance instance)
    {
        return $"{groupName}@@{serviceName}@@{instance.Ip}:{instance.Port}";
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_beatTask != null) await _beatTask.ConfigureAwait(false);
        _cts.Dispose();
    }

    private class BeatInfo
    {
        public string ServiceName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public Instance Instance { get; set; } = new();
        public long Period { get; set; }
        public long LastBeatTime { get; set; }
    }
}
