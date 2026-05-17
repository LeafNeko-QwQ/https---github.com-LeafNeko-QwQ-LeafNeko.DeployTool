using System.Diagnostics;
using System.IO;
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
        // 全局崩溃捕获 — 写入桌面 crash.log
        var crashLog = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "leafneko_crash.log");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            System.IO.File.WriteAllText(crashLog,
                $"=== 未处理异常 ===\n{DateTime.Now}\n{ex}\n");
        };
        DispatcherUnhandledException += (_, args) =>
        {
            System.IO.File.WriteAllText(crashLog,
                $"=== Dispatcher 异常 ===\n{DateTime.Now}\n{args.Exception}\n");
            args.Handled = true;
        };

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 初始化文件日志 — 所有 Trace.WriteLine 自动写入日志文件
        SetupFileLogging();

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
        Trace.WriteLine($"[App] 应用退出 — {DateTime.Now}");
        Trace.Flush();
        base.OnExit(e);
    }

    private static void SetupFileLogging()
    {
        try
        {
            PathHelper.EnsureAll();
            var logFile = Path.Combine(PathHelper.LogsDir, $"deploytool_{DateTime.Now:yyyyMMdd}.log");
            var listener = new TextWriterTraceListener(logFile, "FileLogger");
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
            Trace.WriteLine($"[App] 日志已启动 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            PathHelper.CleanOldLogs(7);
        }
        catch
        {
            // 日志初始化失败不阻塞启动
        }
    }
}
