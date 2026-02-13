using RepoSync.Models;
using RepoSync.Services;

namespace RepoSync;

/// <summary>
/// 主同步编排器 - 对应 sync-repos.sh 的 while true 主循环
/// 
/// 编排所有仓库的同步流程：
/// 1. CentOS 7.9.2009 的 os/updates/extras 仓库
/// 2. Docker CE 仓库
/// 3. EPEL 仓库
/// </summary>
public class SyncOrchestrator
{
    private readonly SyncConfig _config;
    private readonly HttpClient _httpClient;
    private readonly RepomdChecker _repomdChecker;
    private readonly RepoSyncer _repoSyncer;
    private readonly RepoMetadataGenerator _metadataGenerator;

    public SyncOrchestrator(SyncConfig config)
    {
        _config = config;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RepoSync/1.0 (CentOS Mirror Sync)");
        _repomdChecker = new RepomdChecker(_httpClient);
        _repoSyncer = new RepoSyncer(_httpClient, config.MaxConcurrentDownloads);
        _metadataGenerator = new RepoMetadataGenerator();
    }

    /// <summary>
    /// 启动同步循环（对应 shell: while true; do ... sleep ${SYNC_INTERVAL}; done）
    /// </summary>
    public async Task RunLoop(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnce();
            }
            catch (Exception ex)
            {
                Logger.Log($"同步过程中出现未处理的错误: {ex.Message}");
                Logger.Log(ex.StackTrace ?? "");
            }

            Logger.Log($"下次同步将在 {_config.SyncIntervalSeconds / 3600} 小时后执行...");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.SyncIntervalSeconds), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                Logger.Log("同步已取消");
                break;
            }
        }
    }

    /// <summary>
    /// 执行一次完整同步
    /// </summary>
    public async Task RunOnce()
    {
        Logger.LogSeparator();
        Logger.Log("开始同步仓库");
        Logger.LogSeparator();

        // =============================================
        // 同步 CentOS 7.9.2009 仓库
        // （对应 shell: for repo in "${!REPO_MAP[@]}"; do ... done）
        // =============================================
        Logger.Log("正在同步 CentOS 7.9.2009 仓库...");

        foreach (var repo in _config.CentOSRepos)
        {
            var remoteRepomd = $"{repo.BaseUrl.TrimEnd('/')}/repodata/repomd.xml";
            var localRepomd = Path.Combine(repo.LocalPath, "repodata", "repomd.xml");

            Logger.Log($"检查 {repo.Name} 仓库是否有更新...");

            if (!await _repomdChecker.HasChanged(remoteRepomd, localRepomd))
            {
                Logger.Log($"{repo.Name} 仓库未变化，跳过同步");
                continue;
            }

            // 同步仓库（下载包 + 元数据）
            await _repoSyncer.SyncRepository(repo.BaseUrl, repo.LocalPath, repo.Name);

            // 确保元数据完整
            await _metadataGenerator.EnsureMetadata(repo.LocalPath, repo.Name);
        }

        // =============================================
        // 同步 Docker CE 仓库
        // （对应 shell: Docker CE 同步段落）
        // =============================================
        if (_config.DockerRepo != null)
        {
            var dockerRepo = _config.DockerRepo;
            var dockerRepomd = $"{dockerRepo.BaseUrl.TrimEnd('/')}/repodata/repomd.xml";
            var dockerLocalRepomd = Path.Combine(dockerRepo.LocalPath, "repodata", "repomd.xml");

            Logger.Log("检查 Docker CE 仓库是否有更新...");

            if (await _repomdChecker.HasChanged(dockerRepomd, dockerLocalRepomd))
            {
                Logger.Log("正在同步 Docker CE 仓库...");
                await _repoSyncer.SyncRepository(dockerRepo.BaseUrl, dockerRepo.LocalPath, dockerRepo.Name);
                await _metadataGenerator.EnsureMetadata(dockerRepo.LocalPath, dockerRepo.Name);
            }
            else
            {
                Logger.Log("Docker CE 仓库未变化，跳过同步");
            }
        }

        // =============================================
        // 同步 EPEL 仓库
        // （对应 shell: EPEL 同步段落）
        // =============================================
        if (_config.EpelRepo != null)
        {
            var epelRepo = _config.EpelRepo;
            var epelRepomd = $"{epelRepo.BaseUrl.TrimEnd('/')}/repodata/repomd.xml";
            var epelLocalRepomd = Path.Combine(epelRepo.LocalPath, "repodata", "repomd.xml");

            Logger.Log("检查 EPEL 仓库是否有更新...");

            if (await _repomdChecker.HasChanged(epelRepomd, epelLocalRepomd))
            {
                Logger.Log("正在同步 EPEL 仓库...");
                await _repoSyncer.SyncRepository(epelRepo.BaseUrl, epelRepo.LocalPath, epelRepo.Name);
                await _metadataGenerator.EnsureMetadata(epelRepo.LocalPath, epelRepo.Name);
            }
            else
            {
                Logger.Log("EPEL 仓库未变化，跳过同步");
            }
        }

        Logger.LogSeparator();
        Logger.Log("同步完成");
        Logger.LogSeparator();

        // 显示磁盘使用情况（对应 shell: du -sh）
        foreach (var repo in _config.CentOSRepos)
        {
            Logger.Log($"{repo.Name} 目录大小: {FileUtils.GetDirectorySize(repo.LocalPath)}");
        }
        if (_config.DockerRepo != null)
        {
            Logger.Log($"Docker CE 目录大小: {FileUtils.GetDirectorySize(_config.DockerRepo.LocalPath)}");
        }
        if (_config.EpelRepo != null)
        {
            Logger.Log($"EPEL 目录大小: {FileUtils.GetDirectorySize(_config.EpelRepo.LocalPath)}");
        }

        Logger.LogSeparator();
    }
}
