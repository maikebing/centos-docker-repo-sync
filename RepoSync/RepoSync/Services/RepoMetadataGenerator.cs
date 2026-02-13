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
/// createrepo 内部会扫描目录下所有 RPM 文件，读取每个文件的 header 信息，
/// 然后生成 primary.xml.gz, filelists.xml.gz, other.xml.gz 和 repomd.xml。
/// 
/// 本实现采用简化策略，只在必要时重新生成元数据：
/// - 当远程仓库已通过 reposync --download-metadata 下载了完整元数据时，
///   本生成器仅验证元数据完整性，不会覆盖远程下载的元数据。
/// 
/// 本实现的局限性：
/// 1. 优先使用远程下载的元数据（更完整准确）
/// 2. 仅在远程元数据缺失时，才根据 RPM 文件名生成基础元数据
/// </summary>
public class RepoMetadataGenerator
{
    private static readonly XNamespace RepoNs = "http://linux.duke.edu/metadata/repo";
    private static readonly XNamespace RpmNs = "http://linux.duke.edu/metadata/rpm";
    private static readonly XNamespace CommonNs = "http://linux.duke.edu/metadata/common";

    /// <summary>
    /// 确保仓库元数据完整，对应 createrepo --update
    /// 如果已有有效的元数据和 RPM 文件则跳过，否则重新生成。
    /// </summary>
    public async Task EnsureMetadata(string repoPath, string repoName)
    {
        var repodataDir = Path.Combine(repoPath, "repodata");
        var repomdPath = Path.Combine(repodataDir, "repomd.xml");

        if (File.Exists(repomdPath))
        {
            // 检测已有元数据引用的文件是否都存在
            if (ValidateExistingMetadata(repomdPath))
            {
                Logger.Log($"[{repoName}] 已有元数据完整有效，无需重新生成");
                return;
            }
        }

        // 需要重新生成元数据
        Logger.Log($"[{repoName}] 开始生成仓库元数据...");
        await GenerateMetadata(repoPath, repoName);
        Logger.Log($"[{repoName}] 数据文件生成完成");
    }

    /// <summary>
    /// 验证已有元数据是否完整有效
    /// </summary>
    private bool ValidateExistingMetadata(string repomdPath)
    {
        try
        {
            var doc = XDocument.Load(repomdPath);
            var dataElements = doc.Root?.Elements(RepoNs + "data");
            if (dataElements == null) return false;

            // 检查所有元数据引用的文件是否都存在
            var repoDataDir = Path.GetDirectoryName(repomdPath)!;
            foreach (var data in dataElements)
            {
                var href = data.Element(RepoNs + "location")?.Attribute("href")?.Value;
                if (href == null) continue;

                var repoRoot = Path.GetDirectoryName(repoDataDir)!;
                var fullPath = Path.Combine(repoRoot, href.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    Logger.Log($"数据文件缺失: {href}");
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
    /// 从 RPM 文件生成仓库元数据
    /// 相当于 createrepo 的简化实现:
    /// - 扫描 Packages/ 目录下的 .rpm 文件
    /// - 生成 primary.xml.gz（包含包名、版本、大小、校验和等基本信息）
    /// - 生成 repomd.xml（元数据索引）
    /// 
    /// 注意：本实现无法完整替代 createrepo，因为需要读取 RPM header 信息，
    /// 但对于 yum 基本功能已够用。
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

        // 压缩保存为 primary.xml.gz
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

            // 尝试从文件名解析包信息，格式为 name-version-release.arch.rpm
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

        // 尝试识别 arch 部分
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

        // 按 '-' 从后往前拆分，最后两段分别是 version 和 release
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
    /// 计算字符串内容的 SHA256
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
