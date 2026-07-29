using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 函数绘图序列。通过数学函数 y=f(x) 生成曲线数据，
/// 对应 ECharts 的"函数绘图 Function Plot"示例。
/// 支持 sin(x)、cos(x) 等任意函数，自动在指定范围内采样。
/// </summary>
public class FunctionTrace : Trace
{
    /// <summary>函数 y = f(x)。</summary>
    public Func<double, double>? Function { get; set; }

    /// <summary>X 范围起始值。</summary>
    public double XMin { get; set; } = -10;

    /// <summary>X 范围结束值。</summary>
    public double XMax { get; set; } = 10;

    /// <summary>采样点数（默认 1000，足够平滑）。</summary>
    public int SampleCount { get; set; } = 1000;

    /// <summary>折线样式。</summary>
    public LineOptions Line { get; } = new();

    /// <summary>标记样式。</summary>
    public Marker Marker { get; } = new();

    /// <summary>散点模式（默认仅线条）。</summary>
    public ScatterMode Mode { get; set; } = ScatterMode.Lines;

    /// <summary>填充模式。</summary>
    public ScatterFill Fill { get; set; } = ScatterFill.None;

    /// <summary>填充颜色。</summary>
    public Color? FillColor { get; set; }

    /// <summary>函数表达式文本标签（如 "sin(x)"）。</summary>
    public string? FormulaLabel { get; set; }

    private List<double>? _xs;
    private List<double>? _ys;

    internal override (DataSeries? x, DataSeries? y) GetAxesData() => (null, null);

    internal override void Prepare(PlotCalcContext ctx)
    {
        if (Function == null) { _xs = _ys = []; return; }

        var n = Math.Max(10, SampleCount);
        var xs = new List<double>(n);
        var ys = new List<double>(n);
        var step = (XMax - XMin) / (n - 1);

        for (var i = 0; i < n; i++)
        {
            var x = XMin + i * step;
            var y = Function(x);
            if (double.IsNaN(y) || double.IsInfinity(y)) continue;
            xs.Add(x);
            ys.Add(y);
            ctx.ExtendX(x);
            ctx.ExtendY(y);
        }

        _xs = xs;
        _ys = ys;
        ctx.SetCalc(this, null);
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (_xs == null || _ys == null || _xs.Count == 0) return;

        var pts = new List<Point>(_xs.Count);
        for (var i = 0; i < _xs.Count; i++)
        {
            var p = rc.ToPixels(_xs[i], _ys[i]);
            if (!double.IsNaN(p.X) && !double.IsNaN(p.Y)) pts.Add(p);
        }

        if (pts.Count < 2) return;

        var color = ResolvedColor;
        var opacity = Opacity;

        // 填充
        if (Fill != ScatterFill.None)
        {
            var fill = new StreamGeometry();
            using (var fctx = fill.Open())
            {
                fctx.BeginFigure(pts[0], true);
                for (var i = 1; i < pts.Count; i++) fctx.LineTo(pts[i]);
                if (Fill == ScatterFill.ToZeroY)
                {
                    var zero = rc.YToPixels(0);
                    fctx.LineTo(new Point(pts[^1].X, zero));
                    fctx.LineTo(new Point(pts[0].X, zero));
                }
                fctx.EndFigure(true);
            }
            var fc = FillColor ?? color;
            fc = Avalonia.Media.Color.FromArgb((byte)(fc.A * 0.4), fc.R, fc.G, fc.B);
            dc.DrawGeometry(rc.Brush(fc), null, fill);
        }

        // 线条
        if ((Mode & ScatterMode.Lines) != 0)
        {
            var geo = ScatterTrace.BuildLineGeometry(pts, Line.Shape);
            var pen = rc.Pen(Line.Color ?? color, Line.Width, Line.DashStyle, opacity);
            dc.DrawGeometry(null, pen, geo);
        }

        // 标记
        if ((Mode & ScatterMode.Markers) != 0)
        {
            foreach (var p in pts)
            {
                var geo = MarkerGeometry.Build(Marker.Symbol, p, Marker.Size);
                dc.DrawGeometry(rc.Brush(Marker.Color ?? color), null, geo);
            }
        }
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (FormulaLabel is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = FormulaLabel, Color = ResolvedColor, Trace = this, IsLine = true };
        else if (Name is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = Name, Color = ResolvedColor, Trace = this, IsLine = true };
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (_xs == null || _ys == null) yield break;

        if (mode is HoverMode.X or HoverMode.XUnified)
        {
            var best = -1;
            var bestD = double.PositiveInfinity;
            for (var i = 0; i < _xs.Count; i++)
            {
                var px = rc.XToPixels(_xs[i]);
                if (double.IsNaN(px)) continue;
                var d = Math.Abs(px - pt.X);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0) yield return MakeTarget(rc, best, bestD);
            yield break;
        }

        for (var i = 0; i < _xs.Count; i++)
        {
            var p = rc.ToPixels(_xs[i], _ys[i]);
            if (double.IsNaN(p.X) || double.IsNaN(p.Y)) continue;
            var d = Math.Sqrt((p.X - pt.X) * (p.X - pt.X) + (p.Y - pt.Y) * (p.Y - pt.Y));
            if (d < 30) yield return MakeTarget(rc, i, d);
        }
    }

    private HoverTarget MakeTarget(PlotRenderContext rc, int i, double dist)
    {
        var p = rc.ToPixels(_xs![i], _ys![i]);
        return new HoverTarget
        {
            ScreenPoint = p, Trace = this, Color = ResolvedColor,
            Title = Name ?? FormulaLabel,
            XText = rc.XAxis.FormatHover(_xs[i]),
            YText = rc.YAxis.FormatHover(_ys[i]),
            Distance = dist, Tag = i
        };
    }
}
