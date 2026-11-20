namespace TKCode.Arc;

// 这是液位区间，和 MeasurementQuality 的数据质量分开：质量回答“这份数值能不能用”，
// 区间回答“这份数值处在量程的哪一段”。例如储罐已抽空但采集仍在进行时，
// Value=5 可以同时是 LevelIndicatorState.Low 和 MeasurementQuality.Good。
// 业务处理前应先检查 Quality，再使用这个区间；控件不会自己判定报警。
// Low 表示“小于低液位阈值”，High 表示“大于等于高液位阈值”，两者之间（含低阈值）是 Normal。
// InvalidRange 表示量程或阈值配置错误，不表示传感器采集异常。
/// <summary>The current level band, evaluated from the actual reading rather than the animated liquid.</summary>
public enum LevelIndicatorState
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
