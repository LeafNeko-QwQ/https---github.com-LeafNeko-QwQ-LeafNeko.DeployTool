namespace LeafNeko.DeployTool.Models;

public class DeployHistoryEntry
{
    public DateTime Time { get; set; }
    public string AppName { get; set; } = "";
    public string Operation { get; set; } = "";  // 安装 / 便携部署 / 快捷方式 / 更新
    public bool Success { get; set; }
    public string? Note { get; set; }

    public string DisplayText => $"[{Time:MM-dd HH:mm}] {(Success ? "✓" : "✗")} {AppName} - {Operation}";
}
