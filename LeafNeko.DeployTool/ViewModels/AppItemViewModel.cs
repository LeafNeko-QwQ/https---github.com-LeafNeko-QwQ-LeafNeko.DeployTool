using System.ComponentModel;
using System.Runtime.CompilerServices;
using LeafNeko.DeployTool.Models;

namespace LeafNeko.DeployTool.ViewModels;

public class AppItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private AppStatus _status;
    private string _errorMessage = string.Empty;
    private double _downloadProgress;

    public AppItem Model { get; }

    public string Name => Model.Name;
    public string Url => Model.Url;
    public string Category => Model.Category;
    public string? LocalVersion => Model.LocalVersion;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Model.IsSelected = value; OnPropertyChanged(); }
    }

    public AppStatus Status
    {
        get => _status;
        set { _status = value; Model.Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(IsProcessing)); }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; Model.ErrorMessage = value; OnPropertyChanged(); }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(); }
    }

    public string StatusText => Status switch
    {
        AppStatus.Pending => "待安装",
        AppStatus.Downloading => "下载中",
        AppStatus.Installing => "安装中",
        AppStatus.Completed => "已完成",
        AppStatus.Error => "出错",
        _ => ""
    };

    public string StatusColor => Status switch
    {
        AppStatus.Completed => "#66BB6A",
        AppStatus.Error => "#EF5350",
        AppStatus.Downloading => "#F8A5B2",
        AppStatus.Installing => "#F8A5B2",
        _ => "#9E9E9E"
    };

    public bool IsProcessing => Status == AppStatus.Downloading || Status == AppStatus.Installing;

    public AppItemViewModel(AppItem model)
    {
        Model = model;
        _isSelected = model.IsSelected;
        _status = model.Status;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
