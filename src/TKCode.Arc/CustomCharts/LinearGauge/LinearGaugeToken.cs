namespace TKCode.Arc;

// Token 只保存外观几何，不保存读数或阈值。宽高控制整体等比缩放，
// TrackWidth 和 OutlineThickness 在 220 × 360 参考坐标中使用。
[ControlDesignToken]
internal sealed class LinearGaugeToken : AbstractControlDesignToken
{
    /// <summary>Maximum display width, combined with DialHeight for uniform scaling of the 220 × 360 drawing.</summary>
    public double DialWidth { get; set; }
    /// <summary>Maximum display height, combined with DialWidth for uniform scaling of the 220 × 360 drawing.</summary>
    public double DialHeight { get; set; }
    /// <summary>Outside track width in design-box units, clamped to 16–64.</summary>
    public double TrackWidth { get; set; }
    /// <summary>Track outline thickness in design-box units, clamped to 0–4.</summary>
    public double OutlineThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        // 计算不依赖 UI 树，允许主题运行时在工作线程准备默认值。
        base.CalculateTokenValues(isDarkMode);
        DialWidth = 220;
        DialHeight = 360;
        TrackWidth = 36;
        OutlineThickness = 1.5;
    }
}
