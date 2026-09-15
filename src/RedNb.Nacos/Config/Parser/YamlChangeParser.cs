using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace RedNb.Nacos.Config.Parser;

/// <summary>
/// YAML 格式配置变更解析器
/// </summary>
public class YamlChangeParser : AbstractConfigChangeParser
{
    private static readonly string[] SupportedTypes = { "yaml", "yml" };

    /// <inheritdoc />
    public override bool IsSupport(string configType)
    {
        return SupportedTypes.Any(t => t.Equals(configType, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    protected override Dictionary<string, string> ParseToMap(string content)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        try
        {
            // The RepresentationModel parser is reflection-free (AOT-safe), unlike the
            // DeserializerBuilder path used in 2.0.0. It resolves anchors/aliases and
            // flattens to the same dotted key-value map.
            var stream = new YamlStream();
            stream.Load(new StringReader(content));

            if (stream.Documents.Count > 0)
            {
                FlattenYaml(string.Empty, stream.Documents[0].RootNode, result, new HashSet<YamlNode>(ReferenceEqualityComparer.Instance), 0);
            }
        }
        catch (YamlException)
        {
            result.Clear();
        }

        return result;
    }

    /// <summary>
    /// 将 YAML 节点扁平化为点分隔的键值对
    /// </summary>
    /// <param name="prefix">当前前缀</param>
    /// <param name="node">YAML 节点</param>
    /// <param name="result">结果字典</param>
    private static void FlattenYaml(string prefix, YamlNode node, Dictionary<string, string> result, HashSet<YamlNode> active, int depth)
    {
        if (depth > 128 || !active.Add(node)) throw new YamlException("Cyclic or excessively deep YAML aliases.");
        try
        {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var kvp in ResolveMapping(mapping, new HashSet<YamlNode>(ReferenceEqualityComparer.Instance), depth))
                {
                    var newPrefix = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
                    FlattenYaml(newPrefix, kvp.Value, result, active, depth + 1);
                }
                break;

            case YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    FlattenYaml($"{prefix}[{i}]", sequence.Children[i], result, active, depth + 1);
                }
                break;

            case YamlScalarNode scalar:
                // A null scalar flattens to an empty string, matching the reflection-based
                // Deserializer<object> behavior of 2.0.0 (null values became empty strings).
                result[prefix] = IsNullScalar(scalar) ? string.Empty : scalar.Value ?? string.Empty;
                break;
        }
        }
        finally { active.Remove(node); }
    }

    private static bool IsMergeKey(YamlNode key) => key is YamlScalarNode scalar &&
        (scalar.Tag.ToString() == "tag:yaml.org,2002:merge" ||
         scalar.Style == ScalarStyle.Plain && scalar.Value == "<<" && scalar.Tag.ToString() != "tag:yaml.org,2002:str");

    private static Dictionary<string, YamlNode> ResolveMapping(YamlMappingNode mapping, HashSet<YamlNode> active, int depth)
    {
        if (depth > 128 || !active.Add(mapping)) throw new YamlException("Cyclic or excessively deep YAML merge.");
        try
        {
            var result = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
            foreach (var pair in mapping.Children.Where(pair => IsMergeKey(pair.Key)))
            {
                IEnumerable<YamlNode> sources = pair.Value is YamlSequenceNode sequence ? sequence.Children : [pair.Value];
                foreach (var source in sources)
                {
                    if (source is not YamlMappingNode inherited) throw new YamlException("A YAML merge requires mappings.");
                    foreach (var entry in ResolveMapping(inherited, active, depth + 1)) result.TryAdd(entry.Key, entry.Value);
                }
            }
            foreach (var pair in mapping.Children.Where(pair => !IsMergeKey(pair.Key)))
                result[(pair.Key as YamlScalarNode)?.Value ?? pair.Key.ToString()] = pair.Value;
            return result;
        }
        finally { active.Remove(mapping); }
    }

    /// <summary>
    /// 判断标量节点是否表示 null(显式 null 标签,或未加引号的 null 写法)
    /// </summary>
    private static bool IsNullScalar(YamlScalarNode scalar)
    {
        if (scalar.Tag.ToString() == "tag:yaml.org,2002:str") return false;
        if (scalar.Tag.ToString() == "tag:yaml.org,2002:null")
        {
            return true;
        }

        // The RepresentationModel does not reliably resolve plain-scalar tags, so fall
        // back to style + value inspection for the common null spellings. Quoted
        // strings like "null" remain literal strings.
        return scalar.Style == ScalarStyle.Plain && scalar.Value is "" or "null" or "Null" or "NULL" or "~";
    }
}
