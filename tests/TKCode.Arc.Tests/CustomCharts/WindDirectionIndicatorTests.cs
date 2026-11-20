using System.Globalization;
using Avalonia;
using Avalonia.Automation;
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

public class WindDirectionIndicatorTests
{
    // AvaloniaTheory 使用多组 InlineData 检查相同规则；ShouldBe/ShouldContain 是断言，
    // 条件不符合就让测试失败。这里先保存方向，再模拟质量和静风变化，检查优先级与恢复。
    [AvaloniaTheory]
    [InlineData(MeasurementQuality.NoData, "No data")]
    [InlineData(MeasurementQuality.Stale, "Stale data")]
    [InlineData(MeasurementQuality.Error, "Collection error")]
    public void Quality_Overrides_Calm_Hides_Arrow_And_Recovers_Without_Old_Bearing_Motion(
        MeasurementQuality quality, string expected)
    {
        ArcLanguage.Set(LanguageTags.EnUS);
        var meter = new WindDirectionIndicator { Direction = 45, ShowReading = false };
        var window = TestHelpers.Show(meter, ArcMotionLevel.Full);
        try
        {
            ShowpieceTextRecording.Record(meter.Render).ShouldNotBeEmpty();
            meter.IsCalm = true;
            meter.Quality = quality;
            meter.HasDirection.ShouldBeFalse();
            meter.Direction.ShouldBe(45);
            meter.ReadingText.ShouldBe(expected);
            ControlAutomationPeer.CreatePeerForElement(meter).GetProvider<IValueProvider>()!.Value.ShouldBe(expected);
            string DrawnText() => string.Concat(ShowpieceTextRecording.Record(meter.Render)
                .Select(run => run.Run.Characters.ToString()));
            // 读取真正绘制的字形文字，忽略空格后比较内容，兼容内置提示的换行排版；
            // 还要确保没有省略号，不能只检查属性值完整却让画面提示被截断。
            DrawnText().Replace(" ", string.Empty).ShouldContain(expected.Replace(" ", string.Empty));
            DrawnText().ShouldNotContain("…");
            meter.HasData = false;
            // 原 HasData=false 必须仍优先显示无数据；重新打开后才回到原质量提示。
            meter.ReadingText.ShouldBe("No data");
            meter.HasData = true;
            meter.ReadingText.ShouldBe(expected);
            meter.Direction = 270;
            meter.IsCalm = false;
            meter.Quality = MeasurementQuality.Good;
            meter.DrawnAngle.ShouldBe(270);
            meter.HasDirection.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Quality_Labels_Refresh_Language_And_Detached_State()
    {
        var meter = new WindDirectionIndicator { Quality = MeasurementQuality.Stale };
        var host = new Border { Child = meter };
        var window = TestHelpers.Show(host);
        try
        {
            ArcLanguage.Set(LanguageTags.ZhCN);
            meter.ReadingText.ShouldBe("数据过期");
            ShowpieceTextRecording.Record(meter.Render).ShouldNotBeEmpty();
            ArcLanguage.Set(LanguageTags.ZhTW);
            meter.ReadingText.ShouldBe("資料過期");
            meter.StaleLabel = "Sample expired";
            ArcLanguage.Set(LanguageTags.EnUS);
            meter.ReadingText.ShouldBe("Sample expired");
            host.Child = null;
            meter.Quality = MeasurementQuality.Error;
            ArcLanguage.Set(LanguageTags.ZhCN);
            host.Child = meter;
            Dispatcher.UIThread.RunJobs();
            meter.ReadingText.ShouldBe("采集异常");
            meter.ErrorLabel = "Sensor failed";
            meter.ReadingText.ShouldBe("Sensor failed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(0, 0, "N")]
    [InlineData(360, 0, "N")]
    [InlineData(-90, 270, "W")]
    [InlineData(405, 45, "NE")]
    [InlineData(22.5, 22.5, "NNE")]
    public void Bearings_Wrap_And_Name_The_Wind_Origin(double direction, double expected, string name)
    {
        ArcLanguage.Set(LanguageTags.EnUS);
        var meter = new WindDirectionIndicator { Direction = direction };
        meter.NormalizedDirection.ShouldBe(expected);
        meter.ReadingText.ShouldEndWith($"· {name}");
    }

    [AvaloniaTheory]
    [InlineData(359, 1, 361)]
    [InlineData(1, 359, -1)]
    [InlineData(720, -90, 630)]
    [InlineData(-721, 1, -719)]
    public void Unwrapped_Target_Takes_The_Shortest_Path(double current, double target, double expected)
        => WindDirectionIndicator.NearestAngle(current, target).ShouldBe(expected, 1e-9);

    [AvaloniaFact]
    public void Full_Motion_Crosses_North_And_Redirects_From_The_Current_Position()
    {
        // 手动推进帧时钟，不靠真实等待；同时验证文字立即更新而箭头仍在运动。
        var clock = new ManualFrameClock();
        using var installation = clock.Install();
        var meter = new WindDirectionIndicator { Direction = 359 };
        var window = TestHelpers.Show(meter, ArcMotionLevel.Full);
        try
        {
            meter.Direction = 1;
            meter.ReadingText.ShouldStartWith("1°");
            clock.Advance(TimeSpan.FromMilliseconds(50));
            meter.DrawnAngle.ShouldBeGreaterThan(359);
            meter.DrawnAngle.ShouldBeLessThan(362);
            meter.Direction = 358;
            clock.Advance(TimeSpan.FromSeconds(2));
            meter.DrawnAngle.ShouldBe(358, .05);
            meter.Direction = 1;
            clock.Advance(TimeSpan.FromSeconds(2));
            meter.DrawnAngle.ShouldBe(361, .05);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Reduced_And_Off_Motion_Show_The_Target_Immediately(ArcMotionLevel level)
    {
        var meter = new WindDirectionIndicator { Direction = 359 };
        var window = TestHelpers.Show(meter, level);
        try
        {
            meter.Direction = 1;
            meter.DrawnAngle.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Missing_Data_Overrides_Calm_And_Restored_Data_Does_Not_Animate_An_Old_Bearing()
    {
        var meter = new WindDirectionIndicator { Direction = 45, CalmLabel = "Calm", NoDataLabel = "Unavailable" };
        var window = TestHelpers.Show(meter, ArcMotionLevel.Full);
        try
        {
            meter.HasDirection.ShouldBeTrue();
            meter.IsCalm = true;
            meter.HasDirection.ShouldBeFalse();
            meter.ReadingText.ShouldBe("Calm");
            meter.HasData = false;
            meter.ReadingText.ShouldBe("Unavailable");
            meter.Direction = 270;
            meter.IsCalm = false;
            meter.HasData = true;
            meter.HasDirection.ShouldBeTrue();
            meter.DrawnAngle.ShouldBe(270);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Live_Motion_Policy_And_Reattachment_Converge_To_The_Actual_Reading()
    {
        var meter = new WindDirectionIndicator { Direction = 0 };
        var host = new Border { Child = meter };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            meter.Direction = 180;
            meter.DrawnAngle.ShouldBeLessThan(180);
            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            meter.DrawnAngle.ShouldBe(180);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            meter.Direction = 90;
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            host.Child = meter;
            Dispatcher.UIThread.RunJobs();
            meter.DrawnAngle.ShouldBe(90);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Reads_Actual_Text_And_Respects_The_Applications_Name()
    {
        ArcLanguage.Set(LanguageTags.EnUS);
        var meter = new WindDirectionIndicator { Direction = 90 };
        var window = TestHelpers.Show(meter);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(meter);
            peer.GetName().ShouldBe("Wind direction");
            var provider = peer.GetProvider<IValueProvider>()!;
            provider.IsReadOnly.ShouldBeTrue();
            provider.Value.ShouldNotBeNull().ShouldContain("90° · E");
            AutomationProperties.SetName(meter, "Roof wind");
            peer.GetName().ShouldBe("Roof wind");
            meter.HasData = false;
            provider.Value.ShouldBe("No data");
            provider.SetValue("10");
            meter.Direction.ShouldBe(90);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Validation_Rejects_Invalid_Samples_And_Accepts_Extreme_Finite_Bearings()
    {
        var meter = new WindDirectionIndicator();
        Should.Throw<ArgumentException>(() => meter.Direction = double.NaN);
        Should.Throw<ArgumentException>(() => meter.Direction = double.PositiveInfinity);
        Should.Throw<ArgumentException>(() => meter.Quality = (MeasurementQuality)99);
        meter.Direction = double.MaxValue;
        double.IsFinite(meter.NormalizedDirection).ShouldBeTrue();
        meter.NormalizedDirection.ShouldBeInRange(0, 359.999);
    }

    [AvaloniaFact]
    public void Theme_And_Language_Changes_Refresh_Cached_Drawing_And_Text()
    {
        var meter = new WindDirectionIndicator { Direction = 45 };
        var window = TestHelpers.Show(meter);
        var manager = ArcThemeManager.Current!;
        var previous = manager.State;
        try
        {
            ArcTheme.Apply(theme => theme.Appearance(ArcAppearance.Light).Accent(ArcAccentPreset.Blue));
            Dispatcher.UIThread.RunJobs();
            ShowpieceTextRecording.Record(meter.Render).ShouldNotBeEmpty();
            var blue = meter.NeedleColor;
            // 无头测试字体提供常规与粗体字形，用交叉覆盖检查主题切换后缓存是否刷新。
            foreach (var (mediumWeight, regularWeight) in new[]
                     {
                         (FontWeight.Bold, FontWeight.Normal),
                         (FontWeight.Normal, FontWeight.Bold)
                     })
            {
                ArcTheme.Apply(theme => theme.Appearance(ArcAppearance.Dark).Accent(ArcAccentPreset.Rose)
                    .Token("WeightMedium", mediumWeight.ToString()).Token("WeightRegular", regularWeight.ToString()));
                Dispatcher.UIThread.RunJobs();
                meter.NeedleColor.ShouldNotBe(blue);
                var drawn = ShowpieceTextRecording.Record(meter.Render);
                var medium = drawn.Where(run => run.Run.FontRenderingEmSize > 16).ToList();
                var regular = drawn.Where(run => run.Run.FontRenderingEmSize < 14).ToList();
                medium.ShouldNotBeEmpty();
                regular.ShouldNotBeEmpty();
                medium.ShouldAllBe(run => run.Run.GlyphTypeface.Weight == mediumWeight);
                regular.ShouldAllBe(run => run.Run.GlyphTypeface.Weight == regularWeight);
            }
            ArcLanguage.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            meter.HeadingText.ShouldBe("风向");
            meter.ReadingText.ShouldEndWith("东北");
            meter.IsCalm = true;
            meter.ReadingText.ShouldBe("静风");
            ArcLanguage.Set(LanguageTags.ZhTW);
            Dispatcher.UIThread.RunJobs();
            meter.HeadingText.ShouldBe("風向");
            meter.HasData = false;
            meter.ReadingText.ShouldBe("無資料");
        }
        finally
        {
            manager.Apply(previous);
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false, 640, 320, 900, 900, 320, 320, 1)]
    [InlineData(false, 640, 704, 900, 900, 640, 640, 2)]
    [InlineData(false, 640, 704, 160, 190, 160, 160, .5)]
    [InlineData(true, 640, 352, 900, 900, 320, 352, 1)]
    [InlineData(true, 640, 704, 900, 900, 640, 704, 2)]
    [InlineData(true, 640, 704, 160, 176, 160, 176, .5)]
    public void Geometry_Tokens_Use_The_Same_Uniform_Scale_For_Layout_And_Drawing(
        bool showReading, double width, double height, double availableWidth, double availableHeight,
        double expectedWidth, double expectedHeight, double expectedScale)
    {
        var meter = new WindDirectionIndicator { ShowReading = showReading };
        var window = TestHelpers.Show(meter);
        var manager = ArcThemeManager.Current!;
        var previous = manager.State;
        try
        {
            ArcTheme.Apply(theme => theme
                .Token("WindDirectionIndicator", "DialWidth", width.ToString(CultureInfo.InvariantCulture))
                .Token("WindDirectionIndicator", "DialHeight", height.ToString(CultureInfo.InvariantCulture)));
            Dispatcher.UIThread.RunJobs();
            meter.Measure(new Size(availableWidth, availableHeight));
            meter.DesiredSize.ShouldBe(new Size(expectedWidth, expectedHeight));
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
    public void Compass_Separates_All_Angles_From_Inner_Directions_And_Optional_Reading()
    {
        ArcLanguage.Set(LanguageTags.EnUS);
        var meter = new WindDirectionIndicator { Direction = 22.5 };
        var window = TestHelpers.Show(meter);
        try
        {
            meter.Measure(new Size(900, 900));
            meter.DesiredSize.ShouldBe(new Size(320, 320));
            meter.Arrange(new Rect(meter.DesiredSize));
            string DrawnText() => string.Concat(ShowpieceTextRecording.Record(meter.Render)
                .Select(run => run.Run.Characters.ToString()));
            // 同一段文字可能因字体回退拆成多个 GlyphRun，拼接后再检查用户可见内容。
            var compass = DrawnText();
            for (var i = 0; i < 16; i++)
            {
                compass.ShouldContain($"{(i * 22.5).ToString("0.#", CultureInfo.CurrentCulture)}°");
            }
            compass.ShouldNotContain(meter.ReadingText);
            meter.ShowReading = true;
            meter.Measure(new Size(900, 900));
            meter.DesiredSize.ShouldBe(new Size(320, 352));
            meter.Arrange(new Rect(meter.DesiredSize));
            DrawnText().ShouldContain(meter.ReadingText);
            // 已绘制后的读数变化必须刷新动态缓存，同时保留完整圆盘标签。
            meter.Direction = 90;
            DrawnText().ShouldContain("90° · E");
            DrawnText().ShouldNotContain("22.5° · NNE");
            meter.IsCalm = true;
            meter.ReadingText.ShouldBe("Calm");
            DrawnText().ShouldContain("Calm");
            meter.CalmLabel = "Quiet";
            DrawnText().ShouldContain("Quiet");
            DrawnText().ShouldNotContain("Calm");
            meter.HasData = false;
            meter.ReadingText.ShouldBe("No data");
            DrawnText().ShouldContain("No data");
            meter.NoDataLabel = "Waiting";
            DrawnText().ShouldContain("Waiting");
            DrawnText().ShouldNotContain("No data");
            meter.DesiredSize.ShouldBe(new Size(320, 352));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Drawing_Has_Two_Rings_Four_Markers_And_One_Wide_Pointer_Without_A_Center_Dot()
    {
        var meter = new WindDirectionIndicator();
        var window = TestHelpers.Show(meter);
        try
        {
            meter.Measure(new Size(320, 320));
            meter.Arrange(new Rect(meter.DesiredSize));
            List<Drawing> Record()
            {
                var root = new DrawingGroup();
                using (var context = root.Open())
                {
                    meter.Render(context);
                }
                var shapes = new List<Drawing>();
                void Walk(Drawing drawing)
                {
                    if (drawing is DrawingGroup group)
                    {
                        foreach (var child in group.Children)
                        {
                            Walk(child);
                        }
                    }
                    else
                    {
                        shapes.Add(drawing);
                    }
                }
                Walk(root);
                return shapes;
            }
            var drawn = Record();
            // DrawEllipse 可能记录为绘图原语，不假定具体 Geometry 类型；用实际边界检查形状。
            drawn.Select(shape => shape.GetBounds())
                .Count(bounds => Math.Abs(bounds.Width - bounds.Height) < 1e-9 &&
                                 bounds.Width > 190 && bounds.Width < 240).ShouldBe(2);
            drawn.Where(shape => shape is not GlyphRunDrawing)
                .ShouldNotContain(shape => shape.GetBounds().Size == new Size(10, 10));
            var shapes = drawn.Where(shape => shape is not GlyphRunDrawing).Select(shape => shape.GetBounds()).ToList();
            shapes.Count.ShouldBe(19);
            shapes.Count(bounds => bounds.Size == new Size(14, 18)).ShouldBe(4);
            shapes.Single(bounds => bounds.Width == 48).Height.ShouldBe(108);
            meter.RingWidth = 22;
            meter.NeedleWidth = 56;
            meter.NeedleLength = 100;
            shapes = Record().Where(shape => shape is not GlyphRunDrawing).Select(shape => shape.GetBounds()).ToList();
            shapes.Count(bounds => bounds.Size == new Size(14, 22)).ShouldBe(4);
            shapes.Single(bounds => bounds.Width == 56).Height.ShouldBe(104);
            meter.HasData = false;
            Record().Count(shape => shape is not GlyphRunDrawing).ShouldBe(18);
        }
        finally
        {
            window.Close();
        }
    }
}
