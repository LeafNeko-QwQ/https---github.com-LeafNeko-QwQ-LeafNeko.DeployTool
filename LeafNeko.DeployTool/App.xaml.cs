using System.Text;
using System.Windows;
using LeafNeko.DeployTool.Models;
using LeafNeko.DeployTool.Services;
using LeafNeko.DeployTool.Views;

namespace LeafNeko.DeployTool;

public partial class App : Application
{
    public static bool IsDarkMode { get; private set; }

    public static void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        var themeName = IsDarkMode ? "DarkTheme.xaml" : "PinkTheme.xaml";
        var newDict = new ResourceDictionary { Source = new Uri($"Themes/{themeName}", UriKind.Relative) };

        var oldDict = Current.Resources.MergedDictionaries[0];
        Current.Resources.MergedDictionaries.RemoveAt(0);
        Current.Resources.MergedDictionaries.Insert(0, newDict);

        var config = DeployConfig.Load();
        config.DarkMode = IsDarkMode;
        config.Save();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var config = DeployConfig.Load();
        if (config.DarkMode)
        {
            IsDarkMode = true;
            var darkDict = new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) };
            Resources.MergedDictionaries.RemoveAt(0);
            Resources.MergedDictionaries.Insert(0, darkDict);
        }

        base.OnStartup(e);

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
}
