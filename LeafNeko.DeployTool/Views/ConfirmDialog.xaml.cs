using System.Windows;
using System.Windows.Input;

namespace LeafNeko.DeployTool.Views;

public partial class ConfirmDialog : Window
{
    public bool IsConfirmed { get; private set; }

    public ConfirmDialog(string message, string title, bool isYesNo = true)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;

        if (isYesNo)
        {
            CancelBtn.Content = "否";
            ConfirmBtn.Content = "是";
        }
        else
        {
            CancelBtn.Visibility = Visibility.Collapsed;
            ConfirmBtn.Content = "确定";
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                IsConfirmed = true;
                DialogResult = true;
                Close();
                break;
            case Key.Escape:
                DialogResult = false;
                Close();
                break;
        }
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }
}
