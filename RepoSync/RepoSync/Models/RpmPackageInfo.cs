namespace RepoSync.Models;

/// <summary>
/// 从 primary.xml 解析出的 RPM 包信息
/// </summary>
public class RpmPackageInfo
{
    /// <summary>包名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>架构</summary>
    public string Arch { get; set; } = string.Empty;

    /// <summary>版本号</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Release</summary>
    public string Release { get; set; } = string.Empty;

    /// <summary>Epoch</summary>
    public string Epoch { get; set; } = "0";

    /// <summary>相对于 baseurl 的文件路径，如 Packages/xxx.rpm</summary>
    public string LocationHref { get; set; } = string.Empty;

    /// <summary>包文件大小（字节）</summary>
    public long PackageSize { get; set; }

    /// <summary>包的 SHA256 校验和</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>校验和类型，如 sha256 等</summary>
    public string ChecksumType { get; set; } = "sha256";

    /// <summary>Summary 摘要</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>详细描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>打包者/维护者</summary>
    public string Packager { get; set; } = string.Empty;

    /// <summary>URL</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>构建时间（Unix 时间戳）</summary>
    public long BuildTime { get; set; }

    /// <summary>安装后大小</summary>
    public long InstalledSize { get; set; }

    /// <summary>归档大小</summary>
    public long ArchiveSize { get; set; }

    /// <summary>许可证</summary>
    public string License { get; set; } = string.Empty;

    /// <summary>厂商/维护者</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>分组</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>源 RPM 包名</summary>
    public string SourceRpm { get; set; } = string.Empty;

    /// <summary>Header 起始偏移</summary>
    public long HeaderStart { get; set; }

    /// <summary>Header 结束偏移</summary>
    public long HeaderEnd { get; set; }

    /// <summary>文件修改时间戳</summary>
    public long FileTime { get; set; }
}
