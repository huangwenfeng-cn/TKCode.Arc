namespace TKCode.Arc;

// 区间是对保存读数的分类，Quality 是应用对数据有效性的判断。
// 例如拉力 900 N 且数据过期时，State 仍为 High，画面只显示过期提示。
/// <summary>The threshold band of a linear gauge's actual reading, independent of animation and measurement quality.</summary>
public enum LinearGaugeState
{
    /// <summary>The reading is below the low threshold.</summary>
    Low,
    /// <summary>The reading is at or above the low threshold and below the high threshold.</summary>
    Normal,
    /// <summary>The reading is at or above the high threshold.</summary>
    High,
    /// <summary>The maximum is not greater than the minimum, or the high threshold is below the low threshold.</summary>
    InvalidRange
}
