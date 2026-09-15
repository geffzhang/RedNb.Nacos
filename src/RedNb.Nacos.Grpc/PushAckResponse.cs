using System.Text.Json.Serialization;

namespace RedNb.Nacos.Grpc;

/// <summary>
/// Acknowledgement echoed back over the bi-stream for every server push whose
/// type ends with <c>Request</c>. Must stay a top-level, non-private type: the
/// source-generated context cannot declare metadata for anonymous or nested
/// private types, and a push the client fails to serialize is never acked —
/// which stalls fuzzy watch, whose server side waits for the sync ack.
/// </summary>
internal sealed class PushAckResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }
}
