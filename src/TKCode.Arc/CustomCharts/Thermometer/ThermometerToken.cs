namespace TKCode.Arc;

// Token 是外观参数集合；标记让生成器创建主题可用的资源访问代码。
// DialWidth/DialHeight 约束最终显示大小，其他参数在 220 × 360 参考坐标中使用。
// 例如宽高都翻倍，温度计整体可显示两倍大小；只改宽度不会把球泡横向拉成椭圆。
// 温度、质量和报警状态由控件/应用保存，不属于主题 Token。
[ControlDesignToken]
internal sealed class ThermometerToken : AbstractControlDesignToken
{
    /// <summary>Maximum display width; together with DialHeight bounds a uniform scale of the 220 × 360 drawing.</summary>
    public double DialWidth { get; set; }

    /// <summary>Maximum display height; together with DialWidth bounds a uniform scale of the 220 × 360 drawing.</summary>
    public double DialHeight { get; set; }

    /// <summary>Outside width of the stem in design-box units.</summary>
    public double TubeWidth { get; set; }

    /// <summary>Outside radius of the bulb in design-box units.</summary>
    public double BulbRadius { get; set; }

    /// <summary>Thickness of the glass outline in design-box units.</summary>
    public double OutlineThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        // 这里只计算默认外观，不读控件树或设备。先调用 base 保留基础计算，
        // 再设置管宽、球泡半径等默认值；它们与控件无主题时的回退值一致。
        base.CalculateTokenValues(isDarkMode);
        DialWidth = 220;
        DialHeight = 360;
        TubeWidth = 36;
        BulbRadius = 28;
        OutlineThickness = 1.5;
    }
}
