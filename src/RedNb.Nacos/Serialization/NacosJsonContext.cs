using System.Text.Json.Serialization;
using RedNb.Nacos.Administration;
using RedNb.Nacos.Ai.Models.Mcp;
using RedNb.Nacos.Config;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Naming.Models;

namespace RedNb.Nacos.Serialization;

/// <summary>
/// Source-generated serialization metadata for the core library. Property names keep
/// their C# casing on the wire (matching the reflection-based behavior of 2.0.0);
/// deserialization is case-insensitive, as the failover disk cache requires.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ServiceInfo))]
[JsonSerializable(typeof(Instance))]
[JsonSerializable(typeof(List<Instance>))]
[JsonSerializable(typeof(ConfigInfo))]
[JsonSerializable(typeof(NamingSelector))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(McpCapability))]
[JsonSerializable(typeof(NacosNamespace))]
[JsonSerializable(typeof(List<NacosNamespace>))]
public sealed partial class NacosJsonContext : JsonSerializerContext
{
}
