using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LeafNeko.DeployTool.Services;

public class SystemInfoService : INotifyPropertyChanged
{
    private string _osInfo = "";
    private string _cpuInfo = "";
    private string _ramInfo = "";
    private string _diskInfo = "";
    private bool _diskLow;

    public string OsInfo { get => _osInfo; set { _osInfo = value; OnPropertyChanged(); } }
    public string CpuInfo { get => _cpuInfo; set { _cpuInfo = value; OnPropertyChanged(); } }
    public string RamInfo { get => _ramInfo; set { _ramInfo = value; OnPropertyChanged(); } }
    public string DiskInfo { get => _diskInfo; set { _diskInfo = value; OnPropertyChanged(); } }
    public bool DiskLow { get => _diskLow; set { _diskLow = value; OnPropertyChanged(); } }

    public void Refresh()
    {
        OsInfo = $"{RuntimeInformation.OSDescription.Trim()}";
        CpuInfo = $"{Environment.ProcessorCount} 核心";

        var memInfo = new MEMORYSTATUSEX();
        memInfo.dwLength = (uint)Marshal.SizeOf(memInfo);
        if (GlobalMemoryStatusEx(ref memInfo))
        {
            var total = memInfo.ullTotalPhys / (1024.0 * 1024 * 1024);
            var avail = memInfo.ullAvailPhys / (1024.0 * 1024 * 1024);
            RamInfo = $"可用 {avail:F1} / 总计 {total:F1} GB";
        }
        else
        {
            RamInfo = "不可用";
        }

        try
        {
            var drive = new DriveInfo("C");
            var total = drive.TotalSize / (1024.0 * 1024 * 1024);
            var free = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            DiskInfo = $"可用 {free:F1} / 总计 {total:F1} GB";
            DiskLow = drive.AvailableFreeSpace < 10L * 1024 * 1024 * 1024;
        }
        catch
        {
            DiskInfo = "不可用";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
