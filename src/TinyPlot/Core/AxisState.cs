namespace TinyPlot;

/// <summary>
/// Fully resolved axis: effective type, visible range (in transformed space —
/// log10 for log axes, OLE Automation date for date axes, slot index for
/// category axes), category registry and generated ticks.
/// </summary>
public sealed class AxisState
{
    public required PlotAxis Source { get; init; }

    public AxisType EffectiveType { get; set; } = AxisType.Linear;

    /// <summary>Visible minimum in transformed space.</summary>
    public double Min { get; set; }

    /// <summary>Visible maximum in transformed space.</summary>
    public double Max { get; set; }

    public IReadOnlyList<string> Categories { get; set; } = [];

    public IReadOnlyList<Tick> Ticks { get; set; } = [];

    /// <summary>Maps a raw data value (number / OADate / category index) to transformed space.</summary>
    public double Transform(double raw) => EffectiveType == AxisType.Log ? Math.Log10(raw) : raw;

    /// <summary>Maps a transformed value back to raw data space.</summary>
    public double Untransform(double t) => EffectiveType == AxisType.Log ? Math.Pow(10, t) : t;

    /// <summary>0..1 position of a raw value along the axis. NaN-safe.</summary>
    public double Fraction(double raw)
    {
        var t = Transform(raw);
        var span = Max - Min;
        return span == 0 || double.IsNaN(t) ? double.NaN : (t - Min) / span;
    }

    /// <summary>Transformed value at a 0..1 position.</summary>
    public double ValueAt(double fraction) => Min + fraction * (Max - Min);

    /// <summary>Raw (data-space) value at a 0..1 position.</summary>
    public double RawValueAt(double fraction) => Untransform(ValueAt(fraction));

    public int CategoryIndex(string name)
    {
        for (var i = 0; i < Categories.Count; i++)
            if (Categories[i] == name)
                return i;
        return -1;
    }

    /// <summary>Human readable representation of a raw data value on this axis (for hover labels).</summary>
    public string FormatHover(double raw)
    {
        if (double.IsNaN(raw)) return "";
        switch (EffectiveType)
        {
            case AxisType.Category:
            {
                var i = (int)Math.Round(raw);
                return i >= 0 && i < Categories.Count ? Categories[i] : raw.ToString("0");
            }
            case AxisType.Date:
            {
                DateTime dt;
                try
                {
                    dt = DateTime.FromOADate(raw);
                }
                catch
                {
                    return PlotFmt.HoverValue(raw);
                }

                return dt.TimeOfDay == TimeSpan.Zero
                    ? dt.ToString("yyyy-MM-dd")
                    : dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
            default:
                return Source.TickFormat != null
                    ? raw.ToString(Source.TickFormat, System.Globalization.CultureInfo.InvariantCulture)
                    : PlotFmt.ApplyAffixes(PlotFmt.HoverValue(raw), Source.TickPrefix, Source.TickSuffix);
        }
    }
}
