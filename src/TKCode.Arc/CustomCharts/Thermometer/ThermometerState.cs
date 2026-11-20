namespace TKCode.Arc;

// 这是温度区间，和 MeasurementQuality 的数据质量分开。
// 即使质量过期，保存的 80 度仍可算作 High；业务使用这个结果前需先检查质量。
// Low 是“小于低阈值”，High 是“大于等于高阈值”，中间含低阈值的是 Normal。
// InvalidRange 表示量程或阈值配置错误，不表示传感器采集异常。
/// <summary>The current temperature band, evaluated from the actual reading rather than the animated liquid.</summary>
public enum ThermometerState
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
