using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LeafNeko.DeployTool.Controls;
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
    private TaskCompletionSource<bool>? _confirmTcs;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 全局鼠标释放 — 多选模式下任意位置松手退出多选
        PreviewMouseLeftButtonUp += OnGlobalMouseLeftButtonUp;
    }

    private void OnGlobalMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (AppCard.IsMultiSelectActive)
            AppCard.ExitMultiSelect();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsAllSelected))
            UpdateSelectAllButton();
    }

    private void UpdateSelectAllButton()
    {
        SelectAllBtn.Content = _viewModel.IsAllSelected ? "取消全选 ☑" : "全选 ☐";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            PathHelper.EnsureAll();
            _viewModel.InitChangelog();
            _viewModel.SystemInfo.Refresh();
            await _viewModel.LoadAppsAsync();
            _viewModel.OverallProgress = 0;
            StatusBar.SetPersistentText("准备就绪，等待操作");
            StatusBar.Show("清单已就绪", StatusBarState.Ready, autoHideMs: 3000);
            AppCard.RetryRequested += OnRetryAppRequested;
            _ = CheckSelfUpdateAsync();
            _ = _repo.MeasureSpeedAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[MainWindow] OnLoaded 异常: {ex}");
            StatusBar.Show($"软件加载失败: {ex.Message}", StatusBarState.Error, autoHideMs: 8000);
            var errDialog = new ConfirmDialog(
                $"软件加载失败: {ex.Message}\n\n请检查网络连接后重启应用。",
                "启动错误", isYesNo: false) { Owner = this };
            errDialog.ShowDialog();
        }
    }

    private async Task CheckSelfUpdateAsync()
    {
        try
        {
            var localVersion = MainViewModel.VersionText.TrimStart('v');
            var content = await _repo.DownloadTextAsync("latest-version.txt");
            var remoteVersion = content.Trim();
            Trace.WriteLine($"[SelfUpdate] 本地={localVersion}, 远端={remoteVersion}");

            if (Version.TryParse(remoteVersion, out var rv) &&
                Version.TryParse(localVersion, out var lv) &&
                rv > lv)
            {
                Dispatcher.Invoke(() =>
                {
                    var dialog = new ConfirmDialog(
                        $"发现新版本 v{remoteVersion}！\n当前版本: v{localVersion}\n\n是否下载更新？",
                        "发现新版本") { Owner = this };
                    dialog.ShowDialog();
                    if (dialog.IsConfirmed)
                        _ = DownloadAndApplyUpdateAsync(remoteVersion);
                });
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SelfUpdate] 检查失败: {ex.Message}");
        }
    }

    private async Task DownloadAndApplyUpdateAsync(string version)
    {
        try
        {
            var fileName = $"LeafNeko.DeployTool_{version}.exe";
            var destPath = Path.Combine(PathHelper.BaseDir, fileName);
            _viewModel.ProgressStatus = $"正在下载新版本 v{version}...";

            await _repo.DownloadToFileAsync(
                RepoService.BaseUrl + fileName,
                destPath,
                new Progress<double>(p => _viewModel.OverallProgress = p),
                new Progress<string>(info => _viewModel.ProgressStatus = $"下载更新: {info}"));

            _viewModel.OverallProgress = 100;
            _viewModel.ProgressStatus = "下载完成";

            var dialog = new ConfirmDialog(
                $"新版本已下载到:\n{destPath}\n\n是否打开文件位置？",
                "下载完成") { Owner = this };
            dialog.ShowDialog();
            if (dialog.IsConfirmed)
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{destPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusBar.Show($"下载更新失败: {ex.Message}", StatusBarState.Error, autoHideMs: 5000);
        }
        finally
        {
            _viewModel.OverallProgress = 0;
            _viewModel.ProgressStatus = "就绪，等待操作";
        }
    }

    #region 顶部链接

    private void DarkModeToggle_Click(object sender, MouseButtonEventArgs e)
    {
        App.ToggleTheme();
        if (sender is TextBlock tb)
            tb.Text = App.IsDarkMode ? "☀" : "🌙";
    }

    private void CategoryTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Child is TextBlock tb && tb.DataContext is string category)
        {
            // 分类切换时使用快速入场动画
            AppCard.UseFastEntrance = true;

            _viewModel.SelectedCategory = category;
            SmoothScrollToTop();

            if (_selectedTabBorder != null && _selectedTabText != null)
            {
                _selectedTabBorder.Background = Brushes.Transparent;
                _selectedTabText.Foreground = (Brush)FindResource("TextSecondaryBrush");
            }

            border.Background = (Brush)FindResource("CardAccentBrush");
            tb.Foreground = (Brush)FindResource("PrimaryBrush");

            _selectedTabBorder = border;
            _selectedTabText = tb;

            // 300ms 后恢复入场动画（新卡片正常弹入）
            var restoreTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            restoreTimer.Tick += (_, _) =>
            {
                AppCard.UseFastEntrance = false;
                restoreTimer.Stop();
            };
            restoreTimer.Start();
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
        AppCard.SuppressSelectionPulse = true;
        _viewModel.ToggleSelectAll();
        UpdateSelectAllButton();
        PlaySelectAllAnimations();
        AppCard.SuppressSelectionPulse = false;
    }

    private void CheckVersionBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CheckVersions();
    }

    #endregion

    #region 便携应用部署

    private async void DeployPortableBtn_Click(object sender, RoutedEventArgs e)
    {
        DeployPortableBtn.IsEnabled = false;
        _viewModel.ProgressStatus = "正在获取便携应用清单...";

        try
        {
            string content;
            try
            {
                content = await _repo.DownloadTextAsync("portable-apps.txt");
                Trace.WriteLine("[MainWindow] 便携清单已下载");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[MainWindow] 便携清单下载失败: {ex.Message}");
                StatusBar.Show("无法获取便携应用清单，请确认仓库中已上传 portable-apps.txt。",
                    StatusBarState.Error, autoHideMs: 5000);
                return;
            }

            var (date, log, links) = DeployService.ParsePortableManifest(content);
            if (links.Count == 0)
            {
                StatusBar.Show("便携应用清单为空，请先配置直链。",
                    StatusBarState.PartialError, autoHideMs: 4000);
                return;
            }

            Trace.WriteLine($"[MainWindow] 解析到 {links.Count} 个直链, 日期={date}");

            var confirmWindow = new ConfirmDeployWindow(date, log, links) { Owner = this };
            confirmWindow.ShowDialog();

            if (!confirmWindow.IsConfirmed)
            {
                _viewModel.ProgressStatus = "已取消";
                StatusBar.Show("已取消", StatusBarState.PartialError, autoHideMs: 2000);
                Trace.WriteLine("[MainWindow] 用户取消了便携应用部署");
                return;
            }

            Trace.WriteLine("[MainWindow] 用户确认部署，开始多任务下载");

            SetAllButtonsEnabled(false);
            _viewModel.ActiveTasks.Clear();

            var deployTask = new DeployTask { Name = "便携应用部署", PhaseText = "准备中..." };
            _viewModel.ActiveTasks.Add(deployTask);

            StatusBar.Show("正在部署便携应用...", StatusBarState.Working);

            Func<string, Task<bool>> overwriteCallback = async folderName =>
            {
                return await Dispatcher.InvokeAsync(() =>
                {
                    var dialog = new ConfirmDialog(
                        $"文件夹 \"{folderName}\" 已存在于 C 盘根目录，是否覆盖？\n\n选择「是」覆盖已有文件，「否」跳过该文件夹。",
                        "覆盖确认") { Owner = this };
                    dialog.ShowDialog();
                    return dialog.IsConfirmed;
                });
            };

            await _deployService.DeployPortableFromLinksAsync(
                links, deployTask, overwriteCallback,
                new Progress<string>(info => deployTask.SpeedText = info));

            _viewModel.History.Add("便携应用", "便携部署", true, $"部署 {links.Count} 个");
            _viewModel.RefreshHistory();
            _viewModel.ProgressStatus = "便携应用部署完成！";
            StatusBar.Show($"便携应用部署完成！已处理 {links.Count} 个直链。",
                StatusBarState.Success, autoHideMs: 5000);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[MainWindow] 部署失败: {ex.Message}");
            StatusBar.Show($"部署失败: {ex.Message}", StatusBarState.Error, autoHideMs: 6000);
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

            _viewModel.History.Add("快捷方式", "快捷方式部署", true);
            _viewModel.RefreshHistory();
            _viewModel.ProgressStatus = "快捷方式部署完成！";
            StatusBar.Show("快捷方式已成功复制到桌面。",
                StatusBarState.Success, autoHideMs: 4000);
        }
        catch (FileNotFoundException ex)
        {
            StatusBar.Show(ex.Message, StatusBarState.PartialError, autoHideMs: 5000);
        }
        catch (Exception ex)
        {
            StatusBar.Show($"部署失败: {ex.Message}", StatusBarState.Error, autoHideMs: 5000);
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
            StatusBar.Show("请先勾选要安装的软件", StatusBarState.PartialError, autoHideMs: 3000);
            return;
        }

        SetAllButtonsEnabled(false);
        _viewModel.ActiveTasks.Clear();

        var workItems = selected.Select(app => (
            app.Name,
            (Func<DeployTask, Task>)(async (task) =>
            {
                task.PhaseText = "下载中...";
                var result = await _deployService.DownloadAndInstallAppAsync(app.Name, app.Url,
                    new Progress<double>(p =>
                    {
                        app.DownloadProgress = p;
                        task.OverallProgress = p;
                    }),
                    new Progress<string>(info =>
                    {
                        task.SpeedText = info;
                        app.SpeedText = info;
                        if (app.DownloadProgress > 0 && app.DownloadProgress < 100)
                            app.EtaText = $"剩余 ~{(int)((100 - app.DownloadProgress) / app.DownloadProgress * 5)}s";
                    }),
                    new Progress<string>(path => task.PhaseText = $"安装: {Path.GetFileName(path)}"));
                app.Status = Models.AppStatus.Completed;
                app.IsSelected = false;
                task.OverallProgress = 100;
                task.PhaseText = result;
            })
        )).ToList();

        var runner = new TaskRunner(maxConcurrency: 3);
        _viewModel.ProgressStatus = $"正在并发安装 {selected.Count} 个软件...";
        StatusBar.Show($"正在安装 {selected.Count} 个软件...", StatusBarState.Working);

        try
        {
            runner.Tasks.CollectionChanged += (_, _) =>
            {
                _viewModel.ActiveTasks.Clear();
                foreach (var t in runner.Tasks)
                    _viewModel.ActiveTasks.Add(t);
            };

            await runner.RunAllAsync(workItems);

            var completed = runner.Tasks.Count(t => t.Status == DeployTaskStatus.Completed);
            var failed = runner.Tasks.Count(t => t.Status == DeployTaskStatus.Error);
            _viewModel.History.Add("安装版软件", "安装", failed == 0, $"成功 {completed}/{selected.Count}");
            _viewModel.RefreshHistory();
            _viewModel.ProgressStatus = $"安装完成: {completed}/{selected.Count}";

            if (failed == 0)
                StatusBar.Show($"安装完成！成功: {completed}/{selected.Count}",
                    StatusBarState.Success, autoHideMs: 5000);
            else
                StatusBar.Show($"安装完成！成功 {completed}/{selected.Count}，失败 {failed} 个",
                    StatusBarState.PartialError, autoHideMs: 6000);
        }
        catch (Exception ex)
        {
            StatusBar.Show($"安装过程出错: {ex.Message}", StatusBarState.Error, autoHideMs: 5000);
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

    #region 一键部署全部（并行多任务 + 确认弹窗）

    private async void DeployAllBtn_Click(object sender, RoutedEventArgs e)
    {
        // Show confirm overlay
        var confirmed = await ShowConfirmOverlayAsync();
        if (!confirmed)
        {
            StatusBar.Show("已取消", StatusBarState.PartialError, autoHideMs: 2000);
            return;
        }

        SetAllButtonsEnabled(false);
        _viewModel.ProgressStatus = "正在并行部署全部...";
        StatusBar.Show("正在一键部署全部...", StatusBarState.Working);

        var progressWindow = new DeployProgressWindow { Owner = this };

        var workItems = new List<(string, Func<DeployTask, CancellationToken, Task>)>();

        // 便携应用
        workItems.Add(("📦 便携应用", async (task, ct) =>
        {
            try
            {
                var content = await _repo.DownloadTextAsync("portable-apps.txt");
                var (_, _, links) = DeployService.ParsePortableManifest(content);
                if (links.Count == 0)
                {
                    task.PhaseText = "无便携清单，跳过";
                    task.OverallProgress = 100;
                    return;
                }
                await _deployService.DeployPortableFromLinksAsync(links, task,
                    overwriteCallback: null,
                    new Progress<string>(info => task.SpeedText = info),
                    ct);
                task.PhaseText = "便携应用完成";
            }
            catch (Exception ex)
            {
                task.PhaseText = $"跳过: {ex.Message}";
                Trace.WriteLine($"[DeployAll] 便携跳过: {ex.Message}");
            }
        }));

        // 快捷方式
        workItems.Add(("🔗 快捷方式", async (task, ct) =>
        {
            try
            {
                await _deployService.DeployShortcutsAsync(
                    new Progress<double>(p => task.OverallProgress = p),
                    new Progress<string>(info => task.SpeedText = info),
                    ct);
                task.PhaseText = "快捷方式完成";
            }
            catch (Exception ex)
            {
                task.PhaseText = $"跳过: {ex.Message}";
                Trace.WriteLine($"[DeployAll] 快捷方式跳过: {ex.Message}");
            }
        }));

        // 安装版软件
        workItems.Add(("⬇ 安装版软件", async (task, ct) =>
        {
            _viewModel.SelectAll();
            _viewModel.UpdateSelectionCount();
            Dispatcher.Invoke(() => UpdateSelectAllButton());

            var selected = _viewModel.AllApps.ToList();
            task.PhaseText = $"共 {selected.Count} 个";

            for (var i = 0; i < selected.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var app = selected[i];
                task.PhaseText = $"({i + 1}/{selected.Count}) {app.Name}";
                try
                {
                    var statusMsg = await _deployService.DownloadAndInstallAppAsync(app.Name, app.Url,
                        new Progress<double>(p =>
                        {
                            app.DownloadProgress = p;
                            task.OverallProgress = Math.Clamp((double)i / selected.Count * 100 + p / selected.Count, 0, 100);
                        }),
                        new Progress<string>(info => task.SpeedText = info),
                        new Progress<string>(path => task.PhaseText = $"({i + 1}/{selected.Count}) 安装: {Path.GetFileName(path)}"),
                        ct);
                    app.Status = Models.AppStatus.Completed;
                    app.IsSelected = false;
                    task.PhaseText = statusMsg;
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

        progressWindow.Show();
        try
        {
            await progressWindow.RunAsync(workItems);

            _viewModel.ActiveTasks.Clear();
            foreach (var t in progressWindow.Tasks)
                _viewModel.ActiveTasks.Add(t);

            if (progressWindow.IsCancelled)
            {
                _viewModel.ProgressStatus = "部署已取消";
                StatusBar.Show("部署已取消", StatusBarState.PartialError, autoHideMs: 3000);
            }
            else
            {
                var allOk = progressWindow.Tasks.All(t => t.Status != DeployTaskStatus.Error);
                _viewModel.History.Add("一键部署", "全部部署", allOk, $"任务数 {progressWindow.Tasks.Count}");
                _viewModel.RefreshHistory();
                _viewModel.ProgressStatus = "一键部署全部完成！";

                if (allOk)
                    StatusBar.Show("一键部署全部完成！",
                        StatusBarState.Success, autoHideMs: 5000);
                else
                    StatusBar.Show("一键部署完成（部分任务失败）",
                        StatusBarState.PartialError, autoHideMs: 6000);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DeployAll] 异常: {ex.Message}");
            StatusBar.Show($"部署出错: {ex.Message}", StatusBarState.Error, autoHideMs: 5000);
        }
        finally
        {
            progressWindow.Close();
            _viewModel.OverallProgress = 0;
            _viewModel.UpdateSelectionCount();
            Dispatcher.Invoke(() => UpdateSelectAllButton());
            _viewModel.ProgressStatus = "就绪，等待操作";
            SetAllButtonsEnabled(true);
        }
    }

    #endregion

    #region 确认遮罩

    private Task<bool> ShowConfirmOverlayAsync()
    {
        _confirmTcs = new TaskCompletionSource<bool>();

        ConfirmWarningText.Text = $"当前选中 {_viewModel.AllApps.Count(a => a.IsSelected)} 个软件。\n部署过程可能需要几分钟，请保持网络畅通。";

        ConfirmOverlay.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ConfirmOverlay.BeginAnimation(OpacityProperty, fadeIn);

        var scaleIn = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
        };
        ConfirmCard.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
        ConfirmCard.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);

        return _confirmTcs.Task;
    }

    private void HideConfirmOverlay(bool result)
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) => ConfirmOverlay.Visibility = Visibility.Collapsed;
        ConfirmOverlay.BeginAnimation(OpacityProperty, fadeOut);

        _confirmTcs?.TrySetResult(result);
        _confirmTcs = null;
    }

    private void ConfirmOverlayBg_Click(object sender, MouseButtonEventArgs e)
    {
        // 点击遮罩背景不关闭，防止误触
    }

    private void ConfirmCancelBtn_Click(object sender, RoutedEventArgs e)
    {
        HideConfirmOverlay(false);
    }

    private void ConfirmStartBtn_Click(object sender, RoutedEventArgs e)
    {
        HideConfirmOverlay(true);
    }

    #endregion

    #region 辅助方法

    private static int _sidebarCardIndex;

    private void SidebarCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border card) return;

        var index = Interlocked.Increment(ref _sidebarCardIndex);
        card.RenderTransformOrigin = new Point(0.5, 0.5);
        card.RenderTransform = new TranslateTransform(50, 0);
        card.Opacity = 0;

        var sb = new Storyboard { BeginTime = TimeSpan.FromMilliseconds(index * 80) };

        var slideIn = new DoubleAnimation(50, 0, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideIn, card);
        Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeIn, card);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));

        sb.Children.Add(slideIn);
        sb.Children.Add(fadeIn);
        sb.Begin();
    }

    private void SmoothScrollToTop()
    {
        var startOffset = CardScrollViewer.VerticalOffset;
        if (startOffset < 1) return;

        var sw = Stopwatch.StartNew();
        var duration = 350;
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            var elapsed = sw.Elapsed.TotalMilliseconds;
            var t = Math.Clamp(elapsed / duration, 0, 1);
            var eased = 1 - Math.Pow(1 - t, 3);
            CardScrollViewer.ScrollToVerticalOffset(startOffset * (1 - eased));
            if (t >= 1)
            {
                CompositionTarget.Rendering -= handler;
                sw.Stop();
            }
        };
        CompositionTarget.Rendering += handler;
    }

    private void SetAllButtonsEnabled(bool enabled)
    {
        InstallSelectedBtn.IsEnabled = enabled;
        DeployPortableBtn.IsEnabled = enabled;
        DeployShortcutsBtn.IsEnabled = enabled;
        CheckVersionBtn.IsEnabled = enabled;
        DeployAllBtn.IsEnabled = enabled;
        SelectAllBtn.IsEnabled = enabled;
    }

    private void PlaySelectAllAnimations()
    {
        var items = _viewModel.FilteredApps.ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var container = CardItemsControl.ItemContainerGenerator?.ContainerFromItem(items[i]);
            if (container is ContentPresenter cp && VisualTreeHelper.GetChildrenCount(cp) > 0)
            {
                if (VisualTreeHelper.GetChild(cp, 0) is AppCard card)
                    card.PlaySelectAllAnimation(i, items.Count);
            }
        }
    }

    private async void OnRetryAppRequested(AppItemViewModel vm)
    {
        vm.Status = Models.AppStatus.Pending;
        vm.ErrorMessage = "";
        vm.IsSelected = true;

        StatusBar.Show($"正在重试: {vm.Name}", StatusBarState.Working);

        try
        {
            var result = await _deployService.DownloadAndInstallAppAsync(vm.Name, vm.Url,
                new Progress<double>(p => vm.DownloadProgress = p),
                new Progress<string>(info => vm.SpeedText = info),
                new Progress<string>(_ => { }));
            vm.Status = Models.AppStatus.Completed;
            vm.IsSelected = false;
            StatusBar.Show($"{vm.Name} 安装成功", StatusBarState.Success, autoHideMs: 4000);
        }
        catch (Exception ex)
        {
            vm.Status = Models.AppStatus.Error;
            vm.ErrorMessage = ex.Message;
            StatusBar.Show($"{vm.Name} 重试失败: {ex.Message}", StatusBarState.Error, autoHideMs: 5000);
        }
    }

    #endregion

}
