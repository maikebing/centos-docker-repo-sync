using System.Security.Cryptography;

namespace RepoSync.Services;

/// <summary>
/// 日志工具类 - 对应 shell 脚本中的 log() 函数
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
/// 文件工具类 - 对应 shell 脚本中的 md5sum、du -sh 等命令
/// </summary>
public static class FileUtils
{
    /// <summary>
    /// 计算文件的 MD5 校验和，对应 md5sum 命令
    /// </summary>
    public static string ComputeMd5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 计算文件的 SHA256 校验和
    /// </summary>
    public static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 计算目录总大小并格式化输出，对应 du -sh 命令
    /// </summary>
    public static string GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return "目录不存在";

        try
        {
            long totalBytes = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
            return FormatSize(totalBytes);
        }
        catch
        {
            return "目录不存在";
        }
    }

    /// <summary>
    /// 将字节数转换为人类可读的大小格式（如 1.5G、256M）
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
    /// 确保目录存在，不存在则创建，对应 mkdir -p
    /// </summary>
    public static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
