using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 3D 散点图序列。在 3D 空间中显示散点，通过旋转投影到 2D。
/// 对应 ECharts 的 scatter3D。
/// </summary>
public class Scatter3DTrace : Trace
{
    /// <summary>X 坐标数组。</summary>
    public DataSeries? X { get; set; }

    /// <summary>Y 坐标数组。</summary>
    public DataSeries? Y { get; set; }

    /// <summary>Z 坐标数组。</summary>
    public DataSeries? Z { get; set; }

    /// <summary>标记大小（像素）。</summary>
    public double MarkerSize { get; set; } = 6;

    /// <summary>标记颜色。</summary>
    public Color? MarkerColor { get; set; }

    /// <summary>按 Z 值着色的颜色比例尺。</summary>
    public Colorscale? Colorscale { get; set; }

    /// <summary>3D 旋转角度 X（度）。</summary>
    public double RotationX { get; set; } = 30;

    /// <summary>3D 旋转角度 Z（度）。</summary>
    public double RotationZ { get; set; } = -60;

    /// <summary>透视缩放因子。</summary>
    public double Scale { get; set; } = 0.6;

    internal override bool IsCartesian => false;
    internal override bool Is3D => true;

    private List<(Point p, double z, int index)>? _projected;

    internal override (DataSeries? x, DataSeries? y) GetAxesData() => (X, Y);

    internal override void Prepare(PlotCalcContext ctx)
    {
        ctx.SetCalc(this, null);
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (X == null || Y == null || Z == null) return;
        var n = Math.Min(X.Count, Math.Min(Y.Count, Z.Count));
        if (n == 0) return;

        var rect = rc.PlotRect;
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        var size = Math.Min(rect.Width, rect.Height) * Scale;

        // 计算 Z 范围
        var zmin = double.PositiveInfinity;
        var zmax = double.NegativeInfinity;
        for (var i = 0; i < n; i++)
        {
            var z = Z.AsNumber(i);
            if (!double.IsNaN(z)) { zmin = Math.Min(zmin, z); zmax = Math.Max(zmax, z); }
        }
        if (zmin == zmax) zmax = zmin + 1;

        var rotX = RotationX * Math.PI / 180;
        var rotZ = RotationZ * Math.PI / 180;

        _projected = [];
        var color = ResolvedColor;

        for (var i = 0; i < n; i++)
        {
            var x = X.AsNumber(i);
            var y = Y.AsNumber(i);
            var z = Z.AsNumber(i);
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z)) continue;

            // 归一化到 -0.5..0.5
            var nx = x; var ny = y; var nz = (z - zmin) / (zmax - zmin) - 0.5;

            // 旋转
            var x1 = nx * Math.Cos(rotZ) - ny * Math.Sin(rotZ);
            var y1 = nx * Math.Sin(rotZ) + ny * Math.Cos(rotZ);
            var z1 = nz;
            var y2 = y1 * Math.Cos(rotX) - z1 * Math.Sin(rotX);

            var px = cx + x1 * size;
            var py = cy - y2 * size;

            var c = color;
            if (Colorscale != null)
            {
                var t = (z - zmin) / (zmax - zmin);
                c = Colorscale.GetColor(t);
            }

            dc.DrawEllipse(rc.Brush(c, 0.8), rc.Pen(Colors.White, 0.5), new Point(px, py), MarkerSize / 2, MarkerSize / 2);
            _projected.Add((new Point(px, py), z, i));
        }
        // 绘制 3D 坐标轴
        SurfaceTrace.Draw3DAxes(dc, rc, cx, cy, size, rotX, rotZ,
            X?.AsNumber(0) ?? 0, X?.AsNumber(X.Count - 1) ?? 1,
            Y?.AsNumber(0) ?? 0, Y?.AsNumber(Y.Count - 1) ?? 1,
            zmin, zmax);
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (Name is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = Name, Color = ResolvedColor, Trace = this };
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (_projected == null || X == null || Y == null || Z == null) yield break;
        foreach (var (p, z, idx) in _projected)
        {
            var d = Math.Sqrt((p.X - pt.X) * (p.X - pt.X) + (p.Y - pt.Y) * (p.Y - pt.Y));
            if (d <= MarkerSize + 4)
            {
                yield return new HoverTarget
                {
                    ScreenPoint = p, Trace = this, Color = ResolvedColor,
                    Title = Name,
                    XText = $"x: {PlotFmt.HoverValue(X.AsNumber(idx))}",
                    YText = $"y: {PlotFmt.HoverValue(Y.AsNumber(idx))}",
                    ExtraText = $"z: {PlotFmt.HoverValue(z)}",
                    Distance = d,
                    Tag = idx
                };
            }
        }
    }
}
