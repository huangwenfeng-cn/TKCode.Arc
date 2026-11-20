namespace TKCode.Arc.Localization;

// LanguageCatalog 声明允许生成器把 XLF 资源连接到 Get 方法。
// 这些是文字键，不能用译文反向判断 State 或 Quality；状态枚举是独立的数据契约。
/// <summary>Built-in threshold and quality labels of the linear gauge.</summary>
[LanguageCatalog]
public enum LinearGaugeLangResourceKind
{
    /// <summary>Low reading.</summary>
    Low,
    /// <summary>Normal reading.</summary>
    Normal,
    /// <summary>High reading.</summary>
    High,
    /// <summary>Invalid measurement range or thresholds.</summary>
    InvalidRange,
    /// <summary>Missing measurement.</summary>
    NoData,
    /// <summary>Stale measurement.</summary>
    Stale,
    /// <summary>Collection error.</summary>
    Error
}
