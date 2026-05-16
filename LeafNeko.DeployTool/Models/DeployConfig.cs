using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LeafNeko.DeployTool.Helpers;

namespace LeafNeko.DeployTool.Models;

public class DeployConfig
{
    public bool LicenseAccepted { get; set; }
    public bool DarkMode { get; set; }
    public string? LastRunTime { get; set; }

    public static DeployConfig Load()
    {
        try
        {
            if (File.Exists(PathHelper.ConfigFile))
            {
                var json = File.ReadAllText(PathHelper.ConfigFile);
                var config = JsonSerializer.Deserialize<DeployConfig>(json);
                if (config != null)
                    return config;
            }
            else
            {
                var tmp = PathHelper.ConfigFile + ".tmp";
                if (File.Exists(tmp))
                {
                    Trace.WriteLine($"[DeployConfig] 检测到崩溃遗留 .tmp，正在恢复...");
                    var json = File.ReadAllText(tmp);
                    File.Move(tmp, PathHelper.ConfigFile, overwrite: true);
                    var config = JsonSerializer.Deserialize<DeployConfig>(json);
                    if (config != null)
                        return config;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DeployConfig] 加载失败: {ex.Message}");
        }
        return new DeployConfig();
    }

    public void Save()
    {
        try
        {
            PathHelper.EnsureAll();
            var tmp = PathHelper.ConfigFile + ".tmp";
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tmp, json);
            File.Move(tmp, PathHelper.ConfigFile, overwrite: true);
            Trace.WriteLine($"[DeployConfig] 配置已保存到: {PathHelper.ConfigFile}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DeployConfig] 保存失败: {ex.Message}");
        }
    }
}
