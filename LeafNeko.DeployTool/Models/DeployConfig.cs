using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace LeafNeko.DeployTool.Models;

public class DeployConfig
{
    public bool LicenseAccepted { get; set; }
    public string? LastRunTime { get; set; }

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LeafNeko-DeployTool");

    private static string ConfigFile =>
        Path.Combine(ConfigDir, "config.json");

    public static DeployConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var config = JsonSerializer.Deserialize<DeployConfig>(json);
                if (config != null)
                    return config;
            }
            else
            {
                // 处理上次崩溃遗留的 .tmp 文件
                var tmp = ConfigFile + ".tmp";
                if (File.Exists(tmp))
                {
                    var json = File.ReadAllText(tmp);
                    File.Move(tmp, ConfigFile, overwrite: true);
                    var config = JsonSerializer.Deserialize<DeployConfig>(json);
                    if (config != null)
                        return config;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DeployConfig] Load failed: {ex.Message}");
        }
        return new DeployConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var tmp = ConfigFile + ".tmp";
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tmp, json);
            File.Move(tmp, ConfigFile, overwrite: true);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DeployConfig] Save failed: {ex.Message}");
        }
    }
}
