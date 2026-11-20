using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

// 入门阅读顺序：属性注册 → CLR 属性读写 → 读数/质量文字 → Sync → 布局 → Render。
// Value 是应用提供的真实液位，LevelIndicatorState 是它所在的量程区间，Quality 是数据能否使用。
// 三者职责不同：数据过期时仍可保存 45、区间仍为 Normal，但画面只显示过期状态，不画液面。
// 本类继承 Control 并自己绘制罐体，因此没有 ControlTemplate；主题只为它提供外观参数。
// 公共英文 XML 文档供 Gallery 生成三语 API，下面的普通中文注释帮助阅读源码，不改变公开 API。
/// <summary>A read-only level meter that fills a vessel with a spring-driven liquid column, drawn beside a scale with the actual reading and a named threshold state.</summary>
public class LevelIndicator : Control
{
    // 先在固定的 220 × 360 参考坐标上画图，再整体等比例缩放到实际容器。
    // 参考坐标中的 130 不一定是屏幕上的 130 像素；这样缩放时罐体圆角也不会变形。
    private const double DesignWidth = 220;
    private const double DesignHeight = 360;

    // 罐体在参考坐标中的上下边界：液体在这个范围内从底部向上填充。
    private const double VesselTop = 40;
    private const double VesselBottom = 272;
    // 罐体中心的横坐标。与温度计保持同样的偏左布局，右侧留给刻度。
    private const double VesselCenterX = 92;

    // StyledProperty 是 Avalonia 的属性登记对象，让属性支持 XAML、样式和数据绑定。
    // double.IsFinite 拒绝 NaN 和无穷；合法的越界数值仍保存，只有液面高度被限制在量程内。
    /// <summary>The actual level; an out-of-range reading is kept while the liquid is clamped to the range.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(Value), 0, validate: double.IsFinite);
    // Quality 可以独立绑定。validate 拒绝枚举范围之外的值，例如强制转换得到的 99。
    // 它只控制有效性展示，不清零 Value，也不改变区间含义。
    /// <summary>Application-provided measurement quality (default Good); other states hide the number and liquid without changing Value or the state band. No timeout is inferred.</summary>
    public static readonly StyledProperty<MeasurementQuality> QualityProperty =
        AvaloniaProperty.Register<LevelIndicator, MeasurementQuality>(nameof(Quality), MeasurementQuality.Good,
            validate: value => value is >= MeasurementQuality.Good and <= MeasurementQuality.Error);
    // string? 中的 ? 表示允许 null。null 使用内置翻译；空字符串 "" 是明确的自定义空文字。
    // 三个质量标签只替换提示文字，不负责判断有没有数据、是否过期或是否采集失败。
    /// <summary>Custom missing-data text; null follows the application's language.</summary>
    public static readonly StyledProperty<string?> NoDataLabelProperty =
        AvaloniaProperty.Register<LevelIndicator, string?>(nameof(NoDataLabel));
    /// <summary>Custom stale-measurement text; null follows the application's language.</summary>
    public static readonly StyledProperty<string?> StaleLabelProperty =
        AvaloniaProperty.Register<LevelIndicator, string?>(nameof(StaleLabel));
    /// <summary>Custom collection-error text; null follows the application's language.</summary>
    public static readonly StyledProperty<string?> ErrorLabelProperty =
        AvaloniaProperty.Register<LevelIndicator, string?>(nameof(ErrorLabel));
    /// <summary>Lower end of the measurement range (default 0).</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(Minimum), 0, validate: double.IsFinite);
    /// <summary>Upper end of the measurement range (default 100).</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(Maximum), 100, validate: double.IsFinite);
    /// <summary>Readings below this value are low (default 20).</summary>
    public static readonly StyledProperty<double> LowThresholdProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(LowThreshold), 20, validate: double.IsFinite);
    /// <summary>Readings at or above this value are high (default 80).</summary>
    public static readonly StyledProperty<double> HighThresholdProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(HighThreshold), 80, validate: double.IsFinite);
    /// <summary>What is measured; shown above the vessel and used as the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<LevelIndicator, string?>(nameof(Label));
    /// <summary>The display unit (default %); changing it does not convert values or thresholds.</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<LevelIndicator, string>(nameof(Unit), "%");
    /// <summary>Number of scale divisions, from 1 to 10 (default 4); every division keeps a visible label.</summary>
    public static readonly StyledProperty<int> TickCountProperty =
        AvaloniaProperty.Register<LevelIndicator, int>(nameof(TickCount), 4, validate: value => value is >= 1 and <= 10);
    /// <summary>Whether to draw the scale and its values (default true).</summary>
    public static readonly StyledProperty<bool> ShowScaleProperty =
        AvaloniaProperty.Register<LevelIndicator, bool>(nameof(ShowScale), true);
    /// <summary>Color family for low readings (default Danger: a nearly empty vessel is usually the alarm).</summary>
    public static readonly StyledProperty<GaugeTone> LowToneProperty =
        AvaloniaProperty.Register<LevelIndicator, GaugeTone>(nameof(LowTone), GaugeTone.Danger);
    /// <summary>Color family for normal readings (default Success).</summary>
    public static readonly StyledProperty<GaugeTone> NormalToneProperty =
        AvaloniaProperty.Register<LevelIndicator, GaugeTone>(nameof(NormalTone), GaugeTone.Success);
    /// <summary>Color family for high readings (default Danger: an overfilled vessel is an alarm).</summary>
    public static readonly StyledProperty<GaugeTone> HighToneProperty =
        AvaloniaProperty.Register<LevelIndicator, GaugeTone>(nameof(HighTone), GaugeTone.Danger);
    /// <summary>Custom low-state text; null uses the app's language.</summary>
    public static readonly StyledProperty<string?> LowLabelProperty =
        AvaloniaProperty.Register<LevelIndicator, string?>(nameof(LowLabel));
    /// <summary>Custom normal-state text; null uses the app's language.</summary>
    public static readonly StyledProperty<string?> NormalLabelProperty =
        AvaloniaProperty.Register<LevelIndicator, string?>(nameof(NormalLabel));
    /// <summary>Custom high-state text; null uses the app's language.</summary>
    public static readonly StyledProperty<string?> HighLabelProperty =
        AvaloniaProperty.Register<LevelIndicator, string?>(nameof(HighLabel));
    // DirectProperty 暴露计算结果：调用者可以读取 State，但不能直接给它赋值。
    // Sync 会用 SetAndRaise 更新结果并发出通知，绑定到 State 的界面才能跟着更新。
    /// <summary>The band of the actual reading; changes immediately, independently of animation.</summary>
    public static readonly DirectProperty<LevelIndicator, LevelIndicatorState> StateProperty =
        AvaloniaProperty.RegisterDirect<LevelIndicator, LevelIndicatorState>(nameof(State), owner => owner.State);

    // internal 属性由组件主题提供尺寸、圆角和字重，不额外暴露为应用的数据输入。
    // Token 可理解为统一的外观参数；主题切换或覆盖 Token 后这些属性会跟随更新。
    internal static readonly StyledProperty<double> DialWidthProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(DialWidth), 220);
    internal static readonly StyledProperty<double> DialHeightProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(DialHeight), 360);
    internal static readonly StyledProperty<double> VesselWidthProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(VesselWidth), 130);
    internal static readonly StyledProperty<double> OutlineThicknessProperty =
        AvaloniaProperty.Register<LevelIndicator, double>(nameof(OutlineThickness), 1.5);
    // 圆角属于 ArcDesign 的资源：主题把它绑到 RadiusControl，因此跟随主题和角落风格，
    // 不在组件 Token 里另存一份圆角数值。
    internal static readonly StyledProperty<CornerRadius> VesselCornerRadiusProperty =
        AvaloniaProperty.Register<LevelIndicator, CornerRadius>(nameof(VesselCornerRadius));
    internal static readonly StyledProperty<FontWeight> MediumFontWeightProperty =
        AvaloniaProperty.Register<LevelIndicator, FontWeight>(nameof(MediumFontWeight), FontWeight.Medium);
    internal static readonly StyledProperty<FontWeight> RegularFontWeightProperty =
        AvaloniaProperty.Register<LevelIndicator, FontWeight>(nameof(RegularFontWeight), FontWeight.Normal);

    // 下划线开头的是本控件内部字段。Palette 提供主题颜色和字体，避免写死深浅色。
    private readonly ChartsBPalette _palette;
    // _fill 只保存动画中的液面比例（0..1）；readonly 约束引用不被替换，动画值仍然会变化。
    private readonly MotionValue<double> _fill;
    // 缓存排好版的文字，避免动画每一帧都重新创建它们。
    private readonly List<ChartsBRun> _textRuns = new();
    private Rect _dial;
    private LevelIndicatorState _state = LevelIndicatorState.Low;
    private ArcLanguageManager? _languages;
    private LevelIndicatorAutomationPeer? _peer;
    // 记录上一次同步时是否有有效读数。由 false 恢复到 true 时直接显示新液面，
    // 不从断线前的旧液位播放动画；它不是“曾经收到过一次数据”的永久标记。
    private bool _hadReading;

    /// <summary>Creates a level indicator.</summary>
    public LevelIndicator()
    {
        // 构造函数只创建控件自己拥有的辅助对象，不连接设备、不启动采集。
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _fill = new MotionValue<double>(this, Fraction, MotionVectors.Double);
        // += 订阅事件；_ 表示忽略事件参数。动画值变化时只请求下一次重绘。
        _fill.Changed += _ => InvalidateVisual();
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    static LevelIndicator()
    {
        // 测量是“需要多大空间”，绘制是“在这个空间里画什么”。外框尺寸影响测量，
        // 罐体宽度、圆角和描边只影响图形；避免把每个外观变化都当成整棵界面的布局变化。
        AffectsMeasure<LevelIndicator>(DialWidthProperty, DialHeightProperty);
        AffectsRender<LevelIndicator>(VesselWidthProperty, OutlineThicknessProperty, VesselCornerRadiusProperty);
    }

    // 这些 CLR 属性是 C# 调用入口，get/set 都操作 Avalonia 的同一套属性存储。
    // 因此 Tank.Value=45 和 XAML 绑定更新 Value 会进入相同的属性变化处理流程。
    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <inheritdoc cref="QualityProperty"/>
    public MeasurementQuality Quality { get => GetValue(QualityProperty); set => SetValue(QualityProperty, value); }
    /// <inheritdoc cref="NoDataLabelProperty"/>
    public string? NoDataLabel { get => GetValue(NoDataLabelProperty); set => SetValue(NoDataLabelProperty, value); }
    /// <inheritdoc cref="StaleLabelProperty"/>
    public string? StaleLabel { get => GetValue(StaleLabelProperty); set => SetValue(StaleLabelProperty, value); }
    /// <inheritdoc cref="ErrorLabelProperty"/>
    public string? ErrorLabel { get => GetValue(ErrorLabelProperty); set => SetValue(ErrorLabelProperty, value); }
    /// <inheritdoc cref="MinimumProperty"/>
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <inheritdoc cref="LowThresholdProperty"/>
    public double LowThreshold { get => GetValue(LowThresholdProperty); set => SetValue(LowThresholdProperty, value); }
    /// <inheritdoc cref="HighThresholdProperty"/>
    public double HighThreshold { get => GetValue(HighThresholdProperty); set => SetValue(HighThresholdProperty, value); }
    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <inheritdoc cref="UnitProperty"/>
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    /// <inheritdoc cref="TickCountProperty"/>
    public int TickCount { get => GetValue(TickCountProperty); set => SetValue(TickCountProperty, value); }
    /// <inheritdoc cref="ShowScaleProperty"/>
    public bool ShowScale { get => GetValue(ShowScaleProperty); set => SetValue(ShowScaleProperty, value); }
    /// <inheritdoc cref="LowToneProperty"/>
    public GaugeTone LowTone { get => GetValue(LowToneProperty); set => SetValue(LowToneProperty, value); }
    /// <inheritdoc cref="NormalToneProperty"/>
    public GaugeTone NormalTone { get => GetValue(NormalToneProperty); set => SetValue(NormalToneProperty, value); }
    /// <inheritdoc cref="HighToneProperty"/>
    public GaugeTone HighTone { get => GetValue(HighToneProperty); set => SetValue(HighToneProperty, value); }
    /// <inheritdoc cref="LowLabelProperty"/>
    public string? LowLabel { get => GetValue(LowLabelProperty); set => SetValue(LowLabelProperty, value); }
    /// <inheritdoc cref="NormalLabelProperty"/>
    public string? NormalLabel { get => GetValue(NormalLabelProperty); set => SetValue(NormalLabelProperty, value); }
    /// <inheritdoc cref="HighLabelProperty"/>
    public string? HighLabel { get => GetValue(HighLabelProperty); set => SetValue(HighLabelProperty, value); }
    /// <inheritdoc cref="StateProperty"/>
    public LevelIndicatorState State => _state;

    internal double DialWidth { get => GetValue(DialWidthProperty); set => SetValue(DialWidthProperty, value); }
    internal double DialHeight { get => GetValue(DialHeightProperty); set => SetValue(DialHeightProperty, value); }
    internal double VesselWidth { get => GetValue(VesselWidthProperty); set => SetValue(VesselWidthProperty, value); }
    internal double OutlineThickness { get => GetValue(OutlineThicknessProperty); set => SetValue(OutlineThicknessProperty, value); }
    internal CornerRadius VesselCornerRadius { get => GetValue(VesselCornerRadiusProperty); set => SetValue(VesselCornerRadiusProperty, value); }
    internal FontWeight MediumFontWeight { get => GetValue(MediumFontWeightProperty); set => SetValue(MediumFontWeightProperty, value); }
    internal FontWeight RegularFontWeight { get => GetValue(RegularFontWeightProperty); set => SetValue(RegularFontWeightProperty, value); }
    // 液面高度使用动画中的比例，报警判定使用 Value；两者不能互换。
    // 例如动画还在上升时，DrawnFraction 仍是 0.3，而 Value 已经是 45。
    internal double DrawnFraction => _fill.Value;
    internal bool HasReading => Quality == MeasurementQuality.Good;
    // "0.0" 保留一位小数；TrimEnd 去掉单位为空白时多余的尾部空格（Unit="" 时不会留下尾随空格）。
    internal string ReadingText => HasReading ? $"{Value.ToString("0.0", CultureInfo.CurrentCulture)} {Unit}".TrimEnd() : "—";
    // switch 按枚举选择文字。先处理非有效质量，Good 才显示液位区间文字。
    // ?? 表示左边为 null 才取右边：自定义提示优先，否则使用当前语言资源。
    internal string StatusText => Quality switch
    {
        MeasurementQuality.NoData => NoDataLabel ?? LevelIndicatorLangResources.Get(LevelIndicatorLangResourceKind.NoData),
        MeasurementQuality.Stale => StaleLabel ?? LevelIndicatorLangResources.Get(LevelIndicatorLangResourceKind.Stale),
        MeasurementQuality.Error => ErrorLabel ?? LevelIndicatorLangResources.Get(LevelIndicatorLangResourceKind.Error),
        _ => BandText
    };
    private string BandText => State switch
    {
        LevelIndicatorState.Low => LowLabel ?? LevelIndicatorLangResources.Get(LevelIndicatorLangResourceKind.Low),
        LevelIndicatorState.Normal => NormalLabel ?? LevelIndicatorLangResources.Get(LevelIndicatorLangResourceKind.Normal),
        LevelIndicatorState.High => HighLabel ?? LevelIndicatorLangResources.Get(LevelIndicatorLangResourceKind.High),
        _ => LevelIndicatorLangResources.Get(LevelIndicatorLangResourceKind.InvalidRange)
    };
    // ?: 是条件选择表达式。质量颜色优先，只有有效数据才使用高低液位色调。
    // 颜色来自 Palette 的主题资源；旁边始终有文字说明，不能只靠颜色判断状态。
    internal Color StateColor => Quality == MeasurementQuality.Stale ? _palette.Warning :
        Quality == MeasurementQuality.Error ? _palette.Danger :
        !HasReading || State == LevelIndicatorState.InvalidRange ? _palette.TextMuted : Tone switch
    {
        GaugeTone.Success => _palette.Success,
        GaugeTone.Warning => _palette.Warning,
        GaugeTone.Danger => _palette.Danger,
        _ => _palette.Accent
    };
    private GaugeTone Tone => State switch
    {
        LevelIndicatorState.Low => LowTone,
        LevelIndicatorState.High => HighTone,
        _ => NormalTone
    };
    // 量程必须递增、两个阈值顺序必须正确，否则无法把数值映射成液面比例。
    private bool ValidRange => Maximum > Minimum && HighThreshold >= LowThreshold;
    // 罐体在参考坐标中的外框和内部液体区域。inset 表示罐壁厚度：
    // 液体不贴外框，看起来像被装在容器内，而不是把边框涂满。
    private double HalfVesselWidth => Math.Clamp(VesselWidth, 20, 150) / 2;
    private double WallInset => Math.Clamp(OutlineThickness, 0, 4) + 2;
    private Rect VesselRect => new(VesselCenterX - HalfVesselWidth, VesselTop, HalfVesselWidth * 2, VesselBottom - VesselTop);
    private Rect InnerRect => VesselRect.Deflate(WallInset);
    private double VesselRadius => Math.Clamp(VesselCornerRadius.TopLeft, 0, HalfVesselWidth);
    private double InnerRadius => Math.Max(0, VesselRadius - WallInset);
    // 把实际液位映射为 0..1 的液面比例。例如量程 0..100，45 对应 0.45。
    // 比例与数据质量分开计算；非有效质量是否画液面，由 Render 决定。
    private double Fraction
    {
        get
        {
            if (!ValidRange)
            {
                return 0;
            }
            // 与 LinearGauge 共享映射，既处理极大范围，也保留窄范围精度。
            return GaugeScale.Fraction(Value, Minimum, Maximum);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 基类构造期间也可能通知属性变化；辅助字段尚未初始化时先退出，避免空引用。
        if (_fill is null)
        {
            return;
        }
        // 数值、量程、阈值、质量都会影响液面或真实区间；动效等级变化也需要重新同步。
        if (change.Property == ValueProperty || change.Property == MinimumProperty ||
            change.Property == MaximumProperty || change.Property == LowThresholdProperty ||
            change.Property == HighThresholdProperty || change.Property == QualityProperty)
        {
            Sync();
        }
        else if (change.Property == ArcMotion.LevelProperty || change.Property == ArcMotion.TokenLevelProperty)
        {
            Sync();
        }
        // 文字可能包含数值、单位、质量、刻度或颜色，清空旧布局后由下次 Render 按需重建。
        ClearText();
        InvalidateVisual();
        if (change.Property == ValueProperty || change.Property == UnitProperty ||
            change.Property == LabelProperty || change.Property == LowLabelProperty ||
            change.Property == NormalLabelProperty || change.Property == HighLabelProperty ||
            change.Property == StateProperty || change.Property == QualityProperty ||
            change.Property == NoDataLabelProperty || change.Property == StaleLabelProperty ||
            change.Property == ErrorLabelProperty)
        {
            _peer?.Announce();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // “挂载到可视树”表示进入界面。此时订阅应用语言变化，并重新读取当前数据。
        base.OnAttachedToVisualTree(e);
        _languages = ArcLanguageManager.Current;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 应用语言管理器通常比控件活得久，离开界面时必须用 -= 解除订阅，
        // 否则外部事件可能一直引用旧控件。以后重新挂载时再订阅。
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
        // Jump 立即结束旧动画；保留 MotionValue 对象，以便同一控件以后重新挂载。
        _fill.Jump(Fraction);
        _hadReading = false;
        ClearText();
        base.OnDetachedFromVisualTree(e);
    }

    private void Sync()
    {
        // 第一步：依据真实 Value 判定区间。小于低阈值为 Low，高阈值本身属于 High。
        // Quality 不参与这一步，避免破坏已有区间的含义；显示文字单独检查质量。
        var state = !ValidRange ? LevelIndicatorState.InvalidRange :
            Value < LowThreshold ? LevelIndicatorState.Low :
            Value >= HighThreshold ? LevelIndicatorState.High : LevelIndicatorState.Normal;
        SetAndRaise(StateProperty, ref _state, state);
        // 第二步：选择液面过渡。液面是自绘几何，模板 Transition 无法直接表达，因而使用 MotionValue。
        // 只有连续有效读数、控件已加载且 Full 动效时才播放弹簧。
        if (HasReading && _hadReading && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _fill.AnimateTo(Fraction, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            // 首次、质量恢复、非有效质量或 Reduced/Off：立即到达目标，取消旧动画。
            _fill.Jump(Fraction);
        }
        // 最后保存本次是否有效，供下一次更新判断“连续采样”还是“恢复后的首个采样”。
        _hadReading = HasReading;
    }

    private void OnPaletteChanged()
    {
        ClearText();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        ClearText();
        InvalidateVisual();
        _peer?.Announce();
    }

    private void ClearText()
    {
        // 每个文字布局持有排版资源，要先 Dispose 释放，再清空集合。
        // 只 Clear 集合会丢掉引用，却没有按这个对象的约定主动释放布局资源。
        foreach (var run in _textRuns)
        {
            run.Dispose();
        }
        _textRuns.Clear();
    }

    private Size FitDial(Size availableSize)
    {
        // 容器可用空间和主题最大尺寸共同约束缩放，选宽高限制中较小的比例。
        // Measure 与 Arrange 共用这个函数，避免“测量说一种尺寸，绘制用另一种尺寸”。
        var width = Math.Max(1, DialWidth);
        var height = Math.Max(1, DialHeight);
        var scale = Math.Max(0, Math.Min(Math.Min(availableSize.Width, width) / DesignWidth,
            Math.Min(availableSize.Height, height) / DesignHeight));
        return new Size(DesignWidth * scale, DesignHeight * scale);
    }

    protected override Size MeasureOverride(Size availableSize) => FitDial(availableSize);

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = FitDial(finalSize);
        _dial = new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2,
            size.Width, size.Height);
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // Render 只画图，不采集设备数据，也不改 Value/Quality。绘制顺序从底到顶：
        // 罐体外框 → 有效液体 → 标题/数字/状态 → 刻度线及刻度数字。
        base.Render(context);
        var scale = _dial.Width / DesignWidth;
        if (!(scale > 0))
        {
            return;
        }
        EnsureText();
        using (context.PushTransform(Matrix.CreateScale(scale, scale) *
            Matrix.CreateTranslation(_dial.X, _dial.Y)))
        {
            var color = StateColor;
            var vessel = VesselRect;
            var inner = InnerRect;
            var outline = _palette.Pen(_palette.BorderStrong, Math.Max(0, OutlineThickness));
            context.DrawRectangle(_palette.Brush(_palette.SurfaceRaised), outline, vessel, VesselRadius, VesselRadius);
            var fraction = Math.Clamp(_fill.Value, 0, 1);
            // 比例 0 落在容器底部，比例 1 落在容器顶部；共享绘制函数计算裁剪边界。
            if (HasReading)
            {
                // 液体画成“容器内部的圆角矩形”，再用普通矩形裁剪掉液面以上的部分：
                // 底部保留罐体圆角，顶部得到平的液面。裁剪区使用普通 Rect，
                // 因为测试用 DrawingGroup 记录器不实现圆形裁剪，普通矩形足以表达液面。
                // 质量非 Good 时跳过整段液体绘制，保留空罐体和刻度，避免旧液位冒充当前值。
                GaugeScale.DrawFill(context, _palette.Brush(color), inner, InnerRadius, fraction);
                // 填充与裁剪的交界本身就是液面，不再额外画横跨整个内框的同色描边。
                // 例如 100% 时顶部仅应保留圆角轮廓；横线会越过圆角两侧，
                // 0% 时还会在罐底留下看似有液体的线。只画填充能同时避免这两种边界错误。
            }
            // 缓存下标约定：0 标题，1 当前数字或破折号，2 状态，3 开始是刻度文字。
            DrawCentered(context, _textRuns[0], 110, 9);
            DrawCentered(context, _textRuns[1], 110, 300);
            DrawCentered(context, _textRuns[2], 110, 334);
            if (ShowScale && ValidRange)
            {
                var text = 3;
                var tickX = VesselCenterX + HalfVesselWidth + 5;
                for (var i = 0; i <= TickCount; i++)
                {
                    var y = inner.Bottom - inner.Height * i / TickCount;
                    context.DrawLine(_palette.Pen(_palette.TextMuted, 1), new Point(tickX, y), new Point(tickX + 8, y));
                    var run = _textRuns[text++];
                    run.Draw(context, new Point(tickX + 12, y - run.Height / 2));
                }
            }
        }
    }

    private void EnsureText()
    {
        // Ensure 的意思是“确保已准备好”：已有缓存就直接返回，没有才创建。
        // 文字、字体或主题变化会先调用 ClearText，因此不会永久显示旧文字。
        if (_textRuns.Count > 0)
        {
            return;
        }
        _textRuns.Add(ChartsBText.TrimmedRun(Label ?? string.Empty, _palette, _palette.TypeSM,
            _palette.Foreground, 204, MediumFontWeight));
        _textRuns.Add(ChartsBText.TrimmedRun(ReadingText, _palette, _palette.TypeBase * 1.5,
            _palette.Foreground, 204, MediumFontWeight));
        _textRuns.Add(ChartsBText.TrimmedRun(StatusText, _palette, _palette.TypeXS, StateColor, 204,
            RegularFontWeight));
        if (ShowScale && ValidRange)
        {
            for (var i = 0; i <= TickCount; i++)
            {
                var fraction = (double)i / TickCount;
                // 比例 0 是量程下限（画在罐底），比例 1 是量程上限（画在罐顶）。
                var value = GaugeScale.Tick(Minimum, Maximum, fraction);
                if (Math.Abs(value) < 1e-9)
                {
                    value = 0;
                }
                _textRuns.Add(ChartsBText.TrimmedRun(value.ToString("0.#", CultureInfo.CurrentCulture),
                    _palette, _palette.TypeXS, _palette.TextMuted, 36, RegularFontWeight));
            }
        }
    }

    private static void DrawCentered(DrawingContext context, ChartsBRun run, double x, double y)
        => run.Draw(context, new Point(x - run.Width / 2, y));

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new LevelIndicatorAutomationPeer(this);

    private sealed class LevelIndicatorAutomationPeer(LevelIndicator owner) : ControlAutomationPeer(owner), IRangeValueProvider, IValueProvider
    {
        // AutomationPeer 给屏幕阅读器和自动化工具提供信息；自绘数字不是 TextBlock，
        // 所以要在这里主动提供名称、状态和只读数值，而不是让辅助工具读取动画中的液面位置。
        private string? _lastHelp;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        protected override string GetLocalizedControlTypeCore() => "level indicator";
        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(owner);
            return string.IsNullOrEmpty(name) ? owner.Label : name;
        }
        protected override string? GetHelpTextCore() => owner.HasReading ?
            $"{owner.ReadingText}, {owner.StatusText}" : owner.StatusText;
        protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];
        public bool IsReadOnly => true;
        public double Minimum => owner.Minimum;
        public double Maximum => Math.Max(owner.Minimum, owner.Maximum);
        public double Value => Math.Clamp(owner.Value, Minimum, Maximum);
        public double SmallChange => 0;
        public double LargeChange => 0;
        public void SetValue(double value)
        {
        }

        // 数值接口保留旧契约：返回保存的、限制到量程内的数值，不能据此忽略 Quality。
        // 文本接口在非 Good 时只返回质量状态，不把旧数字播报成当前有效测量。
        // 两个 SetValue 都是空实现，因为液位计是显示控件，不能通过辅助接口设定液位。
        string IValueProvider.Value => GetHelpTextCore() ?? string.Empty;
        void IValueProvider.SetValue(string? value)
        {
        }

        public void Announce()
        {
            // 只有实际帮助文字变了才通知，避免重复播报同一状态；不是每帧播报一次。
            var help = GetHelpTextCore();
            if (help != _lastHelp)
            {
                var old = _lastHelp;
                _lastHelp = help;
                RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, old, help);
            }
        }
    }
}
