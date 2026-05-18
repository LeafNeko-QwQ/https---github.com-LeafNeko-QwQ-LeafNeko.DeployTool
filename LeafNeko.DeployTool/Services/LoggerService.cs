using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using LeafNeko.DeployTool.Helpers;

namespace LeafNeko.DeployTool.Services;

public enum LogLevel { Info, Warn, Error, Fatal }

public static class LoggerService
{
    private const int RingSize = 200;
    private static readonly string[] Ring = new string[RingSize];
    private static int _ringHead;
    private static bool _initialized;

    public static string CurrentLogFile { get; private set; } = "";

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        PathHelper.EnsureAll();
        CurrentLogFile = Path.Combine(PathHelper.LogsDir, $"deploytool_{DateTime.Now:yyyyMMdd}.log");

        var header = FormatHeader();
        var fs = new FileStream(CurrentLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var writer = new StreamWriter(fs) { AutoFlush = true };
        writer.WriteLine(header);
        Trace.Listeners.Add(new TextWriterTraceListener(writer, "FileLogger"));
        Trace.AutoFlush = true;
        Trace.WriteLine(header);

        PathHelper.CleanOldLogs(7);
    }

    public static void Info(string component, string message) => Write(LogLevel.Info, component, message);
    public static void Warn(string component, string message) => Write(LogLevel.Warn, component, message);
    public static void Error(string component, string message) => Write(LogLevel.Error, component, message);
    public static void Fatal(string component, string message) => Write(LogLevel.Fatal, component, message);

    public static void WriteCrashLog(Exception ex)
    {
        try
        {
            PathHelper.EnsureAll();
            var now = DateTime.Now;
            var crashFile = Path.Combine(PathHelper.CrashLogsDir, $"crash_{now:yyyyMMdd_HHmmss}.log");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(FormatHeader());
            sb.AppendLine($"=== 崩溃时间: {now:yyyy-MM-dd HH:mm:ss} ===");
            sb.AppendLine();
            sb.AppendLine("--- 最近日志 ---");
            var recent = GetRecent(100);
            foreach (var line in recent)
                sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine("--- 异常详情 ---");
            sb.AppendLine(ex.ToString());

            File.WriteAllText(crashFile, sb.ToString());
            Trace.WriteLine($"[Logger] 崩溃日志已写入: {crashFile}");
        }
        catch
        {
            // 崩溃日志写入失败不再抛异常
        }
    }

    public static string GetLogSummary()
    {
        try
        {
            var files = new List<string>();
            if (Directory.Exists(PathHelper.LogsDir))
                files.AddRange(Directory.GetFiles(PathHelper.LogsDir, "deploytool_*.log"));
            if (Directory.Exists(PathHelper.CrashLogsDir))
                files.AddRange(Directory.GetFiles(PathHelper.CrashLogsDir, "crash_*.log"));

            if (files.Count == 0) return "没有待上传的日志文件。";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"共 {files.Count} 个日志文件：");
            long totalSize = 0;
            int totalLines = 0;
            foreach (var f in files.OrderBy(File.GetLastWriteTime))
            {
                var info = new FileInfo(f);
                totalSize += info.Length;
                var lines = CountLinesSafe(f);
                totalLines += lines;
                var time = File.GetLastWriteTime(f);
                sb.AppendLine($"  {Path.GetFileName(f)}  |  {FormatSize(info.Length)}  |  {lines} 行  |  {time:MM-dd HH:mm}");
            }
            sb.AppendLine();
            sb.Append($"合计: {files.Count} 文件, {FormatSize(totalSize)}, {totalLines} 行");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"读取日志摘要失败: {ex.Message}";
        }
    }

    public static string[] CollectLogFiles()
    {
        var files = new List<string>();
        try
        {
            if (Directory.Exists(PathHelper.LogsDir))
                files.AddRange(Directory.GetFiles(PathHelper.LogsDir, "deploytool_*.log"));
            if (Directory.Exists(PathHelper.CrashLogsDir))
                files.AddRange(Directory.GetFiles(PathHelper.CrashLogsDir, "crash_*.log"));
        }
        catch { }
        return files.ToArray();
    }

    private static void Write(LogLevel level, string component, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpperInvariant(),-5}] [{component}] {message}";
        Trace.WriteLine(line);
        RingPush(line);
    }

    private static void RingPush(string line)
    {
        Ring[_ringHead % RingSize] = line;
        _ringHead++;
    }

    private static string[] GetRecent(int count)
    {
        var result = new List<string>(count);
        var start = Math.Max(0, _ringHead - RingSize);
        for (int i = start; i < _ringHead && result.Count < count; i++)
            result.Add(Ring[i % RingSize]);
        return result.ToArray();
    }

    private static string FormatHeader()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        var version = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "unknown";
        var os = RuntimeInformation.OSDescription.Trim();
        var dotnet = Environment.Version.ToString();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        return
            $"=== LeafNeko DeployTool Log ===\n" +
            $"版本: {version}\n" +
            $"OS:   {os}\n" +
            $".NET: {dotnet}\n" +
            $"启动: {now}\n" +
            $"================================\n";
    }

    private static int CountLinesSafe(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            int count = 0;
            while (reader.ReadLine() != null) count++;
            return count;
        }
        catch { return 0; }
    }

    private static string ReadAllTextSafe(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            >= 1_000_000 => $"{bytes / 1_000_000.0:F1} MB",
            >= 1_000 => $"{bytes / 1_000.0:F1} KB",
            _ => $"{bytes} B"
        };
}
