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

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }
}
