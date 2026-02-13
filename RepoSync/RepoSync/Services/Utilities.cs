using System.Security.Cryptography;

namespace RepoSync.Services;

/// <summary>
/// 鏃ュ織鏈嶅姟 - 瀵瑰簲 shell 鑴氭湰涓殑 log() 鍑芥暟
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();

    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var line = $"[{timestamp}] {message}";
        lock (_lock)
        {
            Console.WriteLine(line);
        }
    }

    public static void LogSeparator()
    {
        Log("==========================================");
    }
}

/// <summary>
/// 鏂囦欢宸ュ叿 - 瀵瑰簲 shell 鑴氭湰涓殑 md5sum銆乨u 绛夋搷浣?
/// </summary>
public static class FileUtils
{
    /// <summary>
    /// 璁＄畻鏂囦欢 MD5锛堝搴?md5sum 鍛戒护锛?
    /// </summary>
    public static string ComputeMd5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 璁＄畻鏂囦欢 SHA256
    /// </summary>
    public static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 璁＄畻鐩綍澶у皬锛堝搴?du -sh 鍛戒护锛?
    /// </summary>
    public static string GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return "鐩綍涓虹┖";

        try
        {
            long totalBytes = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
            return FormatSize(totalBytes);
        }
        catch
        {
            return "鐩綍涓虹┖";
        }
    }

    /// <summary>
    /// 鏍煎紡鍖栨枃浠跺ぇ灏忎负浜虹被鍙鏍煎紡
    /// </summary>
    public static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "K", "M", "G", "T"];
        double size = bytes;
        int i = 0;
        while (size >= 1024 && i < suffixes.Length - 1)
        {
            size /= 1024;
            i++;
        }
        return $"{size:F1}{suffixes[i]}";
    }

    /// <summary>
    /// 纭繚鐩綍瀛樺湪锛堝搴?mkdir -p锛?
    /// </summary>
    public static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
