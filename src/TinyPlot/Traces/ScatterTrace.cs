using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// Scatter / line / area chart, the counterpart of plotly.js type "scatter".
/// </summary>
public class ScatterTrace : Trace
{
    public DataSeries? X { get; set; }

    public DataSeries? Y { get; set; }

    public ScatterMode Mode { get; set; } = ScatterMode.LinesMarkers;

    public LineOptions Line { get; } = new();

    public Marker Marker { get; } = new();

    public ScatterFill Fill { get; set; } = ScatterFill.None;

    public Color? FillColor { get; set; }

    /// <summary>Per-point text labels (requires a Text mode).</summary>
    public IReadOnlyList<string>? Text { get; set; }

    public TraceTextPosition TextPosition { get; set; } = TraceTextPosition.TopCenter;

    /// <summary>Custom per-point hover text appended to the default label.</summary>
    public IReadOnlyList<string>? HoverText { get; set; }

    public bool HasLine => (Mode & ScatterMode.Lines) != 0;

    public bool HasMarkers => (Mode & ScatterMode.Markers) != 0;

    public bool HasText => (Mode & ScatterMode.Text) != 0 && Text != null;

    internal int PointCount => Math.Max(X?.Count ?? 0, Y?.Count ?? 0);

    internal override (DataSeries? x, DataSeries? y) GetAxesData() => (X, Y);

    /// <summary>Pixel-space points from the last calc pass (used for fill-to-next).</summary>
    internal List<Point>? PixelPoints { get; private set; }

    internal List<double>? XRaw { get; private set; }

    internal List<double>? YRaw { get; private set; }

    internal override void Prepare(PlotCalcContext ctx)
    {
        var n = PointCount;
        var xs = new List<double>(n);
        var ys = new List<double>(n);
        for (var i = 0; i < n; i++)
        {
            var x = X != null && i < X.Count ? ctx.XValue(X, i) : i;
            var y = Y != null && i < Y.Count ? ctx.YValue(Y, i) : 0;
            xs.Add(x);
            ys.Add(y);
            ctx.ExtendX(x);
            ctx.ExtendY(y);
        }

        if (Fill == ScatterFill.ToZeroY && n > 0)
            ctx.ExtendY(0);

        XRaw = xs;
        YRaw = ys;
        ctx.SetCalc(this, null);
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (XRaw == null || YRaw == null || XRaw.Count == 0) return;

        // pixel points per data index; NaN breaks the line into segments (plotly.js behaviour)
        var all = new (Point p, bool ok)[XRaw.Count];
        for (var i = 0; i < XRaw.Count; i++)
        {
            var p = rc.ToPixels(XRaw[i], YRaw[i]);
            all[i] = (p, !double.IsNaN(p.X) && !double.IsNaN(p.Y));
        }

        var segments = new List<List<Point>>();
        var current = new List<Point>();
        foreach (var (p, ok) in all)
        {
            if (ok) current.Add(p);
            else if (current.Count > 0)
            {
                segments.Add(current);
                current = new List<Point>();
            }
        }

        if (current.Count > 0) segments.Add(current);

        PixelPoints = all.Where(a => a.ok).Select(a => a.p).ToList();

        var color = ResolvedColor;
        var opacity = Opacity;

        // fill
        if (Fill != ScatterFill.None)
        {
            foreach (var pts in segments)
            {
                if (pts.Count < 2) continue;
                var fill = new StreamGeometry();
                using (var fctx = fill.Open())
                {
                    fctx.BeginFigure(pts[0], true);
                    AppendLinePath(fctx, pts, Line.Shape);
                    if (Fill == ScatterFill.ToZeroY)
                    {
                        var zero = rc.YToPixels(rc.YAxis.EffectiveType == AxisType.Log ? 1 : 0);
                        zero = Math.Clamp(zero, rc.PlotRect.Top - 1000, rc.PlotRect.Bottom + 1000);
                        fctx.LineTo(new Point(pts[^1].X, zero));
                        fctx.LineTo(new Point(pts[0].X, zero));
                    }
                    else if (Fill == ScatterFill.ToNextY && rc.LastScatterPoints is { Count: > 1 } prev)
                    {
                        for (var i = prev.Count - 1; i >= 0; i--)
                            fctx.LineTo(prev[i]);
                    }

                    fctx.EndFigure(true);
                }

                var fc = FillColor ?? color;
                fc = Avalonia.Media.Color.FromArgb((byte)(fc.A * 0.5), fc.R, fc.G, fc.B);
                using (dc.PushOpacity(opacity))
                    dc.DrawGeometry(rc.Brush(fc), null, fill);
            }
        }

        // line
        if (HasLine)
        {
            var pen = rc.Pen(Line.Color ?? color, Line.Width, Line.DashStyle, opacity);
            foreach (var pts in segments)
            {
                if (pts.Count < 2) continue;
                dc.DrawGeometry(null, pen, BuildLineGeometry(pts, Line.Shape));
            }
        }

        rc.LastScatterPoints = PixelPoints;

        // markers
        if (HasMarkers)
        {
            using var _ = dc.PushOpacity(opacity * Marker.Opacity);
            for (var i = 0; i < all.Length; i++)
            {
                if (!all[i].ok) continue;
                var mc = Marker.PointColor(i, color);
                DrawMarker(dc, rc, all[i].p, mc);
            }
        }

        // text labels
        if (HasText && Text != null)
        {
            using var _ = dc.PushOpacity(opacity);
            for (var i = 0; i < all.Length && i < Text.Count; i++)
            {
                if (!all[i].ok || string.IsNullOrEmpty(Text[i])) continue;
                var ft = rc.Text(Text[i], rc.Theme.FontColor);
                var pos = TextOffset(all[i].p, ft, TextPosition, Marker.Size / 2 + 3);
                dc.DrawText(ft, pos);
            }
        }
    }

    internal void DrawMarker(DrawingContext dc, PlotRenderContext rc, Point p, Color color)
    {
        var geo = MarkerGeometry.Build(Marker.Symbol, p, Marker.Size);
        dc.DrawGeometry(rc.Brush(color), Marker.OutlineWidth > 0 ? rc.Pen(Marker.OutlineColor ?? Colors.White, Marker.OutlineWidth) : null, geo);
    }

    internal static Point TextOffset(Point p, FormattedText ft, TraceTextPosition pos, double pad)
        => pos switch
        {
            TraceTextPosition.TopCenter => new Point(p.X - ft.Width / 2, p.Y - pad - ft.Height),
            TraceTextPosition.TopLeft => new Point(p.X - pad - ft.Width, p.Y - pad - ft.Height),
            TraceTextPosition.TopRight => new Point(p.X + pad, p.Y - pad - ft.Height),
            TraceTextPosition.MiddleLeft => new Point(p.X - pad - ft.Width, p.Y - ft.Height / 2),
            TraceTextPosition.MiddleCenter => new Point(p.X - ft.Width / 2, p.Y - ft.Height / 2),
            TraceTextPosition.MiddleRight => new Point(p.X + pad, p.Y - ft.Height / 2),
            TraceTextPosition.BottomCenter => new Point(p.X - ft.Width / 2, p.Y + pad),
            TraceTextPosition.BottomLeft => new Point(p.X - pad - ft.Width, p.Y + pad),
            TraceTextPosition.BottomRight => new Point(p.X + pad, p.Y + pad),
            _ => new Point(p.X - ft.Width / 2, p.Y - pad - ft.Height)
        };

    internal static StreamGeometry BuildLineGeometry(IReadOnlyList<Point> pts, LineShape shape)
    {
        var geo = new StreamGeometry();
        using var ctx = geo.Open();
        ctx.BeginFigure(pts[0], false);
        AppendLinePath(ctx, pts, shape);
        ctx.EndFigure(false);
        return geo;
    }

    internal static void AppendLinePath(StreamGeometryContext ctx, IReadOnlyList<Point> pts, LineShape shape)
    {
        switch (shape)
        {
            case LineShape.Linear:
                for (var i = 1; i < pts.Count; i++) ctx.LineTo(pts[i]);
                break;
            case LineShape.Spline:
                AppendCatmullRom(ctx, pts);
                break;
            case LineShape.Hv:
                for (var i = 1; i < pts.Count; i++)
                {
                    ctx.LineTo(new Point(pts[i].X, pts[i - 1].Y));
                    ctx.LineTo(pts[i]);
                }

                break;
            case LineShape.Vh:
                for (var i = 1; i < pts.Count; i++)
                {
                    ctx.LineTo(new Point(pts[i - 1].X, pts[i].Y));
                    ctx.LineTo(pts[i]);
                }

                break;
            case LineShape.Hvh:
                for (var i = 1; i < pts.Count; i++)
                {
                    var midX = (pts[i - 1].X + pts[i].X) / 2;
                    ctx.LineTo(new Point(midX, pts[i - 1].Y));
                    ctx.LineTo(new Point(midX, pts[i].Y));
                    ctx.LineTo(pts[i]);
                }

                break;
            case LineShape.Vhv:
                for (var i = 1; i < pts.Count; i++)
                {
                    var midY = (pts[i - 1].Y + pts[i].Y) / 2;
                    ctx.LineTo(new Point(pts[i - 1].X, midY));
                    ctx.LineTo(new Point(pts[i].X, midY));
                    ctx.LineTo(pts[i]);
                }

                break;
        }
    }

    /// <summary>Catmull-Rom spline converted to cubic beziers.</summary>
    private static void AppendCatmullRom(StreamGeometryContext ctx, IReadOnlyList<Point> pts)
    {
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[Math.Max(0, i - 1)];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[Math.Min(pts.Count - 1, i + 2)];
            var c1 = new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);
            ctx.CubicBezierTo(c1, c2, p2);
        }
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (XRaw == null || YRaw == null || XRaw.Count == 0) yield break;

        if (mode is HoverMode.X or HoverMode.XUnified)
        {
            // nearest by x only
            var best = -1;
            var bestDist = double.PositiveInfinity;
            for (var i = 0; i < XRaw.Count; i++)
            {
                var px = rc.XToPixels(XRaw[i]);
                if (double.IsNaN(px)) continue;
                var d = Math.Abs(px - pt.X);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            if (best >= 0) yield return MakeTarget(rc, best, bestDist);
            yield break;
        }

        for (var i = 0; i < XRaw.Count; i++)
        {
            var p = rc.ToPixels(XRaw[i], YRaw[i]);
            if (double.IsNaN(p.X) || double.IsNaN(p.Y)) continue;
            var d = Math.Sqrt((p.X - pt.X) * (p.X - pt.X) + (p.Y - pt.Y) * (p.Y - pt.Y));
            if (d < 40)
                yield return MakeTarget(rc, i, d);
        }
    }

    private HoverTarget MakeTarget(PlotRenderContext rc, int i, double dist)
    {
        var p = rc.ToPixels(XRaw![i], YRaw![i]);
        return new HoverTarget
        {
            ScreenPoint = p,
            Trace = this,
            Color = Marker.PointColor(i, ResolvedColor),
            Title = Name,
            XText = rc.XAxis.FormatHover(XRaw[i]),
            YText = rc.YAxis.FormatHover(YRaw[i]),
            ExtraText = HoverText != null && i < HoverText.Count ? HoverText[i] : Text != null && i < Text.Count ? Text[i] : null,
            Distance = dist,
            Tag = i
        };
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (Name is { Length: > 0 } name && ShowLegend)
        {
            yield return new LegendItem
            {
                Label = name,
                Color = ResolvedColor,
                Trace = this,
                Symbol = HasMarkers ? Marker.Symbol : null,
                IsLine = HasLine
            };
        }
    }
}
