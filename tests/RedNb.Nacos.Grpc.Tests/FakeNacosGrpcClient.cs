using RedNb.Nacos.Core;
using RedNb.Nacos.GrpcClient;

namespace RedNb.Nacos.Grpc.Tests;

/// <summary>
/// Hand-rolled test double for <see cref="NacosGrpcClient"/>.
///
/// Subclasses <see cref="NacosGrpcClient"/> and overrides the virtual seam methods
/// (<see cref="RequestAsync{TResponse}"/>, <see cref="SendStreamRequestAsync"/>,
/// <see cref="SendStreamRequestWithResponseAsync{TResponse}"/>,
/// <see cref="RegisterPushHandler"/>, and <see cref="ConnectAsync"/>) to capture
/// every dispatched frame and the registered push handler, returning a
/// configurable fake response for the unary request path. No real gRPC
/// connection is established.
///
/// Reusable across test classes in the Grpc.Tests assembly. Constructed with
/// <c>new FakeNacosGrpcClient(options)</c> and optionally wired into a
/// production class via the new testable constructor overloads added by
/// Tasks 3.3 / 3.5.
/// </summary>
internal sealed class FakeNacosGrpcClient : NacosGrpcClient
{
    /// <summary>
    /// Captures every unary request dispatched via
    /// <see cref="RequestAsync{TResponse}"/>, paired with the type and request body.
    /// </summary>
    public List<(string type, object request)> Captured { get; } = new();

    /// <summary>
    /// Captures every stream-mode dispatch
    /// (<see cref="SendStreamRequestAsync"/> and
    /// <see cref="SendStreamRequestWithResponseAsync{TResponse}"/>),
    /// paired with the type and request body.
    /// </summary>
    public List<(string type, object request)> StreamCalls { get; } = new();

    /// <summary>
    /// Captures every push handler registered via
    /// <see cref="RegisterPushHandler"/>, keyed by handlerId.
    /// </summary>
    public Dictionary<string, Action<string, string>> PushHandlers { get; } = new();

    /// <summary>
    /// When non-null, returned from <see cref="RequestAsync{TResponse}"/>
    /// if the requested <typeparamref name="TResponse"/> matches the
    /// configured response type. Otherwise <c>null</c> is returned.
    /// </summary>
    public object? Response { get; set; }

    public FakeNacosGrpcClient(NacosClientOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// No-op override — keeps the fake disconnected so the rest of the
    /// service's <c>EnsureInitializedAsync</c> / <c>InitializeAsync</c>
    /// path can run without a live gRPC server.
    /// </summary>
    public override Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task<TResponse?> RequestAsync<TResponse>(string type, object request,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        Captured.Add((type, request));

        if (Response is TResponse typed)
        {
            return Task.FromResult<TResponse?>(typed);
        }

        return Task.FromResult<TResponse?>(null);
    }

    public override Task SendStreamRequestAsync(string type, object request,
        CancellationToken cancellationToken = default)
    {
        StreamCalls.Add((type, request));
        return Task.CompletedTask;
    }

    public override Task<TResponse?> SendStreamRequestWithResponseAsync<TResponse>(string type, object request,
        TimeSpan timeout, CancellationToken cancellationToken = default) where TResponse : class
    {
        StreamCalls.Add((type, request));
        return Task.FromResult<TResponse?>(default);
    }

    public override void RegisterPushHandler(string handlerId, Action<string, string> handler)
    {
        PushHandlers[handlerId] = handler;
        // Also call base so UnregisterPushHandler on the base class can remove it.
        // We mirror the entry in our own dict for assertion convenience.
        base.RegisterPushHandler(handlerId, handler);
    }

    public override void UnregisterPushHandler(string handlerId)
    {
        PushHandlers.Remove(handlerId);
        base.UnregisterPushHandler(handlerId);
    }

    /// <summary>
    /// Invokes a captured push handler with the same (type, body) signature
    /// the production code would receive over the bi-stream, exercising the
    /// full HandlePushMessage dispatch chain.
    /// </summary>
    public void SimulatePush(string handlerId, string type, string body)
    {
        PushHandlers[handlerId].Invoke(type, body);
    }
}
