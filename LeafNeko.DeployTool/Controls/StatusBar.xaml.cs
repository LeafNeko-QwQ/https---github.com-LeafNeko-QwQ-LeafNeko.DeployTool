using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LeafNeko.DeployTool.Controls;

public enum StatusBarState
{
    Hidden,
    Preparing,
    Ready,
    Working,
    Success,
    Error,
    PartialError
}

public partial class StatusBar : UserControl
{
    private DispatcherTimer? _hideTimer;
    private Storyboard? _pulseStoryboard;

    public StatusBar()
    {
        InitializeComponent();
    }

    public void Show(string text, StatusBarState state, double progress = 0, int autoHideMs = 0)
    {
        KillTimer();
        StopPulse();
        BgProgress.Value = progress;

        // Reset from any previous fade-out
        StatusBorder.Opacity = 1;
        StatusBorder.Visibility = Visibility.Visible;

        switch (state)
        {
            case StatusBarState.Hidden:
                StatusBorder.Visibility = Visibility.Collapsed;
                return;

            case StatusBarState.Preparing:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                StatusText.Foreground = (Brush)FindResource("TextPrimaryBrush");
                StatusText.Text = text;
                break;

            case StatusBarState.Ready:
                StatusBorder.Background = new LinearGradientBrush(
                    Color.FromRgb(0xE0, 0xE0, 0xE0),
                    Color.FromRgb(0x81, 0xC7, 0x84),
                    0);
                StatusText.Foreground = (Brush)FindResource("TextPrimaryBrush");
                StatusText.Text = text;
                break;

            case StatusBarState.Working:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
                StatusText.Foreground = Brushes.White;
                StatusText.Text = text;
                StartPulse();
                break;

            case StatusBarState.Success:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                StatusText.Foreground = Brushes.White;
                StatusText.Text = text;
                SystemSounds.Asterisk.Play();
                break;

            case StatusBarState.Error:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
                StatusText.Foreground = Brushes.White;
                StatusText.Text = text;
                FlashRed();
                SystemSounds.Hand.Play();
                break;

            case StatusBarState.PartialError:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
                StatusText.Text = text;
                SystemSounds.Exclamation.Play();
                break;
        }

        if (autoHideMs > 0)
        {
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(autoHideMs) };
            _hideTimer.Tick += (_, _) =>
            {
                KillTimer();
                FadeOut();
            };
            _hideTimer.Start();
        }
    }

    private void StartPulse()
    {
        _pulseStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
        var anim = new ColorAnimation(
            Color.FromRgb(0xE5, 0x39, 0x35),
            Color.FromRgb(0xFF, 0x52, 0x52),
            TimeSpan.FromMilliseconds(800));
        Storyboard.SetTarget(anim, StatusBorder);
        Storyboard.SetTargetProperty(anim, new PropertyPath("Background.Color"));
        _pulseStoryboard.Children.Add(anim);
        _pulseStoryboard.Begin();
    }

    private void StopPulse()
    {
        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;
    }

    private void FlashRed()
    {
        var flash = new ColorAnimation
        {
            From = Color.FromRgb(0xFF, 0x17, 0x17),
            To = Color.FromRgb(0xE5, 0x39, 0x35),
            Duration = TimeSpan.FromMilliseconds(200),
            AutoReverse = true
        };
        StatusBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, flash);
    }

    private void FadeOut()
    {
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => StatusBorder.Visibility = Visibility.Collapsed;
        StatusBorder.BeginAnimation(OpacityProperty, fade);
    }

    private void KillTimer()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }
}
