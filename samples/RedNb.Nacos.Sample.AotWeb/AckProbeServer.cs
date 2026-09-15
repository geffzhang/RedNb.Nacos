using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RedNb.Nacos;
using RedNb.Nacos.Grpc;
using RedNb.Nacos.Grpc.Protos;
using RedNb.Nacos.Sample.Aot;

// A protocol peer, separate from the real Nacos tests. It confirms receipt of
// both ACK shapes over an actual HTTP/2 bidirectional stream, including the
// missing-id case that a normal Nacos server does not reliably produce.
internal static class AckProbeServer
{
    public static async Task RunAsync()
    {
        var confirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        await using var app = builder.Build();
        RequestDelegate unary = async context =>
        {
            var request = await ReadAsync(context.Request.Body, context.RequestAborted);
            LiveChecks.Require(request.Metadata.Type == "ServerCheckRequest", "Unexpected probe unary request");
            context.Response.ContentType = "application/grpc";
            await WriteAsync(context.Response.Body, "ServerCheckResponse", """{"resultCode":200,"connectionId":"ack-probe"}""", context.RequestAborted);
            context.Response.AppendTrailer("grpc-status", "0");
        };
        RequestDelegate stream = async context =>
        {
            try
            {
                context.Response.ContentType = "application/grpc";
                await context.Response.StartAsync(context.RequestAborted);
                var setup = await ReadAsync(context.Request.Body, context.RequestAborted);
                LiveChecks.Require(setup.Metadata.Type == "ConnectionSetupRequest", "Missing connection setup");
                using (var json = JsonDocument.Parse(setup.Body.Value.ToStringUtf8()))
                    LiveChecks.Require(json.RootElement.GetProperty("clientVersion").GetString() == NacosConstants.ClientVersion, "Stale handshake version");
                await WriteAsync(context.Response.Body, "SetupAckRequest", "{}", context.RequestAborted);
                foreach (var id in new string?[] { "probe-ack", null })
                {
                    await WriteAsync(context.Response.Body, "AckProbeRequest", id == null ? "{}" : """{"requestId":"probe-ack"}""", context.RequestAborted);
                    var ack = await ReadAsync(context.Request.Body, context.RequestAborted);
                    LiveChecks.Require(ack.Metadata.Type == "AckProbeResponse", "Wrong ACK response type");
                    using var json = JsonDocument.Parse(ack.Body.Value.ToStringUtf8());
                    LiveChecks.Require(json.RootElement.GetProperty("success").GetBoolean(), "ACK failure");
                    var hasId = json.RootElement.TryGetProperty("requestId", out var requestId);
                    LiveChecks.Require(id == null ? !hasId : hasId && requestId.GetString() == id, "ACK requestId mismatch");
                }
                confirmed.TrySetResult();
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
            }
            catch (OperationCanceledException) when (confirmed.Task.IsCompletedSuccessfully) { }
            catch (Exception error) { confirmed.TrySetException(error); }
        };
        app.MapPost("/Request/request", unary);
        app.MapPost("/BiRequestStream/requestBiStream", stream);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var client = new NacosGrpcClient(new NacosClientOptions { ServerAddresses = new Uri(address).Authority, GrpcPortOffset = 0 });
        await client.ConnectAsync();
        await confirmed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await client.DisposeAsync();
        await app.StopAsync();
        Console.WriteLine("PASS ACK peer confirmed with requestId and without requestId");
    }

    private static async Task<Payload> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[5];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1));
        LiveChecks.Require(header[0] == 0 && length is > 0 and < 1048576, "Invalid probe gRPC frame");
        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return Payload.Parser.ParseFrom(body);
    }

    private static async Task WriteAsync(Stream stream, string type, string json, CancellationToken cancellationToken)
    {
        var bytes = new Payload { Metadata = new Metadata { Type = type }, Body = new Google.Protobuf.WellKnownTypes.Any { Value = ByteString.CopyFromUtf8(json) } }.ToByteArray();
        var header = new byte[5];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), bytes.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
