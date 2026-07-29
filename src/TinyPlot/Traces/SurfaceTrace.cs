using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 3D 曲面图序列。通过 z = f(x, y) 函数生成曲面数据，
/// 使用等距投影渲染为 2D 热力图样式的表面。
/// 对应 ECharts 的 surface3D。
/// </summary>
public class SurfaceTrace : Trace
{
    /// <summary>曲面函数 z = f(x, y)。</summary>
    public Func<double, double, double>? Function { get; set; }

    /// <summary>X 范围。</summary>
    public double XMin { get; set; } = -5;
    public double XMax { get; set; } = 5;

    /// <summary>Y 范围。</summary>
    public double YMin { get; set; } = -5;
    public double YMax { get; set; } = 5;

    /// <summary>采样分辨率。</summary>
    public int Resolution { get; set; } = 50;

    /// <summary>颜色比例尺。</summary>
    public Colorscale Colorscale { get; set; } = Colorscale.Viridis;

    /// <summary>3D 旋转角度 X（度）。</summary>
    public double RotationX { get; set; } = 30;

    /// <summary>3D 旋转角度 Z（度）。</summary>
    public double RotationZ { get; set; } = -60;

    /// <summary>透视缩放因子。</summary>
    public double Scale { get; set; } = 0.7;

    /// <summary>显式 Z 范围（NaN=自动）。</summary>
    public double ZMin { get; set; } = double.NaN;
    public double ZMax { get; set; } = double.NaN;

    internal override bool IsCartesian => false;
    internal override bool Is3D => true;

    private List<(Point p, double z, int xi, int yi)>? _projected;

    internal override void Prepare(PlotCalcContext ctx) { ctx.SetCalc(this, null); }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Function == null) return;

        var n = Resolution;
        var data = new double[n, n];
        var zmin = double.PositiveInfinity;
        var zmax = double.NegativeInfinity;

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var x = XMin + (XMax - XMin) * i / (n - 1);
                var y = YMin + (YMax - YMin) * j / (n - 1);
                var z = Function(x, y);
                data[i, j] = z;
                if (!double.IsNaN(z) && !double.IsInfinity(z))
                {
                    zmin = Math.Min(zmin, z);
                    zmax = Math.Max(zmax, z);
                }
            }
        }

        if (!double.IsNaN(ZMin)) zmin = ZMin;
        if (!double.IsNaN(ZMax)) zmax = ZMax;
        if (zmin == zmax) zmax = zmin + 1;

        var rect = rc.PlotRect;
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        var size = Math.Min(rect.Width, rect.Height) * Scale;

        var rotX = RotationX * Math.PI / 180;
        var rotZ = RotationZ * Math.PI / 180;

        // 投影到 2D
        _projected = [];
        var projectedGrid = new Point[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var nx = (double)i / (n - 1) - 0.5;
                var ny = (double)j / (n - 1) - 0.5;
                var nz = (data[i, j] - zmin) / (zmax - zmin) - 0.5;

                // 旋转
                var x1 = nx * Math.Cos(rotZ) - ny * Math.Sin(rotZ);
                var y1 = nx * Math.Sin(rotZ) + ny * Math.Cos(rotZ);
                var z1 = nz;

                var y2 = y1 * Math.Cos(rotX) - z1 * Math.Sin(rotX);
                var z2 = y1 * Math.Sin(rotX) + z1 * Math.Cos(rotX);

                var px = cx + x1 * size;
                var py = cy - y2 * size;

                projectedGrid[i, j] = new Point(px, py);
                _projected.Add((new Point(px, py), data[i, j], i, j));
            }
        }

        // 绘制面片（从后到前排序）
        for (var i = 0; i < n - 1; i++)
        {
            for (var j = 0; j < n - 1; j++)
            {
                var p0 = projectedGrid[i, j];
                var p1 = projectedGrid[i + 1, j];
                var p2 = projectedGrid[i + 1, j + 1];
                var p3 = projectedGrid[i, j + 1];

                var avgZ = (data[i, j] + data[i + 1, j] + data[i + 1, j + 1] + data[i, j + 1]) / 4;
                var t = (avgZ - zmin) / (zmax - zmin);
                var color = Colorscale.GetColor(t);

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(p0, true);
                    ctx.LineTo(p1);
                    ctx.LineTo(p2);
                    ctx.LineTo(p3);
                    ctx.EndFigure(true);
                }
                dc.DrawGeometry(rc.Brush(color, 0.85), rc.Pen(color, 0.3), geo);
            }
        }

        // 绘制 3D 坐标轴
        Draw3DAxes(dc, rc, cx, cy, size, rotX, rotZ, XMin, XMax, YMin, YMax, zmin, zmax);
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (Name is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = Name, Color = ResolvedColor, Trace = this };
    }

    /// <summary>绘制 3D 坐标轴（X/Y/Z 轴线 + 端点标签）。</summary>
    internal static void Draw3DAxes(DrawingContext dc, PlotRenderContext rc,
        double cx, double cy, double size, double rotX, double rotZ,
        double xMin, double xMax, double yMin, double yMax, double zMin, double zMax)
    {
        var fontColor = rc.Layout.FontColor ?? rc.Theme.FontColor;
        var axisColor = rc.Theme.AxisLineColor;
        var axisPen = rc.Pen(axisColor, 1.5, opacity: 0.7);
        var gridPen = rc.Pen(rc.Theme.GridColor, 0.5, opacity: 0.3);

        // 投影函数
        Point Project(double nx, double ny, double nz)
        {
            var x1 = nx * Math.Cos(rotZ) - ny * Math.Sin(rotZ);
            var y1 = nx * Math.Sin(rotZ) + ny * Math.Cos(rotZ);
            var y2 = y1 * Math.Cos(rotX) - nz * Math.Sin(rotX);
            return new Point(cx + x1 * size, cy - y2 * size);
        }

        // 原点和三个轴端点（归一化空间 -0.5..0.5）
        var origin = Project(0, 0, 0);
        var xEnd = Project(0.5, 0, 0);
        var yEnd = Project(0, 0.5, 0);
        var zEnd = Project(0, 0, 0.5);

        // 绘制轴线
        dc.DrawLine(axisPen, origin, xEnd);
        dc.DrawLine(axisPen, origin, yEnd);
        dc.DrawLine(axisPen, origin, zEnd);

        // 绘制网格线（每个轴 5 条辅助线）
        for (var i = 1; i <= 4; i++)
        {
            var t = i * 0.125;
            // X 方向网格
            dc.DrawLine(gridPen, Project(t - 0.5, -0.5, -0.5), Project(t - 0.5, 0.5, -0.5));
            dc.DrawLine(gridPen, Project(t - 0.5, -0.5, -0.5), Project(t - 0.5, -0.5, 0.5));
            // Y 方向网格
            dc.DrawLine(gridPen, Project(-0.5, t - 0.5, -0.5), Project(0.5, t - 0.5, -0.5));
            dc.DrawLine(gridPen, Project(-0.5, t - 0.5, -0.5), Project(-0.5, t - 0.5, 0.5));
            // Z 方向网格
            dc.DrawLine(gridPen, Project(-0.5, -0.5, t - 0.5), Project(0.5, -0.5, t - 0.5));
            dc.DrawLine(gridPen, Project(-0.5, -0.5, t - 0.5), Project(-0.5, 0.5, t - 0.5));
        }

        // 轴标签
        var fontSize = Math.Max(9, rc.Layout.FontSize - 2);
        var xLabel = rc.Text("X", fontColor, fontSize, FontWeight.Bold);
        var yLabel = rc.Text("Y", fontColor, fontSize, FontWeight.Bold);
        var zLabel = rc.Text("Z", fontColor, fontSize, FontWeight.Bold);
        dc.DrawText(xLabel, new Point(xEnd.X + 4, xEnd.Y - xLabel.Height / 2));
        dc.DrawText(yLabel, new Point(yEnd.X + 4, yEnd.Y - yLabel.Height / 2));
        dc.DrawText(zLabel, new Point(zEnd.X + 4, zEnd.Y - zLabel.Height));

        // 端点数值标签
        var valFontSize = Math.Max(8, rc.Layout.FontSize - 3);
        var xMinLabel = rc.Text(PlotFmt.Number(xMin), fontColor, valFontSize);
        var xMaxLabel = rc.Text(PlotFmt.Number(xMax), fontColor, valFontSize);
        var yMinLabel = rc.Text(PlotFmt.Number(yMin), fontColor, valFontSize);
        var yMaxLabel = rc.Text(PlotFmt.Number(yMax), fontColor, valFontSize);
        var zMinLabel = rc.Text(PlotFmt.Number(zMin), fontColor, valFontSize);
        var zMaxLabel = rc.Text(PlotFmt.Number(zMax), fontColor, valFontSize);

        var xMinPt = Project(-0.5, 0, 0);
        var yMinPt = Project(0, -0.5, 0);
        var zMinPt = Project(0, 0, -0.5);
        dc.DrawText(xMinLabel, new Point(xMinPt.X - xMinLabel.Width - 2, xMinPt.Y + 2));
        dc.DrawText(xMaxLabel, new Point(xEnd.X + 2, xEnd.Y + 2));
        dc.DrawText(yMinLabel, new Point(yMinPt.X - yMinLabel.Width - 2, yMinPt.Y + 2));
        dc.DrawText(yMaxLabel, new Point(yEnd.X + 2, yEnd.Y + 2));
        dc.DrawText(zMinLabel, new Point(zMinPt.X - zMinLabel.Width - 2, zMinPt.Y));
        dc.DrawText(zMaxLabel, new Point(zEnd.X + 2, zEnd.Y - zMaxLabel.Height));
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (_projected == null) yield break;
        var best = (p: default(Point), z: 0.0, xi: 0, yi: 0, d: double.MaxValue);
        foreach (var (p, z, xi, yi) in _projected)
        {
            var d = Math.Sqrt((p.X - pt.X) * (p.X - pt.X) + (p.Y - pt.Y) * (p.Y - pt.Y));
            if (d < best.d) best = (p, z, xi, yi, d);
        }
        if (best.d < 20)
        {
            yield return new HoverTarget
            {
                ScreenPoint = best.p, Trace = this, Color = ResolvedColor,
                Title = Name,
                XText = $"x: {PlotFmt.Number(XMin + (XMax - XMin) * best.xi / (Resolution - 1))}",
                YText = $"y: {PlotFmt.Number(YMin + (YMax - YMin) * best.yi / (Resolution - 1))}",
                ExtraText = $"z: {PlotFmt.Number(best.z)}",
                Distance = best.d
            };
        }
    }
}
