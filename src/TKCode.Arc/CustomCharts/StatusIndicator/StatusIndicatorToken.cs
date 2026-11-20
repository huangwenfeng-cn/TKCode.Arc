namespace TKCode.Arc;

// Token 是组件统一的外观参数，不保存设备状态或读数。
// 标记让生成器创建 XAML 资源访问代码，主题通过 TokenResource 动态使用这些值。
[ControlDesignToken]
internal sealed class StatusIndicatorToken : AbstractControlDesignToken
{
    /// <summary>Diameter of the lamp in device-independent pixels.</summary>
    public double Diameter { get; set; }
    /// <summary>Space between the lamp and its visible text.</summary>
    public double LabelGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        // 先执行基础计算，再提供本组件的默认几何。单位是设备无关像素，
        // 不直接等于高 DPI 屏幕上的物理像素；颜色和字体仍取全局主题资源。
        base.CalculateTokenValues(isDarkMode);
        Diameter = 16;
        LabelGap = 8;
    }
}
