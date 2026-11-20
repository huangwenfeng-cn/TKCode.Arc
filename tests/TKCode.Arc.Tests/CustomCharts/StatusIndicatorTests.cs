using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Charts;

public class StatusIndicatorTests
{
    [AvaloniaTheory]
    [InlineData(StatusIndicatorState.Unknown, "Unknown", ArcDesignTokenKind.SurfaceMuted)]
    [InlineData(StatusIndicatorState.Offline, "Offline", ArcDesignTokenKind.TextMuted)]
    [InlineData(StatusIndicatorState.Idle, "Idle", ArcDesignTokenKind.Accent)]
    [InlineData(StatusIndicatorState.Running, "Running", ArcDesignTokenKind.Success)]
    [InlineData(StatusIndicatorState.Warning, "Warning", ArcDesignTokenKind.Warning)]
    [InlineData(StatusIndicatorState.Fault, "Fault", ArcDesignTokenKind.Danger)]
    public void Feedback_Immediately_Updates_Text_And_Theme_Color(StatusIndicatorState state, string text, ArcDesignTokenKind color)
    {
        ArcLanguage.Set(LanguageTags.EnUS);
        var indicator = new StatusIndicator();
        var window = TestHelpers.Show(indicator, ArcMotionLevel.Full);
        try
        {
            indicator.State = state;
            Dispatcher.UIThread.RunJobs();
            indicator.DisplayText.ShouldBe(text);
            var lamp = Lamp(indicator);
            lamp.Background.ShouldBe(ArcTokens.Get<IBrush>(indicator, color));
            indicator.Focusable.ShouldBeFalse();
            Should.Throw<ArgumentException>(() => indicator.State = (StatusIndicatorState)999);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Automation_Preserves_Text_When_The_Label_Is_Hidden()
    {
        var indicator = new StatusIndicator { State = StatusIndicatorState.Fault, Label = "Drive overload", ShowLabel = false };
        AutomationProperties.SetName(indicator, "Motor M-01");
        var window = TestHelpers.Show(indicator);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(indicator);
            peer.GetName().ShouldBe("Motor M-01");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
            var value = peer.GetProvider<IValueProvider>()!;
            value.IsReadOnly.ShouldBeTrue();
            value.Value.ShouldBe("Drive overload");
            value.SetValue("Running");
            indicator.State.ShouldBe(StatusIndicatorState.Fault);
            indicator.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Label").IsVisible.ShouldBeFalse();
            indicator.Label = null;
            ArcLanguage.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            value.Value.ShouldBe("故障");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Language_Subscription_Recovers_After_Detachment()
    {
        ArcLanguage.Set(LanguageTags.EnUS);
        var indicator = new StatusIndicator { State = StatusIndicatorState.Offline };
        var host = new Border { Child = indicator };
        var window = TestHelpers.Show(host);
        try
        {
            host.Child = null;
            ArcLanguage.Set(LanguageTags.ZhTW);
            indicator.DisplayText.ShouldBe("Offline");
            host.Child = indicator;
            Dispatcher.UIThread.RunJobs();
            indicator.DisplayText.ShouldBe("離線");
            ArcLanguage.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            indicator.DisplayText.ShouldBe("离线");
            indicator.Label = "网关断开";
            ArcLanguage.Set(LanguageTags.EnUS);
            indicator.DisplayText.ShouldBe("网关断开");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Geometry_Font_And_Color_Follow_Dynamic_Theme_Overrides()
    {
        var indicator = new StatusIndicator { State = StatusIndicatorState.Running };
        var window = TestHelpers.Show(indicator);
        try
        {
            var lamp = Lamp(indicator);
            lamp.Width.ShouldBe(16);
            lamp.Height.ShouldBe(16);
            ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark)
                .Token("StatusIndicator", "Diameter", "28")
                .Token("StatusIndicator", "LabelGap", "12")
                .Token("WeightMedium", "Bold"));
            Dispatcher.UIThread.RunJobs();
            lamp.Width.ShouldBe(28);
            lamp.Height.ShouldBe(28);
            indicator.FontWeight.ShouldBe(FontWeight.Bold);
            indicator.GetVisualDescendants().OfType<StackPanel>().Single().Spacing.ShouldBe(12);
            lamp.Background.ShouldBe(ArcTokens.Get<IBrush>(indicator, ArcDesignTokenKind.Success));
            indicator.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double.IsFinite(indicator.DesiredSize.Width).ShouldBeTrue();
            indicator.DesiredSize.Height.ShouldBeGreaterThanOrEqualTo(28);
            indicator.ShowLabel = false;
            // 模板子元素的可见性会排入布局队列；先完成布局，再读取期望尺寸。
            Dispatcher.UIThread.RunJobs();
            indicator.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            indicator.DesiredSize.ShouldBe(new Size(28, 28));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Full)]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Blinking_Selector_Respects_Policy_Visibility_And_Detachment(ArcMotionLevel level)
    {
        var indicator = new StatusIndicator { State = StatusIndicatorState.Warning, IsBlinking = true };
        var host = new Border { Child = indicator };
        var window = TestHelpers.Show(host, level);
        try
        {
            Dispatcher.UIThread.RunJobs();
            indicator.Classes.Contains(":blinking").ShouldBeTrue();
            indicator.Classes.Contains(":motion-full").ShouldBe(level == ArcMotionLevel.Full);
            if (level != ArcMotionLevel.Full)
            {
                Lamp(indicator).Opacity.ShouldBe(1);
            }
            indicator.IsBlinking = false;
            Dispatcher.UIThread.RunJobs();
            indicator.Classes.Contains(":blinking").ShouldBeFalse();
            Lamp(indicator).Opacity.ShouldBe(1);
            indicator.IsBlinking = true;
            indicator.IsVisible = false;
            indicator.Classes.Contains(":blinking").ShouldBeFalse();
            indicator.IsVisible = true;
            host.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            indicator.Classes.Contains(":blinking").ShouldBeFalse();
            host.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            indicator.Classes.Contains(":blinking").ShouldBeTrue();
            host.Child = null;
            indicator.Classes.Contains(":blinking").ShouldBeFalse();
            indicator.State = StatusIndicatorState.Fault;
            host.Child = indicator;
            Dispatcher.UIThread.RunJobs();
            indicator.Classes.Contains(":blinking").ShouldBeTrue();
            indicator.State.ShouldBe(StatusIndicatorState.Fault);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detached_Blinking_Indicator_Does_Not_Remain_In_Global_Subscriptions()
    {
        var reference = TestHelpers.ShowAndClose(
            () => new StatusIndicator { IsBlinking = true, State = StatusIndicatorState.Warning },
            level: ArcMotionLevel.Full);
        TestHelpers.Collect(reference);
        reference.IsAlive.ShouldBeFalse();
    }

    private static Border Lamp(StatusIndicator indicator) =>
        indicator.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "PART_Lamp");
}
