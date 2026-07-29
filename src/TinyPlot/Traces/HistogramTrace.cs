using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// Histogram, the counterpart of plotly.js type "histogram".
/// </summary>
public class HistogramTrace : Trace
{
    public DataSeries? X { get; set; }

    public DataSeries? Y { get; set; }

    /// <summary>Number of bins. 0 = auto (square root rule).</summary>
    public int NBins { get; set; }

    public BarMarker Marker { get; } = new();

    public Orientation Orientation => Y != null && X == null ? Orientation.Horizontal : Orientation.Vertical;

    internal List<(double start, double end, double count)>? Bins { get; private set; }

    internal override (DataSeries? x, DataSeries? y) GetAxesData() => (X, Y);

    internal override void Prepare(PlotCalcContext ctx)
    {
        var source = X ?? Y;
        if (source == null || source.Count == 0)
        {
            Bins = null;
            return;
        }

        var values = Enumerable.Range(0, source.Count)
            .Select(source.AsNumber)
            .Where(v => !double.IsNaN(v))
            .ToArray();
        if (values.Length == 0)
        {
            Bins = null;
            return;
        }

        var min = values.Min();
        var max = values.Max();
        if (min == max)
        {
            min -= 0.5;
            max += 0.5;
        }

        var nBins = NBins > 0 ? NBins : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(values.Length)));
        var step = (max - min) / nBins;

        var counts = new double[nBins];
        foreach (var v in values)
        {
            var idx = (int)((v - min) / step);
            if (idx >= nBins) idx = nBins - 1;
            if (idx < 0) idx = 0;
            counts[idx]++;
        }

        Bins = new List<(double, double, double)>(nBins);
        for (var i = 0; i < nBins; i++)
            Bins.Add((min + i * step, min + (i + 1) * step, counts[i]));

        var maxCount = counts.Max();
        if (Orientation == Orientation.Vertical)
        {
            ctx.ExtendXRange(min, max);
            ctx.ExtendYRange(0, maxCount);
        }
        else
        {
            ctx.ExtendYRange(min, max);
            ctx.ExtendXRange(0, maxCount);
        }

        ctx.SetCalc(this, null);
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Bins == null) return;
        using var _ = dc.PushOpacity(Opacity * Marker.Opacity);
        var color = Marker.Color ?? ResolvedColor;
        var pen = rc.Pen(Marker.LineColor ?? rc.Theme.PlotBackground, Math.Max(1, Marker.LineWidth));

        foreach (var (start, end, count) in Bins)
        {
            Rect r;
            if (Orientation == Orientation.Vertical)
            {
                var x0 = rc.XToPixels(start);
                var x1 = rc.XToPixels(end);
                var y0 = rc.YToPixels(0);
                var y1 = rc.YToPixels(count);
                if (new[] { x0, x1, y0, y1 }.Any(double.IsNaN)) continue;
                r = new Rect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Max(1, Math.Abs(y1 - y0)));
            }
            else
            {
                var y0 = rc.YToPixels(start);
                var y1 = rc.YToPixels(end);
                var x0 = rc.XToPixels(0);
                var x1 = rc.XToPixels(count);
                if (new[] { x0, x1, y0, y1 }.Any(double.IsNaN)) continue;
                r = new Rect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(1, Math.Abs(x1 - x0)), Math.Abs(y1 - y0));
            }

            dc.DrawRectangle(rc.Brush(color), pen, r);
        }
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (Bins == null) yield break;
        foreach (var (start, end, count) in Bins)
        {
            Rect r;
            if (Orientation == Orientation.Vertical)
            {
                r = new Rect(
                    new Point(Math.Min(rc.XToPixels(start), rc.XToPixels(end)), Math.Min(rc.YToPixels(0), rc.YToPixels(count))),
                    new Point(Math.Max(rc.XToPixels(start), rc.XToPixels(end)), Math.Max(rc.YToPixels(0), rc.YToPixels(count))));
            }
            else
            {
                r = new Rect(
                    new Point(Math.Min(rc.XToPixels(0), rc.XToPixels(count)), Math.Min(rc.YToPixels(start), rc.YToPixels(end))),
                    new Point(Math.Max(rc.XToPixels(0), rc.XToPixels(count)), Math.Max(rc.YToPixels(start), rc.YToPixels(end))));
            }

            if (r.Contains(pt))
            {
                yield return new HoverTarget
                {
                    ScreenPoint = pt,
                    Trace = this,
                    Color = Marker.Color ?? ResolvedColor,
                    Title = Name,
                    XText = $"{PlotFmt.HoverValue(start)} – {PlotFmt.HoverValue(end)}",
                    YText = $"count: {PlotFmt.HoverValue(count)}",
                    Distance = 0
                };
                yield break;
            }
        }
    }
}
