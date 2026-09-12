using System.Security.Cryptography;
using System.Text;

namespace RedNb.Nacos.Utils;

internal static class CacheIdentity
{
    public static string For(NacosClientOptions options)
    {
        var servers = string.Join(",", options.GetServerAddressList().Order(StringComparer.Ordinal));
        var identity = $"{servers}\n{options.ContextPath}\n{options.Namespace ?? "public"}\n{options.Username}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}
