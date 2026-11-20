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
// Value 是应用提供的真实数值，State 是它所在的温度区间，Quality 是数据能否使用。
// 三者各有职责：数据过期时可以仍保存 80 度、State 仍为 High，但画面只显示过期状态。
// 本类继承 Control，自己绘制温度计；公共英文 XML 文档供 Gallery 生成三语 API，
// 以下普通中文注释帮助阅读源码，不会替换或改变公开 API 文档。
/// <summary>A read-only temperature meter with a spring-driven liquid column, scale, actual reading and named threshold state.</summary>
public class Thermometer : Control
{
    // 先在固定的 220 × 360 参考坐标上画图，再整体等比例缩放到实际容器。
    // 参考坐标中的 20 不一定是屏幕上的 20 像素；这样缩放时球泡仍然是圆形。
    private const double DesignWidth = 220;
    private const double DesignHeight = 360;

    // StyledProperty 是 Avalonia 的属性登记对象，让属性支持 XAML、样式和数据绑定。
    // ValueProperty 不是某个温度计的当前数值；每个实例的数值通过下方 Value 属性读取。
    // double.IsFinite 拒绝 NaN 和无穷；合法的越界数值仍保存，只有液柱高度被限制。
    /// <summary>The actual temperature; an out-of-range reading is kept while the liquid is clamped.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(Value), 0, validate: double.IsFinite);
    // Quality 可以独立绑定。validate 拒绝枚举范围之外的值，例如强制转换得到的 99。
    // 它只控制有效性展示，不清零 Value，也不改变原来 State 的温度区间含义。
    /// <summary>Application-provided measurement quality (default Good); other states hide the number and liquid without changing Value or State. No timeout is inferred.</summary>
    public static readonly StyledProperty<MeasurementQuality> QualityProperty =
        AvaloniaProperty.Register<Thermometer, MeasurementQuality>(nameof(Quality), MeasurementQuality.Good,
            validate: value => value is >= MeasurementQuality.Good and <= MeasurementQuality.Error);
    // string? 中的 ? 表示允许 null。null 使用内置翻译；空字符串 "" 则是明确的自定义空文字。
    // 三个 Label 属性只替换提示文字，不负责判断有没有数据、是否过期或是否采集失败。
    /// <summary>Custom missing-data text; null follows the application's language.</summary>
    public static readonly StyledProperty<string?> NoDataLabelProperty =
        AvaloniaProperty.Register<Thermometer, string?>(nameof(NoDataLabel));
    /// <summary>Custom stale-measurement text; null follows the application's language.</summary>
    public static readonly StyledProperty<string?> StaleLabelProperty =
        AvaloniaProperty.Register<Thermometer, string?>(nameof(StaleLabel));
    /// <summary>Custom collection-error text; null follows the application's language.</summary>
    public static readonly StyledProperty<string?> ErrorLabelProperty =
        AvaloniaProperty.Register<Thermometer, string?>(nameof(ErrorLabel));
    /// <summary>Lower end of the measurement range (default -20).</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(Minimum), -20, validate: double.IsFinite);
    /// <summary>Upper end of the measurement range (default 100).</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(Maximum), 100, validate: double.IsFinite);
    /// <summary>Readings below this value are low (default 10).</summary>
    public static readonly StyledProperty<double> LowThresholdProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(LowThreshold), 10, validate: double.IsFinite);
    /// <summary>Readings at or above this value are high (default 70).</summary>
    public static readonly StyledProperty<double> HighThresholdProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(HighThreshold), 70, validate: double.IsFinite);
    /// <summary>What is measured; shown above the stem and used as the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Thermometer, string?>(nameof(Label));
    /// <summary>The display unit (default °C); changing it does not convert values or thresholds.</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<Thermometer, string>(nameof(Unit), "°C");
    /// <summary>Number of scale divisions, from 2 to 24 (default 12).</summary>
    public static readonly StyledProperty<int> TickCountProperty =
        AvaloniaProperty.Register<Thermometer, int>(nameof(TickCount), 12, validate: value => value is >= 2 and <= 24);
    /// <summary>Whether to draw the scale and its values (default true).</summary>
    public static readonly StyledProperty<bool> ShowScaleProperty =
        AvaloniaProperty.Register<Thermometer, bool>(nameof(ShowScale), true);
    /// <summary>Color family for low readings (default Accent).</summary>
    public static readonly StyledProperty<GaugeTone> LowToneProperty =
        AvaloniaProperty.Register<Thermometer, GaugeTone>(nameof(LowTone), GaugeTone.Accent);
    /// <summary>Color family for normal readings (default Success).</summary>
    public static readonly StyledProperty<GaugeTone> NormalToneProperty =
        AvaloniaProperty.Register<Thermometer, GaugeTone>(nameof(NormalTone), GaugeTone.Success);
    /// <summary>Color family for high readings (default Danger).</summary>
    public static readonly StyledProperty<GaugeTone> HighToneProperty =
        AvaloniaProperty.Register<Thermometer, GaugeTone>(nameof(HighTone), GaugeTone.Danger);
    /// <summary>Custom low-state text; null uses the app's language.</summary>
    public static readonly StyledProperty<string?> LowLabelProperty =
        AvaloniaProperty.Register<Thermometer, string?>(nameof(LowLabel));
    /// <summary>Custom normal-state text; null uses the app's language.</summary>
    public static readonly StyledProperty<string?> NormalLabelProperty =
        AvaloniaProperty.Register<Thermometer, string?>(nameof(NormalLabel));
    /// <summary>Custom high-state text; null uses the app's language.</summary>
    public static readonly StyledProperty<string?> HighLabelProperty =
        AvaloniaProperty.Register<Thermometer, string?>(nameof(HighLabel));
    // DirectProperty 暴露计算结果：调用者可以读取 State，但不能直接给它赋值。
    // Sync 会使用 SetAndRaise 更新结果并发出通知，绑定到 State 的界面才能跟着更新。
    /// <summary>The band of the actual reading; changes immediately, independently of animation.</summary>
    public static readonly DirectProperty<Thermometer, ThermometerState> StateProperty =
        AvaloniaProperty.RegisterDirect<Thermometer, ThermometerState>(nameof(State), owner => owner.State);

    // internal 属性由组件主题提供尺寸和字重，不额外暴露为应用的数据输入。
    // Token 可理解为统一的外观参数；主题切换或覆盖 Token 后，这些属性会跟随更新。
    internal static readonly StyledProperty<double> DialWidthProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(DialWidth), 220);
    internal static readonly StyledProperty<double> DialHeightProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(DialHeight), 360);
    internal static readonly StyledProperty<double> TubeWidthProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(TubeWidth), 36);
    internal static readonly StyledProperty<double> BulbRadiusProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(BulbRadius), 28);
    internal static readonly StyledProperty<double> OutlineThicknessProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(OutlineThickness), 1.5);
    internal static readonly StyledProperty<FontWeight> MediumFontWeightProperty =
        AvaloniaProperty.Register<Thermometer, FontWeight>(nameof(MediumFontWeight), FontWeight.Medium);
    internal static readonly StyledProperty<FontWeight> RegularFontWeightProperty =
        AvaloniaProperty.Register<Thermometer, FontWeight>(nameof(RegularFontWeight), FontWeight.Normal);

    // 下划线开头的是本控件内部字段。Palette 提供主题颜色/字体，避免写死深浅色。
    private readonly ChartsBPalette _palette;
    // _fill 只保存动画中的液柱比例；readonly 约束引用不被替换，动画值仍然会变化。
    private readonly MotionValue<double> _fill;
    // 缓存排好版的文字和封闭图形，避免动画每一帧都重新创建它们。
    private readonly List<ChartsBRun> _textRuns = new();
    private StreamGeometry? _glass;
    private StreamGeometry? _liquid;
    private Rect _dial;
    private ThermometerState _state = ThermometerState.Low;
    private ArcLanguageManager? _languages;
    private ThermometerAutomationPeer? _peer;
    // 记录上一次同步时是否有有效读数。由 false 恢复到 true 时直接显示新液位，
    // 不从断线前的旧液位播放动画；不是“曾经收到过一次数据”的永久标记。
    private bool _hadReading;

    /// <summary>Creates a thermometer.</summary>
    public Thermometer()
    {
        // 构造函数负责创建控件自己拥有的辅助对象，不在这里连接设备或启动采集。
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _fill = new MotionValue<double>(this, Fraction, MotionVectors.Double);
        // += 订阅事件；_ 表示忽略事件传来的参数。动画值变化时只请求下一次重绘。
        _fill.Changed += _ => InvalidateVisual();
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    static Thermometer()
    {
        // 测量是“需要多大空间”，绘制是“在空间内画什么”。外框尺寸影响测量，
        // 管宽和描边只影响图形；避免把每个外观变化都当成整棵界面的布局变化。
        AffectsMeasure<Thermometer>(DialWidthProperty, DialHeightProperty);
        AffectsRender<Thermometer>(TubeWidthProperty, BulbRadiusProperty, OutlineThicknessProperty);
    }

    // 这些 CLR 属性是 C# 调用入口，get/set 都操作 Avalonia 的同一套属性存储。
    // 因此 Live.Value=40 和 XAML 绑定更新 Value 会进入相同的属性变化处理流程。
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
    public ThermometerState State => _state;

    internal double DialWidth { get => GetValue(DialWidthProperty); set => SetValue(DialWidthProperty, value); }
    internal double DialHeight { get => GetValue(DialHeightProperty); set => SetValue(DialHeightProperty, value); }
    internal double TubeWidth { get => GetValue(TubeWidthProperty); set => SetValue(TubeWidthProperty, value); }
    internal double BulbRadius { get => GetValue(BulbRadiusProperty); set => SetValue(BulbRadiusProperty, value); }
    internal double OutlineThickness { get => GetValue(OutlineThicknessProperty); set => SetValue(OutlineThicknessProperty, value); }
    internal FontWeight MediumFontWeight { get => GetValue(MediumFontWeightProperty); set => SetValue(MediumFontWeightProperty, value); }
    internal FontWeight RegularFontWeight { get => GetValue(RegularFontWeightProperty); set => SetValue(RegularFontWeightProperty, value); }
    // => 表示每次访问时返回右侧表达式；这里的显示判断不是额外保存的一份业务状态。
    // 数字使用 Value，液位使用 DrawnFraction，不能反过来用动画中的液位作报警判定。
    internal double DrawnFraction => _fill.Value;
    internal bool HasReading => Quality == MeasurementQuality.Good;
    internal string ReadingText => HasReading ? $"{Value.ToString("0.0", CultureInfo.CurrentCulture)} {Unit}".TrimEnd() : "—";
    // switch 按枚举选择文字。先处理非有效质量，Good 才显示温度区间文字。
    // ?? 表示左边为 null 才取右边：自定义提示优先，否则使用当前语言资源。
    internal string StatusText => Quality switch
    {
        MeasurementQuality.NoData => NoDataLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.NoData),
        MeasurementQuality.Stale => StaleLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.Stale),
        MeasurementQuality.Error => ErrorLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.Error),
        _ => ThresholdText
    };
    private string ThresholdText => State switch
    {
        ThermometerState.Low => LowLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.Low),
        ThermometerState.Normal => NormalLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.Normal),
        ThermometerState.High => HighLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.High),
        _ => ThermometerLangResources.Get(ThermometerLangResourceKind.InvalidRange)
    };
    // ?: 是条件选择表达式。质量颜色优先，只有有效数据才使用高低温色调。
    // 颜色来自 Palette 的主题资源；旁边始终有文字说明，不能只靠颜色判断状态。
    internal Color StateColor => Quality == MeasurementQuality.Stale ? _palette.Warning :
        Quality == MeasurementQuality.Error ? _palette.Danger :
        !HasReading || State == ThermometerState.InvalidRange ? _palette.TextMuted : Tone switch
    {
        GaugeTone.Success => _palette.Success,
        GaugeTone.Warning => _palette.Warning,
        GaugeTone.Danger => _palette.Danger,
        _ => _palette.Accent
    };
    private GaugeTone Tone => State switch
    {
        ThermometerState.Low => LowTone,
        ThermometerState.High => HighTone,
        _ => NormalTone
    };
    private bool ValidRange => Maximum > Minimum && HighThreshold >= LowThreshold;
    // 把实际温度映射为 0..1 的液位比例。例如量程 -20..100，40 度对应 0.5。
    // 比例与数据质量分开计算；非有效质量是否画液柱，由 Render 决定。
    private double Fraction
    {
        get
        {
            if (!ValidRange)
            {
                return 0;
            }
            // 先缩小参与运算的数值，避免极大的合法 double 在上下限相减时溢出。
            // Math.Clamp 把最终液位限制在 0..1，Value 本身不会被改成量程边界。
            var scale = Math.Max(1, Math.Max(Math.Abs(Minimum), Math.Abs(Maximum)));
            return Math.Clamp((Value / scale - Minimum / scale) /
                              (Maximum / scale - Minimum / scale), 0, 1);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 基类构造时也可能通知属性变化；辅助字段尚未初始化时先退出，避免空引用。
        if (_fill is null)
        {
            return;
        }
        // 数值、量程、阈值、质量都会影响液位或真实区间；动效等级变化也需要重新同步。
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
        if (change.Property == TubeWidthProperty || change.Property == BulbRadiusProperty)
        {
            // 改了容器形状就丢弃旧几何缓存，下次绘制重新创建，不能继续用旧轮廓。
            _glass = null;
            _liquid = null;
        }
        // 文字可能包含数值、单位、质量或颜色，清空旧布局后由下次 Render 按需重建。
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
        // Quality 不参与这一步，避免破坏已有 State 的含义；显示文字单独检查质量。
        var state = !ValidRange ? ThermometerState.InvalidRange :
            Value < LowThreshold ? ThermometerState.Low :
            Value >= HighThreshold ? ThermometerState.High : ThermometerState.Normal;
        SetAndRaise(StateProperty, ref _state, state);
        // 第二步：选择液位过渡。液柱是自绘几何，模板 Transition 无法直接表达，
        // 因而使用 MotionValue。只有连续有效读数、控件已加载且 Full 动效时才播放。
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
        // Measure 和 Arrange 共用这个函数，避免“测量说一种尺寸，绘制用另一种尺寸”。
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
        // 玻璃外形 → 有效液柱 → 标题/数字/状态 → 刻度与刻度数字。
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
            var width = Math.Clamp(TubeWidth, 20, 60);
            EnsureGeometry();
            var outline = _palette.Pen(_palette.BorderStrong, Math.Max(0, OutlineThickness));
            context.DrawGeometry(_palette.Brush(_palette.SurfaceRaised), outline, _glass!);
            // 液位高度和刻度都包含顶部圆弧，满量程时才会填满圆顶，不留一块空白。
            var scaleHeight = 253 - _liquid!.Bounds.Top;
            var top = 253 - scaleHeight * Math.Clamp(_fill.Value, 0, 1);
            if (HasReading)
            {
                // Clip 是裁剪区域：液体轮廓不用每帧重算，只展示液面以下的部分。
                // 质量非 Good 时跳过整段液体绘制，保留玻璃与刻度，避免旧值冒充当前值。
                using (context.PushClip(new Rect(0, top, DesignWidth, DesignHeight - top)))
                {
                    context.DrawGeometry(_palette.Brush(color), null, _liquid!);
                }
            }
            // 缓存下标约定：0 标题，1 当前数字或破折号，2 状态，3 开始是刻度文字。
            DrawCentered(context, _textRuns[0], 110, 9);
            DrawCentered(context, _textRuns[1], 110, 310);
            DrawCentered(context, _textRuns[2], 110, 342);
            if (ShowScale && ValidRange)
            {
                var text = 3;
                for (var i = 0; i <= TickCount; i++)
                {
                    var y = 253 - scaleHeight * i / TickCount;
                    var major = i % 2 == 0 || i == TickCount;
                    var x = 92 + width / 2 + 10;
                    context.DrawLine(_palette.Pen(_palette.TextMuted, 1), new Point(x, y),
                        new Point(x + (major ? 10 : 5), y));
                    if (major)
                    {
                        var run = _textRuns[text++];
                        run.Draw(context, new Point(x + 17, y - run.Height / 2));
                    }
                }
            }
        }
    }

    private void EnsureGeometry()
    {
        if (_glass is not null && _liquid is not null)
        {
            return;
        }
        var width = Math.Clamp(TubeWidth, 20, 60);
        var radius = Math.Clamp(BulbRadius, width / 2, 36);
        // 玻璃和液体各是一条封闭轮廓，把直管与球泡连成一体，避免叠圆造成接缝。
        // 两条路径保留在缓存中；液位变化只移动裁剪区域，不重新生成容器形状。
        _glass = VesselGeometry(width / 2, radius, 43);
        _liquid = VesselGeometry(Math.Max(8, width - 14) / 2, Math.Max(6, radius - 7), 50);
    }

    private static StreamGeometry VesselGeometry(double halfWidth, double radius, double top)
    {
        // StreamGeometry 是绘图路径。halfWidth 是管半宽，radius 是球泡半径，
        // top 是管顶高度。屏幕 Y 坐标向下增大；这些数值都采用前面的参考坐标。
        const double centerX = 92;
        const double centerY = 275;
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        // 从左上起点开始：顶部半圆 → 右侧直管 → 平滑连接球泡 → 底部半圆
        // → 左侧平滑连接 → 左侧直管 → 闭合。贝塞尔控制点决定连接处的弯曲程度。
        path.BeginFigure(new Point(centerX - halfWidth, top + halfWidth), true);
        path.ArcTo(new Point(centerX + halfWidth, top + halfWidth),
            new Size(halfWidth, halfWidth), 0, false, SweepDirection.Clockwise);
        path.LineTo(new Point(centerX + halfWidth, centerY - radius));
        path.CubicBezierTo(new Point(centerX + halfWidth, centerY - radius * .65),
            new Point(centerX + radius, centerY - radius * .5),
            new Point(centerX + radius, centerY));
        path.ArcTo(new Point(centerX - radius, centerY),
            new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        path.CubicBezierTo(new Point(centerX - radius, centerY - radius * .5),
            new Point(centerX - halfWidth, centerY - radius * .65),
            new Point(centerX - halfWidth, centerY - radius));
        path.LineTo(new Point(centerX - halfWidth, top + halfWidth));
        path.EndFigure(true);
        return geometry;
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
                if (i % 2 != 0 && i != TickCount)
                {
                    continue;
                }
                var fraction = (double)i / TickCount;
                var value = Minimum * (1 - fraction) + Maximum * fraction;
                if (Math.Abs(value) < 1e-9)
                {
                    value = 0;
                }
                _textRuns.Add(ChartsBText.TrimmedRun(value.ToString("0.#", CultureInfo.CurrentCulture),
                    _palette, _palette.TypeXS, _palette.TextMuted, 65, RegularFontWeight));
            }
        }
    }

    private static void DrawCentered(DrawingContext context, ChartsBRun run, double x, double y)
        => run.Draw(context, new Point(x - run.Width / 2, y));

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ThermometerAutomationPeer(this);

    private sealed class ThermometerAutomationPeer(Thermometer owner) : ControlAutomationPeer(owner), IRangeValueProvider, IValueProvider
    {
        // AutomationPeer 给屏幕阅读器和自动化工具提供信息；自绘数字不是 TextBlock，
        // 所以要在这里主动提供名称、状态和只读数值，而不是让辅助工具读取动画位置。
        private string? _lastHelp;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        protected override string GetLocalizedControlTypeCore() => "thermometer";
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
        public void SetValue(double value) { }
        // 数值接口保留旧契约：返回保存的、限制到量程内的数值，不能据此忽略 Quality。
        // 文本接口在非 Good 时只返回质量状态，不把旧数字播报成当前有效测量。
        // 两个 SetValue 都是空实现，因为温度计是显示控件，不能通过辅助接口设定温度。
        string IValueProvider.Value => GetHelpTextCore() ?? string.Empty;
        void IValueProvider.SetValue(string? value) { }

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
