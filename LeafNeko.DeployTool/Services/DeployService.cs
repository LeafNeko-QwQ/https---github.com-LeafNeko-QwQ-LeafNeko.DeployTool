using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace LeafNeko.DeployTool.Services;

public class DeployService
{
    private readonly RepoService _repo;

    public DeployService()
    {
        _repo = new RepoService();
    }

    /// <summary>
    /// 部署所有编号 portable-apps*.zip（支持 portable-apps, portable-apps1, portable-apps2...）
    /// overwriteCallback: 返回 true 表示覆盖，false 表示跳过
    /// phaseCallback: 报告当前阶段 (phase, percent) — phase: 0=下载 1=解压 2=复制
    /// </summary>
    public async Task DeployPortableAppsAsync(
        IProgress<double>? progress = null,
        Func<string, Task<bool>>? overwriteCallback = null,
        IProgress<string>? speedCallback = null,
        IProgress<(int phase, double percent, string label)>? phaseCallback = null)
    {
        var zipNames = await DiscoverNumberedZipsAsync("portable-apps");
        if (zipNames.Count == 0)
            throw new FileNotFoundException("云仓库中未找到任何 portable-apps*.zip，请确认文件已上传。");

        var baseTemp = Path.Combine(Path.GetTempPath(), "LeafNeko-DeployTool", "PortableExtract");
        Directory.CreateDirectory(baseTemp);

        for (var i = 0; i < zipNames.Count; i++)
        {
            var zipName = zipNames[i];
            var zipLabel = $"({i + 1}/{zipNames.Count}) {zipName}";

            // 阶段 0: 下载
            phaseCallback?.Report((0, 0, zipLabel));
            speedCallback?.Report($"正在下载 {zipName}...");

            var data = await _repo.DownloadBytesAsync(zipName,
                new Progress<double>(p =>
                {
                    phaseCallback?.Report((0, p, zipLabel));
                    progress?.Report((double)i / zipNames.Count * 100 + p / zipNames.Count * 0.5 * 100);
                }),
                speedCallback);

            var tempZip = Path.Combine(baseTemp, zipName);
            await File.WriteAllBytesAsync(tempZip, data);

            // 阶段 1: 解压
            phaseCallback?.Report((1, 0, zipLabel));
            var extractDir = Path.Combine(baseTemp, $"extract_{i}");
            Directory.CreateDirectory(extractDir);
            await ExtractZipWithProgressAsync(tempZip, extractDir,
                p => phaseCallback?.Report((1, p, zipLabel)));

            // 获取解压后的第一层文件夹
            var topDirs = Directory.GetDirectories(extractDir);
            var topFiles = Directory.GetFiles(extractDir);

            // 阶段 2: 复制
            var totalItems = topDirs.Length + topFiles.Length;
            var copiedItems = 0;

            foreach (var dir in topDirs)
            {
                var dirName = Path.GetFileName(dir);
                var destPath = Path.Combine(@"C:\", dirName);

                bool shouldCopy = true;
                if (Directory.Exists(destPath) && overwriteCallback != null)
                    shouldCopy = await overwriteCallback(dirName);

                if (shouldCopy)
                    CopyDirectoryRecursive(dir, destPath);

                copiedItems++;
                phaseCallback?.Report((2, (double)copiedItems / totalItems * 100, dirName));
            }

            foreach (var file in topFiles)
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(@"C:\", fileName);
                File.Copy(file, destPath, true);

                copiedItems++;
                phaseCallback?.Report((2, (double)copiedItems / totalItems * 100, fileName));
            }

            File.Delete(tempZip);
            Directory.Delete(extractDir, true);

            progress?.Report((double)(i + 1) / zipNames.Count * 100);
        }

        try { Directory.Delete(baseTemp, true); } catch { }
        progress?.Report(100);
    }

    /// <summary>
    /// 快捷方式部署：下载 shortcuts.zip，解压到桌面（直接覆盖）
    /// </summary>
    public async Task DeployShortcutsAsync(IProgress<double>? progress = null,
        IProgress<string>? speedCallback = null)
    {
        progress?.Report(0);

        var data = await _repo.DownloadBytesAsync("shortcuts.zip",
            new Progress<double>(p => progress?.Report(p * 0.5)),
            speedCallback);

        var tempZip = Path.Combine(Path.GetTempPath(), "LeafNeko-DeployTool", "shortcuts.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(tempZip)!);
        await File.WriteAllBytesAsync(tempZip, data);

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        await ExtractZipWithProgressAsync(tempZip, desktopPath,
            p => progress?.Report(50 + p * 0.5));

        File.Delete(tempZip);
        progress?.Report(100);
    }

    /// <summary>
    /// 下载安装包并运行安装程序
    /// </summary>
    public async Task DownloadAndInstallAppAsync(string name, string url,
        IProgress<double>? downloadProgress = null,
        IProgress<string>? speedCallback = null,
        IProgress<string>? statusCallback = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LeafNeko-DeployTool", SanitizeFileName(name));
        Directory.CreateDirectory(tempDir);

        var fileName = await GetFileNameAsync(url);
        var filePath = Path.Combine(tempDir, fileName);

        statusCallback?.Report(filePath);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LeafNeko.DeployTool/1.0");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var stream = await response.Content.ReadAsStreamAsync();

        await using var fs = File.Create(filePath);
        var buffer = new byte[8192];
        var totalRead = 0L;

        var sw = Stopwatch.StartNew();
        var lastReport = 0L;

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalRead += bytesRead;
            if (totalBytes > 0)
                downloadProgress?.Report((double)totalRead / totalBytes * 100);

            if (speedCallback != null && sw.ElapsedMilliseconds - lastReport > 250)
            {
                lastReport = sw.ElapsedMilliseconds;
                speedCallback.Report(RepoService.FormatSpeedInfo(totalRead, totalBytes, sw.Elapsed));
            }
        }

        await fs.DisposeAsync();

        // PE 文件验证：检查文件存在且以 MZ 开头
        ValidateExecutable(filePath);

        RunInstaller(filePath);
    }

    private static void ValidateExecutable(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"下载的文件不存在: {filePath}");

        var info = new FileInfo(filePath);
        if (info.Length == 0)
            throw new InvalidOperationException($"下载的文件为空 (0 字节): {filePath}");

        if (info.Length < 2)
            throw new InvalidOperationException($"下载的文件太小 ({info.Length} 字节)，不是有效的可执行文件: {filePath}");

        var header = new byte[2];
        using var fs = File.OpenRead(filePath);
        fs.ReadExactly(header, 0, 2);

        if (header[0] != 'M' || header[1] != 'Z')
            throw new InvalidOperationException(
                $"下载的文件不是有效的 Windows 可执行文件 (缺少 MZ 头)。\n" +
                $"可能是转链服务返回了 HTML 页面而非安装包。\n" +
                $"文件路径: {filePath}\n" +
                $"文件大小: {info.Length} 字节");
    }

    public void RunInstaller(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        // 无扩展名时尝试重命名为 .exe
        if (string.IsNullOrEmpty(ext))
        {
            var newPath = filePath + ".exe";
            File.Move(filePath, newPath);
            filePath = newPath;
            ext = ".exe";
        }

        ProcessStartInfo psi;
        if (ext == ".msi")
        {
            psi = new ProcessStartInfo("msiexec.exe", $"/i \"{filePath}\" /passive")
            {
                UseShellExecute = true
            };
        }
        else
        {
            psi = new ProcessStartInfo(filePath)
            {
                UseShellExecute = true
            };
        }

        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"无法启动安装程序: {filePath}\n{ex.Message}", ex);
        }
    }

    public void CleanTemp()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LeafNeko-DeployTool");
        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
        catch { }
    }

    // ==================== 私有方法 ====================

    private async Task<List<string>> DiscoverNumberedZipsAsync(string baseName)
    {
        var names = new List<string>();
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LeafNeko.DeployTool/1.0");

        // 先尝试无编号版本
        try
        {
            var url0 = RepoService.BaseUrl + baseName + ".zip";
            using var resp0 = await http.GetAsync(url0, HttpCompletionOption.ResponseHeadersRead);
            if (FileExists(resp0))
                names.Add(baseName + ".zip");
        }
        catch { }

        // 扫描编号版本（最多 20 个分包）
        for (int i = 1; i <= 20; i++)
        {
            try
            {
                var name = $"{baseName}{i}.zip";
                var url = RepoService.BaseUrl + name;
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!FileExists(resp))
                    break;
                names.Add(name);
            }
            catch
            {
                break;
            }
        }

        return names;
    }

    // Gitee 对存在的 raw 文件返回 302，不存在返回 404。禁用自动重定向以快速判断。
    private static bool FileExists(HttpResponseMessage resp) =>
        resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.Found;

    /// <summary>
    /// 从 URL 获取文件名（FileNameStar > FileName > 重定向 URI > 原始 URL > setup.exe）
    /// </summary>
    private async Task<string> GetFileNameAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("LeafNeko.DeployTool/1.0");
            var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            var cd = resp.Content.Headers.ContentDisposition;

            // RFC 5987: filename*=UTF-8''...
            if (cd?.FileNameStar != null)
                return SanitizeFileName(cd.FileNameStar.Trim('"'));

            // 传统 ASCII filename="..."
            if (cd?.FileName != null)
                return SanitizeFileName(cd.FileName.Trim('"'));

            // 回退：从重定向后的 URI 提取文件名
            var finalUri = resp.RequestMessage?.RequestUri;
            if (finalUri != null)
            {
                var name = Path.GetFileName(finalUri.AbsolutePath);
                if (!string.IsNullOrEmpty(name) && Path.HasExtension(name))
                    return SanitizeFileName(name);
            }
        }
        catch { }

        // 从原始 URL 路径提取文件名
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.AbsolutePath);
        if (!string.IsNullOrEmpty(fileName) && Path.HasExtension(fileName))
            return SanitizeFileName(fileName);

        // 无扩展名 → 默认 .exe
        return string.IsNullOrEmpty(fileName) ? "setup.exe" : SanitizeFileName(fileName) + ".exe";
    }

    private static async Task ExtractZipWithProgressAsync(string zipPath, string destDir,
        Action<double> progressCallback)
    {
        await Task.Run(() =>
        {
            var encoding = DetectZipEncoding(zipPath);
            ExtractEntries(zipPath, destDir, encoding, progressCallback);
        });
    }

    /// <summary>
    /// 检测 ZIP 文件的正确编码。先尝试 UTF-8，如果条目名包含替换字符则回退到 GBK。
    /// </summary>
    private static Encoding DetectZipEncoding(string zipPath)
    {
        try
        {
            using var fs = File.OpenRead(zipPath);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read, false, Encoding.UTF8);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.Contains('�'))
                {
                    // UTF-8 解码失败，回退到 GBK
                    return Encoding.GetEncoding(936);
                }
            }
        }
        catch { }
        return Encoding.UTF8;
    }

    private static void ExtractEntries(string zipPath, string destDir, Encoding encoding,
        Action<double> progressCallback)
    {
        using var fs = File.OpenRead(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read, false, encoding);
        var total = archive.Entries.Count;
        var processed = 0;

        foreach (var entry in archive.Entries)
        {
            var destPath = Path.Combine(destDir, entry.FullName);
            if (string.IsNullOrEmpty(entry.Name))
                Directory.CreateDirectory(destPath);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                entry.ExtractToFile(destPath, true);
            }
            processed++;
            progressCallback((double)processed / total * 100);
        }
    }

    private static async Task ExtractZipAsync(string zipPath, string destDir)
    {
        await ExtractZipWithProgressAsync(zipPath, destDir, _ => { });
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSub);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(result) ? "app" : result;
    }
}
