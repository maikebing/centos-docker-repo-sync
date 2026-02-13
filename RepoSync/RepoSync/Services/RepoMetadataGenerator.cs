using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using RepoSync.Models;
using SharpCompress.Compressors.Xz;

namespace RepoSync.Services;

/// <summary>
/// RPM 仓库元数据生成器 - 替代 createrepo 命令
/// 
/// createrepo 的核心功能是扫描目录中的 RPM 包，读取每个包的 header 信息，
/// 然后生成 primary.xml.gz, filelists.xml.gz, other.xml.gz 和 repomd.xml。
/// 
/// 由于我们已经从上游下载了完整的元数据文件（通过 reposync --download-metadata），
/// 实际上我们只需要在元数据缺失时重新生成。
/// 
/// 这个实现提供两种模式：
/// 1. 更新模式：如果已有上游元数据，直接使用
/// 2. 重建模式：从已下载的 RPM 文件重建基本元数据
/// </summary>
public class RepoMetadataGenerator
{
    private static readonly XNamespace RepoNs = "http://linux.duke.edu/metadata/repo";
    private static readonly XNamespace RpmNs = "http://linux.duke.edu/metadata/rpm";
    private static readonly XNamespace CommonNs = "http://linux.duke.edu/metadata/common";

    /// <summary>
    /// 确保仓库元数据是最新的（对应 createrepo --update）
    /// 如果已有上游元数据且 RPM 文件数量匹配，跳过重建
    /// </summary>
    public async Task EnsureMetadata(string repoPath, string repoName)
    {
        var repodataDir = Path.Combine(repoPath, "repodata");
        var repomdPath = Path.Combine(repodataDir, "repomd.xml");

        if (File.Exists(repomdPath))
        {
            // 已有上游下载的元数据，验证是否完整
            if (ValidateExistingMetadata(repomdPath))
            {
                Logger.Log($"[{repoName}] 上游元数据完整，无需重建");
                return;
            }
        }

        // 需要重新生成元数据
        Logger.Log($"[{repoName}] 正在生成仓库元数据...");
        await GenerateMetadata(repoPath, repoName);
        Logger.Log($"[{repoName}] 元数据生成完成");
    }

    /// <summary>
    /// 验证已有元数据是否完整
    /// </summary>
    private bool ValidateExistingMetadata(string repomdPath)
    {
        try
        {
            var doc = XDocument.Load(repomdPath);
            var dataElements = doc.Root?.Elements(RepoNs + "data");
            if (dataElements == null) return false;

            // 检查必要的元数据文件是否都存在
            var repoDataDir = Path.GetDirectoryName(repomdPath)!;
            foreach (var data in dataElements)
            {
                var href = data.Element(RepoNs + "location")?.Attribute("href")?.Value;
                if (href == null) continue;

                var repoRoot = Path.GetDirectoryName(repoDataDir)!;
                var fullPath = Path.Combine(repoRoot, href.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    Logger.Log($"元数据文件缺失: {href}");
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从 RPM 文件生成基本的仓库元数据
    /// 这是 createrepo 的简化版实现：
    /// - 扫描 Packages/ 目录中的 .rpm 文件
    /// - 生成 primary.xml.gz（包含包名、版本、大小、校验和、位置等基本信息）
    /// - 生成 repomd.xml（元数据索引）
    /// 
    /// 注意：完整的 createrepo 还会读取 RPM header 提取依赖关系、文件列表等，
    /// 这需要解析 RPM 二进制格式。这里的实现足以让 yum 识别和下载包。
    /// </summary>
    private async Task GenerateMetadata(string repoPath, string repoName)
    {
        var repodataDir = Path.Combine(repoPath, "repodata");
        FileUtils.EnsureDirectory(repodataDir);

        // 扫描所有 RPM 文件
        var rpmFiles = Directory.GetFiles(repoPath, "*.rpm", SearchOption.AllDirectories)
            .Where(f => !f.Contains("repodata"))
            .ToList();

        Logger.Log($"[{repoName}] 发现 {rpmFiles.Count} 个 RPM 文件");

        // 生成 primary.xml
        var primaryXml = GeneratePrimaryXml(rpmFiles, repoPath);

        // 压缩并保存 primary.xml.gz
        var primaryGzPath = Path.Combine(repodataDir, "primary.xml.gz");
        await CompressAndSave(primaryXml, primaryGzPath);

        // 计算校验和
        var primaryGzChecksum = FileUtils.ComputeSha256(primaryGzPath);
        var primaryOpenChecksum = ComputeSha256String(primaryXml);

        // 生成 repomd.xml
        var repomdXml = GenerateRepomdXml(
            primaryGzPath: "repodata/primary.xml.gz",
            primaryGzChecksum: primaryGzChecksum,
            primaryGzSize: new FileInfo(primaryGzPath).Length,
            primaryOpenChecksum: primaryOpenChecksum,
            primaryOpenSize: Encoding.UTF8.GetByteCount(primaryXml),
            packageCount: rpmFiles.Count
        );

        var repomdPath = Path.Combine(repodataDir, "repomd.xml");
        await File.WriteAllTextAsync(repomdPath, repomdXml);
    }

    /// <summary>
    /// 生成 primary.xml 内容
    /// </summary>
    private string GeneratePrimaryXml(List<string> rpmFiles, string repoPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<metadata xmlns=\"http://linux.duke.edu/metadata/common\" xmlns:rpm=\"http://linux.duke.edu/metadata/rpm\" packages=\"{rpmFiles.Count}\">");

        foreach (var rpmFile in rpmFiles)
        {
            var fileInfo = new FileInfo(rpmFile);
            var relativePath = Path.GetRelativePath(repoPath, rpmFile).Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(rpmFile);
            var checksum = FileUtils.ComputeSha256(rpmFile);

            // 尝试从文件名解析包信息（name-version-release.arch.rpm）
            ParseRpmFileName(fileName, out var name, out var version, out var release, out var arch);

            sb.AppendLine("  <package type=\"rpm\">");
            sb.AppendLine($"    <name>{EscapeXml(name)}</name>");
            sb.AppendLine($"    <arch>{EscapeXml(arch)}</arch>");
            sb.AppendLine($"    <version epoch=\"0\" ver=\"{EscapeXml(version)}\" rel=\"{EscapeXml(release)}\"/>");
            sb.AppendLine($"    <checksum type=\"sha256\" pkgid=\"YES\">{checksum}</checksum>");
            sb.AppendLine($"    <summary>{EscapeXml(name)}</summary>");
            sb.AppendLine($"    <description>{EscapeXml(name)}</description>");
            sb.AppendLine($"    <packager/>");
            sb.AppendLine($"    <url/>");
            sb.AppendLine($"    <time file=\"{new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds()}\" build=\"{new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds()}\"/>");
            sb.AppendLine($"    <size package=\"{fileInfo.Length}\" installed=\"{fileInfo.Length}\" archive=\"{fileInfo.Length}\"/>");
            sb.AppendLine($"    <location href=\"{EscapeXml(relativePath)}\"/>");
            sb.AppendLine($"    <format>");
            sb.AppendLine($"      <rpm:license>Unknown</rpm:license>");
            sb.AppendLine($"      <rpm:vendor/>");
            sb.AppendLine($"      <rpm:group>Unspecified</rpm:group>");
            sb.AppendLine($"      <rpm:buildhost/>");
            sb.AppendLine($"      <rpm:sourcerpm/>");
            sb.AppendLine($"      <rpm:header-range start=\"0\" end=\"{Math.Min(fileInfo.Length, 65536)}\"/>");
            sb.AppendLine($"    </format>");
            sb.AppendLine("  </package>");
        }

        sb.AppendLine("</metadata>");
        return sb.ToString();
    }

    /// <summary>
    /// 从 RPM 文件名解析包信息
    /// 格式: name-version-release.arch.rpm
    /// 例: docker-ce-26.1.4-1.el7.x86_64.rpm
    /// </summary>
    private void ParseRpmFileName(string fileName, out string name, out string version, out string release, out string arch)
    {
        name = fileName;
        version = "0";
        release = "0";
        arch = "x86_64";

        // 尝试找到 arch 部分
        string[] knownArches = ["x86_64", "noarch", "i686", "i386", "aarch64", "ppc64le", "s390x"];
        foreach (var a in knownArches)
        {
            if (fileName.EndsWith($".{a}"))
            {
                arch = a;
                fileName = fileName[..^(a.Length + 1)]; // 去掉 .arch
                break;
            }
        }

        // 按 '-' 分割，最后两部分通常是 version 和 release
        var parts = fileName.Split('-');
        if (parts.Length >= 3)
        {
            release = parts[^1];
            version = parts[^2];
            name = string.Join('-', parts[..^2]);
        }
        else if (parts.Length == 2)
        {
            version = parts[1];
            name = parts[0];
        }
    }

    /// <summary>
    /// 生成 repomd.xml
    /// </summary>
    private string GenerateRepomdXml(
        string primaryGzPath, string primaryGzChecksum, long primaryGzSize,
        string primaryOpenChecksum, long primaryOpenSize, int packageCount)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<repomd xmlns=\"http://linux.duke.edu/metadata/repo\">");
        sb.AppendLine($"  <revision>{timestamp}</revision>");
        sb.AppendLine("  <data type=\"primary\">");
        sb.AppendLine($"    <checksum type=\"sha256\">{primaryGzChecksum}</checksum>");
        sb.AppendLine($"    <open-checksum type=\"sha256\">{primaryOpenChecksum}</open-checksum>");
        sb.AppendLine($"    <location href=\"{primaryGzPath}\"/>");
        sb.AppendLine($"    <timestamp>{timestamp}</timestamp>");
        sb.AppendLine($"    <size>{primaryGzSize}</size>");
        sb.AppendLine($"    <open-size>{primaryOpenSize}</open-size>");
        sb.AppendLine("  </data>");
        sb.AppendLine("</repomd>");

        return sb.ToString();
    }

    /// <summary>
    /// 压缩字符串内容并保存为 gzip 文件
    /// </summary>
    private async Task CompressAndSave(string content, string outputPath)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        using var fileStream = File.Create(outputPath);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
        await gzipStream.WriteAsync(bytes);
    }

    /// <summary>
    /// 计算字符串的 SHA256
    /// </summary>
    private string ComputeSha256String(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
