using System.Text;
using RepoSync;
using RepoSync.Models;
using RepoSync.Services;

// 强制标准输出使用 UTF-8 编码（无 BOM），避免 Docker 容器中中文乱码
var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.OutputEncoding = utf8NoBom;
Console.InputEncoding = utf8NoBom;
Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8NoBom) { AutoFlush = true });
Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8NoBom) { AutoFlush = true });

// ==========================================
// CentOS / Docker CE / EPEL 仓库同步工具
// C# 实现 - 替代 sync-repos.sh
// ==========================================
//
// 对应关系:
// - shell: log()                   -> Logger.Log()
// - shell: check_repomd_changed()  -> RepomdChecker.HasChanged()
// - shell: reposync                -> RepoSyncer.SyncRepository()
//     (解析 repomd.xml -> primary.xml -> 下载 RPM)
// - shell: createrepo              -> RepoMetadataGenerator.EnsureMetadata()
// - shell: while true; sleep       -> SyncOrchestrator.RunLoop()
// - shell: mkdir -p / mv / rm      -> System.IO.Directory / File
// - shell: md5sum                  -> System.Security.Cryptography.MD5
// - shell: wget                    -> HttpClient
// - shell: du -sh                  -> FileUtils.GetDirectorySize()
// ==========================================

Logger.Log("CentOS 仓库同步工具 (C# 版) 启动");

// 配置（对应 shell 脚本中的变量定义部分）
var config = new SyncConfig
{
    SyncIntervalSeconds = 86400, // 每天执行一次（对应 SYNC_INTERVAL=86400）
    MaxConcurrentDownloads = 5,  // 并发下载数（shell 的 reposync 默认是串行）
    HttpTimeoutSeconds = 300,

    // CentOS 7.9.2009 仓库（对应 REPO_MAP 和 REPO_URL）
    CentOSRepos = new List<RepoDefinition>
    {
        new()
        {
            Name = "os",
            BaseUrl = "https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/os/x86_64",
            LocalPath = "/data/repos/centos/7.9.2009/os"
        },
        new()
        {
            Name = "updates",
            BaseUrl = "https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/updates/x86_64",
            LocalPath = "/data/repos/centos/7.9.2009/updates"
        },
        new()
        {
            Name = "extras",
            BaseUrl = "https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/extras/x86_64",
            LocalPath = "/data/repos/centos/7.9.2009/extras"
        }
    },

    // Docker CE 仓库
    DockerRepo = new RepoDefinition
    {
        Name = "Docker CE",
        BaseUrl = "https://download.docker.com/linux/centos/7/x86_64/stable",
        LocalPath = "/data/repos/docker-ce/docker-ce-stable"
    },

    // EPEL 仓库
    EpelRepo = new RepoDefinition
    {
        Name = "EPEL",
        BaseUrl = "https://mirrors.aliyun.com/epel/7/x86_64",
        LocalPath = "/data/repos/epel/7/epel"
    }
};

// 支持命令行参数覆盖
if (args.Length > 0)
{
    switch (args[0].ToLower())
    {
        case "--once":
            // 只执行一次同步，不循环
            Logger.Log("模式: 单次同步");
            var onceOrchestrator = new SyncOrchestrator(config);
            await onceOrchestrator.RunOnce();
            return;

        case "--interval" when args.Length > 1 && int.TryParse(args[1], out var interval):
            config.SyncIntervalSeconds = interval;
            Logger.Log($"同步间隔设置为 {interval} 秒");
            break;

        case "--concurrency" when args.Length > 1 && int.TryParse(args[1], out var concurrency):
            config.MaxConcurrentDownloads = concurrency;
            Logger.Log($"并发下载数设置为 {concurrency}");
            break;

        case "--help":
            Console.WriteLine("用法: RepoSync [选项]");
            Console.WriteLine();
            Console.WriteLine("选项:");
            Console.WriteLine("  --once              只执行一次同步，不循环");
            Console.WriteLine("  --interval <秒>     设置同步间隔（默认 86400 秒）");
            Console.WriteLine("  --concurrency <数>  设置并发下载数（默认 5）");
            Console.WriteLine("  --help              显示帮助信息");
            return;
    }
}

// 启动循环同步（对应 shell: while true; do ... done）
using var cts = new CancellationTokenSource();

// 处理 Ctrl+C 优雅退出
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Logger.Log("收到退出信号，正在优雅停止...");
    cts.Cancel();
};

var orchestrator = new SyncOrchestrator(config);
await orchestrator.RunLoop(cts.Token);

Logger.Log("程序已退出");
