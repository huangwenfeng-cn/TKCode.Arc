using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.Blocks.Showpieces;

namespace TKCode.Arc.Tests.Charts;

public class LinearGaugeTests
{
    // 测试不只查看属性，还记录真实绘制指令，核对满量程时柱条确实留在圆角轨道内。
    [AvaloniaTheory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(1, .01)]
    [InlineData(99, .99)]
    [InlineData(100, 1)]
    [InlineData(125, 1)]
    public void Bar_Clamps_At_The_Rounded_Track_While_The_Actual_Reading_Is_Preserved(double value, double fraction)
    {
        var meter = new LinearGauge { Value = value, LowThreshold = -20, HighThreshold = 200 };
        var window = TestHelpers.Show(meter);
        try
        {
            meter.DrawnFraction.ShouldBe(fraction, 1e-9);
            meter.Value.ShouldBe(value);
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                meter.Render(context);
            }
            var filled = Groups(drawing).Single(group => group.ClipGeometry != null);
            var bar = filled.Children.OfType<GeometryDrawing>().Single().Geometry!.Bounds;
            filled.ClipGeometry!.Bounds.Top.ShouldBe(bar.Bottom - bar.Height * fraction, 1e-9);
            filled.ClipGeometry.Bounds.Bottom.ShouldBe(bar.Bottom, 1e-9);
            // 阈值移到量程外，剩余描边只能是轨道和刻度，不能有同色顶部横线。
            Groups(drawing).SelectMany(group => group.Children.OfType<GeometryDrawing>())
                .Where(shape => shape.Pen?.Brush is ISolidColorBrush brush && brush.Color == meter.StateColor).ShouldBeEmpty();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Thresholds_And_Text_Update_Before_The_Spring_And_Readonly_Automation_Reports_Quality()
    {
        var meter = new LinearGauge { Label = "Force", Unit = "N", Maximum = 1000, LowThreshold = 200, HighThreshold = 800, Value = 450, Decimals = 0 };
        var window = TestHelpers.Show(meter, ArcMotionLevel.Full);
        try
        {
            meter.State.ShouldBe(LinearGaugeState.Normal);
            var notified = false;
            meter.PropertyChanged += (_, change) => notified |= change.Property == LinearGauge.StateProperty;
            meter.Value = 1000;
            meter.State.ShouldBe(LinearGaugeState.High);
            notified.ShouldBeTrue();
            meter.ReadingText.ShouldBe("1000 N");
            meter.DrawnFraction.ShouldBeLessThan(1);
            var peer = ControlAutomationPeer.CreatePeerForElement(meter);
            peer.GetName().ShouldBe("Force");
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.IsReadOnly.ShouldBeTrue();
            range.SetValue(0);
            meter.Value.ShouldBe(1000);
            foreach (var quality in new[] { MeasurementQuality.NoData, MeasurementQuality.Stale, MeasurementQuality.Error })
            {
                meter.Quality = quality;
                meter.ReadingText.ShouldBe("—");
                meter.State.ShouldBe(LinearGaugeState.High);
                peer.GetProvider<IValueProvider>()!.Value.ShouldBe(meter.StatusText);
                var drawing = new DrawingGroup();
                using (var context = drawing.Open()) { meter.Render(context); }
                Groups(drawing).Where(group => group.ClipGeometry != null).ShouldBeEmpty();
                meter.Value = 200;
                meter.Quality = MeasurementQuality.Good;
                meter.DrawnFraction.ShouldBe(.2, 1e-9);
                meter.State.ShouldBe(LinearGaugeState.Normal);
                meter.Value = 1000;
            }
            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            meter.Value = 199;
            meter.State.ShouldBe(LinearGaugeState.Low);
            meter.DrawnFraction.ShouldBe(.199, 1e-9);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Theme_Weights_Language_And_Detached_Updates_Invalidate_Cached_Text()
    {
        var meter = new LinearGauge { Label = "Force", Value = 45 };
        var host = new Border { Child = meter };
        var window = TestHelpers.Show(host);
        var manager = ArcThemeManager.Current!;
        var previous = manager.State;
        try
        {
            ShowpieceTextRecording.Record(meter.Render).ShouldNotBeEmpty();
            ArcTheme.Apply(theme => theme.Token("WeightMedium", "Bold").Token("WeightRegular", "Normal")
                .Token("LinearGauge", "DialWidth", "440").Token("LinearGauge", "DialHeight", "720")
                .Appearance(ArcAppearance.Dark).Accent(ArcAccentPreset.Rose));
            Dispatcher.UIThread.RunJobs();
            meter.Measure(new Size(900, 900));
            meter.DesiredSize.ShouldBe(new Size(440, 720));
            meter.Arrange(new Rect(meter.DesiredSize));
            var drawn = ShowpieceTextRecording.Record(meter.Render);
            drawn.ShouldAllBe(run => Math.Abs(run.Transform.M11 - 2) < 1e-9);
            drawn.Where(run => run.Run.FontRenderingEmSize >= 14).ShouldAllBe(run => run.Run.GlyphTypeface.Weight == FontWeight.Bold);
            ArcLanguage.Set(LanguageTags.ZhCN);
            meter.Value = 90;
            meter.StatusText.ShouldBe("偏高");
            host.Child = null;
            meter.Quality = MeasurementQuality.Stale;
            ArcLanguage.Set(LanguageTags.ZhTW);
            host.Child = meter;
            Dispatcher.UIThread.RunJobs();
            meter.StatusText.ShouldBe("資料過期");
            meter.StaleLabel = "等待新样本";
            ArcLanguage.Set(LanguageTags.EnUS);
            meter.StatusText.ShouldBe("等待新样本");
            meter.Value = 65;
            meter.Quality = MeasurementQuality.Good;
            meter.DrawnFraction.ShouldBe(.65, 1e-9);
            meter.Maximum = meter.Minimum;
            meter.State.ShouldBe(LinearGaugeState.InvalidRange);
            meter.DrawnFraction.ShouldBe(0);
        }
        finally { manager.Apply(previous); window.Close(); }
    }

    [Fact]
    public void Shared_Mapping_Preserves_Tiny_Ranges_And_Handles_Extreme_Doubles()
    {
        GaugeScale.Fraction(0, -double.MaxValue, double.MaxValue).ShouldBe(.5, 1e-9);
        GaugeScale.Fraction(double.MaxValue, -double.MaxValue, double.MaxValue).ShouldBe(1);
        GaugeScale.Fraction(double.Epsilon, 0, double.Epsilon * 2).ShouldBe(.5);
        var meter = new LinearGauge();
        Should.Throw<ArgumentException>(() => meter.Value = double.NaN);
        Should.Throw<ArgumentException>(() => meter.Maximum = double.PositiveInfinity);
        Should.Throw<ArgumentException>(() => meter.Decimals = 7);
        Should.Throw<ArgumentException>(() => meter.TickCount = 0);
        Should.Throw<ArgumentException>(() => meter.Quality = (MeasurementQuality)99);
    }
    private static IEnumerable<DrawingGroup> Groups(DrawingGroup drawing)
    {
        yield return drawing;
        foreach (var child in drawing.Children.OfType<DrawingGroup>())
        {
            foreach (var group in Groups(child)) { yield return group; }
        }
    }
}
