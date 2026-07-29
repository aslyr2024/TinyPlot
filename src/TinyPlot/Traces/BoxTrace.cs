using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// Box plot, the counterpart of plotly.js type "box". Quartiles are computed
/// from raw values (linear interpolation method).
/// </summary>
public class BoxTrace : Trace
{
    /// <summary>Distribution values (vertical boxes).</summary>
    public DataSeries? Y { get; set; }

    /// <summary>Distribution values (horizontal boxes).</summary>
    public DataSeries? X { get; set; }

    /// <summary>Category position of this box on the position axis (defaults to trace index).</summary>
    public string? Category { get; set; }

    public Orientation Orientation { get; set; } = Orientation.Vertical;

    public bool ShowOutliers { get; set; } = true;

    public double WhiskerWidth { get; set; } = 0.5;

    internal BoxStats? Stats { get; private set; }

    internal double Position { get; private set; }

    internal override (DataSeries? x, DataSeries? y) GetAxesData()
    {
        // boxes are positioned by category / index, values on the other axis
        if (Orientation == Orientation.Vertical)
            return (Category != null ? new DataSeries([Category]) : null, Y);
        return (X, Category != null ? new DataSeries([Category]) : null);
    }

    internal override void Prepare(PlotCalcContext ctx)
    {
        var values = (Orientation == Orientation.Vertical ? Y : X);
        if (values == null || values.Count == 0)
        {
            Stats = null;
            return;
        }

        var sorted = Enumerable.Range(0, values.Count)
            .Select(values.AsNumber)
            .Where(v => !double.IsNaN(v))
            .OrderBy(v => v)
            .ToArray();
        if (sorted.Length == 0)
        {
            Stats = null;
            return;
        }

        var q1 = Percentile(sorted, 0.25);
        var med = Percentile(sorted, 0.5);
        var q3 = Percentile(sorted, 0.75);
        var iqr = q3 - q1;
        var loFence = q1 - 1.5 * iqr;
        var hiFence = q3 + 1.5 * iqr;
        var inliers = sorted.Where(v => v >= loFence && v <= hiFence).ToArray();
        var outliers = sorted.Where(v => v < loFence || v > hiFence).ToArray();

        Stats = new BoxStats
        {
            Q1 = q1,
            Median = med,
            Q3 = q3,
            WhiskerLo = inliers.Length > 0 ? inliers[0] : sorted[0],
            WhiskerHi = inliers.Length > 0 ? inliers[^1] : sorted[^1],
            Outliers = outliers
        };

        Position = Category != null
            ? (Orientation == Orientation.Vertical ? ctx.XAxis.CategoryIndex(Category) : ctx.YAxis.CategoryIndex(Category))
            : ctx.NextBoxPosition(this);

        if (Orientation == Orientation.Vertical)
        {
            ctx.ExtendXRange(Position - 0.5, Position + 0.5);
            ctx.ExtendYRange(sorted[0], sorted[^1]);
        }
        else
        {
            ctx.ExtendYRange(Position - 0.5, Position + 0.5);
            ctx.ExtendXRange(sorted[0], sorted[^1]);
        }

        ctx.SetCalc(this, Stats);
    }

    private static double Percentile(double[] sorted, double q)
    {
        var pos = (sorted.Length - 1) * q;
        var lo = (int)Math.Floor(pos);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Stats is not { } s) return;
        var color = ResolvedColor;
        using var _ = dc.PushOpacity(Opacity);
        var pen = rc.Pen(color, 1.5);
        var fill = rc.Brush(color, 0.5);

        // half slot width in pixels
        double halfW;
        if (Orientation == Orientation.Vertical)
            halfW = Math.Abs(rc.XToPixels(Position + 0.25) - rc.XToPixels(Position - 0.25));
        else
            halfW = Math.Abs(rc.YToPixels(Position + 0.25) - rc.YToPixels(Position - 0.25));
        halfW = Math.Max(3, halfW);

        if (Orientation == Orientation.Vertical)
        {
            var cx = rc.XToPixels(Position);
            var yQ1 = rc.YToPixels(s.Q1);
            var yQ3 = rc.YToPixels(s.Q3);
            var yMed = rc.YToPixels(s.Median);
            var yLo = rc.YToPixels(s.WhiskerLo);
            var yHi = rc.YToPixels(s.WhiskerHi);
            if (new[] { cx, yQ1, yQ3, yMed, yLo, yHi }.Any(double.IsNaN)) return;

            var boxRect = new Rect(cx - halfW, Math.Min(yQ3, yQ1), halfW * 2, Math.Max(1, Math.Abs(yQ1 - yQ3)));
            dc.DrawRectangle(fill, pen, boxRect);
            dc.DrawLine(rc.Pen(color, 2.5), new Point(cx - halfW, yMed), new Point(cx + halfW, yMed));
            dc.DrawLine(pen, new Point(cx, yQ3), new Point(cx, yHi));
            dc.DrawLine(pen, new Point(cx, yQ1), new Point(cx, yLo));
            var whiskHalf = halfW * WhiskerWidth;
            dc.DrawLine(pen, new Point(cx - whiskHalf, yHi), new Point(cx + whiskHalf, yHi));
            dc.DrawLine(pen, new Point(cx - whiskHalf, yLo), new Point(cx + whiskHalf, yLo));

            if (ShowOutliers)
                foreach (var o in s.Outliers)
                {
                    var py = rc.YToPixels(o);
                    if (!double.IsNaN(py))
                        dc.DrawEllipse(null, pen, new Point(cx, py), 3, 3);
                }
        }
        else
        {
            var cy = rc.YToPixels(Position);
            var xQ1 = rc.XToPixels(s.Q1);
            var xQ3 = rc.XToPixels(s.Q3);
            var xMed = rc.XToPixels(s.Median);
            var xLo = rc.XToPixels(s.WhiskerLo);
            var xHi = rc.XToPixels(s.WhiskerHi);
            if (new[] { cy, xQ1, xQ3, xMed, xLo, xHi }.Any(double.IsNaN)) return;

            var boxRect = new Rect(Math.Min(xQ1, xQ3), cy - halfW, Math.Max(1, Math.Abs(xQ3 - xQ1)), halfW * 2);
            dc.DrawRectangle(fill, pen, boxRect);
            dc.DrawLine(rc.Pen(color, 2.5), new Point(xMed, cy - halfW), new Point(xMed, cy + halfW));
            dc.DrawLine(pen, new Point(xQ3, cy), new Point(xHi, cy));
            dc.DrawLine(pen, new Point(xQ1, cy), new Point(xLo, cy));
            var whiskHalf = halfW * WhiskerWidth;
            dc.DrawLine(pen, new Point(xHi, cy - whiskHalf), new Point(xHi, cy + whiskHalf));
            dc.DrawLine(pen, new Point(xLo, cy - whiskHalf), new Point(xLo, cy + whiskHalf));

            if (ShowOutliers)
                foreach (var o in s.Outliers)
                {
                    var px = rc.XToPixels(o);
                    if (!double.IsNaN(px))
                        dc.DrawEllipse(null, pen, new Point(px, cy), 3, 3);
                }
        }
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (Stats is not { } s) yield break;
        var posPx = Orientation == Orientation.Vertical ? rc.XToPixels(Position) : rc.YToPixels(Position);
        var d = Orientation == Orientation.Vertical ? Math.Abs(posPx - pt.X) : Math.Abs(posPx - pt.Y);
        if (d > 40) yield break;

        yield return new HoverTarget
        {
            ScreenPoint = new Point(
                Orientation == Orientation.Vertical ? posPx : pt.X,
                Orientation == Orientation.Vertical ? pt.Y : posPx),
            Trace = this,
            Color = ResolvedColor,
            Title = Name,
            XText = null,
            YText = null,
            ExtraText = $"max: {PlotFmt.HoverValue(s.WhiskerHi)}\nq3: {PlotFmt.HoverValue(s.Q3)}\nmedian: {PlotFmt.HoverValue(s.Median)}\nq1: {PlotFmt.HoverValue(s.Q1)}\nmin: {PlotFmt.HoverValue(s.WhiskerLo)}",
            Distance = d
        };
    }

    internal sealed class BoxStats
    {
        public double Q1 { get; init; }

        public double Median { get; init; }

        public double Q3 { get; init; }

        public double WhiskerLo { get; init; }

        public double WhiskerHi { get; init; }

        public double[] Outliers { get; init; } = [];
    }
}
