using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

// 仪表共享的范围运算和圆角柱条绘制；不拥有控件、事件或缓存。
// 阅读顺序：先把真实数值变成比例，再用比例裁剪完整圆角形状。
// 液位计和通用线性仪表使用同一算法，满量程边界修正不需要维护两份。
internal static class GaugeScale
{
    internal static double Fraction(double value, double minimum, double maximum)
    {
        if (maximum <= minimum || value <= minimum)
        {
            return 0;
        }
        if (value >= maximum)
        {
            return 1;
        }
        // 常见范围直接相减，保留很窄的范围精度；例如 1 与其下一个 double。
        // 横跨正负极大数时差值会变成无穷，此时先缩小三个数再相减。
        var range = maximum - minimum;
        if (double.IsFinite(range))
        {
            return (value - minimum) / range;
        }
        var scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        return Math.Clamp((value / scale - minimum / scale) /
            (maximum / scale - minimum / scale), 0, 1);
    }

    internal static double Tick(double minimum, double maximum, double fraction)
        => minimum * (1 - fraction) + maximum * fraction;

    internal static void DrawFill(DrawingContext context, IBrush brush, Rect inner,
        double radius, double fraction)
    {
        // 弹簧可能短暂越过目标，绘制时仍限制在轨道内；真实 Value 不在这里修改。
        var height = inner.Height * Math.Clamp(fraction, 0, 1);
        using (context.PushClip(new Rect(inner.Left, inner.Bottom - height, inner.Width, height)))
        {
            context.DrawRectangle(brush, null, new RoundedRect(inner, radius));
        }
        // 裁剪交界就是柱条顶部。额外横线会在满量程时越过圆角，不能叠加。
    }
}
