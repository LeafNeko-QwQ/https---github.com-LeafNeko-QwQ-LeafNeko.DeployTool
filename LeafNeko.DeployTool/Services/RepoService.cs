using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace LeafNeko.DeployTool.Services;

public class RepoService
{
    public const string BaseUrl = "https://gitee.com/LeafNeko-QwQ/zip-deploy-manifest/raw/master/";

    private readonly HttpClient _http;

    public RepoService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LeafNeko.DeployTool/1.0");
    }

    public async Task<string> DownloadTextAsync(string fileName)
    {
        var url = BaseUrl + fileName;
        var response = await _http.GetStringAsync(url);
        return response;
    }

    public async Task<byte[]> DownloadBytesAsync(string fileName,
        IProgress<double>? progress = null,
        IProgress<string>? speedCallback = null,
        CancellationToken ct = default)
    {
        var url = BaseUrl + fileName;

        return await RetryAsync(async () =>
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync(ct);

            var buffer = new byte[8192];
            var totalRead = 0L;
            using var ms = new MemoryStream();

            var sw = Stopwatch.StartNew();
            var lastReport = 0L;

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await ms.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                if (totalBytes > 0)
                    progress?.Report((double)totalRead / totalBytes * 100);

                if (speedCallback != null && sw.ElapsedMilliseconds - lastReport > 250)
                {
                    lastReport = sw.ElapsedMilliseconds;
                    speedCallback.Report(FormatSpeedInfo(totalRead, totalBytes, sw.Elapsed));
                }
            }

            return ms.ToArray();
        }, fileName);
    }

    private static async Task<T> RetryAsync<T>(Func<Task<T>> action, string fileName, CancellationToken ct = default, int maxRetries = 3)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex) when (attempt < maxRetries && IsRetryable(ex))
            {
                await DelayRetry(attempt, fileName, "HTTP错误");
            }
            catch (TaskCanceledException) when (attempt < maxRetries && !ct.IsCancellationRequested)
            {
                await DelayRetry(attempt, fileName, "超时");
            }
            catch (IOException) when (attempt < maxRetries)
            {
                await DelayRetry(attempt, fileName, "连接中断");
            }
        }
        return await action();
    }

    private static async Task RetryAsync(Func<Task> action, string fileName, CancellationToken ct = default, int maxRetries = 3)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries && IsRetryable(ex))
            {
                await DelayRetry(attempt, fileName, "HTTP错误");
            }
            catch (TaskCanceledException) when (attempt < maxRetries && !ct.IsCancellationRequested)
            {
                await DelayRetry(attempt, fileName, "超时");
            }
            catch (IOException) when (attempt < maxRetries)
            {
                await DelayRetry(attempt, fileName, "连接中断");
            }
        }
        await action();
    }

    private static bool IsRetryable(HttpRequestException ex) =>
        ex.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||
        ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
        ex.StatusCode == System.Net.HttpStatusCode.BadGateway ||
        ex.StatusCode == null;

    private static async Task DelayRetry(int attempt, string fileName, string reason)
    {
        var delay = (int)Math.Pow(2, attempt);
        Trace.WriteLine($"[RepoService] {fileName} {reason}重试 {attempt + 1}，{delay}s 后重试...");
        await Task.Delay(delay * 1000);
    }

    /// <summary>
    /// 从任意 URL 下载文件到指定路径，支持进度和速度报告，自带重试。
    /// </summary>
    public async Task DownloadToFileAsync(string url, string destPath,
        IProgress<double>? progress = null,
        IProgress<string>? speedCallback = null,
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await RetryAsync(async () =>
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync(ct);

            using var fs = File.Create(destPath);
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
                    progress?.Report((double)totalRead / totalBytes * 100);

                if (speedCallback != null && sw.ElapsedMilliseconds - lastReport > 250)
                {
                    lastReport = sw.ElapsedMilliseconds;
                    speedCallback.Report(FormatSpeedInfo(totalRead, totalBytes, sw.Elapsed));
                }
            }
        }, destPath, ct);
    }

    /// <summary>快速验证 ZIP 文件 PK 头</summary>
    public static bool IsValidZip(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;
            var info = new FileInfo(filePath);
            if (info.Length < 4) return false;
            var header = new byte[4];
            using var fs = File.OpenRead(filePath);
            fs.ReadExactly(header, 0, 4);
            return header[0] == 0x50 && header[1] == 0x4B;
        }
        catch { return false; }
    }

    public static string FormatSpeedInfo(long bytesRead, long totalBytes, TimeSpan elapsed)
    {
        var speed = bytesRead / Math.Max(elapsed.TotalSeconds, 0.01);
        var speedText = FormatBytesPerSec(speed);
        var elapsedText = FormatElapsed(elapsed);

        if (totalBytes > 0)
        {
            var remaining = totalBytes - bytesRead;
            var eta = speed > 0 ? TimeSpan.FromSeconds(remaining / speed) : TimeSpan.Zero;
            return $"{elapsedText} | {speedText} | 剩余 {FormatDuration(eta)}";
        }

        return $"{elapsedText} | {speedText}";
    }

    private static string FormatBytesPerSec(double bytesPerSec) =>
        bytesPerSec switch
        {
            >= 1_000_000 => $"{bytesPerSec / 1_000_000:F1} MB/s",
            >= 1_000 => $"{bytesPerSec / 1_000:F0} KB/s",
            _ => $"{bytesPerSec:F0} B/s"
        };

    private static string FormatElapsed(TimeSpan t) =>
        t.TotalHours >= 1 ? $"已用 {t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}" :
        t.TotalMinutes >= 1 ? $"已用 {t.Minutes}:{t.Seconds:D2}" :
        $"已用 {t.Seconds}s";

    private static string FormatDuration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{t.Hours}h{t.Minutes}m" :
        t.TotalMinutes >= 1 ? $"{t.Minutes}m{t.Seconds}s" :
        $"{t.Seconds}s";

    public double MeasuredSpeedBytesPerSec { get; private set; }

    public async Task MeasureSpeedAsync()
    {
        try
        {
            var url = BaseUrl + "latest-version.txt";
            var sw = Stopwatch.StartNew();
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsByteArrayAsync();
            sw.Stop();

            if (data.Length > 0 && sw.Elapsed.TotalSeconds > 0)
                MeasuredSpeedBytesPerSec = data.Length / sw.Elapsed.TotalSeconds;

            Trace.WriteLine($"[RepoService] 测速完成: {FormatBytesPerSec(MeasuredSpeedBytesPerSec)} ({data.Length} bytes in {sw.Elapsed.TotalSeconds:F2}s)");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RepoService] 测速失败: {ex.Message}");
        }
    }
}
