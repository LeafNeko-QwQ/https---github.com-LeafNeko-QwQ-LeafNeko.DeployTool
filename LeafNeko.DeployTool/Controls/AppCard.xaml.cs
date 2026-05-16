using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using LeafNeko.DeployTool.ViewModels;

namespace LeafNeko.DeployTool.Controls;

public partial class AppCard : UserControl
{
    private const double MaxTiltAngle = 3.0;
    private const double MaxTranslate = 2.0;
    private const double HoverScale = 1.03;
    private const double HoverShadowBlur = 12;
    private const double HoverShadowDepth = 3;
    private const int ReturnDurationMs = 300;
    private const int ReturnFrameMs = 16;
    private const int BounceDurationMs = 200;

    // Pre-created transforms — modified in-place, never reallocated
    private readonly ScaleTransform _scale;
    private readonly SkewTransform _skew;
    private readonly TranslateTransform _translate;
    private readonly TransformGroup _transformGroup;

    // Regression animation state
    private DispatcherTimer? _returnTimer;
    private double _fromScaleX, _fromScaleY;
    private double _fromSkewX, _fromSkewY;
    private double _fromTransX, _fromTransY;
    private double _fromShadowBlur, _fromShadowDepth, _fromShadowDir;
    private double _fromGlowOpacity;
    private int _returnElapsed;
    private bool _isAnimatingBack;

    // Bounce animation state
    private DispatcherTimer? _bounceTimer;
    private int _bounceElapsed;
    private double _bounceBaseScale;

    public AppCard()
    {
        InitializeComponent();

        _scale = new ScaleTransform(1, 1);
        _skew = new SkewTransform(0, 0);
        _translate = new TranslateTransform(0, 0);
        _transformGroup = new TransformGroup();
        _transformGroup.Children.Add(_scale);
        _transformGroup.Children.Add(_skew);
        _transformGroup.Children.Add(_translate);

        MainBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        MainBorder.RenderTransform = _transformGroup;

        MouseMove += OnMouseMove;
        MouseLeave += OnMouseLeave;
        PreviewMouseLeftButtonDown += OnCardClick;
    }

    private void OnCardClick(object sender, MouseButtonEventArgs e)
    {
        // 点击在 CheckBox 上则不处理（CheckBox 自己处理勾选）
        if (IsCheckBoxSource(e.OriginalSource as DependencyObject))
            return;

        if (DataContext is AppItemViewModel vm && !vm.IsProcessing)
        {
            vm.IsSelected = !vm.IsSelected;
            StartBounce();
        }
    }

    private static bool IsCheckBoxSource(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is CheckBox)
                return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    private void StartBounce()
    {
        _bounceTimer?.Stop();
        _bounceElapsed = 0;
        _bounceBaseScale = _scale.ScaleX;

        _bounceTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Normal,
            OnBounceTick,
            Dispatcher);
        _bounceTimer.Start();
    }

    private void OnBounceTick(object? sender, EventArgs e)
    {
        _bounceElapsed += 16;
        var t = Math.Min(1.0, (double)_bounceElapsed / BounceDurationMs);

        // Overshoot bounce: 1 → 0.92 → 1.02 → 1 (using sin-based bounce)
        var scale = BounceEase(t);
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;

        if (t >= 1.0)
        {
            _bounceTimer?.Stop();
            _bounceTimer = null;
            _scale.ScaleX = _bounceBaseScale;
            _scale.ScaleY = _bounceBaseScale;
        }
    }

    private static double BounceEase(double t)
    {
        // Spring-like bounce: damped sine
        if (t < 0.7)
        {
            var s = t / 0.7;
            return 1 - 0.08 * Math.Sin(s * Math.PI * 1.5);
        }
        else
        {
            var s = (t - 0.7) / 0.3;
            return 1 - 0.08 * Math.Sin((1 + s * 0.5) * Math.PI * 1.5) * (1 - s);
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var halfW = ActualWidth / 2;
        var halfH = ActualHeight / 2;

        var offsetX = (pos.X - halfW) / halfW;
        var offsetY = (pos.Y - halfH) / halfH;

        var rotateX = -offsetY * MaxTiltAngle;
        var rotateY = offsetX * MaxTiltAngle;
        var translateX = offsetX * MaxTranslate;
        var translateY = offsetY * MaxTranslate;

        // Modify transforms in-place — zero allocations
        if (!_isAnimatingBack)
        {
            _scale.ScaleX = HoverScale;
            _scale.ScaleY = HoverScale;
            _skew.AngleX = rotateY * 0.3;
            _skew.AngleY = rotateX * 0.3;
            _translate.X = translateX;
            _translate.Y = translateY;
        }

        MainBorder.RenderTransformOrigin = new Point(0.5 + offsetX * 0.3, 0.5 + offsetY * 0.3);

        if (MainBorder.Effect is DropShadowEffect shadow)
        {
            shadow.BlurRadius = HoverShadowBlur;
            shadow.ShadowDepth = HoverShadowDepth;
            shadow.Direction = 225 + (offsetX - offsetY) * 45;
        }

        GlowOverlay.RenderTransformOrigin = new Point(
            0.5 - offsetX * 0.5,
            0.5 - offsetY * 0.5
        );
        GlowOverlay.Opacity = 1;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        StopReturnTimer();

        // Snap current values
        _fromScaleX = _scale.ScaleX;
        _fromScaleY = _scale.ScaleY;
        _fromSkewX = _skew.AngleX;
        _fromSkewY = _skew.AngleY;
        _fromTransX = _translate.X;
        _fromTransY = _translate.Y;

        if (MainBorder.Effect is DropShadowEffect shadow)
        {
            _fromShadowBlur = shadow.BlurRadius;
            _fromShadowDepth = shadow.ShadowDepth;
            _fromShadowDir = shadow.Direction;
        }
        else
        {
            _fromShadowBlur = 8;
            _fromShadowDepth = 2;
            _fromShadowDir = 315;
        }

        _fromGlowOpacity = GlowOverlay.Opacity;

        MainBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        GlowOverlay.RenderTransformOrigin = new Point(0.5, 0.5);

        _returnElapsed = 0;
        _isAnimatingBack = true;

        _returnTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(ReturnFrameMs),
            DispatcherPriority.Normal,
            OnReturnTick,
            Dispatcher);
        _returnTimer.Start();
    }

    private void OnReturnTick(object? sender, EventArgs e)
    {
        _returnElapsed += ReturnFrameMs;
        var t = Math.Min(1.0, (double)_returnElapsed / ReturnDurationMs);
        var eased = CubicEaseOut(t);

        _scale.ScaleX = Lerp(_fromScaleX, 1, eased);
        _scale.ScaleY = Lerp(_fromScaleY, 1, eased);
        _skew.AngleX = Lerp(_fromSkewX, 0, eased);
        _skew.AngleY = Lerp(_fromSkewY, 0, eased);
        _translate.X = Lerp(_fromTransX, 0, eased);
        _translate.Y = Lerp(_fromTransY, 0, eased);

        if (MainBorder.Effect is DropShadowEffect shadow)
        {
            shadow.BlurRadius = Lerp(_fromShadowBlur, 8, eased);
            shadow.ShadowDepth = Lerp(_fromShadowDepth, 2, eased);
            shadow.Direction = Lerp(_fromShadowDir, 315, eased);
        }

        GlowOverlay.Opacity = Lerp(_fromGlowOpacity, 0, eased);

        if (t >= 1.0)
        {
            StopReturnTimer();
            _isAnimatingBack = false;
        }
    }

    private void StopReturnTimer()
    {
        if (_returnTimer != null)
        {
            _returnTimer.Stop();
            _returnTimer = null;
        }
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * t;

    private static double CubicEaseOut(double t)
    {
        var t1 = t - 1;
        return t1 * t1 * t1 + 1;
    }
}
