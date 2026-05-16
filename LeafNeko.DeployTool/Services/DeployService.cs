using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using LeafNeko.DeployTool.Helpers;

namespace LeafNeko.DeployTool.Services;

public class DeployService
{
    private readonly RepoService _repo;

    public DeployService()
    {
        _repo = new RepoService();
    }

    /// <summary>
    /// 解析 portable-apps.txt。返回 (日期, 更新日志, 直链列表)。
    /// 格式: #YYYY-MM-DD / #log:内容 / 其余行为直链 URL
    /// </summary>
    public static (string date, string log, List<string> links) ParsePortableManifest(string content)
    {
        var date = "";
        var log = "";
        var links = new List<string>();

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            if (line.StartsWith("#log:"))
                log = line.Substring(5).Trim();
            else if (line.StartsWith("#"))
                date = line.TrimStart('#').Trim();
            else
                links.Add(line);
        }

        return (date, log, links);
    }

    /// <summary>
    /// 从直链部署便携应用到 C:\。每个直链独立下载 ZIP → 解压到 C:\。
    /// deployTask: 用于汇报进度到 UI。
    /// overwriteCallback: 目标文件夹已存在时询问用户。
    /// </summary>
    public async Task DeployPortableFromLinksAsync(
        List<string> links,
        Models.DeployTask deployTask,
        Func<string, Task<bool>>? overwriteCallback = null,
        IProgress<string>? speedCallback = null,
        CancellationToken ct = default)
    {
        PathHelper.EnsureAll();
        deployTask.Status = Models.DeployTaskStatus.Running;

        for (var i = 0; i < links.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var link = links[i];
            var fileName = GetFileNameFromUrl(link);
            deployTask.PhaseText = $"({i + 1}/{links.Count}) 下载: {fileName}";

            // 阶段 0: 下载
            var downloadPath = Path.Combine(PathHelper.DownloadsDir, fileName);
            await _repo.DownloadToFileAsync(link, downloadPath,
                new Progress<double>(p =>
                {
                    deployTask.DownloadProgress = p;
                    deployTask.OverallProgress = (double)i / links.Count * 100 + p / links.Count * 50;
                }),
                speedCallback,
                ct);

            // 阶段 1: 解压
            deployTask.PhaseText = $"({i + 1}/{links.Count}) 解压: {fileName}";
            var extractDir = Path.Combine(PathHelper.ExtractDir, $"extract_{i}");
            Directory.CreateDirectory(extractDir);
            await ExtractZipWithProgressAsync(downloadPath, extractDir,
                p =>
                {
                    deployTask.ExtractProgress = p;
                    deployTask.OverallProgress = (double)i / links.Count * 100 + 50 + p / links.Count * 50;
                });

            // 阶段 2: 复制到 C:\
            deployTask.PhaseText = $"({i + 1}/{links.Count}) 复制: {fileName}";
            var topDirs = Directory.GetDirectories(extractDir);
            var topFiles = Directory.GetFiles(extractDir);

            foreach (var dir in topDirs)
            {
                var dirName = Path.GetFileName(dir);
                var destPath = Path.Combine(@"C:\", dirName);

                if (Directory.Exists(destPath) && overwriteCallback != null)
                {
                    var ok = await overwriteCallback(dirName);
                    if (!ok) continue;
                }
                CopyDirectoryRecursive(dir, destPath);
            }
            foreach (var file in topFiles)
            {
                var fileName2 = Path.GetFileName(file);
                File.Copy(file, Path.Combine(@"C:\", fileName2), true);
            }

            // 清理单个下载和临时文件
            try { File.Delete(downloadPath); } catch { }
            try { Directory.Delete(extractDir, true); } catch { }
        }

        deployTask.OverallProgress = 100;
        deployTask.Status = Models.DeployTaskStatus.Completed;
    }

    private static string GetFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var name = Path.GetFileName(uri.AbsolutePath);
            if (!string.IsNullOrEmpty(name))
                return SanitizeFileName(name);
        }
        catch { }
        return $"download_{Guid.NewGuid():N}.zip";
    }

    /// <summary>
    /// 快捷方式部署：下载 shortcuts.zip，解压到桌面（直接覆盖）
    /// </summary>
    public async Task DeployShortcutsAsync(IProgress<double>? progress = null,
        IProgress<string>? speedCallback = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report(0);
        PathHelper.EnsureAll();

        var data = await _repo.DownloadBytesAsync("shortcuts.zip",
            new Progress<double>(p => progress?.Report(p * 0.5)),
            speedCallback,
            ct);

        var tempZip = Path.Combine(PathHelper.ShortcutsDir, "shortcuts.zip");
        await File.WriteAllBytesAsync(tempZip, data);

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        await ExtractZipWithProgressAsync(tempZip, desktopPath,
            p => progress?.Report(50 + p * 0.5));

        try { File.Delete(tempZip); } catch { }
        progress?.Report(100);
    }

    /// <summary>
    /// 下载安装包并运行安装程序
    /// </summary>
    public async Task<string> DownloadAndInstallAppAsync(string name, string url,
        IProgress<double>? downloadProgress = null,
        IProgress<string>? speedCallback = null,
        IProgress<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        PathHelper.EnsureAll();
        var tempDir = Path.Combine(PathHelper.DownloadsDir, SanitizeFileName(name));
        Directory.CreateDirectory(tempDir);

        var fileName = await GetFileNameAsync(url);
        var filePath = Path.Combine(tempDir, fileName);

        statusCallback?.Report(filePath);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LeafNeko.DeployTool/1.0");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var stream = await response.Content.ReadAsStreamAsync(ct);

        await using var fs = File.Create(filePath);
        var buffer = new byte[8192];
        var totalRead = 0L;

        var sw = Stopwatch.StartNew();
        var lastReport = 0L;

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
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

        ValidateExecutable(filePath);

        var (success, message) = await RunInstallerAsync(filePath, statusCallback, ct);
        if (!success)
            throw new InvalidOperationException(message);

        return message;
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

    public async Task<(bool success, string message)> RunInstallerAsync(string filePath,
        IProgress<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

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

        var isPortable = filePath.Contains("portable", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("便携", StringComparison.OrdinalIgnoreCase);

        statusCallback?.Report(isPortable ? "正在启动..." : "正在安装...");

        try
        {
            var sw = Stopwatch.StartNew();
            using var process = Process.Start(psi);
            if (process == null)
            {
                return (true, isPortable ? "启动成功" : "安装程序已启动，请按照安装向导完成操作");
            }

            await process.WaitForExitAsync(ct);
            sw.Stop();

            if (process.ExitCode == 0)
            {
                if (sw.Elapsed.TotalSeconds < 3)
                    return (true, isPortable ? "启动成功" : "安装程序已启动，请按照安装向导完成操作");
                return (true, isPortable ? "启动成功" : "安装成功");
            }

            return (false, $"安装失败 (退出码: {process.ExitCode})");
        }
        catch (OperationCanceledException)
        {
            return (false, "安装已取消");
        }
        catch (Exception ex)
        {
            return (false, $"无法启动: {ex.Message}");
        }
    }

    public void CleanTemp()
    {
        PathHelper.CleanTemp();
    }

    // ==================== 私有方法 ====================

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
