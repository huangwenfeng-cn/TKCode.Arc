namespace TKCode.Arc;

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
