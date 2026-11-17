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

// 阅读顺序：属性输入 → Sync 更新视觉角度 → Measure/Arrange 确定缩放 → Render 分层绘制。
// 控件只接收业务层给出的风向和状态；设备通信、风速与静风阈值由应用负责。
// 公共 XML 文档保留英文，供 Gallery 生成三语 API 表；以下中文注释解释实现原理。
/// <summary>A read-only compass showing meteorological wind origin, with an optional direction reading and explicit calm or missing-data states.</summary>
public class WindDirectionIndicator : Control
{
    // 所有几何先在这套参考坐标中计算，再整体等比例缩放；这里的单位不是最终屏幕像素。
    // 默认圆盘为 320 × 320，底部读数开启后增加 32 单位，圆心与圆盘形状不变。
    private const double DesignWidth = 320;
    private const double DesignHeight = 352;
    private const double OuterRadius = 116;
    private static readonly Point Center = new(160, 160);

    // StyledProperty 让输入支持 XAML、数据绑定和样式；CLR 属性通过 GetValue/SetValue 访问同一存储。
    // 校验只拒绝 NaN 和无穷；负数和超过一圈的有限角度可以输入，显示前再归一化。
    /// <summary>Wind origin in degrees clockwise from north (default 0); finite values wrap into [0, 360).</summary>
    public static readonly StyledProperty<double> DirectionProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, double>(nameof(Direction), 0, validate: double.IsFinite);
    /// <summary>Whether the measurement is available (default true); false hides the arrow and all measured values.</summary>
    public static readonly StyledProperty<bool> HasDataProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, bool>(nameof(HasData), true);
    /// <summary>Whether the application reports calm wind (default false); calm hides the arrow and direction. No calm threshold is inferred.</summary>
    public static readonly StyledProperty<bool> IsCalmProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, bool>(nameof(IsCalm));
    /// <summary>Whether to show the direction reading below the compass (default false); enabled adds 32 drawing units to its height.</summary>
    public static readonly StyledProperty<bool> ShowReadingProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, bool>(nameof(ShowReading));
    /// <summary>Accessible name; null follows the application's language. Place visible headings outside the compass.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, string?>(nameof(Label));
    /// <summary>Custom calm text; null follows the application's language.</summary>
    public static readonly StyledProperty<string?> CalmLabelProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, string?>(nameof(CalmLabel));
    /// <summary>Custom missing-data text; null follows the application's language.</summary>
    public static readonly StyledProperty<string?> NoDataLabelProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, string?>(nameof(NoDataLabel));

    // 内部属性承接主题 Token，不作为额外公共配置 API；无主题时的回退值与 Token 默认值一致。
    internal static readonly StyledProperty<double> DialWidthProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, double>(nameof(DialWidth), DesignWidth);
    internal static readonly StyledProperty<double> DialHeightProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, double>(nameof(DialHeight), DesignHeight);
    internal static readonly StyledProperty<double> NeedleLengthProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, double>(nameof(NeedleLength), 68);
    internal static readonly StyledProperty<double> NeedleWidthProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, double>(nameof(NeedleWidth), 48);
    internal static readonly StyledProperty<double> RingWidthProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, double>(nameof(RingWidth), 18);
    internal static readonly StyledProperty<double> OutlineThicknessProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, double>(nameof(OutlineThickness), 1.5);
    internal static readonly StyledProperty<FontWeight> MediumFontWeightProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, FontWeight>(nameof(MediumFontWeight), FontWeight.Medium);
    internal static readonly StyledProperty<FontWeight> RegularFontWeightProperty =
        AvaloniaProperty.Register<WindDirectionIndicator, FontWeight>(nameof(RegularFontWeight), FontWeight.Normal);

    // Palette 跟踪主题颜色和字号；MotionValue 只保存视觉角度，不改变真实 Direction。
    private readonly ChartsBPalette _palette;
    private readonly MotionValue<double> _angle;
    // 文字布局缓存：0 是实际读数/状态，1..16 是外圈角度，17..24 是内圈八方位。
    // 普通采样仅替换第 0 项，避免每次采样都重新排版整个圆盘。
    private readonly List<ChartsBRun> _text = new();
    private StreamGeometry? _needle;
    private StreamGeometry? _marker;
    private Rect _dial;
    // 记住上一次是否有有效方向，防止从静风/缺测恢复时把旧方向动画当成当前测量。
    private bool _hadDirection;
    private ArcLanguageManager? _languages;
    private WindDirectionAutomationPeer? _peer;

    /// <summary>Creates a wind direction indicator.</summary>
    public WindDirectionIndicator()
    {
        // 复用图表共享原语；每帧只请求重绘，几何和静态文字仍然复用缓存。
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _angle = new MotionValue<double>(this, NormalizedDirection, MotionVectors.Double);
        _angle.Changed += _ => InvalidateVisual();
        // 礼貌播报避免读数更新打断屏幕阅读器正在读的内容。
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    static WindDirectionIndicator()
    {
        // 宽高和读数开关改变期望尺寸；针与环的几何参数只影响绘制。
        AffectsMeasure<WindDirectionIndicator>(DialWidthProperty, DialHeightProperty, ShowReadingProperty);
        AffectsRender<WindDirectionIndicator>(NeedleLengthProperty, NeedleWidthProperty, RingWidthProperty, OutlineThicknessProperty);
    }

    /// <inheritdoc cref="DirectionProperty"/>
    public double Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }
    /// <inheritdoc cref="HasDataProperty"/>
    public bool HasData { get => GetValue(HasDataProperty); set => SetValue(HasDataProperty, value); }
    /// <inheritdoc cref="IsCalmProperty"/>
    public bool IsCalm { get => GetValue(IsCalmProperty); set => SetValue(IsCalmProperty, value); }
    /// <inheritdoc cref="ShowReadingProperty"/>
    public bool ShowReading { get => GetValue(ShowReadingProperty); set => SetValue(ShowReadingProperty, value); }
    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <inheritdoc cref="CalmLabelProperty"/>
    public string? CalmLabel { get => GetValue(CalmLabelProperty); set => SetValue(CalmLabelProperty, value); }
    /// <inheritdoc cref="NoDataLabelProperty"/>
    public string? NoDataLabel { get => GetValue(NoDataLabelProperty); set => SetValue(NoDataLabelProperty, value); }

    internal double DialWidth { get => GetValue(DialWidthProperty); set => SetValue(DialWidthProperty, value); }
    internal double DialHeight { get => GetValue(DialHeightProperty); set => SetValue(DialHeightProperty, value); }
    internal double NeedleLength { get => GetValue(NeedleLengthProperty); set => SetValue(NeedleLengthProperty, value); }
    internal double NeedleWidth { get => GetValue(NeedleWidthProperty); set => SetValue(NeedleWidthProperty, value); }
    internal double RingWidth { get => GetValue(RingWidthProperty); set => SetValue(RingWidthProperty, value); }
    internal double OutlineThickness { get => GetValue(OutlineThicknessProperty); set => SetValue(OutlineThicknessProperty, value); }
    internal FontWeight MediumFontWeight { get => GetValue(MediumFontWeightProperty); set => SetValue(MediumFontWeightProperty, value); }
    internal FontWeight RegularFontWeight { get => GetValue(RegularFontWeightProperty); set => SetValue(RegularFontWeightProperty, value); }
    // 实际值用于文字和无障碍；DrawnAngle 是动画中的视觉值，两者有意分开。
    internal double NormalizedDirection => Normalize(Direction);
    internal double DrawnAngle => _angle.Value;
    internal bool HasDirection => HasData && !IsCalm;
    internal Color NeedleColor => _palette.Accent;
    internal string HeadingText => Label ?? Text(WindDirectionIndicatorLangResourceKind.WindDirection);
    // 缺测优先于静风。四舍五入后再归一化，避免 359.99° 显示成 360°。
    internal string ReadingText => !HasData ? NoDataLabel ?? Text(WindDirectionIndicatorLangResourceKind.NoData) :
        IsCalm ? CalmLabel ?? Text(WindDirectionIndicatorLangResourceKind.Calm) :
        $"{Normalize(Math.Round(NormalizedDirection, 1)).ToString("0.#", CultureInfo.CurrentCulture)}° · {DirectionName(NormalizedDirection)}";
    private double ReferenceHeight => ShowReading ? DesignHeight : DesignWidth;
    private double InnerRadius => OuterRadius - Math.Clamp(RingWidth, 12, 22);

    private static string Text(WindDirectionIndicatorLangResourceKind kind) => WindDirectionIndicatorLangResources.Get(kind);
    // 十六方位每段 22.5°；加半段 11.25° 后取整，得到最近的方位而非前一个方位。
    // 枚举前十六项必须按从北开始的顺时针顺序排列，最后用 %16 将北附近折回第 0 项。
    private static string DirectionName(double angle)
        => Text((WindDirectionIndicatorLangResourceKind)((int)Math.Floor((angle + 11.25) / 22.5) % 16));
    // C# 的负数余数仍可能为负，因此先加一圈、再取余，结果稳定落在 [0, 360)。
    private static double Normalize(double angle) => (angle % 360 + 360) % 360;

    internal static double NearestAngle(double current, double direction)
    {
        // 先归一化再相减，避免极大的有限输入在相减时溢出。
        // 差值限制在 [-180, 180]：359° → 1° 的差是 +2°，展开后的目标为 361°。
        // 保留 current 的圈数使正在运动的箭头连续，不会在跨北时绕远路。
        var delta = Normalize(direction) - Normalize(current);
        if (delta > 180)
        {
            delta -= 360;
        }
        else if (delta < -180)
        {
            delta += 360;
        }
        return current + delta;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 基类构造过程中也可能触发属性回调，此时本类的动画字段尚未初始化。
        if (_angle is null)
        {
            return;
        }
        if (change.Property == DirectionProperty || change.Property == HasDataProperty ||
            change.Property == IsCalmProperty || change.Property == ArcMotion.LevelProperty ||
            change.Property == ArcMotion.TokenLevelProperty)
        {
            Sync();
        }
        if (change.Property == NeedleLengthProperty || change.Property == NeedleWidthProperty ||
            change.Property == RingWidthProperty)
        {
            _needle = null;
        }
        if (change.Property == RingWidthProperty)
        {
            // 环宽同时决定内圈半径和三角标记高度；针长的安全上限也随内圈改变。
            _marker = null;
        }
        if (change.Property == MediumFontWeightProperty || change.Property == RegularFontWeightProperty)
        {
            ClearText();
        }
        else if (change.Property == DirectionProperty || change.Property == HasDataProperty ||
                 change.Property == IsCalmProperty || change.Property == CalmLabelProperty ||
                 change.Property == NoDataLabelProperty)
        {
            RefreshReading();
        }
        // 位置、尺寸和动效策略不改变文字内容或字体，无需重建静态文字布局。
        InvalidateVisual();
        _peer?.Announce();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 语言管理器比控件活得更久：只在挂载期间订阅，并在卸载时对称解除。
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
        // 停止当前视觉过渡并重置有效方向历史；重新挂载时直接显示最新输入。
        // MotionValue 属于控件自身，保留对象以支持重挂载，不在这里永久销毁。
        _angle.Jump(NormalizedDirection);
        _hadDirection = false;
        ClearText();
        base.OnDetachedFromVisualTree(e);
    }

    private void Sync()
    {
        // 模板 Transition 无法表达角度展开和从当前视觉位置求最短路径，因此使用 MotionValue。
        // 仅连续有效采样且 Full 动效时播放弹簧；首次、恢复、Reduced/Off 都直接跳到实际值。
        if (HasDirection && _hadDirection && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _angle.AnimateTo(NearestAngle(_angle.Value, Direction), MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _angle.Jump(NormalizedDirection);
        }
        _hadDirection = HasDirection;
    }

    private void OnPaletteChanged()
    {
        // 主题可能同时改变颜色、字号和字体，所有文字布局都必须重新创建。
        ClearText();
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
        // ChartsBRun 持有文字布局资源，替换或清空前需要释放，不能仅清空 List。
        foreach (var run in _text)
        {
            run.Dispose();
        }
        _text.Clear();
    }

    private ChartsBRun CreateReadingRun()
        => ChartsBText.TrimmedRun(ReadingText, _palette, _palette.TypeSM,
            HasDirection ? _palette.Foreground : _palette.TextMuted, HasDirection ? 300 : 140, RegularFontWeight);

    private void RefreshReading()
    {
        // 尚未绘制时没有缓存，留给 EnsureText 一次性创建；已有缓存只替换动态读数。
        if (_text.Count == 0)
        {
            return;
        }
        _text[0].Dispose();
        _text[0] = CreateReadingRun();
    }

    private Size FitDial(Size available)
    {
        // 取宽高约束中较小的比例，保证圆盘不会变成椭圆。
        // Measure 和 Arrange 共用此函数；无穷可用尺寸仍受 Token 最大显示尺寸约束。
        var scale = Math.Max(0, Math.Min(Math.Min(available.Width, Math.Max(1, DialWidth)) / DesignWidth,
            Math.Min(available.Height, Math.Max(1, DialHeight)) / ReferenceHeight));
        return new Size(DesignWidth * scale, ReferenceHeight * scale);
    }

    protected override Size MeasureOverride(Size availableSize) => FitDial(availableSize);

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = FitDial(finalSize);
        // 父容器给的区域可能更大，将实际绘图区域居中，Render 使用同一布局结果。
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
        // 先缩放参考坐标，再平移到布局位置；内部所有半径、字体和针宽一起缩放。
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(_dial.X, _dial.Y)))
        {
            // 从底到顶：圆盘与双圈 → 刻度和标签 → 指针或状态 → 可选底部读数。
            var outline = _palette.Pen(_palette.BorderStrong, Math.Max(0, OutlineThickness));
            context.DrawEllipse(_palette.Brush(_palette.SurfaceRaised), outline, Center, OuterRadius, OuterRadius);
            context.DrawEllipse(null, outline, Center, InnerRadius, InnerRadius);
            EnsureMarker();
            for (var i = 0; i < 16; i++)
            {
                var radians = i * Math.PI / 8;
                var major = i % 4 == 0;
                // 每四个刻度是一个主方向，用三角形替代刻度线；其余刻度连接内外圆环。
                if (major)
                {
                    using (context.PushTransform(Matrix.CreateRotation(radians) * Matrix.CreateTranslation(Center.X, Center.Y)))
                    {
                        context.DrawGeometry(_palette.Brush(_palette.Foreground), null, _marker!);
                    }
                }
                else
                {
                    context.DrawLine(_palette.Pen(_palette.TextMuted, 1),
                        Polar(radians, InnerRadius), Polar(radians, OuterRadius));
                }
                DrawCentered(context, _text[1 + i], Polar(radians, 136));
                if (i % 2 == 0)
                {
                    DrawCentered(context, _text[17 + i / 2], Polar(radians, InnerRadius - 15));
                }
            }
            if (HasDirection)
            {
                EnsureNeedle();
                // 针在局部坐标中指向负 Y（北）；先绕局部原点旋转，再平移至圆心。
                // 箭头尖端指向风吹来的方向，而不是空气继续流向的下风方向。
                using (context.PushTransform(Matrix.CreateRotation(Normalize(_angle.Value) * Math.PI / 180) *
                    Matrix.CreateTranslation(Center.X, Center.Y)))
                {
                    context.DrawGeometry(_palette.Brush(_palette.Accent), null, _needle!);
                }
            }
            else
            {
                // 静风和缺测保留罗盘背景，在圆心用明确文字替代方向箭头。
                DrawCentered(context, _text[0], Center);
            }
            if (ShowReading && HasDirection)
            {
                DrawCentered(context, _text[0], new Point(160, 336));
            }
        }
    }

    // 屏幕 Y 轴向下；sin 放在 X、负 cos 放在 Y，使 0° 在北、90° 在东，顺时针递增。
    private static Point Polar(double radians, double radius)
        => new(Center.X + Math.Sin(radians) * radius, Center.Y - Math.Cos(radians) * radius);

    private void EnsureNeedle()
    {
        if (_needle is not null)
        {
            return;
        }
        var length = Math.Clamp(NeedleLength, 40, InnerRadius - 30);
        var halfWidth = Math.Clamp(NeedleWidth, 24, 56) / 2;
        // 四个顶点：尖端 → 右尾 → 向内凹的尾心 → 左尾，闭合成实心凹尾箭头。
        // 针长限制在内圈以内，旋转后仍避开内侧方位文字；缓存只在相关几何变化时失效。
        _needle = new StreamGeometry();
        using var path = _needle.Open();
        path.BeginFigure(new Point(0, -length), true);
        path.LineTo(new Point(halfWidth, 40));
        path.LineTo(new Point(0, 23));
        path.LineTo(new Point(-halfWidth, 40));
        path.EndFigure(true);
    }

    private void EnsureMarker()
    {
        if (_marker is not null)
        {
            return;
        }
        _marker = new StreamGeometry();
        // 主方向标记也是北向局部几何，Render 复用它旋转到东、南、西。
        using var path = _marker.Open();
        path.BeginFigure(new Point(0, -OuterRadius), true);
        path.LineTo(new Point(7, -InnerRadius));
        path.LineTo(new Point(-7, -InnerRadius));
        path.EndFigure(true);
    }

    private void EnsureText()
    {
        if (_text.Count > 0)
        {
            return;
        }
        _text.Add(CreateReadingRun());
        // 外圈十六角度必须完整显示，因此使用 Run，不使用带省略号的 TrimmedRun。
        for (var i = 0; i < 16; i++)
        {
            var major = i % 4 == 0;
            _text.Add(ChartsBText.Run($"{(i * 22.5).ToString("0.#", CultureInfo.CurrentCulture)}°",
                _palette, major ? _palette.TypeSM : _palette.TypeXS, _palette.TextSecondary,
                major ? MediumFontWeight : RegularFontWeight));
        }
        for (var i = 0; i < 8; i++)
        {
            // 内圈仅八方位；主方向字体更大。方位和用户状态文案有长度限制，允许省略。
            var major = i % 2 == 0;
            _text.Add(ChartsBText.TrimmedRun(DirectionName(i * 45),
                _palette, major ? _palette.TypeBase * 1.25 : _palette.TypeSM, _palette.Foreground, 48,
                major ? MediumFontWeight : RegularFontWeight));
        }
    }

    // 文字绘制位置是左上角；减去半个布局宽高，使文字中心落在目标点。
    private static void DrawCentered(DrawingContext context, ChartsBRun run, Point center)
        => run.Draw(context, new Point(center.X - run.Width / 2, center.Y - run.Height / 2));

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new WindDirectionAutomationPeer(this);

    private sealed class WindDirectionAutomationPeer(WindDirectionIndicator owner) : ControlAutomationPeer(owner), IValueProvider
    {
        // 自绘文字不是独立子控件，通过只读 Value Provider 对外提供实际读数。
        // 不读取动画角度，确保辅助技术和业务数据一致。
        private string? _lastHelp;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string? GetNameCore()
        {
            // 应用显式指定的无障碍名称优先，其次使用 Label 或内置本地化标题。
            var name = AutomationProperties.GetName(owner);
            return string.IsNullOrEmpty(name) ? owner.HeadingText : name;
        }
        protected override string? GetHelpTextCore() => owner.ReadingText;
        protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];
        public bool IsReadOnly => true;
        public string Value => GetHelpTextCore() ?? string.Empty;
        public void SetValue(string? value) { }

        public void Announce()
        {
            // 只在实际播报文字变化时通知，避免布局/动效属性变化重复播报相同读数。
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
