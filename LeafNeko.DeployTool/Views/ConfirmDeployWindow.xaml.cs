using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace LeafNeko.DeployTool.Views;

public partial class ConfirmDeployWindow : Window, INotifyPropertyChanged
{
    private string _dateText = "";
    private string _updateLog = "";
    private int _linkCount;

    public ObservableCollection<string> Links { get; } = new();

    public string DateText
    {
        get => _dateText;
        set { _dateText = value; OnPropertyChanged(); }
    }

    public string UpdateLog
    {
        get => _updateLog;
        set { _updateLog = value; OnPropertyChanged(); }
    }

    public string LinkCountText => $"🔗 本次部署内容 (共 {_linkCount} 个直链)";

    public bool IsConfirmed { get; private set; }

    public ConfirmDeployWindow(string date, string log, List<string> links)
    {
        InitializeComponent();
        DataContext = this;

        DateText = string.IsNullOrEmpty(date) ? "更新日期: 未知" : $"更新日期: {date}";
        UpdateLog = string.IsNullOrEmpty(log) ? "(暂无更新日志)" : log;
        _linkCount = links.Count;
        foreach (var link in links)
            Links.Add(link);

        OnPropertyChanged(nameof(LinkCountText));
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
