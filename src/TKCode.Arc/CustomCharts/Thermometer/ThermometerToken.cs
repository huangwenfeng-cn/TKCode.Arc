namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ThermometerToken : AbstractControlDesignToken
{
    /// <summary>Maximum display width; together with DialHeight bounds a uniform scale of the 220 × 360 drawing.</summary>
    public double DialWidth { get; set; }

    /// <summary>Maximum display height; together with DialWidth bounds a uniform scale of the 220 × 360 drawing.</summary>
    public double DialHeight { get; set; }

    /// <summary>Outside width of the stem in design-box units.</summary>
    public double TubeWidth { get; set; }

    /// <summary>Outside radius of the bulb in design-box units.</summary>
    public double BulbRadius { get; set; }

    /// <summary>Thickness of the glass outline in design-box units.</summary>
    public double OutlineThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DialWidth = 220;
        DialHeight = 360;
        TubeWidth = 36;
        BulbRadius = 28;
        OutlineThickness = 1.5;
    }
}
