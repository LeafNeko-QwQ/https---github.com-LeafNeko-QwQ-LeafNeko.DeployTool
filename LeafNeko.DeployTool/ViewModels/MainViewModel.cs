using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using LeafNeko.DeployTool.Models;
using LeafNeko.DeployTool.Services;

namespace LeafNeko.DeployTool.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
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

    public ObservableCollection<AppItemViewModel> AllApps { get; } = new();
    public ObservableCollection<AppItemViewModel> FilteredApps { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<DeployTask> ActiveTasks { get; } = new();

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
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // fallback to demo
        }
        catch { }

        ProgressStatus = "仓库未配置清单，加载演示数据...";
        OverallProgress = 80;
        LoadDemoData();
        OverallProgress = 100;
        ProgressStatus = $"已加载 {AllApps.Count} 个演示软件（云端仓库尚未配置清单文件）";
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

    public void LoadDemoData()
    {
        var demoApps = new List<AppItem>
        {
            new() { Name = "7-Zip", Url = "https://www.7-zip.org/a/7z2408-x64.exe", Category = "压缩工具" },
            new() { Name = "Google Chrome", Url = "https://dl.google.com/chrome/install/standalone/ChromeSetup64.exe", Category = "浏览器" },
            new() { Name = "Firefox", Url = "https://download.mozilla.org/?product=firefox-latest&os=win64&lang=zh-CN", Category = "浏览器" },
            new() { Name = "VLC 媒体播放器", Url = "https://get.videolan.org/vlc/3.0.21/win64/vlc-3.0.21-win64.exe", Category = "影音播放" },
            new() { Name = "PotPlayer", Url = "https://t1.daumcdn.net/potplayer/PotPlayer/Version/Latest/PotPlayerSetup64.exe", Category = "影音播放" },
            new() { Name = "Notepad++", Url = "https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.7.7/npp.8.7.7.Installer.x64.exe", Category = "办公" },
            new() { Name = "WPS Office", Url = "https://official-package.wpscdn.cn/wps/download/WPS_Setup.exe", Category = "办公" },
            new() { Name = "Bandizip", Url = "https://www.bandisoft.com/bandizip/dl.php?web", Category = "压缩工具" },
        };

        LoadFromList(demoApps);
    }

    public void ApplyFilter()
    {
        FilteredApps.Clear();
        foreach (var app in AllApps)
        {
            if (_selectedCategory == "全部" || app.Category == _selectedCategory)
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
