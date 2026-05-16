using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LeafNeko.DeployTool.Models;

public enum DeployTaskStatus { Pending, Running, Completed, Error }

public class DeployTask : INotifyPropertyChanged
{
    private string _name = "";
    private string _phaseText = "";
    private string _speedText = "";
    private double _overallProgress;
    private double _downloadProgress;
    private double _extractProgress;
    private DeployTaskStatus _status = DeployTaskStatus.Pending;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string PhaseText
    {
        get => _phaseText;
        set { _phaseText = value; OnPropertyChanged(); }
    }

    public string SpeedText
    {
        get => _speedText;
        set { _speedText = value; OnPropertyChanged(); }
    }

    public double OverallProgress
    {
        get => _overallProgress;
        set { _overallProgress = value; OnPropertyChanged(); }
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

    public DeployTaskStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public string StatusText => Status switch
    {
        DeployTaskStatus.Pending => "等待中",
        DeployTaskStatus.Running => "进行中",
        DeployTaskStatus.Completed => "已完成",
        DeployTaskStatus.Error => "出错",
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
