namespace TKCode.Arc.Localization;

// LanguageCatalog 生成资源访问代码；XLF 的 file id 对应枚举全名，unit id 对应成员名。
// 前十六项从北开始、顺时针每 22.5° 排列，DirectionName 使用整数索引选取，不能重排。
// 标题和状态项必须放在这十六项之后，避免角度索引误选到状态文字。
/// <summary>Compass directions and built-in labels for the wind direction indicator.</summary>
[LanguageCatalog]
public enum WindDirectionIndicatorLangResourceKind
{
    /// <summary>North.</summary>
    North,
    /// <summary>North-northeast.</summary>
    NorthNortheast,
    /// <summary>Northeast.</summary>
    Northeast,
    /// <summary>East-northeast.</summary>
    EastNortheast,
    /// <summary>East.</summary>
    East,
    /// <summary>East-southeast.</summary>
    EastSoutheast,
    /// <summary>Southeast.</summary>
    Southeast,
    /// <summary>South-southeast.</summary>
    SouthSoutheast,
    /// <summary>South.</summary>
    South,
    /// <summary>South-southwest.</summary>
    SouthSouthwest,
    /// <summary>Southwest.</summary>
    Southwest,
    /// <summary>West-southwest.</summary>
    WestSouthwest,
    /// <summary>West.</summary>
    West,
    /// <summary>West-northwest.</summary>
    WestNorthwest,
    /// <summary>Northwest.</summary>
    Northwest,
    /// <summary>North-northwest.</summary>
    NorthNorthwest,
    /// <summary>Wind direction heading.</summary>
    WindDirection,
    /// <summary>Calm wind.</summary>
    Calm,
    /// <summary>Missing measurement.</summary>
    NoData
}
