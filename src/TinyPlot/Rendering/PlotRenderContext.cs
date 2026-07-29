using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// Everything a trace needs to render itself: resolved axes, plot rectangle,
/// theme colors and pixel mapping helpers.
/// </summary>
public sealed class PlotRenderContext
{
    public required Rect PlotRect { get; init; }

    public required AxisState XAxis { get; init; }

    public required AxisState YAxis { get; init; }

    public required PlotTheme Theme { get; init; }

    public required Layout Layout { get; init; }

    public required Typeface Typeface { get; init; }

    public IReadOnlyList<Color> Colorway => Layout.Colorway as IReadOnlyList<Color> ?? Theme.Colorway;

    /// <summary>Cross-trace bar positioning computed by the plot (group/stack).</summary>
    internal BarLayoutState? Bars { get; set; }

    /// <summary>Points of the last rendered scatter trace, for fill="tonexty".</summary>
    internal List<Point>? LastScatterPoints { get; set; }

    /// <summary>Per-trace calc results (histogram bins, box stats, cached bitmaps...).</summary>
    internal Dictionary<Trace, object?> CalcData { get; } = new();

    public T? Calc<T>(Trace trace) where T : class
        => CalcData.TryGetValue(trace, out var v) ? v as T : null;

    public double XToPixels(double xRaw) => PlotRect.X + XAxis.Fraction(xRaw) * PlotRect.Width;

    public double YToPixels(double yRaw) => PlotRect.Bottom - YAxis.Fraction(yRaw) * PlotRect.Height;

    public Point ToPixels(double xRaw, double yRaw) => new(XToPixels(xRaw), YToPixels(yRaw));

    public double XFromPixels(double px) => XAxis.RawValueAt((px - PlotRect.X) / PlotRect.Width);

    public double YFromPixels(double py) => YAxis.RawValueAt((PlotRect.Bottom - py) / PlotRect.Height);

    // ---- drawing helpers --------------------------------------------------

    public SolidColorBrush Brush(Color c, double opacity = 1)
    {
        if (opacity < 1)
            c = Color.FromArgb((byte)(c.A * opacity), c.R, c.G, c.B);
        return new SolidColorBrush(c);
    }

    public Pen Pen(Color c, double width = 1, IDashStyle? dash = null, double opacity = 1)
        => new(Brush(c, opacity), width, dash, PenLineCap.Round, PenLineJoin.Round);

    public FormattedText Text(string s, Color color, double? size = null, FontWeight weight = FontWeight.Normal, double opacity = 1)
        => new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface, size ?? Layout.FontSize, Brush(color, opacity))
        {
            MaxTextWidth = double.PositiveInfinity,
            MaxTextHeight = double.PositiveInfinity
        };

    /// <summary>White or dark text color, whichever contrasts better with the given background.</summary>
    public Color ContrastColor(Color bg)
    {
        var lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
        return lum > 0.55 ? Color.Parse("#2a3f5f") : Colors.White;
    }
}

/// <summary>A single hoverable item found near the cursor.</summary>
public sealed class HoverTarget
{
    public required Point ScreenPoint { get; init; }

    public required Trace Trace { get; init; }

    public required Color Color { get; init; }

    public string? Title { get; init; }

    public string? XText { get; init; }

    public string? YText { get; init; }

    public string? ExtraText { get; init; }

    /// <summary>Cursor distance used to pick the closest target.</summary>
    public double Distance { get; set; }

    /// <summary>Trace-specific payload (bar rect, pie slice index, heatmap cell...).</summary>
    internal object? Tag { get; init; }
}

/// <summary>One entry in the legend.</summary>
public sealed class LegendItem
{
    public required string Label { get; init; }

    public required Color Color { get; init; }

    public Trace? Trace { get; init; }

    public MarkerSymbol? Symbol { get; init; }

    public bool IsLine { get; init; }

    /// <summary>Pie traces use the tag to identify a single slice.</summary>
    public object? Tag { get; init; }

    public bool IsHidden { get; set; }
}
