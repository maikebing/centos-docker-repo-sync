using System.Collections.Concurrent;

namespace RepoSync.Services;

/// <summary>
/// 本地文件缓存索引 - 扫描所有本地仓库目录中的 RPM 文件，
/// 按文件大小建立索引，需要时再计算校验和精确匹配。
/// 
/// 当同步新仓库时，如果某个包在其他仓库中已存在（checksum 相同），
/// 直接本地复制，避免重复从网络下载。
/// </summary>
public class LocalFileCache
{
    /// <summary>
    /// 文件大小 -> 文件路径列表（大小相同的文件可能有多个）
    /// </summary>
    private readonly Dictionary<long, List<string>> _sizeIndex = new();

    /// <summary>
    /// 已计算过的 checksum 缓存：文件路径 -> sha256
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _checksumCache = new();

    /// <summary>
    /// 索引中的文件总数
    /// </summary>
    public int FileCount { get; private set; }

    /// <summary>
    /// 扫描指定目录列表，建立本地文件大小索引
    /// </summary>
    public void BuildIndex(IEnumerable<string> scanDirectories)
    {
        _sizeIndex.Clear();
        _checksumCache.Clear();
        FileCount = 0;

        foreach (var dir in scanDirectories)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.rpm", SearchOption.AllDirectories))
                {
                    try
                    {
                        var size = new FileInfo(file).Length;
                        if (!_sizeIndex.TryGetValue(size, out var list))
                        {
                            list = new List<string>();
                            _sizeIndex[size] = list;
                        }
                        list.Add(file);
                        FileCount++;
                    }
                    catch
                    {
                        // 忽略无法访问的文件
                    }
                }
            }
            catch
            {
                // 忽略无法访问的目录
            }
        }

        Logger.Log($"本地文件缓存索引已建立，共 {FileCount} 个 RPM 文件");
    }

    /// <summary>
    /// 根据文件大小和校验和，尝试在本地缓存中找到匹配的文件。
    /// 先按大小筛选候选文件（O(1)），再按需计算 SHA256 精确匹配。
    /// </summary>
    /// <param name="expectedSize">期望的文件大小</param>
    /// <param name="expectedChecksum">期望的校验和</param>
    /// <param name="checksumType">校验和类型（sha256 等）</param>
    /// <returns>匹配的本地文件路径，未找到则返回 null</returns>
    public string? FindMatchingFile(long expectedSize, string expectedChecksum, string checksumType = "sha256")
    {
        if (string.IsNullOrEmpty(expectedChecksum) || expectedSize <= 0)
            return null;

        // 按大小快速筛选
        if (!_sizeIndex.TryGetValue(expectedSize, out var candidates))
            return null;

        // 对候选文件逐一验证校验和
        foreach (var filePath in candidates)
        {
            if (!File.Exists(filePath)) continue;

            try
            {
                var checksum = GetOrComputeChecksum(filePath, checksumType);
                if (checksum == expectedChecksum)
                    return filePath;
            }
            catch
            {
                // 忽略计算失败的文件
            }
        }

        return null;
    }

    /// <summary>
    /// 获取或计算文件的校验和（带缓存）
    /// </summary>
    private string GetOrComputeChecksum(string filePath, string checksumType)
    {
        var cacheKey = $"{checksumType}:{filePath}";
        return _checksumCache.GetOrAdd(cacheKey, _ =>
        {
            return checksumType.ToLower() switch
            {
                "sha256" => FileUtils.ComputeSha256(filePath),
                "md5" => FileUtils.ComputeMd5(filePath),
                _ => FileUtils.ComputeSha256(filePath)
            };
        });
    }

    /// <summary>
    /// 将新下载的文件注册到索引中，供后续仓库同步时复用
    /// </summary>
    public void RegisterFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var size = new FileInfo(filePath).Length;
            lock (_sizeIndex)
            {
                if (!_sizeIndex.TryGetValue(size, out var list))
                {
                    list = new List<string>();
                    _sizeIndex[size] = list;
                }
                if (!list.Contains(filePath))
                {
                    list.Add(filePath);
                    FileCount++;
                }
            }
        }
        catch
        {
            // 忽略
        }
    }
}
