namespace TKCode.Arc;

// 生成器据此生成 Token 资源访问器；身份为 WindDirectionIndicator，保持与控件同一平坦命名空间。
// 这里描述外观几何，不存风向、风速等实时业务数据。
[ControlDesignToken]
internal sealed class WindDirectionIndicatorToken : AbstractControlDesignToken
{
    /// <summary>Maximum display width; together with DialHeight bounds uniform scaling of the 320-unit compass.</summary>
    public double DialWidth { get; set; }
    /// <summary>Maximum display height; reference height is 320, or 352 when ShowReading is enabled.</summary>
    public double DialHeight { get; set; }
    /// <summary>Distance from center to arrow tip in drawing units; clamped to 40 through inner radius minus 30.</summary>
    public double NeedleLength { get; set; }
    /// <summary>Width of the solid arrow's tail in drawing units; clamped to 24–56.</summary>
    public double NeedleWidth { get; set; }
    /// <summary>Distance between the two compass rings in drawing units; clamped to 12–22.</summary>
    public double RingWidth { get; set; }
    /// <summary>Thickness of the compass outline in drawing units.</summary>
    public double OutlineThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        // Token 计算可能在工作线程执行，只做确定性计算，不访问控件、设备或文件。
        // 宽高限制最终显示尺寸；其余长度使用参考绘图坐标，随后跟随圆盘整体缩放。
        // 默认值与控件内部属性的无主题回退值保持一致；调用 base 保留基础计算契约。
        base.CalculateTokenValues(isDarkMode);
        DialWidth = 320;
        DialHeight = 352;
        NeedleLength = 68;
        NeedleWidth = 48;
        RingWidth = 18;
        OutlineThickness = 1.5;
    }
}
