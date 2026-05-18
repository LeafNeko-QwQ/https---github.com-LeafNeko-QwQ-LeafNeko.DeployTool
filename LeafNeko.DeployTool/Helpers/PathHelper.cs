using System.Diagnostics;
using System.IO;

namespace LeafNeko.DeployTool.Helpers;

public static class PathHelper
{
    /// <summary>
    /// 桌面\装机助手临时目录\ — 所有缓存、配置、临时文件统一存放于此。
    /// </summary>
    public static string BaseDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "装机助手临时目录");

    public static string ConfigDir => BaseDir;
    public static string ConfigFile => Path.Combine(BaseDir, "config.json");

    public static string DownloadsDir => Path.Combine(BaseDir, "downloads");
    public static string ExtractDir => Path.Combine(BaseDir, "extract");
    public static string ShortcutsDir => Path.Combine(BaseDir, "shortcuts");
    public static string LogsDir => Path.Combine(BaseDir, "logs");
    public static string CrashLogsDir => Path.Combine(BaseDir, "crash-logs");

    public static void EnsureAll()
    {
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(DownloadsDir);
        Directory.CreateDirectory(ExtractDir);
        Directory.CreateDirectory(ShortcutsDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(CrashLogsDir);
    }

    /// <summary>
    /// 清理所有临时文件（下载缓存 + 解压目录 + 快捷方式目录），保留 config.json。
    /// </summary>
    public static void CleanTemp()
    {
        try
        {
            if (Directory.Exists(DownloadsDir))
            {
                Directory.Delete(DownloadsDir, true);
                Trace.WriteLine($"[PathHelper] 已删除: {DownloadsDir}");
            }
            if (Directory.Exists(ExtractDir))
            {
                Directory.Delete(ExtractDir, true);
                Trace.WriteLine($"[PathHelper] 已删除: {ExtractDir}");
            }
            if (Directory.Exists(ShortcutsDir))
            {
                Directory.Delete(ShortcutsDir, true);
                Trace.WriteLine($"[PathHelper] 已删除: {ShortcutsDir}");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PathHelper] 清理异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 彻底删除整个装机助手临时目录（包括 config.json）。
    /// </summary>
    public static void DeleteAll()
    {
        try
        {
            if (Directory.Exists(BaseDir))
            {
                Directory.Delete(BaseDir, true);
                Trace.WriteLine($"[PathHelper] 已彻底删除: {BaseDir}");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PathHelper] 删除异常: {ex.Message}");
        }
    }

    public static void CleanOldLogs(int keepDays = 7)
    {
        try
        {
            if (!Directory.Exists(LogsDir)) return;
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.GetFiles(LogsDir, "deploytool_*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                    Trace.WriteLine($"[PathHelper] 已删除旧日志: {file}");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PathHelper] 日志清理异常: {ex.Message}");
        }
    }
}
