using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LeafNeko.DeployTool.Services;
using LeafNeko.DeployTool.ViewModels;

namespace LeafNeko.DeployTool.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly DeployService _deployService = new();
    private readonly DownloadService _downloadService = new();
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
        await _viewModel.LoadAppsAsync();
        _viewModel.OverallProgress = 0;
    }

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

    private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleSelectAll();
        UpdateSelectAllButton();
    }

    private void CheckVersionBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CheckVersions();
    }

    private async void InstallSelectedBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.AllApps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先勾选要安装的软件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        InstallSelectedBtn.IsEnabled = false;
        DeployPortableBtn.IsEnabled = false;
        DeployShortcutsBtn.IsEnabled = false;
        CheckVersionBtn.IsEnabled = false;
        _viewModel.ResetProgressDetail();

        try
        {
            await _downloadService.InstallAppsAsync(
                _viewModel.AllApps.ToList(),
                new Progress<string>(status => _viewModel.ProgressStatus = status),
                new Progress<double>(p => _viewModel.OverallProgress = p),
                new Progress<(int index, double progress)>(item =>
                    _viewModel.AllApps[item.index].DownloadProgress = item.progress),
                new Progress<string>(SetSpeedInfo),
                new Progress<string>(path => _viewModel.DownloadFileName = $"下载到: {path}"));

            MessageBox.Show("安装完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show($"安装过程出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.OverallProgress = 0;
            _viewModel.ResetProgressDetail();
            InstallSelectedBtn.IsEnabled = true;
            DeployPortableBtn.IsEnabled = true;
            DeployShortcutsBtn.IsEnabled = true;
            CheckVersionBtn.IsEnabled = true;
        }
    }

    private async void DeployPortableBtn_Click(object sender, RoutedEventArgs e)
    {
        DeployPortableBtn.IsEnabled = false;
        _viewModel.ProgressStatus = "正在部署便携应用...";
        _viewModel.ResetProgressDetail();

        try
        {
            Func<string, Task<bool>> overwriteCallback = folderName =>
            {
                var result = MessageBox.Show(
                    $"文件夹 \"{folderName}\" 已存在于 C 盘根目录，是否覆盖？\n\n选择「是」覆盖已有文件，「否」跳过该文件夹。",
                    "覆盖确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                return Task.FromResult(result == MessageBoxResult.Yes);
            };

            await _deployService.DeployPortableAppsAsync(
                new Progress<double>(p => _viewModel.OverallProgress = p),
                overwriteCallback,
                new Progress<string>(SetSpeedInfo),
                new Progress<(int phase, double percent, string label)>(SetPhaseInfo));

            _viewModel.ProgressStatus = "便携应用部署完成！";
            MessageBox.Show("便携应用已成功解压到 C 盘根目录。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
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
            _viewModel.ResetProgressDetail();
            _viewModel.ProgressStatus = "就绪，等待操作";
            DeployPortableBtn.IsEnabled = true;
        }
    }

    private async void DeployShortcutsBtn_Click(object sender, RoutedEventArgs e)
    {
        DeployShortcutsBtn.IsEnabled = false;
        _viewModel.ProgressStatus = "正在部署快捷方式...";
        _viewModel.ResetProgressDetail();

        try
        {
            await _deployService.DeployShortcutsAsync(
                new Progress<double>(p => _viewModel.OverallProgress = p),
                new Progress<string>(SetSpeedInfo));

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
            _viewModel.ResetProgressDetail();
            _viewModel.ProgressStatus = "就绪，等待操作";
            DeployShortcutsBtn.IsEnabled = true;
        }
    }

    private async void DeployAllBtn_Click(object sender, RoutedEventArgs e)
    {
        SetAllButtonsEnabled(false);
        _viewModel.ResetProgressDetail();

        try
        {
            // Step 1: 部署便携应用
            _viewModel.PhaseText = "📦 一键部署 - 便携应用";
            _viewModel.ProgressStatus = "正在部署便携应用...";
            try
            {
                await _deployService.DeployPortableAppsAsync(
                    new Progress<double>(p => _viewModel.OverallProgress = p / 3),
                    folderName =>
                    {
                        var result = MessageBox.Show(
                            $"文件夹 \"{folderName}\" 已存在于 C 盘根目录，是否覆盖？\n\n选择「是」覆盖已有文件，「否」跳过该文件夹。",
                            "覆盖确认",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        return Task.FromResult(result == MessageBoxResult.Yes);
                    },
                    new Progress<string>(SetSpeedInfo),
                    new Progress<(int phase, double percent, string label)>(SetPhaseInfo));
            }
            catch (FileNotFoundException ex)
            {
                _viewModel.ProgressStatus = $"便携应用跳过: {ex.Message}";
            }

            _viewModel.ResetProgressDetail();

            // Step 2: 部署快捷方式
            _viewModel.PhaseText = "🔗 一键部署 - 快捷方式";
            _viewModel.ProgressStatus = "正在部署快捷方式...";
            try
            {
                await _deployService.DeployShortcutsAsync(
                    new Progress<double>(p => _viewModel.OverallProgress = 33.3 + p / 3),
                    new Progress<string>(SetSpeedInfo));
            }
            catch (FileNotFoundException ex)
            {
                _viewModel.ProgressStatus = $"快捷方式跳过: {ex.Message}";
            }

            _viewModel.ResetProgressDetail();

            // Step 3: 安装全部软件
            _viewModel.PhaseText = "⬇ 一键部署 - 安装版软件";
            _viewModel.SelectAll();
            _viewModel.UpdateSelectionCount();
            UpdateSelectAllButton();

            await _downloadService.InstallAppsAsync(
                _viewModel.AllApps.ToList(),
                new Progress<string>(status => _viewModel.ProgressStatus = status),
                new Progress<double>(p => _viewModel.OverallProgress = 66.7 + p / 3),
                new Progress<(int index, double progress)>(item =>
                    _viewModel.AllApps[item.index].DownloadProgress = item.progress),
                new Progress<string>(SetSpeedInfo),
                new Progress<string>(path => _viewModel.DownloadFileName = $"下载到: {path}"));

            _viewModel.OverallProgress = 100;
            _viewModel.ProgressStatus = "一键部署全部完成！";
            _viewModel.PhaseText = "✅ 全部完成";
            MessageBox.Show("一键部署全部完成！\n便携应用 + 快捷方式 + 安装版软件已全部处理。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show($"一键部署出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.OverallProgress = 0;
            _viewModel.ResetProgressDetail();
            _viewModel.ProgressStatus = "就绪，等待操作";
            SetAllButtonsEnabled(true);
        }
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

    private void SetSpeedInfo(string info)
    {
        var parts = info.Split(" | ");
        if (parts.Length >= 1) _viewModel.ElapsedText = parts[0];
        if (parts.Length >= 2) _viewModel.SpeedText = parts[1];
        if (parts.Length >= 3) _viewModel.EtaText = parts[2];
    }

    private void SetPhaseInfo((int phase, double percent, string label) info)
    {
        _viewModel.DownloadFileName = info.label;
        switch (info.phase)
        {
            case 0:
                _viewModel.PhaseText = "📥 下载中";
                _viewModel.DownloadProgress = info.percent;
                break;
            case 1:
                _viewModel.PhaseText = "📦 解压中";
                _viewModel.ExtractProgress = info.percent;
                break;
            case 2:
                _viewModel.PhaseText = "📋 复制中";
                _viewModel.CopyProgress = info.percent;
                break;
        }
    }
}
