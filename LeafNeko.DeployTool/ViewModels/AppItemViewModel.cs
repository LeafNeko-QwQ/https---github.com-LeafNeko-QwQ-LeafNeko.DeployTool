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
    private string _speedText = "";
    private string _etaText = "";

    public AppItem Model { get; }

    public string Name => Model.Name;
    public string Url => Model.Url;
    public string Category => Model.Category;
    public string? LocalVersion
    {
        get => Model.LocalVersion;
        set { Model.LocalVersion = value; OnPropertyChanged(); }
    }

    public bool IsOutdated
    {
        get => Model.IsOutdated;
        set { Model.IsOutdated = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUpdateIndicator)); }
    }

    public bool HasUpdateIndicator => IsOutdated && Status == AppStatus.Completed;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Model.IsSelected = value; OnPropertyChanged(); }
    }

    public AppStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            Model.Status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsProcessing));
            OnPropertyChanged(nameof(IsRetryVisible));
        }
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

    public bool IsRetryVisible => Status == AppStatus.Error;

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
