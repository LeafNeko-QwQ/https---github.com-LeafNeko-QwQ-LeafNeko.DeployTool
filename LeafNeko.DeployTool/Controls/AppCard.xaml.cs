using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using LeafNeko.DeployTool.ViewModels;

namespace LeafNeko.DeployTool.Controls;

public partial class AppCard : UserControl
{
    // ── 视觉参数 ──
    private const double MaxTiltAngle = 8.0;
    private const double MaxTranslate = 6.0;
    private const double HoverScale = 1.05;
    private const double HoverShadowBlur = 18;
    private const double HoverShadowDepth = 6;
    private const double RestShadowBlur = 8;
    private const double RestShadowDepth = 2;

    // ── 跟随速度（越小越丝滑/延迟越长, 类似 CSS transition-duration）──
    private const double FollowSpeed = 0.14;
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

    // ── 长按多选 ──
    private static bool _isMultiSelectActive;
    private static DispatcherTimer? _longPressTimer;
    private static AppCard? _longPressOrigin;
    private static Point _longPressStartPos;
    private static HashSet<AppCard>? _pendingMultiSelectCards;

    // ── 分类快速入场（延迟缩到 15ms/卡，持续时间 250ms）──
    public static bool UseFastEntrance { get; set; }

    // ── 选中脉冲抑制（全选时跳过脉冲避免大量动画并发）──
    public static bool SuppressSelectionPulse { get; set; }

    // ── 分类切换跳过入场动画 ──
    public static bool SkipEntranceAnimation { get; set; }

    // ── 重试事件（静态，MainWindow 订阅一次即可处理所有卡片）──
    public static event Action<AppItemViewModel>? RetryRequested;

    // ── 动画模板 — 预创建关键帧结构，每次调用 Clone() 复用（远快于 new + 逐个 Add KeyFrame）──
    private static readonly DoubleAnimationUsingKeyFrames BounceTemplate = CreateBounceTemplate();
    private static readonly DoubleAnimationUsingKeyFrames PulseTemplate = CreatePulseTemplate();
    private static readonly DoubleAnimationUsingKeyFrames SkewWaveTemplate = CreateSkewWaveTemplate();
    private static readonly DoubleAnimationUsingKeyFrames TransWaveTemplate = CreateTransWaveTemplate();
    private static readonly DoubleAnimation EntranceScaleTemplate = new(0.82, 1.0, TimeSpan.FromMilliseconds(480))
    {
        EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }
    };
    private static readonly DoubleAnimation EntranceFadeTemplate = new(0, 1, TimeSpan.FromMilliseconds(350))
    {
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };

    private static DoubleAnimationUsingKeyFrames CreateBounceTemplate()
    {
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(420) };
        a.KeyFrames.Add(new SplineDoubleKeyFrame(0.88, TimeSpan.FromMilliseconds(0), new KeySpline(0.5, 0, 1, 0.5)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(1.07, TimeSpan.FromMilliseconds(140), new KeySpline(0, 0, 0.5, 1)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(0.97, TimeSpan.FromMilliseconds(270), new KeySpline(0.5, 0, 1, 0.5)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(420), new KeySpline(0.2, 0, 0.4, 1)));
        return a;
    }

    private static DoubleAnimationUsingKeyFrames CreatePulseTemplate()
    {
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(350) };
        a.KeyFrames.Add(new SplineDoubleKeyFrame(1.05, TimeSpan.FromMilliseconds(0), new KeySpline(0.5, 0, 1, 0.5)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(180), new KeySpline(0, 0, 0.5, 1)));
        return a;
    }

    private static DoubleAnimationUsingKeyFrames CreateSkewWaveTemplate()
    {
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(500) };
        a.KeyFrames.Add(new SplineDoubleKeyFrame(0, TimeSpan.FromMilliseconds(0), new KeySpline(0.5, 0, 1, 0.5)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(6, TimeSpan.FromMilliseconds(120), new KeySpline(0, 0, 0.5, 1)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(-3, TimeSpan.FromMilliseconds(250), new KeySpline(0.5, 0, 1, 0.5)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(0, TimeSpan.FromMilliseconds(380), new KeySpline(0, 0, 0.5, 1)));
        return a;
    }

    private static DoubleAnimationUsingKeyFrames CreateTransWaveTemplate()
    {
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(500) };
        a.KeyFrames.Add(new SplineDoubleKeyFrame(0, TimeSpan.FromMilliseconds(0), new KeySpline(0.5, 0, 1, 0.5)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(-4, TimeSpan.FromMilliseconds(120), new KeySpline(0, 0, 0.5, 1)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(2, TimeSpan.FromMilliseconds(250), new KeySpline(0.5, 0, 1, 0.5)));
        a.KeyFrames.Add(new SplineDoubleKeyFrame(0, TimeSpan.FromMilliseconds(380), new KeySpline(0, 0, 0.5, 1)));
        return a;
    }

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
        PreviewMouseLeftButtonUp += OnCardRelease;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    #region 入场动画

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SkipEntranceAnimation)
        {
            var st = (ScaleTransform)ScaleContainer.RenderTransform;
            st.ScaleX = 1.0;
            st.ScaleY = 1.0;
            Opacity = 1;
        }
        else
        {
            var cardDelay = UseFastEntrance ? 15 : 60;
            var duration = UseFastEntrance ? 250 : 480;
            var delay = Interlocked.Increment(ref _entranceCounter) * cardDelay;

            var entrance = EntranceScaleTemplate.Clone();
            entrance.Duration = TimeSpan.FromMilliseconds(duration);
            entrance.BeginTime = TimeSpan.FromMilliseconds(delay);
            var st = (ScaleTransform)ScaleContainer.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, entrance);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, entrance);

            var fadeIn = EntranceFadeTemplate.Clone();
            fadeIn.Duration = TimeSpan.FromMilliseconds(duration * 0.7);
            fadeIn.BeginTime = TimeSpan.FromMilliseconds(delay);
            BeginAnimation(OpacityProperty, fadeIn);
        }

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
        if (SuppressSelectionPulse) return;

        var pulse = PulseTemplate.Clone();
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
        AnimationDriver.Register(this);
    }

    private void StopGameLoop()
    {
        _isRunning = false;
        AnimationDriver.Unregister(this);
    }

    internal void Tick()
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
        _targetSkewX = _mouseOx * MaxTiltAngle * 0.5;
        _targetSkewY = -_mouseOy * MaxTiltAngle * 0.5;
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

        _longPressStartPos = e.GetPosition(null);

        // 长按进度动画：1.0 → 0.93，800ms 缓慢压入
        var pressAnim = new DoubleAnimation(1.0, 0.93, TimeSpan.FromMilliseconds(800))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, pressAnim);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, pressAnim);

        // 启动长按计时器
        CancelLongPressTimer();
        _longPressOrigin = this;
        _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _longPressTimer.Tick += OnLongPressTick;
        _longPressTimer.Start();
    }

    private void OnCardRelease(object sender, MouseButtonEventArgs e)
    {
        if (IsCheckBoxSource(e.OriginalSource as DependencyObject))
            return;

        CancelLongPressTimer();

        // 长按未完成，弹回 1.0
        if (!_isMultiSelectActive)
        {
            var springBack = new DoubleAnimation(_scale.ScaleX, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            springBack.Completed += (_, _) => { _scale.ScaleX = 1.0; _scale.ScaleY = 1.0; };
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, springBack);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, springBack);
        }

        // 正常点击（非多选模式）
        if (!_isMultiSelectActive)
        {
            if (DataContext is AppItemViewModel vm && !vm.IsProcessing)
            {
                vm.IsSelected = !vm.IsSelected;
                StartBounce();
            }
        }
        // 注意：多选模式下的退出由 MainWindow 全局 PreviewMouseLeftButtonUp 统一处理
    }

    private static void OnLongPressTick(object? sender, EventArgs e)
    {
        CancelLongPressTimer();
        _isMultiSelectActive = true;

        // 长按成功：弹回 1.0 + 脉冲
        if (_longPressOrigin is { } origin)
        {
            var snapBack = new DoubleAnimation(0.93, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
            };
            origin._scale.BeginAnimation(ScaleTransform.ScaleXProperty, snapBack);
            origin._scale.BeginAnimation(ScaleTransform.ScaleYProperty, snapBack);

            if (origin.DataContext is AppItemViewModel vm && !vm.IsProcessing)
            {
                vm.IsSelected = !vm.IsSelected;
                origin.StartBounce();
            }
        }

        // 批量处理计时器期间鼠标经过的卡片
        if (_pendingMultiSelectCards is { } pending)
        {
            foreach (var card in pending)
            {
                if (card.DataContext is AppItemViewModel vm && !vm.IsProcessing)
                {
                    vm.IsSelected = !vm.IsSelected;
                    card.StartBounce();
                }
            }
            _pendingMultiSelectCards.Clear();
            _pendingMultiSelectCards = null;
        }
    }

    internal static bool IsMultiSelectActive => _isMultiSelectActive;

    internal static void ExitMultiSelect()
    {
        _isMultiSelectActive = false;
    }

    private static void CancelLongPressTimer()
    {
        if (_longPressTimer != null)
        {
            _longPressTimer.Stop();
            _longPressTimer = null;
        }
        _pendingMultiSelectCards?.Clear();
        _pendingMultiSelectCards = null;
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        // 长按计时器运行中，记录进入的卡片，计时器触发后批量处理
        if (_longPressTimer != null && _longPressOrigin != this)
        {
            _pendingMultiSelectCards ??= new();
            _pendingMultiSelectCards.Add(this);
            return;
        }

        if (!_isMultiSelectActive || _longPressOrigin == this) return;

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

        var bounce = BounceTemplate.Clone();
        bounce.Completed += (_, _) =>
        {
            _isBouncing = false;
            _curScale = _targetScale;
        };

        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
    }

    #endregion

    #region 重试按钮

    private void RetryBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppItemViewModel vm)
            RetryRequested?.Invoke(vm);
    }

    #endregion

    #region 全选 3D 波浪动画

    public void PlaySelectAllAnimation(int index, int total)
    {
        var delay = index * 40;

        var skewAnim = SkewWaveTemplate.Clone();
        skewAnim.BeginTime = TimeSpan.FromMilliseconds(delay);

        var transAnim = TransWaveTemplate.Clone();
        transAnim.BeginTime = TimeSpan.FromMilliseconds(delay);

        _skew.BeginAnimation(SkewTransform.AngleXProperty, skewAnim);
        _translate.BeginAnimation(TranslateTransform.YProperty, transAnim);
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
