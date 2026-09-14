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
                FlattenYaml(string.Empty, stream.Documents[0].RootNode, result);
            }
        }
        catch (YamlException)
        {
            // 解析失败时返回空字典
        }

        return result;
    }

    /// <summary>
    /// 将 YAML 节点扁平化为点分隔的键值对
    /// </summary>
    /// <param name="prefix">当前前缀</param>
    /// <param name="node">YAML 节点</param>
    /// <param name="result">结果字典</param>
    private static void FlattenYaml(string prefix, YamlNode node, Dictionary<string, string> result)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var kvp in mapping.Children)
                {
                    var keyNode = kvp.Key as YamlScalarNode;
                    if (keyNode is { Value: "<<" })
                    {
                        // YAML merge key: flatten the merged mapping into the current prefix,
                        // matching the Deserializer<object> merge behavior of 2.0.0.
                        FlattenYaml(prefix, kvp.Value, result);
                        continue;
                    }

                    var key = keyNode?.Value ?? kvp.Key.ToString() ?? string.Empty;
                    var newPrefix = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";
                    FlattenYaml(newPrefix, kvp.Value, result);
                }
                break;

            case YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    FlattenYaml($"{prefix}[{i}]", sequence.Children[i], result);
                }
                break;

            case YamlScalarNode scalar:
                // A null scalar flattens to an empty string, matching the reflection-based
                // Deserializer<object> behavior of 2.0.0 (null values became empty strings).
                result[prefix] = IsNullScalar(scalar) ? string.Empty : scalar.Value ?? string.Empty;
                break;
        }
    }

    /// <summary>
    /// 判断标量节点是否表示 null(显式 null 标签,或未加引号的 null 写法)
    /// </summary>
    private static bool IsNullScalar(YamlScalarNode scalar)
    {
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
