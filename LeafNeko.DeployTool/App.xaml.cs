using System.Diagnostics;
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

    private static bool IsSystemDarkMode()
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("SystemUsesLightTheme");
            if (value is int ival)
                return ival == 0;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[App] 读取系统主题失败: {ex.Message}");
        }
        return false;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var config = DeployConfig.Load();

        bool useDark;
        if (config.AutoDarkMode)
        {
            useDark = IsSystemDarkMode();
            IsDarkMode = useDark;
        }
        else
        {
            useDark = config.DarkMode;
            IsDarkMode = useDark;
        }

        if (useDark)
        {
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

        // 后台清理 24h+ 旧文件
        Task.Run(() => DeployService.CleanOldDownloads());

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
        base.OnExit(e);
    }
}
