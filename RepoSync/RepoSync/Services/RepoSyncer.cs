using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using RepoSync.Models;
using SharpCompress.Compressors.Xz;

namespace RepoSync.Services;

/// <summary>
/// RPM 仓库同步器 - 替代 reposync 命令
/// 
/// 原理：
/// 1. 下载 repodata/repomd.xml 获取元数据文件列表
/// 2. 从 repomd.xml 中找到 primary.xml.gz 的位置
/// 3. 下载并解析 primary.xml.gz，获取所有 RPM 包的信息
/// 4. 对比本地已有文件，仅下载缺失或校验不通过的包
/// 5. 保存元数据文件
/// </summary>
public class RepoSyncer
{
    private readonly HttpClient _httpClient;
    private readonly int _maxConcurrent;

    // repomd.xml 中常用的 XML 命名空间
    private static readonly XNamespace RepoNs = "http://linux.duke.edu/metadata/repo";
    private static readonly XNamespace RpmNs = "http://linux.duke.edu/metadata/rpm";
    private static readonly XNamespace CommonNs = "http://linux.duke.edu/metadata/common";

    public RepoSyncer(HttpClient httpClient, int maxConcurrentDownloads = 5)
    {
        _httpClient = httpClient;
        _maxConcurrent = maxConcurrentDownloads;
    }

    /// <summary>
    /// 同步一个仓库（替代 reposync + createrepo）
    /// </summary>
    /// <param name="baseUrl">远程仓库 baseurl（如 https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/os/x86_64）</param>
    /// <param name="localPath">本地目标目录</param>
    /// <param name="repoName">仓库名称（用于日志）</param>
    public async Task SyncRepository(string baseUrl, string localPath, string repoName)
    {
        Logger.Log($"开始同步 {repoName} 仓库...");

        // 确保目录存在
        FileUtils.EnsureDirectory(localPath);
        FileUtils.EnsureDirectory(Path.Combine(localPath, "Packages"));
        FileUtils.EnsureDirectory(Path.Combine(localPath, "repodata"));

        // 步骤1: 下载并解析 repomd.xml
        Logger.Log($"[{repoName}] 下载 repomd.xml...");
        var repomdUrl = $"{baseUrl.TrimEnd('/')}/repodata/repomd.xml";
        var repomdContent = await _httpClient.GetStringAsync(repomdUrl);
        var repomdDoc = XDocument.Parse(repomdContent);

        // 保存 repomd.xml
        var repomdLocalPath = Path.Combine(localPath, "repodata", "repomd.xml");
        await File.WriteAllTextAsync(repomdLocalPath, repomdContent);
        Logger.Log($"[{repoName}] repomd.xml 已保存");

        // 步骤2: 下载所有元数据文件（primary, filelists, other, comps 等）
        await DownloadMetadataFiles(repomdDoc, baseUrl, localPath, repoName);

        // 步骤3: 从 primary.xml 中解析包列表
        var primaryHref = GetDataHref(repomdDoc, "primary");
        if (primaryHref == null)
        {
            Logger.Log($"[{repoName}] 警告: 无法找到 primary 元数据");
            return;
        }

        var primaryLocalPath = Path.Combine(localPath, primaryHref);
        if (!File.Exists(primaryLocalPath))
        {
            Logger.Log($"[{repoName}] 警告: primary 元数据文件不存在");
            return;
        }

        Logger.Log($"[{repoName}] 解析包列表...");
        var packages = ParsePrimaryXml(primaryLocalPath);
        Logger.Log($"[{repoName}] 共发现 {packages.Count} 个包");

        // 步骤4: 下载缺失的 RPM 包
        await DownloadPackages(packages, baseUrl, localPath, repoName);

        Logger.Log($"[{repoName}] 同步完成");
    }

    /// <summary>
    /// 下载 repomd.xml 中引用的所有元数据文件
    /// </summary>
    private async Task DownloadMetadataFiles(XDocument repomd, string baseUrl, string localPath, string repoName)
    {
        var dataElements = repomd.Root?.Elements(RepoNs + "data") ?? Enumerable.Empty<XElement>();

        foreach (var dataElement in dataElements)
        {
            var type = dataElement.Attribute("type")?.Value ?? "unknown";
            var locationElement = dataElement.Element(RepoNs + "location");
            var href = locationElement?.Attribute("href")?.Value;

            if (href == null) continue;

            var remoteUrl = $"{baseUrl.TrimEnd('/')}/{href}";
            var localFile = Path.Combine(localPath, href.Replace('/', Path.DirectorySeparatorChar));

            // 检查是否已存在且校验和匹配
            var checksumElement = dataElement.Element(RepoNs + "checksum");
            var expectedChecksum = checksumElement?.Value;

            if (File.Exists(localFile) && expectedChecksum != null)
            {
                var localChecksum = FileUtils.ComputeSha256(localFile);
                if (localChecksum == expectedChecksum)
                {
                    Logger.Log($"[{repoName}] 元数据 {type} 已是最新，跳过");
                    continue;
                }
            }

            Logger.Log($"[{repoName}] 下载元数据: {type} ({href})");
            FileUtils.EnsureDirectory(Path.GetDirectoryName(localFile)!);

            try
            {
                var data = await _httpClient.GetByteArrayAsync(remoteUrl);
                await File.WriteAllBytesAsync(localFile, data);
            }
            catch (Exception ex)
            {
                Logger.Log($"[{repoName}] 警告: 下载 {type} 失败: {ex.Message}");
            }
        }

        // 下载 comps.xml（如果在 repomd 同级目录有 comps 文件的引用）
        // 某些仓库的 comps 文件不在 repodata 目录下
        var groupElement = repomd.Root?.Elements(RepoNs + "data")
            .FirstOrDefault(e => e.Attribute("type")?.Value == "group");
        if (groupElement != null)
        {
            var groupHref = groupElement.Element(RepoNs + "location")?.Attribute("href")?.Value;
            if (groupHref != null && !groupHref.StartsWith("repodata/"))
            {
                var remoteUrl = $"{baseUrl.TrimEnd('/')}/{groupHref}";
                var localFile = Path.Combine(localPath, groupHref.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(localFile))
                {
                    Logger.Log($"[{repoName}] 下载 comps/group 文件: {groupHref}");
                    FileUtils.EnsureDirectory(Path.GetDirectoryName(localFile)!);
                    try
                    {
                        var data = await _httpClient.GetByteArrayAsync(remoteUrl);
                        await File.WriteAllBytesAsync(localFile, data);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[{repoName}] 警告: 下载 comps 失败: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 从 repomd.xml 中获取指定类型的数据文件路径
    /// </summary>
    private string? GetDataHref(XDocument repomd, string dataType)
    {
        return repomd.Root?.Elements(RepoNs + "data")
            .FirstOrDefault(e => e.Attribute("type")?.Value == dataType)
            ?.Element(RepoNs + "location")
            ?.Attribute("href")
            ?.Value;
    }

    /// <summary>
    /// 解析 primary.xml(.gz/.xz) 文件，提取所有包信息
    /// 这是 reposync 的核心功能之一
    /// </summary>
    private List<RpmPackageInfo> ParsePrimaryXml(string primaryPath)
    {
        var packages = new List<RpmPackageInfo>();

        // 根据文件扩展名选择解压方式
        Stream xmlStream;
        Stream fileStream = File.OpenRead(primaryPath);

        if (primaryPath.EndsWith(".gz"))
        {
            xmlStream = new GZipStream(fileStream, CompressionMode.Decompress);
        }
        else if (primaryPath.EndsWith(".xz"))
        {
            xmlStream = new XZStream(fileStream);
        }
        else
        {
            xmlStream = fileStream;
        }

        try
        {
            var doc = XDocument.Load(xmlStream);
            var packageElements = doc.Root?.Elements(CommonNs + "package") ?? Enumerable.Empty<XElement>();

            foreach (var pkg in packageElements)
            {
                var info = new RpmPackageInfo();

                info.Name = pkg.Element(CommonNs + "name")?.Value ?? "";
                info.Arch = pkg.Element(CommonNs + "arch")?.Value ?? "";
                info.Summary = pkg.Element(CommonNs + "summary")?.Value ?? "";
                info.Description = pkg.Element(CommonNs + "description")?.Value ?? "";
                info.Packager = pkg.Element(CommonNs + "packager")?.Value ?? "";
                info.Url = pkg.Element(CommonNs + "url")?.Value ?? "";

                // 版本信息
                var versionEl = pkg.Element(CommonNs + "version");
                if (versionEl != null)
                {
                    info.Epoch = versionEl.Attribute("epoch")?.Value ?? "0";
                    info.Version = versionEl.Attribute("ver")?.Value ?? "";
                    info.Release = versionEl.Attribute("rel")?.Value ?? "";
                }

                // 校验和
                var checksumEl = pkg.Element(CommonNs + "checksum");
                if (checksumEl != null)
                {
                    info.Checksum = checksumEl.Value;
                    info.ChecksumType = checksumEl.Attribute("type")?.Value ?? "sha256";
                }

                // 位置（包的相对路径）
                var locationEl = pkg.Element(CommonNs + "location");
                info.LocationHref = locationEl?.Attribute("href")?.Value ?? "";

                // 大小信息
                var sizeEl = pkg.Element(CommonNs + "size");
                if (sizeEl != null)
                {
                    info.PackageSize = long.TryParse(sizeEl.Attribute("package")?.Value, out var ps) ? ps : 0;
                    info.InstalledSize = long.TryParse(sizeEl.Attribute("installed")?.Value, out var ins) ? ins : 0;
                    info.ArchiveSize = long.TryParse(sizeEl.Attribute("archive")?.Value, out var arc) ? arc : 0;
                }

                // 时间信息
                var timeEl = pkg.Element(CommonNs + "time");
                if (timeEl != null)
                {
                    info.FileTime = long.TryParse(timeEl.Attribute("file")?.Value, out var ft) ? ft : 0;
                    info.BuildTime = long.TryParse(timeEl.Attribute("build")?.Value, out var bt) ? bt : 0;
                }

                // 格式信息
                var formatEl = pkg.Element(CommonNs + "format");
                if (formatEl != null)
                {
                    info.License = formatEl.Element(RpmNs + "license")?.Value ?? "";
                    info.Vendor = formatEl.Element(RpmNs + "vendor")?.Value ?? "";
                    info.Group = formatEl.Element(RpmNs + "group")?.Value ?? "";
                    info.SourceRpm = formatEl.Element(RpmNs + "sourcerpm")?.Value ?? "";

                    var headerRange = formatEl.Element(RpmNs + "header-range");
                    if (headerRange != null)
                    {
                        info.HeaderStart = long.TryParse(headerRange.Attribute("start")?.Value, out var hs) ? hs : 0;
                        info.HeaderEnd = long.TryParse(headerRange.Attribute("end")?.Value, out var he) ? he : 0;
                    }
                }

                packages.Add(info);
            }
        }
        finally
        {
            xmlStream.Dispose();
            if (xmlStream != fileStream)
                fileStream.Dispose();
        }

        return packages;
    }

    /// <summary>
    /// 并发下载 RPM 包，跳过已存在且校验通过的文件
    /// 替代 reposync 的核心下载功能
    /// </summary>
    private async Task DownloadPackages(List<RpmPackageInfo> packages, string baseUrl, string localPath, string repoName)
    {
        // 筛选需要下载的包
        var toDownload = new List<RpmPackageInfo>();
        int skipped = 0;

        foreach (var pkg in packages)
        {
            var localFile = Path.Combine(localPath, pkg.LocationHref.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localFile))
            {
                // 检查文件大小是否匹配
                var fileInfo = new FileInfo(localFile);
                if (fileInfo.Length == pkg.PackageSize)
                {
                    skipped++;
                    continue;
                }
            }
            toDownload.Add(pkg);
        }

        Logger.Log($"[{repoName}] 需要下载: {toDownload.Count} 个包，跳过: {skipped} 个已存在的包");

        if (toDownload.Count == 0) return;

        // 使用 SemaphoreSlim 控制并发数
        var semaphore = new SemaphoreSlim(_maxConcurrent);
        int downloaded = 0;
        int failed = 0;
        var lockObj = new object();

        var tasks = toDownload.Select(async pkg =>
        {
            await semaphore.WaitAsync();
            try
            {
                var remoteUrl = $"{baseUrl.TrimEnd('/')}/{pkg.LocationHref}";
                var localFile = Path.Combine(localPath, pkg.LocationHref.Replace('/', Path.DirectorySeparatorChar));
                FileUtils.EnsureDirectory(Path.GetDirectoryName(localFile)!);

                // 下载文件
                var response = await _httpClient.GetAsync(remoteUrl);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(localFile, data);

                lock (lockObj)
                {
                    downloaded++;
                    if (downloaded % 50 == 0 || downloaded == toDownload.Count)
                    {
                        Logger.Log($"[{repoName}] 下载进度: {downloaded}/{toDownload.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                lock (lockObj)
                {
                    failed++;
                }
                Logger.Log($"[{repoName}] 下载失败: {pkg.Name} - {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        Logger.Log($"[{repoName}] 下载完成 - 成功: {downloaded}, 失败: {failed}");
    }
}
