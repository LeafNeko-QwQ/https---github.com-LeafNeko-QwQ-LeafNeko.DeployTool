using System.Text;
using System.Windows;
using LeafNeko.DeployTool.Services;
using LeafNeko.DeployTool.Views;

namespace LeafNeko.DeployTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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
