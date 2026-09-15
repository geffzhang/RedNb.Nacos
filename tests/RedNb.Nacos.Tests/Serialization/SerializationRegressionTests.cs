using System.Collections;
using System.Text;
using System.Text.Json;
using RedNb.Nacos.Serialization;
using Xunit;

namespace RedNb.Nacos.Tests.Serialization;

public class SerializationRegressionTests
{
    public static IEnumerable<object[]> Values()
    {
        yield return [ulong.MaxValue];
        yield return [double.MaxValue];
        yield return [1e-30];
        yield return [float.MaxValue];
        yield return [decimal.MaxValue];
        yield return [new int[] { 1, 2 }];
        yield return [new byte[] { 1, 2, 255 }];
        yield return [new Dictionary<string, string> { ["key"] = "value" }];
        yield return [new Dictionary<int, string> { [1] = "one" }];
        yield return [new Dictionary<string, object> { ["array"] = new[] { 1, 2 }, ["nested"] = new Dictionary<string, string> { ["x"] = "y" } }];
        yield return [new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero)];
        yield return [new DateOnly(2026, 9, 15)];
        yield return [new TimeOnly(10, 20, 30)];
        yield return [Guid.Parse("557c4b1c-545d-4f44-904e-9ea9d1ce2e2f")];
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void ObjectConverterPreservesSystemTextJsonValues(object value)
    {
        var expected = JsonSerializer.Serialize(value, value.GetType());
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            new ObjectValueConverter().Write(writer, value, JsonSerializerOptions.Default);
        Assert.Equal(expected, Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void CyclicContainerFailsWithJsonException()
    {
        var cyclic = new ArrayList(); cyclic.Add(cyclic);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        Assert.Throws<JsonException>(() => new ObjectValueConverter().Write(writer, cyclic, JsonSerializerOptions.Default));
    }
}
