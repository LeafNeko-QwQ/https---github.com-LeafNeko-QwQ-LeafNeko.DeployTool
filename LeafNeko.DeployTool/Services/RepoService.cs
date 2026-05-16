using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace LeafNeko.DeployTool.Services;

public class RepoService
{
    public const string BaseUrl = "https://gitee.com/LeafNeko-QwQ/zip-deploy-manifest/raw/main/";

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
        IProgress<string>? speedCallback = null)
    {
        var url = BaseUrl + fileName;

        return await RetryAsync(async () =>
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync();

            var buffer = new byte[8192];
            var totalRead = 0L;
            using var ms = new MemoryStream();

            var sw = Stopwatch.StartNew();
            var lastReport = 0L;

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await ms.WriteAsync(buffer.AsMemory(0, bytesRead));
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

    private static async Task<T> RetryAsync<T>(Func<Task<T>> action, string fileName, int maxRetries = 3)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex) when (attempt < maxRetries &&
                (ex.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                 ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                 ex.StatusCode == System.Net.HttpStatusCode.BadGateway))
            {
                var delay = (int)Math.Pow(2, attempt); // 1s, 2s, 4s
                Debug.WriteLine($"[RepoService] {fileName} 重试 {attempt + 1}/{maxRetries}，{delay}s 后重试...");
                await Task.Delay(delay * 1000);
            }
            catch (TaskCanceledException) when (attempt < maxRetries)
            {
                var delay = (int)Math.Pow(2, attempt);
                Debug.WriteLine($"[RepoService] {fileName} 超时重试 {attempt + 1}/{maxRetries}，{delay}s 后重试...");
                await Task.Delay(delay * 1000);
            }
        }
        return await action(); // Last attempt — let it throw
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
}
