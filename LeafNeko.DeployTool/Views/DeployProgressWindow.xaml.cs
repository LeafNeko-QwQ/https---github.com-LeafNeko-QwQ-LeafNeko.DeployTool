using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LeafNeko.DeployTool.Models;

namespace LeafNeko.DeployTool.Views;

public partial class DeployProgressWindow : Window, INotifyPropertyChanged
{
    private double _overallProgress;
    private string _elapsedText = "";
    private string _etaText = "";
    private bool _cancelled;

    public ObservableCollection<DeployTask> Tasks { get; } = new();
    public CancellationTokenSource Cts { get; } = new();

    public bool IsCancelled => _cancelled;

    public double OverallProgress
    {
        get => _overallProgress;
        set { _overallProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverallProgressText)); }
    }

    public string OverallProgressText => $"已完成 {OverallProgress:F0}%";

    public string ElapsedText
    {
        get => _elapsedText;
        set { _elapsedText = value; OnPropertyChanged(); }
    }

    public string EtaText
    {
        get => _etaText;
        set { _etaText = value; OnPropertyChanged(); }
    }

    public DeployProgressWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public async Task RunAsync(List<(string, Func<DeployTask, CancellationToken, Task>)> workItems)
    {
        var sw = Stopwatch.StartNew();
        var totalTasks = workItems.Count;

        foreach (var (name, action) in workItems)
        {
            var task = new DeployTask { Name = name, PhaseText = "等待中..." };
            Tasks.Add(task);
        }

        // 启动所有任务
        var running = new List<Task>();
        foreach (var (_, action) in workItems)
        {
            var task = Tasks[running.Count];
            running.Add(RunOneAsync(task, action));
        }

        // 定时刷新总进度
        using var timer = new System.Timers.Timer(200);
        timer.Elapsed += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (Tasks.Count > 0)
                    OverallProgress = Tasks.Average(t => t.OverallProgress);
                ElapsedText = $"已用 {sw.Elapsed.Minutes}:{sw.Elapsed.Seconds:D2}";
                if (OverallProgress > 0 && OverallProgress < 100)
                {
                    var elapsed = sw.Elapsed.TotalSeconds;
                    var eta = elapsed / (OverallProgress / 100) - elapsed;
                    if (eta > 1)
                        EtaText = $"预计剩余 ~{(int)eta / 60}m{(int)eta % 60}s";
                }
            });
        };
        timer.Start();

        try
        {
            await Task.WhenAll(running);
            OverallProgress = 100;
            ElapsedText = $"总用时 {sw.Elapsed.Minutes}:{sw.Elapsed.Seconds:D2}";
            EtaText = "";
        }
        catch (OperationCanceledException)
        {
            Trace.WriteLine("[DeployProgressWindow] 用户取消了部署");
        }
        finally
        {
            timer.Stop();
            sw.Stop();
        }
    }

    private async Task RunOneAsync(DeployTask task, Func<DeployTask, CancellationToken, Task> action)
    {
        try
        {
            task.Status = DeployTaskStatus.Running;
            await action(task, Cts.Token);
            if (task.Status != DeployTaskStatus.Error)
                task.Status = DeployTaskStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            task.PhaseText = "已取消";
            task.Status = DeployTaskStatus.Error;
        }
        catch (Exception ex)
        {
            task.PhaseText = ex.Message;
            task.Status = DeployTaskStatus.Error;
            Trace.WriteLine($"[DeployProgressWindow] 任务失败 {task.Name}: {ex.Message}");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _cancelled = true;
        Cts.Cancel();
        CancelBtn.IsEnabled = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
