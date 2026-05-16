using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using LeafNeko.DeployTool.Models;
using LeafNeko.DeployTool.Services;

namespace LeafNeko.DeployTool.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public static string VersionText { get; } =
        Assembly.GetExecutingAssembly().GetName().Version is Version v
            ? $"v{v.Major}.{v.Minor}.{v.Build}"
            : "v1.0.10";
    private string _selectedCategory = "全部";
    private string _progressStatus = "就绪，等待操作";
    private double _overallProgress;
    private int _selectedCount;
    private int _totalCount;
    private string _elapsedText = "";
    private string _speedText = "";
    private string _etaText = "";
    private string _phaseText = "";
    private string _downloadFileName = "";
    private double _downloadProgress;
    private double _extractProgress;
    private double _copyProgress;

    private readonly RepoService _repo = new();
    private readonly ManifestService _manifest = new();
    private readonly VersionService _version = new();
    private readonly SystemInfoService _systemInfo = new();
    private readonly HistoryService _history = new();

    public ObservableCollection<AppItemViewModel> AllApps { get; } = new();
    public ObservableCollection<AppItemViewModel> FilteredApps { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<DeployTask> ActiveTasks { get; } = new();

    public SystemInfoService SystemInfo => _systemInfo;

    public HistoryService History => _history;

    public void RefreshHistory()
    {
        OnPropertyChanged(nameof(RecentHistoryText));
    }

    public string RecentHistoryText
    {
        get
        {
            var recent = _history.Load();
            if (recent.Count == 0) return "暂无部署记录";
            var last5 = recent.TakeLast(5).Reverse();
            return string.Join("\n", last5.Select(e => e.DisplayText));
        }
    }

    public string ChangelogText { get; } = @"v1.0.0 — 2026-05-16

🎉 首次发布
• 粉色主题界面，软件卡片网格，分类标签切换
• 从 Gitee 仓库拉取软件清单 + 便携应用清单
• 安装版软件：下载 EXE/MSI 自动运行安装程序
• 便携应用部署：直链下载 ZIP → 解压到 C:\
• 快捷方式部署：下载快捷方式包 → 解压到桌面
• 版本检测：扫描注册表检测本地已安装软件

✨ 动画体验
• 卡片悬停 3D 倾斜 + 阴影跟随 + 辉光渐变 (CSS 风格平滑过渡)
• 入场动画 + 点击弹簧弹跳 (SplineKeyFrame)
• 选中高亮：粉色边框 + 背景切换

🔧 核心能力
• 全选 / 取消全选分离，整卡点击勾选
• 一键部署全部：便携 + 快捷方式 + 安装版并行执行
• 多任务并发下载 (最大 3 并发，SemaphoreSlim)
• 部署确认弹窗：展示更新日志 + 内容清单
• 指数退避重试：502/503/504/超时/连接中断

🇨🇳 本地化
• ZIP 中文文件名自动编码检测 (GBK 回退)
• 转链服务文件名识别 (RFC 5987)
• PE 文件头 MZ 验证

🛠 技术栈
• .NET 9 WPF 单文件自包含 EXE
• MVVM 架构 + 桌面统一临时目录
• 原子化配置写入 + Trace 操作日志";

    private string _searchText = "";

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; ApplyFilter(); OnPropertyChanged(); }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value; ApplyFilter(); OnPropertyChanged(); }
    }

    public string ProgressStatus
    {
        get => _progressStatus;
        set { _progressStatus = value; OnPropertyChanged(); }
    }

    public double OverallProgress
    {
        get => _overallProgress;
        set { _overallProgress = value; OnPropertyChanged(); }
    }

    public int SelectedCount
    {
        get => _selectedCount;
        set { _selectedCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedCountText)); }
    }

    public int TotalCount
    {
        get => _totalCount;
        set { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedCountText)); }
    }

    public string SelectedCountText => $"已选: {SelectedCount}/{TotalCount}";

    public string ElapsedText
    {
        get => _elapsedText;
        set { _elapsedText = value; OnPropertyChanged(); }
    }

    public string SpeedText
    {
        get => _speedText;
        set { _speedText = value; OnPropertyChanged(); }
    }

    public string EtaText
    {
        get => _etaText;
        set { _etaText = value; OnPropertyChanged(); }
    }

    public string PhaseText
    {
        get => _phaseText;
        set { _phaseText = value; OnPropertyChanged(); }
    }

    public string DownloadFileName
    {
        get => _downloadFileName;
        set { _downloadFileName = value; OnPropertyChanged(); }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(); }
    }

    public double ExtractProgress
    {
        get => _extractProgress;
        set { _extractProgress = value; OnPropertyChanged(); }
    }

    public double CopyProgress
    {
        get => _copyProgress;
        set { _copyProgress = value; OnPropertyChanged(); }
    }

    public void ResetProgressDetail()
    {
        ElapsedText = "";
        SpeedText = "";
        EtaText = "";
        PhaseText = "";
        DownloadFileName = "";
        DownloadProgress = 0;
        ExtractProgress = 0;
        CopyProgress = 0;
    }

    public async Task LoadAppsAsync()
    {
        ProgressStatus = "正在连接云端仓库...";
        OverallProgress = 10;

        try
        {
            var content = await _repo.DownloadTextAsync("app-list.txt");
            OverallProgress = 50;

            var apps = _manifest.Parse(content);

            if (apps.Count > 0)
            {
                ProgressStatus = "正在加载软件列表...";
                OverallProgress = 70;
                LoadFromList(apps);
                OverallProgress = 100;
                ProgressStatus = $"已从云端加载 {apps.Count} 个软件";
                return;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[MainViewModel] 清单加载失败: {ex.Message}");
        }

        AllApps.Clear();
        Categories.Clear();
        Categories.Add("全部");
        OverallProgress = 100;
        ProgressStatus = "暂无可用软件，请检查仓库配置";
    }

    public void CheckVersions()
    {
        ProgressStatus = "正在检测已安装版本...";
        OverallProgress = 0;

        var total = AllApps.Count;
        var checked_ = 0;

        foreach (var app in AllApps)
        {
            var info = _version.Detect(app.Name);
            if (info != null && info.IsInstalled)
            {
                app.Status = AppStatus.Completed;
                app.LocalVersion = info.Version;
                app.IsOutdated = VersionService.IsOutdated(app.LocalVersion, app.Url);
            }
            checked_++;
            OverallProgress = (double)checked_ / total * 100;
        }

        ProgressStatus = "版本检测完成";
        OverallProgress = 0;
    }

    private void LoadFromList(List<AppItem> apps)
    {
        AllApps.Clear();
        Categories.Clear();

        var cats = new HashSet<string> { "全部" };
        foreach (var app in apps)
        {
            cats.Add(app.Category);
            var vm = new AppItemViewModel(app);
            vm.PropertyChanged += OnAppItemPropertyChanged;
            AllApps.Add(vm);
        }

        foreach (var cat in cats)
            Categories.Add(cat);

        TotalCount = AllApps.Count;
        ApplyFilter();
        UpdateSelectionCount();
    }

    private void OnAppItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
        {
            UpdateSelectionCount();
        }
    }


    public void ApplyFilter()
    {
        FilteredApps.Clear();
        foreach (var app in AllApps)
        {
            var catMatch = _selectedCategory == "全部" || app.Category == _selectedCategory;
            var searchMatch = string.IsNullOrEmpty(_searchText)
                || app.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || app.Category.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

            if (catMatch && searchMatch)
                FilteredApps.Add(app);
        }
    }

    public bool IsAllSelected => FilteredApps.Count > 0 && FilteredApps.All(a => a.IsSelected);

    public void SelectAll()
    {
        foreach (var app in FilteredApps)
            app.IsSelected = true;
        UpdateSelectionCount();
        OnPropertyChanged(nameof(IsAllSelected));
    }

    public void DeselectAll()
    {
        foreach (var app in FilteredApps)
            app.IsSelected = false;
        UpdateSelectionCount();
        OnPropertyChanged(nameof(IsAllSelected));
    }

    public void ToggleSelectAll()
    {
        if (IsAllSelected)
            DeselectAll();
        else
            SelectAll();
    }

    public void UpdateSelectionCount()
    {
        SelectedCount = AllApps.Count(a => a.IsSelected);
        OnPropertyChanged(nameof(IsAllSelected));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
