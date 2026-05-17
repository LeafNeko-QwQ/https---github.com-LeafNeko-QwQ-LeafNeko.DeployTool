using LeafNeko.DeployTool.Services;
using System;
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
    private string _persistentText = "";

    // ── 预创建画刷 ──
    private static readonly SolidColorBrush PreparingBg = new(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private static readonly SolidColorBrush WorkingBg = new(Color.FromRgb(0xE5, 0x39, 0x35));
    private static readonly SolidColorBrush SuccessBg = new(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly SolidColorBrush ErrorBgSolid = new(Color.FromRgb(0xE5, 0x39, 0x35));
    private static readonly SolidColorBrush PartialErrorBg = new(Color.FromRgb(0xFF, 0xC1, 0x07));
    private static readonly SolidColorBrush PersistentBg = new(Color.FromRgb(0xE8, 0xE8, 0xE8));
    private static readonly SolidColorBrush DarkTextBrush = new(Color.FromRgb(0x3D, 0x3D, 0x3D));
    private static readonly Color ReadyColor1 = Color.FromRgb(0xE0, 0xE0, 0xE0);
    private static readonly Color ReadyColor2 = Color.FromRgb(0x81, 0xC7, 0x84);

    public StatusBar()
    {
        InitializeComponent();
        StatusBorder.Background = PersistentBg;
    }

    public void SetPersistentText(string text)
    {
        _persistentText = text;
    }

    public void Show(string text, StatusBarState state, double progress = 0, int autoHideMs = 0)
    {
        // 取消所有进行中的动画，避免堆积
        StatusBorder.BeginAnimation(OpacityProperty, null);
        StatusBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
        KillTimer();
        StopPulse();
        BgProgress.Value = progress;

        if (state == StatusBarState.Hidden)
        {
            FadeToCollapsed();
            return;
        }

        if (StatusBorder.Visibility == Visibility.Visible && StatusBorder.Opacity > 0.2)
        {
            CrossFadeText(text, state, autoHideMs);
        }
        else
        {
            ApplyState(text, state);
            StatusBorder.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            StatusBorder.BeginAnimation(OpacityProperty, fadeIn);
            if (autoHideMs > 0)
                StartAutoHide(autoHideMs);
        }
    }

    private void CrossFadeText(string text, StatusBarState state, int autoHideMs)
    {
        // 第一步：文字淡出到 0.3（不完全消失，避免闪烁感）
        var fadeOut = new DoubleAnimation(StatusBorder.Opacity, 0.3, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            // 背景色平滑过渡
            var targetBg = GetBrushForState(state);
            if (targetBg is SolidColorBrush scb && StatusBorder.Background is SolidColorBrush oldBg)
            {
                var colorAnim = new ColorAnimation(oldBg.Color, scb.Color, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                StatusBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
            }
            else
            {
                StatusBorder.Background = targetBg;
            }

            StatusText.Foreground = GetForegroundForState(state);
            StatusText.Text = text;

            if (state == StatusBarState.Working)
                StartPulse();
            if (state == StatusBarState.Error)
                FlashRed();

            PlayStateSound(state);

            // 淡入回 1.0
            var fadeIn = new DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            StatusBorder.BeginAnimation(OpacityProperty, fadeIn);
        };
        StatusBorder.BeginAnimation(OpacityProperty, fadeOut);

        if (autoHideMs > 0)
            StartAutoHide(autoHideMs);
    }

    private void ApplyState(string text, StatusBarState state)
    {
        StatusBorder.Background = GetBrushForState(state);
        StatusText.Foreground = GetForegroundForState(state);
        StatusText.Text = text;

        if (state == StatusBarState.Working)
            StartPulse();
        if (state == StatusBarState.Error)
            FlashRed();

        PlayStateSound(state);
    }

    private Brush GetBrushForState(StatusBarState state) => state switch
    {
        StatusBarState.Preparing => PreparingBg,
        StatusBarState.Ready => new LinearGradientBrush(ReadyColor1, ReadyColor2, 0),
        StatusBarState.Working => WorkingBg,
        StatusBarState.Success => SuccessBg,
        StatusBarState.Error => ErrorBgSolid,
        StatusBarState.PartialError => PartialErrorBg,
        _ => PersistentBg
    };

    private Brush GetForegroundForState(StatusBarState state)
    {
        return state switch
        {
            StatusBarState.Working => Brushes.White,
            StatusBarState.Success => Brushes.White,
            StatusBarState.Error => Brushes.White,
            StatusBarState.PartialError => DarkTextBrush,
            _ => (Brush)FindResource("TextPrimaryBrush")
        };
    }

    private static void PlayStateSound(StatusBarState state)
    {
        switch (state)
        {
            case StatusBarState.Ready: SoundService.PlayBindDone(); break;
            case StatusBarState.Success: SoundService.PlayStart(); break;
            case StatusBarState.Error: SoundService.PlaySigmaDisable(); break;
            case StatusBarState.PartialError: SoundService.PlayDisable(); break;
        }
    }

    private void ShowPersistent()
    {
        StatusBorder.BeginAnimation(OpacityProperty, null);
        StatusBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);

        if (StatusBorder.Visibility == Visibility.Visible && StatusBorder.Opacity > 0.2)
        {
            var fadeOut = new DoubleAnimation(StatusBorder.Opacity, 0.3, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, _) =>
            {
                StopPulse();
                if (StatusBorder.Background is SolidColorBrush scb)
                {
                    var colorAnim = new ColorAnimation(
                        scb.Color,
                        PersistentBg.Color,
                        TimeSpan.FromMilliseconds(200))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    StatusBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
                }
                else
                {
                    StatusBorder.Background = PersistentBg;
                }
                StatusText.Foreground = DarkTextBrush;
                StatusText.Text = _persistentText;
                var fadeIn = new DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                StatusBorder.BeginAnimation(OpacityProperty, fadeIn);
            };
            StatusBorder.BeginAnimation(OpacityProperty, fadeOut);
        }
        else
        {
            StopPulse();
            StatusBorder.Background = PersistentBg;
            StatusText.Foreground = DarkTextBrush;
            StatusText.Text = _persistentText;
            StatusBorder.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            StatusBorder.BeginAnimation(OpacityProperty, fadeIn);
        }
    }

    private void StartAutoHide(int ms)
    {
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        _hideTimer.Tick += (_, _) =>
        {
            KillTimer();
            if (!string.IsNullOrEmpty(_persistentText))
                ShowPersistent();
            else
                FadeToCollapsed();
        };
        _hideTimer.Start();
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

    private void FadeToCollapsed()
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
