namespace TKCode.Arc.Localization;

// 这些枚举成员标识翻译条目，不保存设备状态。LanguageCatalog 由生成器处理，
// Get(kind) 按当前语言读取同目录 XLF 中相应的 unit id；DeviceStatus 用作无障碍名称。
/// <summary>Built-in device status labels.</summary>
[LanguageCatalog]
public enum StatusIndicatorLangResourceKind
{
    /// <summary>Accessible component name.</summary>
    DeviceStatus,
    /// <summary>Unavailable state.</summary>
    Unknown,
    /// <summary>Disconnected device.</summary>
    Offline,
    /// <summary>Connected and idle.</summary>
    Idle,
    /// <summary>Normal operation.</summary>
    Running,
    /// <summary>Warning state.</summary>
    Warning,
    /// <summary>Fault state.</summary>
    Fault
}
