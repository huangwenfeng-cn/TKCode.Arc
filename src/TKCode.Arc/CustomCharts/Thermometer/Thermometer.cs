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

/// <summary>A read-only temperature meter with a spring-driven liquid column, scale, actual reading and named threshold state.</summary>
public class Thermometer : Control
{
    // Drawing coordinates stay fixed; the size tokens bound a uniform scale of this reference box.
    private const double DesignWidth = 220;
    private const double DesignHeight = 360;

    /// <summary>The actual temperature; an out-of-range reading is kept while the liquid is clamped.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Thermometer, double>(nameof(Value), 0, validate: double.IsFinite);
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
    /// <summary>The band of the actual reading; changes immediately, independently of animation.</summary>
    public static readonly DirectProperty<Thermometer, ThermometerState> StateProperty =
        AvaloniaProperty.RegisterDirect<Thermometer, ThermometerState>(nameof(State), owner => owner.State);

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

    private readonly ChartsBPalette _palette;
    private readonly MotionValue<double> _fill;
    private readonly List<ChartsBRun> _textRuns = new();
    private StreamGeometry? _glass;
    private StreamGeometry? _liquid;
    private Rect _dial;
    private ThermometerState _state = ThermometerState.Low;
    private ArcLanguageManager? _languages;
    private ThermometerAutomationPeer? _peer;

    /// <summary>Creates a thermometer.</summary>
    public Thermometer()
    {
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _fill = new MotionValue<double>(this, Fraction, MotionVectors.Double);
        _fill.Changed += _ => InvalidateVisual();
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    static Thermometer()
    {
        AffectsMeasure<Thermometer>(DialWidthProperty, DialHeightProperty);
        AffectsRender<Thermometer>(TubeWidthProperty, BulbRadiusProperty, OutlineThicknessProperty);
    }

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
    internal double DrawnFraction => _fill.Value;
    internal string ReadingText => $"{Value.ToString("0.0", CultureInfo.CurrentCulture)} {Unit}".TrimEnd();
    internal string StatusText => State switch
    {
        ThermometerState.Low => LowLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.Low),
        ThermometerState.Normal => NormalLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.Normal),
        ThermometerState.High => HighLabel ?? ThermometerLangResources.Get(ThermometerLangResourceKind.High),
        _ => ThermometerLangResources.Get(ThermometerLangResourceKind.InvalidRange)
    };
    internal Color StateColor => State == ThermometerState.InvalidRange ? _palette.TextMuted : Tone switch
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
    private double Fraction
    {
        get
        {
            if (!ValidRange)
            {
                return 0;
            }
            var scale = Math.Max(1, Math.Max(Math.Abs(Minimum), Math.Abs(Maximum)));
            return Math.Clamp((Value / scale - Minimum / scale) /
                              (Maximum / scale - Minimum / scale), 0, 1);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_fill is null)
        {
            return;
        }
        if (change.Property == ValueProperty || change.Property == MinimumProperty ||
            change.Property == MaximumProperty || change.Property == LowThresholdProperty ||
            change.Property == HighThresholdProperty)
        {
            Sync();
        }
        else if (change.Property == ArcMotion.LevelProperty || change.Property == ArcMotion.TokenLevelProperty)
        {
            Sync();
        }
        if (change.Property == TubeWidthProperty || change.Property == BulbRadiusProperty)
        {
            _glass = null;
            _liquid = null;
        }
        ClearText();
        InvalidateVisual();
        if (change.Property == ValueProperty || change.Property == UnitProperty ||
            change.Property == LabelProperty || change.Property == LowLabelProperty ||
            change.Property == NormalLabelProperty || change.Property == HighLabelProperty ||
            change.Property == StateProperty)
        {
            _peer?.Announce();
        }
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
        ClearText();
        base.OnDetachedFromVisualTree(e);
    }

    private void Sync()
    {
        var state = !ValidRange ? ThermometerState.InvalidRange :
            Value < LowThreshold ? ThermometerState.Low :
            Value >= HighThreshold ? ThermometerState.High : ThermometerState.Normal;
        SetAndRaise(StateProperty, ref _state, state);
        // The fill changes drawing geometry, not a templated property, so it needs a MotionValue rather than a theme transition.
        if (IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _fill.AnimateTo(Fraction, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _fill.Jump(Fraction);
        }
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
        foreach (var run in _textRuns)
        {
            run.Dispose();
        }
        _textRuns.Clear();
    }

    private Size FitDial(Size availableSize)
    {
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
            // Include the rounded cap in both the liquid travel and the scale, so the maximum fills the entire vessel.
            var scaleHeight = 253 - _liquid!.Bounds.Top;
            var top = 253 - scaleHeight * Math.Clamp(_fill.Value, 0, 1);
            using (context.PushClip(new Rect(0, top, DesignWidth, DesignHeight - top)))
            {
                context.DrawGeometry(_palette.Brush(color), null, _liquid!);
            }
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
        // One closed outline joins the stem and bulb without an overlapping circular seam.
        // Keep both paths cached: animation changes only the liquid's horizontal clipping level.
        _glass = VesselGeometry(width / 2, radius, 43);
        _liquid = VesselGeometry(Math.Max(8, width - 14) / 2, Math.Max(6, radius - 7), 50);
    }

    private static StreamGeometry VesselGeometry(double halfWidth, double radius, double top)
    {
        const double centerX = 92;
        const double centerY = 275;
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
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

    private sealed class ThermometerAutomationPeer(Thermometer owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        private string? _lastHelp;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        protected override string GetLocalizedControlTypeCore() => "thermometer";
        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(owner);
            return string.IsNullOrEmpty(name) ? owner.Label : name;
        }
        protected override string? GetHelpTextCore() => $"{owner.ReadingText}, {owner.StatusText}";
        protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];
        public bool IsReadOnly => true;
        public double Minimum => owner.Minimum;
        public double Maximum => Math.Max(owner.Minimum, owner.Maximum);
        public double Value => Math.Clamp(owner.Value, Minimum, Maximum);
        public double SmallChange => 0;
        public double LargeChange => 0;
        public void SetValue(double value) { }

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
