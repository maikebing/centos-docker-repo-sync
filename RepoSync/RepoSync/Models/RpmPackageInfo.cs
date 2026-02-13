namespace RepoSync.Models;

/// <summary>
/// �� primary.xml �������� RPM ����Ϣ
/// </summary>
public class RpmPackageInfo
{
    /// <summary>����</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>�ܹ�</summary>
    public string Arch { get; set; } = string.Empty;

    /// <summary>�汾��</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Release</summary>
    public string Release { get; set; } = string.Empty;

    /// <summary>Epoch</summary>
    public string Epoch { get; set; } = "0";

    /// <summary>������ baseurl ���ļ�·������ Packages/xxx.rpm</summary>
    public string LocationHref { get; set; } = string.Empty;

    /// <summary>���ļ���С���ֽڣ�</summary>
    public long PackageSize { get; set; }

    /// <summary>���� SHA256 У����</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>У�������ͣ��� sha256 ��</summary>
    public string ChecksumType { get; set; } = "sha256";

    /// <summary>Summary ժҪ</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>��ϸ����</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>������/ά����</summary>
    public string Packager { get; set; } = string.Empty;

    /// <summary>URL</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>����ʱ�䣨Unix ʱ������</summary>
    public long BuildTime { get; set; }

    /// <summary>��װ����С</summary>
    public long InstalledSize { get; set; }

    /// <summary>�鵵��С</summary>
    public long ArchiveSize { get; set; }

    /// <summary>����֤</summary>
    public string License { get; set; } = string.Empty;

    /// <summary>����/ά����</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>����</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>Դ RPM ����</summary>
    public string SourceRpm { get; set; } = string.Empty;

    /// <summary>Header ��ʼƫ��</summary>
    public long HeaderStart { get; set; }

    /// <summary>Header ����ƫ��</summary>
    public long HeaderEnd { get; set; }

    /// <summary>�ļ��޸�ʱ����</summary>
    public long FileTime { get; set; }
}
