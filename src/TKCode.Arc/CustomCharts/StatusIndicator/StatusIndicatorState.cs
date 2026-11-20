namespace TKCode.Arc;

// 枚举把设备反馈分为六种明确状态。状态必须来自应用的实际反馈，
// 不要把“命令已经发出”直接当成 Running，也不要把 Unknown 自动当成 Offline。
/// <summary>Application-provided device state; the indicator does not infer connectivity or alarms.</summary>
public enum StatusIndicatorState
{
    /// <summary>No reliable state is available.</summary>
    // 未知：刚启动、缺少可靠反馈或应用暂时无法确认状态。
    Unknown,
    /// <summary>The device is disconnected.</summary>
    // 离线：应用已经确认连接不可用，与“尚未确认”的 Unknown 区分。
    Offline,
    /// <summary>The device is connected but not running.</summary>
    // 待机：设备在线，但当前没有运行。
    Idle,
    /// <summary>The device is running normally.</summary>
    // 运行：设备反馈正在正常运行。
    Running,
    /// <summary>The application reports a warning.</summary>
    // 警告：应用报告需要关注的情况，具体规则由业务决定。
    Warning,
    /// <summary>The application reports a fault.</summary>
    // 故障：应用报告故障；控件不负责确认、复位或排除故障。
    Fault
}
