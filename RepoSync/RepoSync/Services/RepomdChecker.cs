namespace RepoSync.Services;

/// <summary>
/// repomd.xml 变更检测 - 对应 shell 脚本中的 check_repomd_changed() 函数
/// 
/// 原理：下载远程 repomd.xml，与本地文件比较 MD5，
/// 如果不同则表示仓库有更新。
/// </summary>
public class RepomdChecker
{
    private readonly HttpClient _httpClient;

    public RepomdChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 检查远程 repomd.xml 是否与本地不同
    /// </summary>
    /// <param name="remoteUrl">远程 repomd.xml 的 URL</param>
    /// <param name="localPath">本地 repomd.xml 的路径</param>
    /// <returns>true = 有变化/需要同步，false = 未变化</returns>
    public async Task<bool> HasChanged(string remoteUrl, string localPath)
    {
        // 本地不存在，需要同步（对应 shell: if [ ! -f "${local_file}" ]）
        if (!File.Exists(localPath))
        {
            Logger.Log("本地元数据不存在，需要同步");
            return true;
        }

        string tempFile = Path.GetTempFileName();
        try
        {
            // 下载远程 repomd.xml（对应 shell: wget -q -O "${tmp_file}" "${remote_url}"）
            var response = await _httpClient.GetAsync(remoteUrl);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log("无法获取远程元数据，执行同步");
                return true;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(tempFile, content);

            // 对比 MD5（对应 shell: md5sum 比较）
            var remoteMd5 = FileUtils.ComputeMd5(tempFile);
            var localMd5 = FileUtils.ComputeMd5(localPath);

            if (remoteMd5 == localMd5)
            {
                return false; // 未变化
            }
            else
            {
                return true; // 有变化
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"检查 repomd 出错: {ex.Message}，执行同步");
            return true;
        }
        finally
        {
            // 清理临时文件（对应 shell: rm -f "${tmp_file}"）
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
