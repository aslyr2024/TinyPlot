using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// Candlestick chart, the counterpart of plotly.js type "candlestick".
/// </summary>
public class CandlestickTrace : Trace
{
    public DataSeries? X { get; set; }

    public DataSeries? Open { get; set; }

    public DataSeries? High { get; set; }

    public DataSeries? Low { get; set; }

    public DataSeries? Close { get; set; }

    public Color IncreasingColor { get; set; } = Avalonia.Media.Color.Parse("#3D9970");

    public Color DecreasingColor { get; set; } = Avalonia.Media.Color.Parse("#FF4136");

    internal List<double>? XRaw { get; private set; }

    internal double SlotWidth { get; private set; } = 0.6;

    internal override (DataSeries? x, DataSeries? y) GetAxesData() => (X, Close);

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (Name is { Length: > 0 } name && ShowLegend)
            yield return new LegendItem { Label = name, Color = IncreasingColor, Trace = this, IsLine = true };
    }

    internal override void Prepare(PlotCalcContext ctx)
    {
        var n = Count;
        var xs = new List<double>(n);
        for (var i = 0; i < n; i++)
        {
            var x = X != null && i < X.Count ? ctx.XValue(X, i) : i;
            xs.Add(x);
            ctx.ExtendX(x);
        }

        // slot width from minimum x spacing
        var sorted = xs.Distinct().OrderBy(v => v).ToArray();
        var minDelta = double.PositiveInfinity;
        for (var i = 1; i < sorted.Length; i++)
            minDelta = Math.Min(minDelta, sorted[i] - sorted[i - 1]);
        SlotWidth = (double.IsInfinity(minDelta) ? 1 : minDelta) * 0.6;
        if (xs.Count > 0)
            ctx.ExtendXRange(xs.Min() - SlotWidth / 2, xs.Max() + SlotWidth / 2);

        for (var i = 0; i < n; i++)
        {
            ctx.ExtendY(Low?.AsNumber(i) ?? double.NaN);
            ctx.ExtendY(High?.AsNumber(i) ?? double.NaN);
        }

        XRaw = xs;
        ctx.SetCalc(this, null);
    }

    private int Count => Math.Max(X?.Count ?? 0, Math.Max(Open?.Count ?? 0, Close?.Count ?? 0));

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (XRaw == null) return;
        using var _ = dc.PushOpacity(Opacity);
        var halfWpx = Math.Abs(rc.XToPixels(XRaw[0] + SlotWidth / 2) - rc.XToPixels(XRaw[0] - SlotWidth / 2)) / 2;
        halfWpx = Math.Max(1.5, halfWpx);

        for (var i = 0; i < XRaw.Count; i++)
        {
            var o = Open?.AsNumber(i) ?? double.NaN;
            var h = High?.AsNumber(i) ?? double.NaN;
            var l = Low?.AsNumber(i) ?? double.NaN;
            var c = Close?.AsNumber(i) ?? double.NaN;
            if (new[] { o, h, l, c }.Any(double.IsNaN)) continue;

            var rising = c >= o;
            var color = rising ? IncreasingColor : DecreasingColor;
            var pen = rc.Pen(color, 1.5);

            var px = rc.XToPixels(XRaw[i]);
            var pyO = rc.YToPixels(o);
            var pyH = rc.YToPixels(h);
            var pyL = rc.YToPixels(l);
            var pyC = rc.YToPixels(c);
            if (new[] { px, pyO, pyH, pyL, pyC }.Any(double.IsNaN)) continue;

            // wick
            dc.DrawLine(pen, new Point(px, pyH), new Point(px, Math.Min(pyO, pyC)));
            dc.DrawLine(pen, new Point(px, Math.Max(pyO, pyC)), new Point(px, pyL));
            // body
            var top = Math.Min(pyO, pyC);
            var height = Math.Max(1, Math.Abs(pyC - pyO));
            dc.DrawRectangle(rc.Brush(color), pen, new Rect(px - halfWpx, top, halfWpx * 2, height));
        }
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (XRaw == null) yield break;
        var best = -1;
        var bestD = double.PositiveInfinity;
        for (var i = 0; i < XRaw.Count; i++)
        {
            var px = rc.XToPixels(XRaw[i]);
            if (double.IsNaN(px)) continue;
            var d = Math.Abs(px - pt.X);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        if (best < 0 || bestD > 40) yield break;
        var o = Open!.AsNumber(best);
        var h = High!.AsNumber(best);
        var l = Low!.AsNumber(best);
        var c = Close!.AsNumber(best);
        yield return new HoverTarget
        {
            ScreenPoint = new Point(rc.XToPixels(XRaw[best]), pt.Y),
            Trace = this,
            Color = c >= o ? IncreasingColor : DecreasingColor,
            Title = Name ?? rc.XAxis.FormatHover(XRaw[best]),
            XText = rc.XAxis.FormatHover(XRaw[best]),
            ExtraText = $"open: {PlotFmt.HoverValue(o)}\nhigh: {PlotFmt.HoverValue(h)}\nlow: {PlotFmt.HoverValue(l)}\nclose: {PlotFmt.HoverValue(c)}",
            Distance = bestD,
            Tag = best
        };
    }
}
