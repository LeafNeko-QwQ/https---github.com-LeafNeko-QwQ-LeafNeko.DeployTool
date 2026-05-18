using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using LeafNeko.DeployTool.Helpers;
using LeafNeko.DeployTool.Models;
using LeafNeko.DeployTool.Services;
using LeafNeko.DeployTool.Views;

namespace LeafNeko.DeployTool;

public partial class App : Application
{
    public static bool IsDarkMode { get; private set; }
    private TrayIcon? _trayIcon;

    public static void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        ApplyTheme(IsDarkMode);

        var config = DeployConfig.Load();
        config.DarkMode = IsDarkMode;
        config.Save();
    }

    private static void ApplyTheme(bool dark)
    {
        var themeName = dark ? "DarkTheme.xaml" : "PinkTheme.xaml";
        var newDict = new ResourceDictionary { Source = new Uri($"Themes/{themeName}", UriKind.Relative) };
        Current.Resources.MergedDictionaries.RemoveAt(0);
        Current.Resources.MergedDictionaries.Insert(0, newDict);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 初始化结构化日志系统
        LoggerService.Init();

        // 全局崩溃捕获
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            LoggerService.Fatal("App", $"未处理异常: {ex?.Message}");
            if (ex != null) LoggerService.WriteCrashLog(ex);
            if (!IsUserCancel(ex))
                PromptUploadOnNextLaunch();
        };
        DispatcherUnhandledException += (_, args) =>
        {
            LoggerService.Fatal("App", $"Dispatcher 异常: {args.Exception.Message}");
            LoggerService.WriteCrashLog(args.Exception);
            args.Handled = true;

            if (!IsUserCancel(args.Exception))
                Dispatcher.BeginInvoke(() => ShowCrashUploadDialog(args.Exception));
        };

        var config = DeployConfig.Load();

        if (config.DarkMode)
        {
            IsDarkMode = true;
            var darkDict = new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) };
            Resources.MergedDictionaries.RemoveAt(0);
            Resources.MergedDictionaries.Insert(0, darkDict);
        }

        base.OnStartup(e);

        // 系统托盘图标
        _trayIcon = new TrayIcon
        {
            Visible = false,
            ToolTip = "LeafNeko 装机助手"
        };
        _trayIcon.DoubleClick += ShowMainWindow;
        _trayIcon.ExitRequested += ShutdownApp;

        var licenseService = new LicenseService();

        if (licenseService.IsAccepted())
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        else
        {
            var licenseWindow = new LicenseWindow();
            licenseWindow.Show();
        }
    }

    public static void MinimizeToTray()
    {
        var app = (App)Current;
        if (app._trayIcon != null)
            app._trayIcon.Visible = true;
        Current.MainWindow?.Hide();
    }

    public static void ShowMainWindow()
    {
        var app = (App)Current;
        if (app._trayIcon != null)
            app._trayIcon.Visible = false;

        if (Current.MainWindow == null)
        {
            var win = new MainWindow();
            Current.MainWindow = win;
            win.Show();
        }
        else
        {
            Current.MainWindow.Show();
            Current.MainWindow.WindowState = WindowState.Normal;
            Current.MainWindow.Activate();
        }
    }

    public static void ShutdownApp()
    {
        var app = (App)Current;
        app._trayIcon?.Dispose();
        app._trayIcon = null;
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        LoggerService.Info("App", "应用退出");
        Trace.Flush();
        base.OnExit(e);
    }

    private static bool IsUserCancel(Exception? ex)
    {
        if (ex is OperationCanceledException oce && oce.CancellationToken.IsCancellationRequested)
            return true;
        // 递归检查 InnerException（异步操作可能包裹在 AggregateException 中）
        if (ex is AggregateException ae)
            return ae.InnerExceptions.Any(IsUserCancel);
        return ex?.InnerException != null && IsUserCancel(ex.InnerException);
    }

    private static void PromptUploadOnNextLaunch()
    {
        try
        {
            var flagFile = System.IO.Path.Combine(PathHelper.CrashLogsDir, ".pending_upload");
            System.IO.File.WriteAllText(flagFile, DateTime.Now.ToString("O"));
        }
        catch { }
    }

    private static void ShowCrashUploadDialog(Exception ex)
    {
        var summary = "检测到程序崩溃，是否上传日志帮助开发者排查问题？\n\n"
                      + $"错误: {ex.Message}\n\n"
                      + LoggerService.GetLogSummary();
        var files = LoggerService.CollectLogFiles();
        var dialog = new LogUploadDialog(summary, files)
        {
            Owner = Current.MainWindow
        };
        dialog.ShowDialog();
    }
}
