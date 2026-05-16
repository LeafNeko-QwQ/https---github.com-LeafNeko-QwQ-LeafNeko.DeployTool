using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using LeafNeko.DeployTool.Helpers;
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
    private readonly DeployConfig _config = DeployConfig.Load();

    public ObservableCollection<AppItemViewModel> AllApps { get; } = new();
    public ObservableRangeCollection<AppItemViewModel> FilteredApps { get; } = new();
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

    private string _changelogText = "";

    public string ChangelogText
    {
        get => _changelogText;
        set { _changelogText = value; OnPropertyChanged(); }
    }

    public void InitChangelog()
    {
        ChangelogText = @"v1.0.12 — 2026-05-17
• 键盘快捷键：Ctrl+A 全选 / Ctrl+F 搜索 / Enter 安装
• 暗色模式跟随 Windows 系统主题
• 关闭窗口最小化到系统托盘
• 搜索历史下拉（最近 10 条）
• 分类折叠 / 展开面板
• 自定义分类：新建 / 重命名 / 删除 / 排序
• 导出部署报告 TXT/HTML
• 安装成功后 24 小时自动清理缓存
• 系统声音反馈（成功 / 错误 / 警告）

v1.0.10 — 2026-05-17
• 暗色模式（一键切换，配置持久化）
• 软件搜索过滤（按名称/分类实时筛选）
• 独立部署进度窗口（多任务并行 + 取消 + ETA）
• 系统信息面板（OS/CPU/RAM/磁盘）
• 部署历史记录（最近 5 条，JSON 持久化）
• 版本检测增强 + 自更新机制

v1.0.8 — 2026-05-17
• 动画引擎重写：单帧游戏循环替代多路 DispatcherTimer
• 悬停 3D 倾斜 + 阴影跟随 + 辉光渐变
• 入场弹入动画 + SplineKeyFrame 点击弹簧动画
• 应用图标 + 配置模板上传

v1.0.0 — 2026-05-16
• 粉色主题界面，软件卡片网格，分类标签切换
• 从 Gitee 仓库拉取软件清单 + 便携应用清单
• 安装版/便携应用/快捷方式三种部署方式
• 多任务并发下载 (SemaphoreSlim 最大 3 并发)
• 版本检测 + 部署确认弹窗 + 指数退避重试
• ZIP 中文文件名自动编码检测 (GBK 回退)";
    }

    private string _searchText = "";

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; ApplyFilter(); OnPropertyChanged(); }
    }

    // ── 搜索历史 ──
    public ObservableCollection<string> SearchHistory { get; } = new();

    public void AddSearchToHistory(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return;
        SearchHistory.Remove(term);
        SearchHistory.Insert(0, term);
        while (SearchHistory.Count > 10)
            SearchHistory.RemoveAt(SearchHistory.Count - 1);

        _config.SearchHistory = SearchHistory.ToList();
        _config.Save();
    }

    public void LoadSearchHistory()
    {
        SearchHistory.Clear();
        foreach (var h in _config.SearchHistory)
            SearchHistory.Add(h);
    }

    // ── 分类折叠 ──
    public bool IsCategoryCollapsed(string category)
    {
        return _config.CategoryCollapsed.TryGetValue(category, out var c) && c;
    }

    public void ToggleCategoryCollapse(string category)
    {
        _config.CategoryCollapsed[category] = !IsCategoryCollapsed(category);
        _config.Save();
        ApplyFilter();
    }

    // ── 分类编辑 ──
    public void AddCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Categories.Contains(name)) return;
        Categories.Add(name);
    }

    public void RenameCategory(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || oldName == newName || Categories.Contains(newName)) return;
        var idx = Categories.IndexOf(oldName);
        if (idx < 0) return;
        Categories[idx] = newName;

        foreach (var app in AllApps.Where(a => a.Category == oldName))
            app.Category = newName;

        if (_selectedCategory == oldName)
            SelectedCategory = newName;
    }

    public void RemoveCategory(string name)
    {
        if (name == "全部") return;
        Categories.Remove(name);

        foreach (var app in AllApps.Where(a => a.Category == name))
            app.Category = "其他";

        if (_selectedCategory == name)
            SelectedCategory = "全部";
    }

    public void MoveCategoryUp(string name)
    {
        var idx = Categories.IndexOf(name);
        if (idx > 1) // 0 = "全部", 不可移动
            Categories.Move(idx, idx - 1);
    }

    public void MoveCategoryDown(string name)
    {
        var idx = Categories.IndexOf(name);
        if (idx > 0 && idx < Categories.Count - 1)
            Categories.Move(idx, idx + 1);
    }

    public bool AutoDarkMode
    {
        get => _config.AutoDarkMode;
        set { _config.AutoDarkMode = value; _config.Save(); OnPropertyChanged(); }
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
        var matches = new List<AppItemViewModel>();
        foreach (var app in AllApps)
        {
            var catMatch = _selectedCategory == "全部" || app.Category == _selectedCategory;

            // 全部视图下，折叠的分类不显示
            if (_selectedCategory == "全部" && IsCategoryCollapsed(app.Category))
                catMatch = false;

            // 但搜索时忽略折叠
            if (!string.IsNullOrEmpty(_searchText))
                catMatch = _selectedCategory == "全部" || app.Category == _selectedCategory;

            var searchMatch = string.IsNullOrEmpty(_searchText)
                || app.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || app.Category.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

            if (catMatch && searchMatch)
                matches.Add(app);
        }
        FilteredApps.ClearAndAddRange(matches);
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
