namespace LeafNeko.DeployTool.Models;

public enum AppStatus
{
    Pending,     // 待安装
    Downloading, // 下载中
    Installing,  // 安装中
    Completed,   // 已完成
    Error        // 错误
}

public class AppItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public AppStatus Status { get; set; } = AppStatus.Pending;
    public string? LocalVersion { get; set; }
    public bool IsOutdated { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
