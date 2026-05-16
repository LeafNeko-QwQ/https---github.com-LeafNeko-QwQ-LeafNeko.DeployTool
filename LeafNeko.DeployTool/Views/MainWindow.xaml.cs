using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LeafNeko.DeployTool.Helpers;
using LeafNeko.DeployTool.Models;
using LeafNeko.DeployTool.Services;
using LeafNeko.DeployTool.ViewModels;

namespace LeafNeko.DeployTool.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly DeployService _deployService = new();
    private readonly DownloadService _downloadService = new();
    private readonly RepoService _repo = new();
    private Border? _selectedTabBorder;
    private TextBlock? _selectedTabText;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsAllSelected))
            UpdateSelectAllButton();
    }

    private void UpdateSelectAllButton()
    {
        SelectAllBtn.Content = _viewModel.IsAllSelected ? "取消全选" : "☑ 全选";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PathHelper.EnsureAll();
        Trace.WriteLine("[MainWindow] 桌面目录已初始化: " + PathHelper.BaseDir);
        await _viewModel.LoadAppsAsync();
        _viewModel.OverallProgress = 0;
    }

    #region 顶部链接

    private void CategoryTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Child is TextBlock tb && tb.DataContext is string category)
        {
            _viewModel.SelectedCategory = category;

            if (_selectedTabBorder != null && _selectedTabText != null)
            {
                _selectedTabBorder.Background = Brushes.Transparent;
                _selectedTabText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
            }

            border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF0F2F5"));
            tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8A5B2"));

            _selectedTabBorder = border;
            _selectedTabText = tb;
        }
    }

    private void BilibiliLink_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://space.bilibili.com/1580757085") { UseShellExecute = true });
    }

    private void GiteeLink_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://gitee.com/LeafNeko-QwQ/zip-deploy-manifest") { UseShellExecute = true });
    }

    #endregion

    #region 底部按钮

    private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleSelectAll();
        UpdateSelectAllButton();
    }

    private void CheckVersionBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CheckVersions();
    }

    #endregion

    #region 便携应用部署（确认→多任务）

    private async void DeployPortableBtn_Click(object sender, RoutedEventArgs e)
    {
        DeployPortableBtn.IsEnabled = false;
        _viewModel.ProgressStatus = "正在获取便携应用清单...";

        try
        {
            // 1. 下载并解析 portable-apps.txt
            string content;
            try
            {
                content = await _repo.DownloadTextAsync("portable-apps.txt");
                Trace.WriteLine("[MainWindow] 便携清单已下载");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[MainWindow] 便携清单下载失败: {ex.Message}");
                MessageBox.Show("无法获取便携应用清单，请确认仓库中已上传 portable-apps.txt。\n\n" +
                                "格式: #日期\n#log:更新内容\n直链URL每行一个",
                                "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (date, log, links) = DeployService.ParsePortableManifest(content);
            if (links.Count == 0)
            {
                MessageBox.Show("便携应用清单为空，请先配置直链。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Trace.WriteLine($"[MainWindow] 解析到 {links.Count} 个直链, 日期={date}");

            // 2. 弹出确认窗口
            var confirmWindow = new ConfirmDeployWindow(date, log, links) { Owner = this };
            confirmWindow.ShowDialog();

            if (!confirmWindow.IsConfirmed)
            {
                _viewModel.ProgressStatus = "已取消";
                Trace.WriteLine("[MainWindow] 用户取消了便携应用部署");
                return;
            }

            Trace.WriteLine("[MainWindow] 用户确认部署，开始多任务下载");

            // 3. 创建任务并执行
            SetAllButtonsEnabled(false);
            _viewModel.ActiveTasks.Clear();

            var deployTask = new DeployTask { Name = "便携应用部署", PhaseText = "准备中..." };
            _viewModel.ActiveTasks.Add(deployTask);

            Func<string, Task<bool>> overwriteCallback = folderName =>
            {
                var result = MessageBox.Show(
                    $"文件夹 \"{folderName}\" 已存在于 C 盘根目录，是否覆盖？\n\n选择「是」覆盖已有文件，「否」跳过该文件夹。",
                    "覆盖确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                return Task.FromResult(result == MessageBoxResult.Yes);
            };

            await _deployService.DeployPortableFromLinksAsync(
                links, deployTask, overwriteCallback,
                new Progress<string>(info => deployTask.SpeedText = info));

            _viewModel.ProgressStatus = "便携应用部署完成！";
            MessageBox.Show($"便携应用部署完成！\n已处理 {links.Count} 个直链。", "完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[MainWindow] 部署失败: {ex.Message}");
            MessageBox.Show($"部署失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.OverallProgress = 0;
            _viewModel.ActiveTasks.Clear();
            _viewModel.ProgressStatus = "就绪，等待操作";
            SetAllButtonsEnabled(true);
            DeployPortableBtn.IsEnabled = true;
        }
    }

    #endregion

    #region 快捷方式部署

    private async void DeployShortcutsBtn_Click(object sender, RoutedEventArgs e)
    {
        DeployShortcutsBtn.IsEnabled = false;
        _viewModel.ProgressStatus = "正在部署快捷方式...";
        _viewModel.ActiveTasks.Clear();

        var task = new DeployTask { Name = "快捷方式部署", PhaseText = "下载中..." };
        _viewModel.ActiveTasks.Add(task);

        try
        {
            await _deployService.DeployShortcutsAsync(
                new Progress<double>(p => task.OverallProgress = p),
                new Progress<string>(info => task.SpeedText = info));

            _viewModel.ProgressStatus = "快捷方式部署完成！";
            MessageBox.Show("快捷方式已成功复制到桌面。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (FileNotFoundException ex)
        {
            MessageBox.Show(ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"部署失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.OverallProgress = 0;
            _viewModel.ActiveTasks.Clear();
            _viewModel.ProgressStatus = "就绪，等待操作";
            DeployShortcutsBtn.IsEnabled = true;
        }
    }

    #endregion

    #region 一键安装已选

    private async void InstallSelectedBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.AllApps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先勾选要安装的软件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetAllButtonsEnabled(false);
        _viewModel.ActiveTasks.Clear();

        // 为每个选中应用创建独立任务
        var workItems = selected.Select(app => (
            app.Name,
            (Func<DeployTask, Task>)(async (task) =>
            {
                task.PhaseText = "下载中...";
                await _deployService.DownloadAndInstallAppAsync(app.Name, app.Url,
                    new Progress<double>(p =>
                    {
                        app.DownloadProgress = p;
                        task.OverallProgress = p;
                    }),
                    new Progress<string>(info => task.SpeedText = info),
                    new Progress<string>(path => task.PhaseText = $"安装: {Path.GetFileName(path)}"));
                app.Status = Models.AppStatus.Completed;
                app.IsSelected = false;
                task.OverallProgress = 100;
                task.PhaseText = "安装完成";
            })
        )).ToList();

        var runner = new TaskRunner(maxConcurrency: 3);
        _viewModel.ProgressStatus = $"正在并发安装 {selected.Count} 个软件...";

        try
        {
            // 将 runner 的任务同步到 ViewModel
            runner.Tasks.CollectionChanged += (_, _) =>
            {
                _viewModel.ActiveTasks.Clear();
                foreach (var t in runner.Tasks)
                    _viewModel.ActiveTasks.Add(t);
            };

            await runner.RunAllAsync(workItems);

            var completed = runner.Tasks.Count(t => t.Status == DeployTaskStatus.Completed);
            _viewModel.ProgressStatus = $"安装完成: {completed}/{selected.Count}";
            MessageBox.Show($"安装完成！\n成功: {completed}/{selected.Count}", "完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"安装过程出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.OverallProgress = 0;
            _viewModel.ActiveTasks.Clear();
            _viewModel.UpdateSelectionCount();
            UpdateSelectAllButton();
            SetAllButtonsEnabled(true);
        }
    }

    #endregion

    #region 一键部署全部（并行多任务）

    private async void DeployAllBtn_Click(object sender, RoutedEventArgs e)
    {
        SetAllButtonsEnabled(false);
        _viewModel.ActiveTasks.Clear();
        _viewModel.ProgressStatus = "正在并行部署全部...";

        // 创建三个并行任务
        var portableTask = new DeployTask { Name = "📦 便携应用", PhaseText = "等待中..." };
        var shortcutTask = new DeployTask { Name = "🔗 快捷方式", PhaseText = "等待中..." };
        var installTask = new DeployTask { Name = "⬇ 安装版软件", PhaseText = "等待中..." };

        _viewModel.ActiveTasks.Add(portableTask);
        _viewModel.ActiveTasks.Add(shortcutTask);
        _viewModel.ActiveTasks.Add(installTask);

        var workItems = new List<(string, Func<DeployTask, Task>)>();

        // 便携应用
        workItems.Add(("📦 便携应用", async (task) =>
        {
            try
            {
                var content = await _repo.DownloadTextAsync("portable-apps.txt");
                var (_, _, links) = DeployService.ParsePortableManifest(content);
                if (links.Count == 0)
                {
                    task.PhaseText = "无便携清单，跳过";
                    return;
                }
                await _deployService.DeployPortableFromLinksAsync(links, task,
                    overwriteCallback: null,
                    new Progress<string>(info => task.SpeedText = info));
                task.PhaseText = "便携应用完成";
            }
            catch (Exception ex)
            {
                task.PhaseText = $"跳过: {ex.Message}";
                Trace.WriteLine($"[DeployAll] 便携跳过: {ex.Message}");
            }
        }));

        // 快捷方式
        workItems.Add(("🔗 快捷方式", async (task) =>
        {
            try
            {
                await _deployService.DeployShortcutsAsync(
                    new Progress<double>(p => task.OverallProgress = p),
                    new Progress<string>(info => task.SpeedText = info));
                task.PhaseText = "快捷方式完成";
            }
            catch (Exception ex)
            {
                task.PhaseText = $"跳过: {ex.Message}";
                Trace.WriteLine($"[DeployAll] 快捷方式跳过: {ex.Message}");
            }
        }));

        // 安装版软件
        workItems.Add(("⬇ 安装版软件", async (task) =>
        {
            _viewModel.SelectAll();
            _viewModel.UpdateSelectionCount();
            Dispatcher.Invoke(() => UpdateSelectAllButton());

            var selected = _viewModel.AllApps.Where(a => a.IsSelected).ToList();
            task.PhaseText = $"共 {selected.Count} 个";

            for (var i = 0; i < selected.Count; i++)
            {
                var app = selected[i];
                task.PhaseText = $"({i + 1}/{selected.Count}) {app.Name}";
                try
                {
                    await _deployService.DownloadAndInstallAppAsync(app.Name, app.Url,
                        new Progress<double>(p =>
                        {
                            app.DownloadProgress = p;
                            task.OverallProgress = (double)i / selected.Count * 100 + p / selected.Count;
                        }),
                        new Progress<string>(info => task.SpeedText = info),
                        new Progress<string>(path => task.PhaseText = $"({i + 1}/{selected.Count}) 安装: {Path.GetFileName(path)}"));
                    app.Status = Models.AppStatus.Completed;
                    app.IsSelected = false;
                }
                catch (Exception ex)
                {
                    app.Status = Models.AppStatus.Error;
                    app.ErrorMessage = ex.Message;
                }
            }
            task.OverallProgress = 100;
            task.PhaseText = $"安装版完成 ({selected.Count} 个)";
        }));

        var runner = new TaskRunner(maxConcurrency: 3);
        try
        {
            await runner.RunAllAsync(workItems);
            _viewModel.ProgressStatus = "一键部署全部完成！";
            MessageBox.Show("一键部署完成！\n便携应用 + 快捷方式 + 安装版软件已全部处理。", "完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DeployAll] 异常: {ex.Message}");
            MessageBox.Show($"部署出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.OverallProgress = 0;
            _viewModel.ActiveTasks.Clear();
            _viewModel.UpdateSelectionCount();
            Dispatcher.Invoke(() => UpdateSelectAllButton());
            _viewModel.ProgressStatus = "就绪，等待操作";
            SetAllButtonsEnabled(true);
        }
    }

    #endregion

    #region 辅助方法

    private void SetAllButtonsEnabled(bool enabled)
    {
        InstallSelectedBtn.IsEnabled = enabled;
        DeployPortableBtn.IsEnabled = enabled;
        DeployShortcutsBtn.IsEnabled = enabled;
        CheckVersionBtn.IsEnabled = enabled;
        DeployAllBtn.IsEnabled = enabled;
        SelectAllBtn.IsEnabled = enabled;
    }

    #endregion
}
