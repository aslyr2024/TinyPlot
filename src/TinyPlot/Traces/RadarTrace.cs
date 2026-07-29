using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 雷达图序列，对应 ECharts 的 radar 图类型。
/// 每个数据点对应一个维度轴上的值，多个维度组成多边形。
/// </summary>
public class RadarTrace : Trace
{
    /// <summary>各维度的值（与 RadarAxis.Indicators 对应）。</summary>
    public IReadOnlyList<double> Values { get; set; } = [];

    /// <summary>填充颜色（半透明）。</summary>
    public Color? FillColor { get; set; }

    /// <summary>线条宽度。</summary>
    public double LineWidth { get; set; } = 2;

    /// <summary>标记大小。</summary>
    public double MarkerSize { get; set; } = 5;

    internal override bool IsCartesian => false;

    internal override void Prepare(PlotCalcContext ctx) { ctx.SetCalc(this, null); }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Values.Count < 3) return;
        // 雷达图渲染在 Plot 中通过 RadarRenderer 统一处理
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (Name is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = Name, Color = ResolvedColor, Trace = this, IsLine = true };
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode) => [];
}

/// <summary>
/// 雷达图维度轴定义。
/// </summary>
public sealed class RadarAxis
{
    /// <summary>各维度指标名称。</summary>
    public IReadOnlyList<string> Indicators { get; set; } = [];

    /// <summary>各维度最大值（默认自动）。</summary>
    public IReadOnlyList<double>? MaxValues { get; set; }

    /// <summary>中心点（相对绘图区 0..1）。</summary>
    public Point Center { get; set; } = new(0.5, 0.55);

    /// <summary>半径比例（相对绘图区较短边，0..1）。</summary>
    public double RadiusRatio { get; set; } = 0.65;

    /// <summary>网格圈数。</summary>
    public int SplitNumber { get; set; } = 5;

    /// <summary>形状：polygon（多边形）或 circle。</summary>
    public string Shape { get; set; } = "polygon";
}

/// <summary>
/// 雷达图渲染器。负责绘制雷达网格和所有雷达序列。
/// </summary>
internal static class RadarRenderer
{
    internal static void Render(DrawingContext dc, PlotRenderContext rc, RadarAxis axis, List<RadarTrace> traces, Rect plotRect)
    {
        var n = axis.Indicators.Count;
        if (n < 3) return;

        var cx = plotRect.X + axis.Center.X * plotRect.Width;
        var cy = plotRect.Y + axis.Center.Y * plotRect.Height;
        var radius = Math.Min(plotRect.Width, plotRect.Height) * axis.RadiusRatio;
        var angles = new double[n];
        for (var i = 0; i < n; i++)
            angles[i] = -Math.PI / 2 + i * 2 * Math.PI / n;

        // 计算最大值
        var maxVal = 1.0;
        if (axis.MaxValues != null && axis.MaxValues.Count >= n)
            maxVal = axis.MaxValues.Max();
        else
            foreach (var t in traces)
                for (var i = 0; i < Math.Min(t.Values.Count, n); i++)
                    maxVal = Math.Max(maxVal, t.Values[i]);
        if (maxVal <= 0) maxVal = 1;

        var fontColor = rc.Layout.FontColor ?? rc.Theme.FontColor;
        var gridColor = rc.Theme.GridColor;
        var axisColor = rc.Theme.AxisLineColor;

        // 绘制网格圈
        for (var s = 1; s <= axis.SplitNumber; s++)
        {
            var r = radius * s / axis.SplitNumber;
            var pts = new Point[n];
            for (var i = 0; i < n; i++)
                pts[i] = new Point(cx + r * Math.Cos(angles[i]), cy + r * Math.Sin(angles[i]));

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(pts[0], false);
                for (var i = 1; i < n; i++) ctx.LineTo(pts[i]);
                ctx.EndFigure(true);
            }
            dc.DrawGeometry(null, rc.Pen(gridColor, 0.8), geo);
        }

        // 绘制轴线和标签
        for (var i = 0; i < n; i++)
        {
            var end = new Point(cx + radius * Math.Cos(angles[i]), cy + radius * Math.Sin(angles[i]));
            dc.DrawLine(rc.Pen(axisColor, 0.8, opacity: 0.5), new Point(cx, cy), end);

            // 维度名称
            var label = axis.Indicators[i];
            var ft = rc.Text(label, fontColor, rc.Layout.FontSize);
            var lx = cx + (radius + 14) * Math.Cos(angles[i]);
            var ly = cy + (radius + 14) * Math.Sin(angles[i]);
            var ha = Math.Cos(angles[i]);
            if (Math.Abs(ha) < 0.1) lx -= ft.Width / 2;
            else if (ha < 0) lx -= ft.Width;
            if (Math.Sin(angles[i]) < -0.5) ly -= ft.Height;
            dc.DrawText(ft, new Point(lx, ly));
        }

        // 绘制各雷达序列
        foreach (var trace in traces)
        {
            if (!trace.Visible || trace.Values.Count < n) continue;
            var color = trace.ResolvedColor;
            var fill = trace.FillColor ?? color;
            var fillA = Avalonia.Media.Color.FromArgb((byte)(fill.A * 0.3), fill.R, fill.G, fill.B);

            var pts = new Point[n];
            for (var i = 0; i < n; i++)
            {
                var v = Math.Clamp(trace.Values[i], 0, maxVal);
                var r = radius * v / maxVal;
                pts[i] = new Point(cx + r * Math.Cos(angles[i]), cy + r * Math.Sin(angles[i]));
            }

            // 填充
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(pts[0], true);
                for (var i = 1; i < n; i++) ctx.LineTo(pts[i]);
                ctx.EndFigure(true);
            }
            dc.DrawGeometry(rc.Brush(fillA), rc.Pen(color, trace.LineWidth), geo);

            // 标记点
            foreach (var p in pts)
                dc.DrawEllipse(rc.Brush(color), null, p, trace.MarkerSize / 2, trace.MarkerSize / 2);
        }
    }
}
