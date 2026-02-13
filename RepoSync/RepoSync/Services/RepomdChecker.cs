namespace RepoSync.Services;

/// <summary>
/// repomd.xml 变更检测器 - 替代 shell 脚本中的 check_repomd_changed() 函数
/// 
/// 工作原理：下载远程 repomd.xml，与本地版本比对 MD5，
/// 如果不同则表示仓库有更新需要同步。
/// </summary>
public class RepomdChecker
{
    private readonly HttpClient _httpClient;

    public RepomdChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 检查远程 repomd.xml 是否与本地版本不同
    /// </summary>
    /// <param name="remoteUrl">远程 repomd.xml 的 URL</param>
    /// <param name="localPath">本地 repomd.xml 的路径</param>
    /// <returns>true = 有变化，需要同步; false = 无变化</returns>
    public async Task<bool> HasChanged(string remoteUrl, string localPath)
    {
        // 本地文件不存在时认为需要同步，对应 shell: if [ ! -f "${local_file}" ]
        if (!File.Exists(localPath))
        {
            Logger.Log("本地元数据文件不存在，需要同步");
            return true;
        }

        string tempFile = Path.GetTempFileName();
        try
        {
            // 下载远程 repomd.xml，对应 shell: wget -q -O "${tmp_file}" "${remote_url}"
            var response = await _httpClient.GetAsync(remoteUrl);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log("无法获取远程元数据，默认执行同步");
                return true;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(tempFile, content);

            // 比对 MD5，对应 shell: md5sum 比较
            var remoteMd5 = FileUtils.ComputeMd5(tempFile);
            var localMd5 = FileUtils.ComputeMd5(localPath);

            if (remoteMd5 == localMd5)
            {
                return false; // 无变化
            }
            else
            {
                return true; // 有变化
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"检查 repomd 时出错: {ex.Message}，默认执行同步");
            return true;
        }
        finally
        {
            // 清理临时文件，对应 shell: rm -f "${tmp_file}"
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
