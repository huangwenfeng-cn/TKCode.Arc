namespace TKCode.Arc;

// Token 是外观参数集合；标记让生成器创建主题可用的资源访问代码。
// DialWidth/DialHeight 约束最终显示尺寸，VesselWidth/OutlineThickness 在 220 × 360 参考坐标中使用。
// 例如宽高翻倍时液位计整体显示两倍大小，罐体仍然保持同一比例，不会被拉成扁的。
// 液位数值、质量和高低液位区间由控件/应用保存，不属于主题 Token。
[ControlDesignToken]
internal sealed class LevelIndicatorToken : AbstractControlDesignToken
{
    /// <summary>Maximum display width; together with DialHeight bounds a uniform scale of the 220 × 360 drawing.</summary>
    public double DialWidth { get; set; }

    /// <summary>Maximum display height; together with DialWidth bounds a uniform scale of the 220 × 360 drawing.</summary>
    public double DialHeight { get; set; }

    /// <summary>Outside width of the vessel in design-box units; clamped to 20–150.</summary>
    public double VesselWidth { get; set; }

    /// <summary>Thickness of the vessel outline in design-box units; also sets the wall inset the liquid keeps from it.</summary>
    public double OutlineThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        // 这里只计算默认外观，不读控件树或设备；计算可能在工作线程执行，因此保持确定性。
        // 先调用 base 保留基础计算契约，再给出与控件无主题时一致的回退值。
        base.CalculateTokenValues(isDarkMode);
        DialWidth = 220;
        DialHeight = 360;
        VesselWidth = 130;
        OutlineThickness = 1.5;
    }
}
