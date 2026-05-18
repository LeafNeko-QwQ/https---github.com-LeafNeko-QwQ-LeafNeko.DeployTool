using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LeafNeko.DeployTool.Services;

namespace LeafNeko.DeployTool.Views;

public partial class LogUploadDialog : Window
{
    private readonly LogUploadService _uploader = new();
    private bool _uploading;

    public bool UploadSuccess { get; private set; }

    public LogUploadDialog(string summary, string[] files)
    {
        InitializeComponent();
        SummaryText.Text = summary;
    }

    private async void UploadBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_uploading) return;
        _uploading = true;
        UploadBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;
        ViewBtn.IsEnabled = false;
        UploadBtn.Content = "上传中...";

        var result = await _uploader.UploadAllPendingAsync();
        UploadSuccess = result.Success;

        SummaryText.Text += $"\n\n---\n{result.Message}";

        UploadBtn.Content = result.Success ? "完成" : "重试";
        UploadBtn.IsEnabled = !result.Success;
        CancelBtn.Content = "关闭";
        CancelBtn.IsEnabled = true;

        _uploading = false;
    }

    private void ViewBtn_Click(object sender, RoutedEventArgs e)
    {
        var files = LoggerService.CollectLogFiles();
        foreach (var f in files)
        {
            try { Process.Start(new ProcessStartInfo(f) { UseShellExecute = true }); }
            catch { }
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_uploading) return;
        DialogResult = UploadSuccess;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when !_uploading:
                UploadBtn_Click(sender, e);
                break;
            case Key.Escape when !_uploading:
                DialogResult = UploadSuccess;
                Close();
                break;
        }
    }
}
