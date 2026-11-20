namespace TKCode.Arc.Localization;

// 这是语言资源键，不是另一套液位或质量状态。
// LanguageCatalog 让生成器创建 LevelIndicatorLangResources.Get；XLF 的 unit id
// 对应这些成员名，例如 Stale 在 zh-CN.xlf 中对应“数据过期”。
// 控件根据 State/Quality 选键，再读取当前语言文字；不要通过翻译文字反向判断状态。
/// <summary>Built-in state labels of the level indicator.</summary>
[LanguageCatalog]
public enum LevelIndicatorLangResourceKind
{
    /// <summary>Low level.</summary>
    Low,
    /// <summary>Normal level.</summary>
    Normal,
    /// <summary>High level.</summary>
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
