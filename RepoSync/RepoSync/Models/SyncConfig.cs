namespace RepoSync.Models;

/// <summary>
/// 仓库同步配置
/// </summary>
public class SyncConfig
{
    /// <summary>同步间隔秒数，默认 86400（24小时）</summary>
    public int SyncIntervalSeconds { get; set; } = 86400;

    /// <summary>最大并发下载数</summary>
    public int MaxConcurrentDownloads { get; set; } = 5;

    /// <summary>HTTP 请求超时秒数</summary>
    public int HttpTimeoutSeconds { get; set; } = 300;

    /// <summary>CentOS 仓库定义列表</summary>
    public List<RepoDefinition> CentOSRepos { get; set; } = new();

    /// <summary>Docker CE 仓库定义</summary>
    public RepoDefinition? DockerRepo { get; set; }

    /// <summary>EPEL 仓库定义</summary>
    public RepoDefinition? EpelRepo { get; set; }
}

/// <summary>
/// 单个仓库的定义信息
/// </summary>
public class RepoDefinition
{
    /// <summary>仓库名称，用于日志标识</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>远程仓库基础 URL</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>本地存储路径</summary>
    public string LocalPath { get; set; } = string.Empty;
}
