using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using LeafNeko.DeployTool.ViewModels;

namespace LeafNeko.DeployTool.Controls;

public partial class AppCard : UserControl
{
    // ── 视觉参数 ──
    private const double MaxTiltAngle = 5.0;
    private const double MaxTranslate = 3.5;
    private const double HoverScale = 1.04;
    private const double HoverShadowBlur = 14;
    private const double HoverShadowDepth = 4;
    private const double RestShadowBlur = 8;
    private const double RestShadowDepth = 2;

    // ── 跟随速度（越小越丝滑/延迟越长, 类似 CSS transition-duration）──
    private const double FollowSpeed = 0.10;
    private const double ReturnSpeed = 0.06;

    // ── 预创建变换（原地修改，零分配）──
    private readonly ScaleTransform _scale;
    private readonly SkewTransform _skew;
    private readonly TranslateTransform _translate;
    private readonly TransformGroup _transformGroup;

    // ── 目标值（鼠标位置决定）──
    private double _targetScale = 1.0;
    private double _targetSkewX, _targetSkewY;
    private double _targetTransX, _targetTransY;
    private double _targetShadowBlur = RestShadowBlur;
    private double _targetShadowDepth = RestShadowDepth;
    private double _targetShadowDir = 315;
    private double _targetGlowOpacity;

    // ── 当前值（每帧向目标值插值, 类似 CSS transition 的中间态）──
    private double _curScale = 1.0;
    private double _curSkewX, _curSkewY;
    private double _curTransX, _curTransY;
    private double _curShadowBlur = RestShadowBlur;
    private double _curShadowDepth = RestShadowDepth;
    private double _curShadowDir = 315;
    private double _curGlowOpacity;

    // ── 鼠标状态 ──
    private bool _isMouseOver;
    private double _mouseOx, _mouseOy;

    // ── 游戏循环 ──
    private bool _isRunning;

    // ── 弹跳隔离标志 ──
    private bool _isBouncing;

    private static int _entranceCounter;

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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    #region 入场动画

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var delay = Interlocked.Increment(ref _entranceCounter) * 60;

        var entrance = new DoubleAnimation
        {
            From = 0.82,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(480),
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }
        };
        ScaleContainer.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, entrance);
        ScaleContainer.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, entrance);

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(350),
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);

        StartGameLoop();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopGameLoop();
        if (DataContext is AppItemViewModel vm)
            vm.PropertyChanged -= OnViewModelPropertyChanged;
    }

    #endregion

    #region 选中脉冲动画

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AppItemViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is AppItemViewModel newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
        {
            if (DataContext is AppItemViewModel vm && vm.IsSelected)
                StartSelectionPulse();
        }
    }

    private void StartSelectionPulse()
    {
        var pulse = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(350) };
        pulse.KeyFrames.Add(new SplineDoubleKeyFrame(1.05,
            TimeSpan.FromMilliseconds(0), new KeySpline(0.5, 0, 1, 0.5)));
        pulse.KeyFrames.Add(new SplineDoubleKeyFrame(1.0,
            TimeSpan.FromMilliseconds(180), new KeySpline(0, 0, 0.5, 1)));

        var scaleTransform = (ScaleTransform)ScaleContainer.RenderTransform;
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }

    #endregion

    #region 游戏循环（单帧循环驱动所有平滑过渡）

    private void StartGameLoop()
    {
        if (_isRunning) return;
        _isRunning = true;
        CompositionTarget.Rendering += OnFrame;
    }

    private void StopGameLoop()
    {
        _isRunning = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_isBouncing) return;

        var speed = _isMouseOver ? FollowSpeed : ReturnSpeed;

        // 所有属性同时平滑过渡 — 类似 CSS transition: all
        _curScale = Slerp(_curScale, _targetScale, speed);
        _curSkewX = Slerp(_curSkewX, _targetSkewX, speed);
        _curSkewY = Slerp(_curSkewY, _targetSkewY, speed);
        _curTransX = Slerp(_curTransX, _targetTransX, speed);
        _curTransY = Slerp(_curTransY, _targetTransY, speed);
        _curShadowBlur = Slerp(_curShadowBlur, _targetShadowBlur, speed);
        _curShadowDepth = Slerp(_curShadowDepth, _targetShadowDepth, speed);
        _curShadowDir = Slerp(_curShadowDir, _targetShadowDir, speed);
        _curGlowOpacity = Slerp(_curGlowOpacity, _targetGlowOpacity, speed * 0.7);

        _scale.ScaleX = _curScale;
        _scale.ScaleY = _curScale;
        _skew.AngleX = _curSkewX;
        _skew.AngleY = _curSkewY;
        _translate.X = _curTransX;
        _translate.Y = _curTransY;

        if (MainBorder.Effect is DropShadowEffect shadow)
        {
            shadow.BlurRadius = _curShadowBlur;
            shadow.ShadowDepth = _curShadowDepth;
            shadow.Direction = _curShadowDir;
        }

        GlowOverlay.Opacity = _curGlowOpacity;

        // 视差透视原点
        var px = 0.5 + _mouseOx * 0.3;
        var py = 0.5 + _mouseOy * 0.3;
        MainBorder.RenderTransformOrigin = new Point(
            Slerp(MainBorder.RenderTransformOrigin.X, px, speed),
            Slerp(MainBorder.RenderTransformOrigin.Y, py, speed));

        // 辉光随鼠标滑过
        GlowOverlay.RenderTransformOrigin = new Point(
            0.5 - _mouseOx * 0.4,
            0.5 - _mouseOy * 0.4);
    }

    #endregion

    #region 鼠标事件

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var halfW = Math.Max(ActualWidth / 2, 1);
        var halfH = Math.Max(ActualHeight / 2, 1);

        _mouseOx = (pos.X - halfW) / halfW;
        _mouseOy = (pos.Y - halfH) / halfH;

        _targetScale = HoverScale;
        _targetSkewX = _mouseOx * MaxTiltAngle * 0.3;
        _targetSkewY = -_mouseOy * MaxTiltAngle * 0.3;
        _targetTransX = _mouseOx * MaxTranslate;
        _targetTransY = _mouseOy * MaxTranslate;
        _targetShadowBlur = HoverShadowBlur;
        _targetShadowDepth = HoverShadowDepth;
        _targetShadowDir = 225 + (_mouseOx - _mouseOy) * 55;
        _targetGlowOpacity = 1;

        _isMouseOver = true;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _targetScale = 1.0;
        _targetSkewX = 0;
        _targetSkewY = 0;
        _targetTransX = 0;
        _targetTransY = 0;
        _targetShadowBlur = RestShadowBlur;
        _targetShadowDepth = RestShadowDepth;
        _targetShadowDir = 315;
        _targetGlowOpacity = 0;
        _mouseOx = 0;
        _mouseOy = 0;

        _isMouseOver = false;
    }

    #endregion

    #region 点击 + 弹跳（WPF Storyboard 驱动，类似 CSS @keyframes spring）

    private void OnCardClick(object sender, MouseButtonEventArgs e)
    {
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
            if (d is CheckBox) return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    private void StartBounce()
    {
        if (_isBouncing) return;
        _isBouncing = true;

        var bounce = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(420) };
        // 0ms: 压入
        bounce.KeyFrames.Add(new SplineDoubleKeyFrame(0.88,
            TimeSpan.FromMilliseconds(0), new KeySpline(0.5, 0, 1, 0.5)));
        // 140ms: 过冲
        bounce.KeyFrames.Add(new SplineDoubleKeyFrame(1.07,
            TimeSpan.FromMilliseconds(140), new KeySpline(0, 0, 0.5, 1)));
        // 280ms: 回弹
        bounce.KeyFrames.Add(new SplineDoubleKeyFrame(0.97,
            TimeSpan.FromMilliseconds(270), new KeySpline(0.5, 0, 1, 0.5)));
        // 420ms: 静止
        bounce.KeyFrames.Add(new SplineDoubleKeyFrame(1.0,
            TimeSpan.FromMilliseconds(420), new KeySpline(0.2, 0, 0.4, 1)));

        bounce.Completed += (_, _) =>
        {
            _isBouncing = false;
            _curScale = _targetScale;
        };

        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
    }

    #endregion

    #region 工具

    /// <summary>带阻尼收束的线性插值 — 当逼近目标时自动吸收微小抖动</summary>
    private static double Slerp(double from, double to, double t)
    {
        var diff = to - from;
        if (Math.Abs(diff) < 0.0005) return to;
        return from + diff * Math.Clamp(t, 0, 1);
    }

    #endregion
}
