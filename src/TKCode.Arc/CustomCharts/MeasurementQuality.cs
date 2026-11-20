namespace TKCode.Arc;

// enum（枚举）把质量限制为下面四种明确选择，避免用随意拼写的字符串表示状态。
// 它回答“这份数值现在能不能用”，不回答“温度高不高”或“设备有没有运行”。
// 例如：Value=80、Quality=Good 表示有效的 80 度；Quality=Stale 表示保存着 80 度，
// 但它已经过期，控件应隐藏当前读数。控件不会自己计时，也不会根据数值猜测采集异常。
// 应用先判断质量，再通过绑定交给控件；恢复时先更新数值，再恢复 Good。
/// <summary>Application-provided measurement validity; controls do not infer timeouts or communication status.</summary>
public enum MeasurementQuality
{
    /// <summary>A current, usable measurement.</summary>
    // 有效。第一项的默认整数值是 0，因此未显式设置 Quality 时为 Good。
    // 保留这个默认值是为了兼容已有用法；应用尚未收到首个样本时应显式设置 NoData。
    Good,
    /// <summary>No measurement is available.</summary>
    // 无数据。例如刚启动尚未收到样本；不要用温度 0 或风向 0 代替“没有数据”。
    NoData,
    /// <summary>The application considers the measurement too old to use.</summary>
    // 数据过期。是否超过允许的采样间隔，由应用根据时间戳判断。
    Stale,
    /// <summary>The application reports a collection or sensor error.</summary>
    // 采集异常。例如传感器故障或采集失败；它不等同于有效数据中的高温报警。
    Error
}
