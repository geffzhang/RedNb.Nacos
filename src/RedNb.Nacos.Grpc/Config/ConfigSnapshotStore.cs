using RedNb.Nacos;
namespace RedNb.Nacos.Grpc.Config;

/// <summary>
/// Local config cache for failover.
/// </summary>
internal class ConfigSnapshotStore
{
    private readonly string _cacheDir;

    public ConfigSnapshotStore(NacosClientOptions options)
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "nacos", "config", RedNb.Nacos.Utils.CacheIdentity.For(options));

        if (!Directory.Exists(_cacheDir))
        {
            Directory.CreateDirectory(_cacheDir);
        }
    }

    public void SaveSnapshot(string dataId, string group, string content)
    {
        try
        {
            var fileName = GetFileName(dataId, group);
            var filePath = Path.Combine(_cacheDir, fileName);
            File.WriteAllText(filePath, content);
        }
        catch
        {
            // Ignore cache save errors
        }
    }

    public string? GetSnapshot(string dataId, string group)
    {
        try
        {
            var fileName = GetFileName(dataId, group);
            var filePath = Path.Combine(_cacheDir, fileName);

            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
        }
        catch
        {
            // Ignore cache read errors
        }

        return null;
    }

    public void RemoveSnapshot(string dataId, string group)
    {
        try
        {
            var fileName = GetFileName(dataId, group);
            var filePath = Path.Combine(_cacheDir, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore cache delete errors
        }
    }

    private static string GetFileName(string dataId, string group)
    {
        return $"{group}@@{dataId}".Replace("/", "_").Replace("\\", "_");
    }
}
