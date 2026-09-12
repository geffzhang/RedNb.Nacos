using System.Text.Json;
using RedNb.Nacos.Ai.Models.A2a;

namespace RedNb.Nacos.Grpc.Ai;

public sealed partial class NacosAiClient
{
    private readonly SemaphoreSlim _endpointLock = new(1, 1);
    private readonly CancellationTokenSource _recoveryCts = new();
    private readonly Dictionary<string, Func<CancellationToken, Task>> _mcpEndpoints = new();
    private readonly Dictionary<string, (string Name, List<AgentEndpoint> Endpoints)> _agentEndpoints = new();
    private Task? _recoveryTask;
    private string? _endpointConnection;
    private bool _disposed;

    private void StartRecovery()
    {
        _endpointConnection ??= _grpc.Value.ConnectionId;
        _recoveryTask ??= Task.Run(RecoverEndpointsAsync);
    }
    private async Task RegisterMcpAsync(string name, string address, int port, string? version, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _endpointLock.WaitAsync(ct);
        try
        {
            await _grpc.Value.RegisterMcpServerEndpointAsync(name, address, port, version, ct);
            _mcpEndpoints[name] = token => _grpc.Value.RegisterMcpServerEndpointAsync(name, address, port, version, token);
            StartRecovery();
        }
        finally { _endpointLock.Release(); }
    }
    private async Task DeregisterMcpAsync(string name, string address, int port, CancellationToken ct)
    {
        await _endpointLock.WaitAsync(ct);
        try { await _grpc.Value.DeregisterMcpServerEndpointAsync(name, address, port, ct); _mcpEndpoints.Remove(name); }
        finally { _endpointLock.Release(); }
    }
    private async Task RegisterAgentAsync(string name, List<AgentEndpoint> endpoints, bool batch, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var copy = JsonSerializer.Deserialize<List<AgentEndpoint>>(JsonSerializer.Serialize(endpoints))!;
        await _endpointLock.WaitAsync(ct);
        try
        {
            if (batch) await _grpc.Value.RegisterAgentEndpointsAsync(name, copy, ct);
            else await _grpc.Value.RegisterAgentEndpointAsync(name, copy.Single(), ct);
            _agentEndpoints[$"{name}@@{copy[0].Version}"] = (name, copy);
            StartRecovery();
        }
        finally { _endpointLock.Release(); }
    }
    private async Task DeregisterAgentAsync(string name, AgentEndpoint endpoint, CancellationToken ct)
    {
        await _endpointLock.WaitAsync(ct);
        try
        {
            await _grpc.Value.DeregisterAgentEndpointAsync(name, endpoint, ct);
            var key = $"{name}@@{endpoint.Version}";
            if (_agentEndpoints.TryGetValue(key, out var cached))
            {
                cached.Endpoints.RemoveAll(e => e.Address == endpoint.Address && e.Port == endpoint.Port);
                if (cached.Endpoints.Count == 0) _agentEndpoints.Remove(key);
            }
        }
        finally { _endpointLock.Release(); }
    }
    private async Task RecoverEndpointsAsync()
    {
        var ct = _recoveryCts.Token;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(3000, ct);
                if (!_grpc.Value.IsConnected || _endpointConnection == _grpc.Value.ConnectionId) continue;
                await _endpointLock.WaitAsync(ct);
                try
                {
                    var generation = _grpc.Value.ConnectionId;
                    foreach (var replay in _mcpEndpoints.Values) await replay(ct);
                    foreach (var entry in _agentEndpoints.Values)
                        await _grpc.Value.RegisterAgentEndpointsAsync(entry.Name, entry.Endpoints, ct);
                    _endpointConnection = generation;
                }
                finally { _endpointLock.Release(); }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (NacosException) { /* Keep pending state and retry on the next cycle. */ }
            catch (global::Grpc.Core.RpcException) { /* Transient channel failure. */ }
        }
    }
}
