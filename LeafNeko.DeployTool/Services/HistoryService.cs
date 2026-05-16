using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LeafNeko.DeployTool.Helpers;
using LeafNeko.DeployTool.Models;

namespace LeafNeko.DeployTool.Services;

public class HistoryService
{
    private static readonly string HistoryFile = Path.Combine(PathHelper.ConfigDir, "history.json");

    public List<DeployHistoryEntry> Load()
    {
        try
        {
            if (File.Exists(HistoryFile))
            {
                var json = File.ReadAllText(HistoryFile);
                return JsonSerializer.Deserialize<List<DeployHistoryEntry>>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[HistoryService] 加载失败: {ex.Message}");
        }
        return new();
    }

    public void Add(string appName, string operation, bool success, string? note = null)
    {
        var list = Load();
        list.Add(new DeployHistoryEntry
        {
            Time = DateTime.Now,
            AppName = appName,
            Operation = operation,
            Success = success,
            Note = note
        });

        // Keep only last 100
        while (list.Count > 100)
            list.RemoveAt(0);

        Save(list);
    }

    private static void Save(List<DeployHistoryEntry> list)
    {
        try
        {
            PathHelper.EnsureAll();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HistoryFile, json);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[HistoryService] 保存失败: {ex.Message}");
        }
    }
}
