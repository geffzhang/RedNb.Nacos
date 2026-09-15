using System.Text.Json.Serialization;
using RedNb.Nacos.Serialization;

namespace RedNb.Nacos.Grpc.Lock;

// Wire models of the Nacos 3.2.4 native mutex LockOperationRequest. They are
// internal top-level types because the source-generated context cannot declare
// metadata for anonymous types, and the server rejects an unacked/unknown
// payload shape. Field names follow the Nacos Java client, not LockInstance.

/// <summary>Payload of a lock acquire/release operation.</summary>
internal sealed class LockOperationRequest
{
    [JsonPropertyName("lockOperationEnum")]
    public string LockOperationEnum { get; set; } = string.Empty;

    [JsonPropertyName("lockInstance")]
    public LockOperationInstance LockInstance { get; set; } = new();
}

/// <summary>Lock instance as the native mutex expects it (note <c>expiredTime</c>).</summary>
internal sealed class LockOperationInstance
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("expiredTime")]
    public long ExpiredTime { get; set; }

    [JsonPropertyName("lockType")]
    public string LockType { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    [JsonConverter(typeof(ObjectDictionaryConverter))]
    public Dictionary<string, object>? Params { get; set; }
}

/// <summary>Response of a lock operation.</summary>
internal sealed class LockOperationResponse
{
    [JsonPropertyName("resultCode")]
    public int ResultCode { get; set; }

    [JsonPropertyName("errorCode")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("result")]
    public bool Result { get; set; }
}
