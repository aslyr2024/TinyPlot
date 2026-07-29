using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// Base class for all trace types, the counterpart of a plotly.js trace object.
/// </summary>
public abstract class Trace
{
    /// <summary>Name shown in the legend and in hover labels.</summary>
    public string? Name { get; set; }

    public bool Visible { get; set; } = true;

    public double Opacity { get; set; } = 1.0;

    public bool ShowLegend { get; set; } = true;

    /// <summary>Primary color. When null, the layout colorway is used.</summary>
    public Color? Color { get; set; }

    /// <summary>Color assigned from the colorway (or <see cref="Color"/> when set).</summary>
    public Color ResolvedColor { get; internal set; }

    internal int ColorIndex { get; set; }

    /// <summary>Cartesian traces participate in axis scaling. Pies do not.</summary>
    internal virtual bool IsCartesian => true;

    /// <summary>Raw axis-bound data used for axis type detection and category registration.</summary>
    internal virtual (DataSeries? x, DataSeries? y) GetAxesData() => (null, null);

    /// <summary>是否为 3D 图表（支持旋转/缩放交互）。</summary>
    internal virtual bool Is3D => false;

    /// <summary>是否支持平移/缩放视图（树图、关系图等）。</summary>
    internal virtual bool SupportsPanZoom => false;

    /// <summary>Compute data extents and trace-specific calc data. Called once per render pass.</summary>
    internal abstract void Prepare(PlotCalcContext ctx);

    internal abstract void Render(DrawingContext dc, PlotRenderContext rc);

    /// <summary>Find hover targets near a point (pixel space).</summary>
    internal abstract IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode);

    internal virtual IEnumerable<LegendItem> GetLegendItems()
    {
        if (Name is { Length: > 0 } name && ShowLegend)
            yield return new LegendItem { Label = name, Color = ResolvedColor, Trace = this };
    }
}

/// <summary>Cross-trace context used during the calc pass.</summary>
public sealed class PlotCalcContext
{
    public required AxisState XAxis { get; init; }

    public required AxisState YAxis { get; init; }

    public required Layout Layout { get; init; }

    /// <summary>Data extents (raw data space) accumulated across traces.</summary>
    public double XMin { get; private set; } = double.PositiveInfinity;

    public double XMax { get; private set; } = double.NegativeInfinity;

    public double YMin { get; private set; } = double.PositiveInfinity;

    public double YMax { get; private set; } = double.NegativeInfinity;

    public double XMinPositive { get; private set; } = double.PositiveInfinity;

    public double YMinPositive { get; private set; } = double.PositiveInfinity;

    internal BarLayoutState? Bars { get; set; }

    private int _boxPosition;

    internal int NextBoxPosition(Trace trace) => _boxPosition++;

    internal Dictionary<Trace, object?> CalcData { get; } = new();

    internal void SetCalc(Trace trace, object? data) => CalcData[trace] = data;

    public void ExtendX(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;
        XMin = Math.Min(XMin, value);
        XMax = Math.Max(XMax, value);
        if (value > 0) XMinPositive = Math.Min(XMinPositive, value);
    }

    public void ExtendY(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;
        YMin = Math.Min(YMin, value);
        YMax = Math.Max(YMax, value);
        if (value > 0) YMinPositive = Math.Min(YMinPositive, value);
    }

    public void ExtendXRange(double lo, double hi)
    {
        ExtendX(lo);
        ExtendX(hi);
    }

    public void ExtendYRange(double lo, double hi)
    {
        ExtendY(lo);
        ExtendY(hi);
    }

    /// <summary>Convert a series index to axis units (category index / OADate / number).</summary>
    public double XValue(DataSeries series, int index)
    {
        if (XAxis.EffectiveType == AxisType.Category)
            return XAxis.CategoryIndex(series.AsText(index) ?? "");
        return series.AsNumber(index);
    }

    public double YValue(DataSeries series, int index)
    {
        if (YAxis.EffectiveType == AxisType.Category)
            return YAxis.CategoryIndex(series.AsText(index) ?? "");
        return series.AsNumber(index);
    }
}

/// <summary>How a trace contributes to the shared bar layout.</summary>
internal sealed class BarSlot
{
    public double Center;      // axis units
    public double Width;       // axis units (full slot, before gap)
    public double Base;        // value where the bar starts
    public double Value;       // bar value (signed)
}

internal sealed class BarLayoutState
{
    public Orientation Orientation { get; set; }

    public BarMode Mode { get; set; }

    public double SlotSize { get; set; } = 1; // distance between adjacent slot centers (axis units)

    // per trace index -> per point slot
    public Dictionary<BarTrace, BarSlot[]> Slots { get; } = new();
}
