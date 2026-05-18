using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LeafNeko.DeployTool.Services;

public class LogUploadService
{
    private const string Token = "55300a3a95d27998ca28bb28ae155b79";

    private const string RepoOwner = "LeafNeko-QwQ";
    private const string RepoName = "leaf-neko.-deploy-tool_-log";
    private const string LogFolder = "crash-logs";
    private const string ApiBase = "https://gitee.com/api/v5";

    private readonly HttpClient _http;

    public LogUploadService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LeafNeko.DeployTool/1.0");
    }

    public async Task<UploadResult> UploadFileAsync(string localPath)
    {
        try
        {
            if (!File.Exists(localPath))
                return UploadResult.Fail("文件不存在: " + localPath);

            var content = await File.ReadAllTextAsync(localPath);
            var sanitized = Sanitize(content);

            var remoteName = BuildRemoteFileName(localPath);
            var apiPath = $"{LogFolder}/{remoteName}";

            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(sanitized));
            var payload = new
            {
                access_token = Token,
                content = base64,
                message = $"upload: {remoteName}"
            };

            var json = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{ApiBase}/repos/{RepoOwner}/{RepoName}/contents/{apiPath}";
            var response = await _http.PostAsync(url, httpContent);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Trace.WriteLine($"[LogUpload] 上传成功: {remoteName}");
                return UploadResult.Ok();
            }

            Trace.WriteLine($"[LogUpload] 上传失败 HTTP {(int)response.StatusCode}: {body}");
            return UploadResult.Fail($"上传失败 ({(int)response.StatusCode}): {ParseGiteeError(body)}");
        }
        catch (TaskCanceledException)
        {
            return UploadResult.Fail("上传超时，请检查网络。");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LogUpload] 上传异常: {ex.Message}");
            return UploadResult.Fail($"上传出错: {ex.Message}");
        }
    }

    public async Task<UploadResult> UploadAllPendingAsync()
    {
        var files = LoggerService.CollectLogFiles();
        if (files.Length == 0)
            return UploadResult.Fail("没有待上传的日志文件。");

        int ok = 0, fail = 0;
        foreach (var f in files)
        {
            var result = await UploadFileAsync(f);
            if (result.Success) ok++; else fail++;
        }

        var msg = $"上传完成: {ok} 成功";
        if (fail > 0) msg += $", {fail} 失败";
        return fail == 0 ? UploadResult.Ok() : UploadResult.Fail(msg);
    }

    private static string BuildRemoteFileName(string localPath)
    {
        var localName = Path.GetFileNameWithoutExtension(localPath);
        // 本地文件名可能是 deploytool_20260518 或 crash_20260518_143025
        // 重命名为 deploytool_v{ver}_{os}_{datetime}.log
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        var version = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "unknown";
        var os = SanitizeFileName(RuntimeInformation.OSDescription.Trim());
        var stamp = File.GetLastWriteTime(localPath).ToString("yyyy-MM-dd_HH-mm-ss");
        var type = localName.StartsWith("crash") ? "crash" : "run";
        return $"deploytool_{type}_{version}_{os}_{stamp}.log";
    }

    private static string SanitizeFileName(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
            else if (c == ' ') sb.Append('-');
        }
        var result = sb.ToString();
        while (result.Contains("--")) result = result.Replace("--", "-");
        return result.Trim('-');
    }

    /// <summary>过滤可能泄露的敏感信息（token/key 模式）</summary>
    internal static string Sanitize(string content)
    {
        // 匹配长度 >= 20 的 hex 串（常见 token 格式）
        content = Regex.Replace(content, @"\b[0-9a-fA-F]{32,}\b", "***REDACTED***");
        // 匹配 private_key / access_token= 等
        content = Regex.Replace(content, @"(access_token|private_key|secret)\s*[=:]\s*\S+",
            "$1=***REDACTED***", RegexOptions.IgnoreCase);
        return content;
    }

    private static string ParseGiteeError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? "未知错误";
        }
        catch { }
        return json.Length > 200 ? json[..200] : json;
    }
}

public class UploadResult
{
    public bool Success { get; private init; }
    public string Message { get; private init; } = "";

    public static UploadResult Ok(string msg = "上传成功") => new() { Success = true, Message = msg };
    public static UploadResult Fail(string msg) => new() { Success = false, Message = msg };
}
