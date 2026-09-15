using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using RedNb.Nacos.Grpc.Lock;
using RedNb.Nacos.Grpc.Serialization;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests.Serialization;

public class NestedUserContextTests
{
    [Fact]
    public void NestedLockPayloadUsesClientContextInsteadOfSdkDefaultContext()
    {
        var options = NacosGrpcJsonOptions.Create(LockUserContext.Default, allowReflectionFallback: false);
        var request = new LockOperationRequest
        {
            LockOperationEnum = "ACQUIRE",
            LockInstance = new LockOperationInstance
            {
                Key = "public@@regression",
                Params = new() { ["user"] = new LockUserPayload { Value = 42 } }
            }
        };
        // Use the same runtime TypeInfo overload as NacosGrpcClient.
        var json = JsonSerializer.Serialize(request, (JsonTypeInfo<LockOperationRequest>)options.GetTypeInfo(typeof(LockOperationRequest)));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(42, document.RootElement.GetProperty("lockInstance").GetProperty("params")
            .GetProperty("user").GetProperty("application_value").GetInt32());
    }
}
public sealed class LockUserPayload { [JsonPropertyName("application_value")] public int Value { get; set; } }
[JsonSerializable(typeof(LockUserPayload))]
internal partial class LockUserContext : JsonSerializerContext { }
