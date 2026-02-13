namespace RepoSync.Models;

/// <summary>
/// 仓库同步配置
/// </summary>
public class SyncConfig
{
    /// <summary>同步间隔（秒），默认 86400（24小时）</summary>
    public int SyncIntervalSeconds { get; set; } = 86400;

    /// <summary>下载并发数</summary>
    public int MaxConcurrentDownloads { get; set; } = 5;

    /// <summary>HTTP 请求超时（秒）</summary>
    public int HttpTimeoutSeconds { get; set; } = 300;

    /// <summary>CentOS 仓库配置列表</summary>
    public List<RepoDefinition> CentOSRepos { get; set; } = new();

    /// <summary>Docker CE 仓库配置</summary>
    public RepoDefinition? DockerRepo { get; set; }

    /// <summary>EPEL 仓库配置</summary>
    public RepoDefinition? EpelRepo { get; set; }
}

/// <summary>
/// 单个仓库定义
/// </summary>
public class RepoDefinition
{
    /// <summary>仓库名称（用于日志）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>远程基础 URL</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>本地存储目录</summary>
    public string LocalPath { get; set; } = string.Empty;
}
