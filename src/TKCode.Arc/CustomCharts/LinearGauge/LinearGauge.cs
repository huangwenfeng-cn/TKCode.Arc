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

// 入门阅读顺序：属性 → 文字/状态 → Sync → 布局 → Render → 无障碍。
// Value 保存真实读数，State 表示阈值区间，Quality 决定这份读数能否展示。
// 本控件适用于拉力、压力、速度等连续数值；单位只是文字，不进行单位换算。
// 不继承 LevelIndicator，避免把通用量程 API 与储罐语义绑定；范围和填充算法共享 GaugeScale。
/// <summary>A read-only vertical range meter with a narrow bar, scale, actual reading and named threshold states.</summary>
public class LinearGauge : Control
{
    private const double DesignWidth = 220;
    private const double DesignHeight = 360;
    private const double TrackCenterX = 86;
    private const double TrackTop = 40;
    private const double TrackBottom = 272;

    // StyledProperty 支持 XAML、样式和绑定。拒绝 NaN/无穷，但保存合法的越界读数。
    /// <summary>The actual reading; the bar is clamped to the range while the number keeps out-of-range values.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<LinearGauge, double>(nameof(Value), 0, validate: double.IsFinite);
    /// <summary>Lower range bound (default 0).</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<LinearGauge, double>(nameof(Minimum), 0, validate: double.IsFinite);
    /// <summary>Upper range bound (default 100).</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<LinearGauge, double>(nameof(Maximum), 100, validate: double.IsFinite);
    /// <summary>Readings below this value are low (default 20).</summary>
    public static readonly StyledProperty<double> LowThresholdProperty =
        AvaloniaProperty.Register<LinearGauge, double>(nameof(LowThreshold), 20, validate: double.IsFinite);
    /// <summary>Readings at or above this value are high (default 80).</summary>
    public static readonly StyledProperty<double> HighThresholdProperty =
        AvaloniaProperty.Register<LinearGauge, double>(nameof(HighThreshold), 80, validate: double.IsFinite);
    /// <summary>The measured quantity, shown above the track and used as the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<LinearGauge, string?>(nameof(Label));
    /// <summary>Display unit (default empty); does not convert readings or thresholds.</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<LinearGauge, string>(nameof(Unit), string.Empty);
    /// <summary>Decimal places in the actual reading, from 0 to 6 (default 1).</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<LinearGauge, int>(nameof(Decimals), 1, validate: value => value is >= 0 and <= 6);
    /// <summary>Number of scale divisions, from 1 to 10 (default 4).</summary>
    public static readonly StyledProperty<int> TickCountProperty =
        AvaloniaProperty.Register<LinearGauge, int>(nameof(TickCount), 4, validate: value => value is >= 1 and <= 10);
    /// <summary>Whether the scale and its values are visible (default true).</summary>
    public static readonly StyledProperty<bool> ShowScaleProperty =
        AvaloniaProperty.Register<LinearGauge, bool>(nameof(ShowScale), true);
    /// <summary>Color family for low readings (default Warning).</summary>
    public static readonly StyledProperty<GaugeTone> LowToneProperty =
        AvaloniaProperty.Register<LinearGauge, GaugeTone>(nameof(LowTone), GaugeTone.Warning);
    /// <summary>Color family for normal readings (default Accent).</summary>
    public static readonly StyledProperty<GaugeTone> NormalToneProperty =
        AvaloniaProperty.Register<LinearGauge, GaugeTone>(nameof(NormalTone), GaugeTone.Accent);
    /// <summary>Color family for high readings (default Danger).</summary>
    public static readonly StyledProperty<GaugeTone> HighToneProperty =
        AvaloniaProperty.Register<LinearGauge, GaugeTone>(nameof(HighTone), GaugeTone.Danger);
    // null 使用当前语言，空字符串则明确隐藏这段文字。标签不负责改变阈值判定。
    /// <summary>Custom low-state text; null follows the application language.</summary>
    public static readonly StyledProperty<string?> LowLabelProperty =
        AvaloniaProperty.Register<LinearGauge, string?>(nameof(LowLabel));
    /// <summary>Custom normal-state text; null follows the application language.</summary>
    public static readonly StyledProperty<string?> NormalLabelProperty =
        AvaloniaProperty.Register<LinearGauge, string?>(nameof(NormalLabel));
    /// <summary>Custom high-state text; null follows the application language.</summary>
    public static readonly StyledProperty<string?> HighLabelProperty =
        AvaloniaProperty.Register<LinearGauge, string?>(nameof(HighLabel));
    /// <summary>Application-provided measurement quality (default Good); other states hide the bar and number without changing Value or State. No timeout is inferred.</summary>
    public static readonly StyledProperty<MeasurementQuality> QualityProperty =
        AvaloniaProperty.Register<LinearGauge, MeasurementQuality>(nameof(Quality), MeasurementQuality.Good,
            validate: value => value is >= MeasurementQuality.Good and <= MeasurementQuality.Error);
    /// <summary>Custom missing-data text; null follows the application language.</summary>
    public static readonly StyledProperty<string?> NoDataLabelProperty =
        AvaloniaProperty.Register<LinearGauge, string?>(nameof(NoDataLabel));
    /// <summary>Custom stale-data text; null follows the application language.</summary>
    public static readonly StyledProperty<string?> StaleLabelProperty =
        AvaloniaProperty.Register<LinearGauge, string?>(nameof(StaleLabel));
    /// <summary>Custom collection-error text; null follows the application language.</summary>
    public static readonly StyledProperty<string?> ErrorLabelProperty =
        AvaloniaProperty.Register<LinearGauge, string?>(nameof(ErrorLabel));
    // DirectProperty 只读，Sync 使用 SetAndRaise 通知绑定；外部不能覆盖计算状态。
    /// <summary>The threshold band of the actual reading; updates immediately, independently of bar motion.</summary>
    public static readonly DirectProperty<LinearGauge, LinearGaugeState> StateProperty =
        AvaloniaProperty.RegisterDirect<LinearGauge, LinearGaugeState>(nameof(State), owner => owner.State);

    internal static readonly StyledProperty<double> DialWidthProperty = AvaloniaProperty.Register<LinearGauge, double>(nameof(DialWidth), 220);
    internal static readonly StyledProperty<double> DialHeightProperty = AvaloniaProperty.Register<LinearGauge, double>(nameof(DialHeight), 360);
    internal static readonly StyledProperty<double> TrackWidthProperty = AvaloniaProperty.Register<LinearGauge, double>(nameof(TrackWidth), 36);
    internal static readonly StyledProperty<double> OutlineThicknessProperty = AvaloniaProperty.Register<LinearGauge, double>(nameof(OutlineThickness), 1.5);
    internal static readonly StyledProperty<CornerRadius> TrackCornerRadiusProperty = AvaloniaProperty.Register<LinearGauge, CornerRadius>(nameof(TrackCornerRadius));
    internal static readonly StyledProperty<FontWeight> MediumFontWeightProperty = AvaloniaProperty.Register<LinearGauge, FontWeight>(nameof(MediumFontWeight), FontWeight.Medium);
    internal static readonly StyledProperty<FontWeight> RegularFontWeightProperty = AvaloniaProperty.Register<LinearGauge, FontWeight>(nameof(RegularFontWeight), FontWeight.Normal);

    // 控件拥有 Palette、MotionValue 和有限数量的文字布局；外部语言订阅仅在挂载期间持有。
    private readonly ChartsBPalette _palette;
    private readonly MotionValue<double> _fill;
    private readonly List<ChartsBRun> _textRuns = new();
    private LinearGaugeState _state;
    private Rect _dial;
    private bool _hadReading;
    private ArcLanguageManager? _languages;
    private LinearGaugeAutomationPeer? _peer;

    /// <summary>Creates a linear gauge.</summary>
    public LinearGauge()
    {
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _fill = new MotionValue<double>(this, Fraction, MotionVectors.Double);
        // 自有动画对象与控件同生命周期；帧更新仅重绘，不重新排版文字。
        _fill.Changed += _ => InvalidateVisual();
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    static LinearGauge()
    {
        AffectsMeasure<LinearGauge>(DialWidthProperty, DialHeightProperty);
        AffectsRender<LinearGauge>(TrackWidthProperty, OutlineThicknessProperty, TrackCornerRadiusProperty);
    }

    // CLR 入口与 XAML 绑定访问同一属性存储；属性变化统一进入 OnPropertyChanged。
    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
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
    /// <inheritdoc cref="DecimalsProperty"/>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
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
    /// <inheritdoc cref="QualityProperty"/>
    public MeasurementQuality Quality { get => GetValue(QualityProperty); set => SetValue(QualityProperty, value); }
    /// <inheritdoc cref="NoDataLabelProperty"/>
    public string? NoDataLabel { get => GetValue(NoDataLabelProperty); set => SetValue(NoDataLabelProperty, value); }
    /// <inheritdoc cref="StaleLabelProperty"/>
    public string? StaleLabel { get => GetValue(StaleLabelProperty); set => SetValue(StaleLabelProperty, value); }
    /// <inheritdoc cref="ErrorLabelProperty"/>
    public string? ErrorLabel { get => GetValue(ErrorLabelProperty); set => SetValue(ErrorLabelProperty, value); }
    /// <inheritdoc cref="StateProperty"/>
    public LinearGaugeState State => _state;

    internal double DialWidth { get => GetValue(DialWidthProperty); set => SetValue(DialWidthProperty, value); }
    internal double DialHeight { get => GetValue(DialHeightProperty); set => SetValue(DialHeightProperty, value); }
    internal double TrackWidth { get => GetValue(TrackWidthProperty); set => SetValue(TrackWidthProperty, value); }
    internal double OutlineThickness { get => GetValue(OutlineThicknessProperty); set => SetValue(OutlineThicknessProperty, value); }
    internal CornerRadius TrackCornerRadius { get => GetValue(TrackCornerRadiusProperty); set => SetValue(TrackCornerRadiusProperty, value); }
    internal FontWeight MediumFontWeight { get => GetValue(MediumFontWeightProperty); set => SetValue(MediumFontWeightProperty, value); }
    internal FontWeight RegularFontWeight { get => GetValue(RegularFontWeightProperty); set => SetValue(RegularFontWeightProperty, value); }
    internal double DrawnFraction => _fill.Value;
    internal bool HasReading => Quality == MeasurementQuality.Good;
    internal string ReadingText => HasReading ? $"{Value.ToString($"F{Decimals}", CultureInfo.CurrentCulture)} {Unit}".TrimEnd() : "—";
    internal string StatusText => Quality switch
    {
        MeasurementQuality.NoData => NoDataLabel ?? LinearGaugeLangResources.Get(LinearGaugeLangResourceKind.NoData),
        MeasurementQuality.Stale => StaleLabel ?? LinearGaugeLangResources.Get(LinearGaugeLangResourceKind.Stale),
        MeasurementQuality.Error => ErrorLabel ?? LinearGaugeLangResources.Get(LinearGaugeLangResourceKind.Error),
        _ => State switch
        {
            LinearGaugeState.Low => LowLabel ?? LinearGaugeLangResources.Get(LinearGaugeLangResourceKind.Low),
            LinearGaugeState.Normal => NormalLabel ?? LinearGaugeLangResources.Get(LinearGaugeLangResourceKind.Normal),
            LinearGaugeState.High => HighLabel ?? LinearGaugeLangResources.Get(LinearGaugeLangResourceKind.High),
            _ => LinearGaugeLangResources.Get(LinearGaugeLangResourceKind.InvalidRange)
        }
    };
    // 质量文字/颜色优先，State 始终按真实 Value 保存。过期数据不能冒充当前正常读数。
    internal Color StateColor => Quality == MeasurementQuality.Stale ? _palette.Warning :
        Quality == MeasurementQuality.Error ? _palette.Danger :
        !HasReading || !ValidRange ? _palette.TextMuted : ToneColor(State switch
        {
            LinearGaugeState.Low => LowTone,
            LinearGaugeState.High => HighTone,
            _ => NormalTone
        });
    private Color ToneColor(GaugeTone tone) => tone switch
    {
        GaugeTone.Success => _palette.Success,
        GaugeTone.Warning => _palette.Warning,
        GaugeTone.Danger => _palette.Danger,
        _ => _palette.Accent
    };
    private bool ValidRange => Maximum > Minimum && HighThreshold >= LowThreshold;
    private double Fraction => ValidRange ? GaugeScale.Fraction(Value, Minimum, Maximum) : 0;
    private Rect TrackRect => new(TrackCenterX - Math.Clamp(TrackWidth, 16, 64) / 2,
        TrackTop, Math.Clamp(TrackWidth, 16, 64), TrackBottom - TrackTop);
    private double Outline => Math.Clamp(OutlineThickness, 0, 4);
    private Rect InnerRect => TrackRect.Deflate(Outline + 2);
    private double Radius => Math.Clamp(TrackCornerRadius.TopLeft, 0, TrackRect.Width / 2);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_fill is null)
        {
            return; // 基类构造期间，辅助字段尚未创建。
        }
        if (change.Property == ValueProperty || change.Property == MinimumProperty ||
            change.Property == MaximumProperty || change.Property == LowThresholdProperty ||
            change.Property == HighThresholdProperty || change.Property == QualityProperty ||
            change.Property == ArcMotion.LevelProperty || change.Property == ArcMotion.TokenLevelProperty)
        {
            Sync();
        }
        // 数字、单位、几何和主题都可能使布局缓存失效。数量最多 14，不随动画帧增长。
        ClearText();
        InvalidateVisual();
        _peer?.Announce(); // 内部比较帮助文字，重复通知不会造成重复播报。
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
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
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
        _fill.Jump(Fraction);
        _hadReading = false;
        ClearText();
        base.OnDetachedFromVisualTree(e);
    }

    private void Sync()
    {
        // 低阈值本身属于 Normal，高阈值本身属于 High；质量不参与区间判定。
        var state = !ValidRange ? LinearGaugeState.InvalidRange :
            Value < LowThreshold ? LinearGaugeState.Low :
            Value >= HighThreshold ? LinearGaugeState.High : LinearGaugeState.Normal;
        SetAndRaise(StateProperty, ref _state, state);
        // 柱条为自绘几何，ControlTheme 的 Transition 无法表达其目标比例。
        // 首个有效样本、恢复、重挂载或 Reduced/Off 都立即落到目标；仅连续 Good 样本播放弹簧。
        if (HasReading && _hadReading && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _fill.AnimateTo(Fraction, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _fill.Jump(Fraction);
        }
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
        // 文字布局持有排版资源，先释放再清空；卸载后再次绘制会按当前资源重建。
        foreach (var run in _textRuns)
        {
            run.Dispose();
        }
        _textRuns.Clear();
    }
    private Size FitDial(Size available)
    {
        // 测量和布局使用同一等比缩放；例如 440 × 360 仍显示 220 × 360，避免拉伸柱条。
        var scale = Math.Max(0, Math.Min(Math.Min(available.Width, Math.Max(1, DialWidth)) / DesignWidth,
            Math.Min(available.Height, Math.Max(1, DialHeight)) / DesignHeight));
        return new Size(DesignWidth * scale, DesignHeight * scale);
    }
    protected override Size MeasureOverride(Size availableSize) => FitDial(availableSize);
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = FitDial(finalSize);
        _dial = new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height);
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var scale = _dial.Width / DesignWidth;
        if (!(scale > 0))
        {
            return;
        }
        EnsureText();
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(_dial.X, _dial.Y)))
        {
            var track = TrackRect;
            var inner = InnerRect;
            context.DrawRectangle(_palette.Brush(_palette.SurfaceRaised),
                _palette.Pen(_palette.BorderStrong, Outline), track, Radius, Radius);
            if (HasReading && ValidRange)
            {
                GaugeScale.DrawFill(context, _palette.Brush(StateColor), inner,
                    Math.Max(0, Radius - Outline - 2), _fill.Value);
            }
            // 左侧短标记对应实际阈值，不跟随动画；范围外阈值不伪装成端点。
            if (ValidRange)
            {
                DrawThreshold(context, LowThreshold, LowTone, inner, track.Left);
                DrawThreshold(context, HighThreshold, HighTone, inner, track.Left);
            }
            DrawCentered(context, _textRuns[0], 9);
            DrawCentered(context, _textRuns[1], 300);
            DrawCentered(context, _textRuns[2], 334);
            if (ShowScale && ValidRange)
            {
                for (var i = 0; i <= TickCount; i++)
                {
                    var y = inner.Bottom - inner.Height * i / TickCount;
                    var x = track.Right + 7;
                    context.DrawLine(_palette.Pen(_palette.TextMuted, 1), new Point(x, y), new Point(x + 8, y));
                    var run = _textRuns[3 + i];
                    run.Draw(context, new Point(x + 13, y - run.Height / 2));
                }
            }
        }
    }

    private void DrawThreshold(DrawingContext context, double value, GaugeTone tone, Rect inner, double left)
    {
        if (value < Minimum || value > Maximum)
        {
            return;
        }
        var y = inner.Bottom - inner.Height * GaugeScale.Fraction(value, Minimum, Maximum);
        context.DrawLine(_palette.Pen(ToneColor(tone), 2), new Point(left - 13, y), new Point(left - 5, y));
    }
    private void EnsureText()
    {
        if (_textRuns.Count > 0)
        {
            return;
        }
        _textRuns.Add(ChartsBText.TrimmedRun(Label ?? string.Empty, _palette, _palette.TypeSM, _palette.Foreground, 204, MediumFontWeight));
        _textRuns.Add(ChartsBText.TrimmedRun(ReadingText, _palette, _palette.TypeBase * 1.5, _palette.Foreground, 204, MediumFontWeight));
        _textRuns.Add(ChartsBText.TrimmedRun(StatusText, _palette, _palette.TypeXS, StateColor, 204, RegularFontWeight));
        if (ShowScale && ValidRange)
        {
            for (var i = 0; i <= TickCount; i++)
            {
                var value = GaugeScale.Tick(Minimum, Maximum, (double)i / TickCount);
                // 四位量程数字可完整显示；极大数用科学记数法避免刻度变成省略号。
                var format = Math.Abs(value) >= 1e6 || (value != 0 && Math.Abs(value) < .001) ? "0.##E+0" : "0.###";
                _textRuns.Add(ChartsBText.TrimmedRun(value.ToString(format, CultureInfo.CurrentCulture),
                    _palette, _palette.TypeXS, _palette.TextMuted, DesignWidth - TrackRect.Right - 28, RegularFontWeight));
            }
        }
    }
    private static void DrawCentered(DrawingContext context, ChartsBRun run, double y)
        => run.Draw(context, new Point(DesignWidth / 2 - run.Width / 2, y));

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new LinearGaugeAutomationPeer(this);
    private sealed class LinearGaugeAutomationPeer(LinearGauge owner) : ControlAutomationPeer(owner), IRangeValueProvider, IValueProvider
    {
        // 自绘文字没有 TextBlock 子节点，辅助接口主动报告真实数字和质量。
        // 数值接口提供保存的范围值；文本接口在非 Good 时仅报告质量，使用方应先检查 Quality。
        private string? _lastHelp;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        protected override string GetLocalizedControlTypeCore() => "linear gauge";
        protected override string? GetNameCore() => string.IsNullOrEmpty(AutomationProperties.GetName(owner)) ? owner.Label : AutomationProperties.GetName(owner);
        protected override string? GetHelpTextCore() => owner.HasReading ? $"{owner.ReadingText}, {owner.StatusText}" : owner.StatusText;
        protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];
        public bool IsReadOnly => true;
        public double Minimum => owner.Minimum;
        public double Maximum => Math.Max(owner.Minimum, owner.Maximum);
        public double Value => Math.Clamp(owner.Value, Minimum, Maximum);
        public double SmallChange => 0;
        public double LargeChange => 0;
        public void SetValue(double value) { }
        string IValueProvider.Value => GetHelpTextCore() ?? string.Empty;
        void IValueProvider.SetValue(string? value) { }
        public void Announce()
        {
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
