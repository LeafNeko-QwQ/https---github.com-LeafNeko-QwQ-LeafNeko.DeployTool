using System.Windows;
using LeafNeko.DeployTool.Services;

namespace LeafNeko.DeployTool.Views;

public partial class LicenseWindow : Window
{
    private readonly LicenseService _licenseService = new();

    public LicenseWindow()
    {
        InitializeComponent();
        LicenseTextBlock.Text = _licenseService.GetLicenseText();
    }

    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        _licenseService.Accept();
        var mainWindow = new MainWindow();
        mainWindow.Show();
        Close();
    }

    private void DisagreeButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
