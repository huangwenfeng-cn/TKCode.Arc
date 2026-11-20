using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Localization;
using TKCode.Arc.Tests.Blocks.Showpieces;

namespace TKCode.Arc.Tests.Charts;

public class ThermometerTests
{
    // 测试阅读提示：先准备控件，再执行属性变化，最后用 ShouldBe 等断言核对结果。
    // AvaloniaTheory 配合 InlineData，让同一流程分别验证三种无效质量；
    // TestHelpers.Show 创建 headless 测试窗口，finally 保证失败时也能关闭窗口。
    [AvaloniaTheory]
    [InlineData(MeasurementQuality.NoData, "No data")]
    [InlineData(MeasurementQuality.Stale, "Stale data")]
    [InlineData(MeasurementQuality.Error, "Collection error")]
    public void Quality_Hides_The_Measurement_Preserves_Threshold_State_And_Recovers_Immediately(
        MeasurementQuality quality, string expected)
    {
        ArcLanguage.Set(LanguageTags.EnUS);
        var meter = new Thermometer { Value = 86 };
        var window = TestHelpers.Show(meter, ArcMotionLevel.Full);
        try
        {
            ShowpieceTextRecording.Record(meter.Render).ShouldNotBeEmpty();
            var peer = ControlAutomationPeer.CreatePeerForElement(meter);
            meter.Value = 100;
            meter.Quality = quality;
            // 保存数值和阈值区间仍存在，但当前显示与辅助接口应只报告质量。
            meter.Value.ShouldBe(100);
            meter.State.ShouldBe(ThermometerState.High);
            meter.HasReading.ShouldBeFalse();
            meter.ReadingText.ShouldBe("—");
            meter.StatusText.ShouldBe(expected);
            peer.GetHelpText().ShouldBe(expected);
            peer.GetProvider<IValueProvider>()!.Value.ShouldBe(expected);
            // 数值范围接口继续保留原有保存值契约。
            peer.GetProvider<IRangeValueProvider>()!.Value.ShouldBe(100);
            var drawing = new DrawingGroup();
            // 记录实际绘制指令：没有液体裁剪组，才能确认旧液柱确实没有画出来。
            using (var context = drawing.Open())
            {
                meter.Render(context);
            }
            DrawingGroups(drawing).ShouldNotContain(group => group.ClipGeometry != null);
            meter.Value = 40;
            meter.Quality = MeasurementQuality.Good;
            // 恢复首个有效样本应直接到 40 度对应的半量程位置，不能等待旧液位动画。
            meter.HasReading.ShouldBeTrue();
            meter.DrawnFraction.ShouldBe(.5, 1e-9);
            meter.State.ShouldBe(ThermometerState.Normal);
            peer.GetHelpText().ShouldContain("40");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Quality_Text_Follows_Language_Custom_Labels_And_Reattachment()
    {
        var meter = new Thermometer { Quality = MeasurementQuality.Stale };
        var host = new Border { Child = meter };
        var window = TestHelpers.Show(host);
        try
        {
            ArcLanguage.Set(LanguageTags.ZhCN);
            meter.StatusText.ShouldBe("数据过期");
            ShowpieceTextRecording.Record(meter.Render).ShouldNotBeEmpty();
            ArcLanguage.Set(LanguageTags.ZhTW);
            meter.StatusText.ShouldBe("資料過期");
            meter.StaleLabel = "Sample expired";
            ArcLanguage.Set(LanguageTags.EnUS);
            meter.StatusText.ShouldBe("Sample expired");
            meter.Maximum = meter.Minimum;
            meter.State.ShouldBe(ThermometerState.InvalidRange);
            meter.StatusText.ShouldBe("Sample expired");
            host.Child = null;
            meter.Quality = MeasurementQuality.Error;
            ArcLanguage.Set(LanguageTags.ZhCN);
            host.Child = meter;
            Dispatcher.UIThread.RunJobs();
            meter.StatusText.ShouldBe("采集异常");
            meter.ErrorLabel = "Sensor failed";
            meter.StatusText.ShouldBe("Sensor failed");
            meter.Quality = MeasurementQuality.NoData;
            meter.NoDataLabel = "Waiting";
            meter.StatusText.ShouldBe("Waiting");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(100)]
    [InlineData(125)]
    public void Maximum_And_Over_Range_Readings_Fill_The_Rounded_Cap(double value)
    {
        var meter = new Thermometer { Value = value };
        var window = TestHelpers.Show(meter);
        try
        {
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                meter.Render(context);
            }
            var filled = DrawingGroups(drawing).Single(group => group.ClipGeometry is not null &&
                group.Children.OfType<GeometryDrawing>().Any(shape => shape.Pen is null &&
                    shape.Geometry is { } geometry && geometry.Bounds.Height > 200));
            var liquid = filled.Children.OfType<GeometryDrawing>().Single().Geometry!.Bounds;
            var clip = filled.ClipGeometry!.Bounds;
            clip.Top.ShouldBeLessThanOrEqualTo(liquid.Top);
            clip.Bottom.ShouldBeGreaterThanOrEqualTo(liquid.Bottom);
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<DrawingGroup> DrawingGroups(DrawingGroup drawing)
    {
        yield return drawing;
        foreach (var child in drawing.Children.OfType<DrawingGroup>())
        {
            foreach (var group in DrawingGroups(child))
            {
                yield return group;
            }
        }
    }

    [AvaloniaFact]
    public void Drawn_Text_Follows_Theme_Weights_After_It_Has_Been_Cached()
    {
        var meter = new Thermometer { Label = "Temperature", Value = 36.5 };
        var window = TestHelpers.Show(meter);
        var manager = ArcThemeManager.Current!;
        var previous = manager.State;
        try
        {
            var initial = ShowpieceTextRecording.Record(meter.Render);
            initial.ShouldNotBeEmpty();
            // The headless font exposes regular and bold faces; other requested weights can be substituted.
            foreach (var (mediumWeight, regularWeight) in new[]
                     {
                         (FontWeight.Bold, FontWeight.Normal),
                         (FontWeight.Normal, FontWeight.Bold)
                     })
            {
                ArcTheme.Apply(theme => theme.Token("WeightMedium", mediumWeight.ToString())
                    .Token("WeightRegular", regularWeight.ToString()));
                Dispatcher.UIThread.RunJobs();
                var drawn = ShowpieceTextRecording.Record(meter.Render);
                var medium = drawn.Where(run => run.Run.FontRenderingEmSize >= 14).ToList();
                var regular = drawn.Where(run => run.Run.FontRenderingEmSize < 14).ToList();
                medium.ShouldNotBeEmpty();
                regular.ShouldNotBeEmpty();
                medium.ShouldAllBe(run => run.Run.GlyphTypeface.Weight == mediumWeight);
                regular.ShouldAllBe(run => run.Run.GlyphTypeface.Weight == regularWeight);
            }
        }
        finally
        {
            manager.Apply(previous);
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(110, 180, 900, 900, 110, 180, .5)]
    [InlineData(440, 720, 900, 900, 440, 720, 2)]
    [InlineData(440, 360, 900, 900, 220, 360, 1)]
    [InlineData(220, 720, 900, 900, 220, 360, 1)]
    [InlineData(440, 720, 110, 180, 110, 180, .5)]
    public void Size_Token_Overrides_Keep_Measurement_And_Drawing_In_Step(
        double width, double height, double availableWidth, double availableHeight,
        double expectedWidth, double expectedHeight, double expectedScale)
    {
        var meter = new Thermometer { Label = "Temperature", Value = 36.5 };
        var window = TestHelpers.Show(meter);
        var manager = ArcThemeManager.Current!;
        var previous = manager.State;
        try
        {
            ArcTheme.Apply(theme => theme
                .Token("Thermometer", "DialWidth", width.ToString(CultureInfo.InvariantCulture))
                .Token("Thermometer", "DialHeight", height.ToString(CultureInfo.InvariantCulture)));
            Dispatcher.UIThread.RunJobs();
            meter.Measure(new Size(availableWidth, availableHeight));
            meter.DesiredSize.Width.ShouldBe(expectedWidth, 1e-9);
            meter.DesiredSize.Height.ShouldBe(expectedHeight, 1e-9);
            meter.Arrange(new Rect(meter.DesiredSize));
            var drawn = ShowpieceTextRecording.Record(meter.Render);
            drawn.ShouldNotBeEmpty();
            drawn.ShouldAllBe(run => Math.Abs(run.Transform.M11 - expectedScale) < 1e-9 &&
                                    Math.Abs(run.Transform.M22 - expectedScale) < 1e-9);
        }
        finally
        {
            manager.Apply(previous);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thresholds_Use_The_Actual_Reading_And_Publish_State_Changes()
    {
        var meter = new Thermometer { Value = 36.5, NormalLabel = "Ready", HighLabel = "Too hot" };
        var window = TestHelpers.Show(meter);
        try
        {
            meter.State.ShouldBe(ThermometerState.Normal);
            meter.StatusText.ShouldBe("Ready");
            var notified = false;
            meter.PropertyChanged += (_, e) => notified |= e.Property == Thermometer.StateProperty;
            meter.Value = 70;
            meter.State.ShouldBe(ThermometerState.High);
            meter.StatusText.ShouldBe("Too hot");
            notified.ShouldBeTrue();
            meter.Value = 10;
            meter.State.ShouldBe(ThermometerState.Normal);
            meter.Value = 9.9;
            meter.State.ShouldBe(ThermometerState.Low);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clamps_The_Liquid_But_Keeps_The_Actual_Out_Of_Range_Reading()
    {
        var meter = new Thermometer { Value = 40 };
        var window = TestHelpers.Show(meter);
        try
        {
            meter.DrawnFraction.ShouldBe(.5, 1e-9);
            meter.Value = 125;
            meter.DrawnFraction.ShouldBe(1);
            meter.ReadingText.ShouldContain("125");
            meter.Value = -50;
            meter.DrawnFraction.ShouldBe(0);
            meter.ReadingText.ShouldContain("-50");
            meter.Maximum = meter.Minimum;
            meter.State.ShouldBe(ThermometerState.InvalidRange);
            meter.DrawnFraction.ShouldBe(0);
            meter.Maximum = 100;
            meter.HighThreshold = meter.LowThreshold - 1;
            meter.State.ShouldBe(ThermometerState.InvalidRange);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Reduced_And_Off_Motion_Land_On_The_Target(ArcMotionLevel level)
    {
        var meter = new Thermometer { Value = -20 };
        var window = TestHelpers.Show(meter, level);
        try
        {
            meter.Value = 100;
            meter.DrawnFraction.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reads_As_A_Read_Only_Temperature_Meter()
    {
        var meter = new Thermometer { Label = "Boiler", Value = 80, HighLabel = "Hot" };
        var window = TestHelpers.Show(meter);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(meter);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.ProgressBar);
            peer.GetName().ShouldBe("Boiler");
            peer.GetHelpText().ShouldContain("80");
            peer.GetHelpText().ShouldContain("Hot");
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.IsReadOnly.ShouldBeTrue();
            range.Minimum.ShouldBe(-20);
            range.Maximum.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rejects_Non_Finite_Readings_And_Invalid_Tick_Counts()
    {
        var meter = new Thermometer();
        Should.Throw<ArgumentException>(() => meter.Value = double.NaN);
        Should.Throw<ArgumentException>(() => meter.Minimum = double.NegativeInfinity);
        Should.Throw<ArgumentException>(() => meter.Maximum = double.PositiveInfinity);
        Should.Throw<ArgumentException>(() => meter.TickCount = 0);
        Should.Throw<ArgumentException>(() => meter.Quality = (MeasurementQuality)99);
    }

    [AvaloniaFact]
    public void Theme_And_Language_Changes_Reach_The_Meter()
    {
        var meter = new Thermometer { Value = 40, NormalTone = GaugeTone.Accent };
        var window = TestHelpers.Show(meter);
        try
        {
            ArcTheme.Apply(theme => theme.Appearance(ArcAppearance.Light).Accent(ArcAccentPreset.Blue));
            Dispatcher.UIThread.RunJobs();
            var blue = meter.StateColor;
            ArcTheme.Apply(theme => theme.Appearance(ArcAppearance.Dark).Accent(ArcAccentPreset.Rose));
            Dispatcher.UIThread.RunJobs();
            meter.StateColor.ShouldNotBe(blue);
            ArcLanguage.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            meter.StatusText.ShouldBe("正常");
            meter.Value = 80;
            meter.StatusText.ShouldBe("高温");
            meter.HighLabel = "锅炉过热";
            ArcLanguage.Set(LanguageTags.EnUS);
            Dispatcher.UIThread.RunJobs();
            meter.StatusText.ShouldBe("锅炉过热");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alarm_State_Does_Not_Wait_For_The_Full_Motion_Spring()
    {
        var meter = new Thermometer { Value = -20 };
        var window = TestHelpers.Show(meter, ArcMotionLevel.Full);
        try
        {
            meter.Value = 100;
            meter.State.ShouldBe(ThermometerState.High);
            meter.ReadingText.ShouldContain("100");
            meter.DrawnFraction.ShouldBeLessThan(1);
            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            meter.DrawnFraction.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reattached_Meter_Shows_Changes_Made_While_Detached()
    {
        var meter = new Thermometer { Value = 20 };
        var host = new Border { Child = meter };
        var window = TestHelpers.Show(host);
        try
        {
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            meter.Value = 100;
            host.Child = meter;
            Dispatcher.UIThread.RunJobs();
            meter.State.ShouldBe(ThermometerState.High);
            meter.DrawnFraction.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
