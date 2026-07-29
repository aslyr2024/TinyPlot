using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 仪表盘样式。
/// </summary>
public enum GaugeStyle
{
    /// <summary>标准仪表盘（弧形+指针）。</summary>
    Standard,
    /// <summary>进度仪表盘（弧形填充，无指针）。</summary>
    Progress,
    /// <summary>得分环（圆形环，无刻度）。</summary>
    Ring,
    /// <summary>温度计样式（竖条）。</summary>
    Thermometer
}

/// <summary>
/// 仪表盘序列，对应 ECharts 的 gauge 图。
/// 支持基础仪表盘、速度仪表盘、进度仪表盘、得分环等。
/// </summary>
public class GaugeTrace : Trace
{
    /// <summary>当前值。</summary>
    public double Value { get; set; }

    /// <summary>最小值。</summary>
    public double Min { get; set; } = 0;

    /// <summary>最大值。</summary>
    public double Max { get; set; } = 100;

    /// <summary>仪表盘标题。</summary>
    public string? Title { get; set; }

    /// <summary>单位文本。</summary>
    public string? Unit { get; set; }

    /// <summary>仪表盘样式。</summary>
    public GaugeStyle Style { get; set; } = GaugeStyle.Standard;

    /// <summary>弧线起始角度（度，0=3点钟方向，-90=12点钟）。</summary>
    public double StartAngle { get; set; } = 225;

    /// <summary>弧线结束角度（度）。</summary>
    public double EndAngle { get; set; } = -45;

    /// <summary>弧线宽度（半径比例 0..1）。</summary>
    public double ArcWidth { get; set; } = 0.12;

    /// <summary>分段颜色（渐变色带）。</summary>
    public IReadOnlyList<(double threshold, Color color)>? Segments { get; set; }

    /// <summary>指针颜色。</summary>
    public Color? NeedleColor { get; set; }

    /// <summary>是否显示刻度。</summary>
    public bool ShowTickLabels { get; set; } = true;

    /// <summary>中心位置（相对绘图区 0..1）。</summary>
    public Point Center { get; set; } = new(0.5, 0.55);

    /// <summary>半径比例（相对绘图区较短边）。</summary>
    public double RadiusRatio { get; set; } = 0.7;

    internal override bool IsCartesian => false;

    internal override void Prepare(PlotCalcContext ctx) { ctx.SetCalc(this, null); }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        var rect = rc.PlotRect;
        var cx = rect.X + Center.X * rect.Width;
        var cy = rect.Y + Center.Y * rect.Height;
        var radius = Math.Min(rect.Width, rect.Height) * RadiusRatio;

        var startRad = StartAngle * Math.PI / 180;
        var endRad = EndAngle * Math.PI / 180;
        var totalSweep = startRad - endRad;
        if (totalSweep <= 0) totalSweep += 2 * Math.PI;

        var arcR = radius * 0.85;
        var innerR = arcR * (1 - ArcWidth);
        var fontColor = rc.Layout.FontColor ?? rc.Theme.FontColor;
        var color = ResolvedColor;
        var frac = Math.Clamp((Value - Min) / (Max - Min), 0, 1);

        // 默认分段颜色（ECharts 风格：绿→黄→红）
        var segments = Segments ?? new List<(double threshold, Color color)>
        {
            (0.3, Avalonia.Media.Color.Parse("#91cc75")),
            (0.7, Avalonia.Media.Color.Parse("#fac858")),
            (1.0, Avalonia.Media.Color.Parse("#ee6666"))
        };

        // 背景弧（半透明灰色）
        DrawArc(dc, rc, cx, cy, arcR, innerR, startRad, -totalSweep, rc.Theme.GridColor, 0.25);

        // 分段彩色弧
        if (Style == GaugeStyle.Progress || Style == GaugeStyle.Ring)
        {
            var filledSweep = totalSweep * frac;
            var segColor = color;
            foreach (var seg in segments) { if (frac <= seg.threshold) { segColor = seg.color; break; } }
            DrawArc(dc, rc, cx, cy, arcR, innerR, startRad, -filledSweep, segColor, 0.9);
        }
        else
        {
            // 标准仪表盘：渐变色带
            var prevAngle = startRad;
            var prevFrac = 0.0;
            foreach (var seg in segments)
            {
                var sweep = totalSweep * (seg.threshold - prevFrac);
                var alpha = frac >= prevFrac ? 0.9 : 0.25;
                DrawArc(dc, rc, cx, cy, arcR, innerR, prevAngle, -sweep, seg.color, alpha);
                prevAngle -= sweep;
                prevFrac = seg.threshold;
            }
        }

        // 刻度线和数值标签
        if (ShowTickLabels && Style != GaugeStyle.Ring)
        {
            var tickCount = 10;
            var labelFontSize = Math.Max(10, rc.Layout.FontSize - 1);
            for (var i = 0; i <= tickCount; i++)
            {
                var t = (double)i / tickCount;
                var angle = startRad - t * totalSweep;
                var isMajor = i % 5 == 0;
                var tickLen = isMajor ? 12 : 6;
                var x1 = cx + (arcR + 4) * Math.Cos(angle);
                var y1 = cy - (arcR + 4) * Math.Sin(angle);
                var x2 = cx + (arcR + 4 + tickLen) * Math.Cos(angle);
                var y2 = cy - (arcR + 4 + tickLen) * Math.Sin(angle);
                dc.DrawLine(rc.Pen(fontColor, isMajor ? 2 : 1, opacity: 0.7), new Point(x1, y1), new Point(x2, y2));

                if (isMajor)
                {
                    var val = Min + t * (Max - Min);
                    var label = PlotFmt.Number(val);
                    var ft = rc.Text(label, fontColor, labelFontSize, FontWeight.Medium);
                    var labelR = arcR + 22;
                    var lx = cx + labelR * Math.Cos(angle);
                    var ly = cy - labelR * Math.Sin(angle);
                    // 根据角度调整对齐方式
                    var ha = Math.Cos(angle);
                    if (Math.Abs(ha) < 0.15) lx -= ft.Width / 2;
                    else if (ha < 0) lx -= ft.Width;
                    if (Math.Sin(angle) < -0.5) ly -= ft.Height;
                    dc.DrawText(ft, new Point(lx, ly - ft.Height / 2));
                }
            }

            // 在弧线两端额外标注 Min 和 Max（确保可见）
            var minFt = rc.Text(PlotFmt.Number(Min), fontColor, labelFontSize, FontWeight.Bold);
            var maxFt = rc.Text(PlotFmt.Number(Max), fontColor, labelFontSize, FontWeight.Bold);
            var endR = arcR + 22;
            // Min 在起始端
            var minLx = cx + endR * Math.Cos(startRad);
            var minLy = cy - endR * Math.Sin(startRad);
            dc.DrawText(minFt, new Point(minLx - minFt.Width / 2, minLy - minFt.Height / 2));
            // Max 在结束端
            var maxLx = cx + endR * Math.Cos(endRad);
            var maxLy = cy - endR * Math.Sin(endRad);
            dc.DrawText(maxFt, new Point(maxLx - maxFt.Width / 2, maxLy - maxFt.Height / 2));
        }

        // 指针
        if (Style == GaugeStyle.Standard)
        {
            var needleAngle = startRad - frac * totalSweep;
            var needleLen = arcR * 0.8;
            var needleColor = NeedleColor ?? Avalonia.Media.Color.Parse("#333333");

            // 指针阴影
            var shadowOffset = 2;
            dc.DrawLine(rc.Pen(Avalonia.Media.Color.FromArgb(40, 0, 0, 0), 4),
                new Point(cx + shadowOffset, cy + shadowOffset),
                new Point(cx + needleLen * Math.Cos(needleAngle) + shadowOffset, cy - needleLen * Math.Sin(needleAngle) + shadowOffset));

            // 指针主体
            dc.DrawLine(rc.Pen(needleColor, 3.5),
                new Point(cx, cy),
                new Point(cx + needleLen * Math.Cos(needleAngle), cy - needleLen * Math.Sin(needleAngle)));

            // 中心装饰圆
            dc.DrawEllipse(rc.Brush(needleColor), rc.Pen(Colors.White, 2), new Point(cx, cy), 6, 6);
        }

        // 值文本（大字号，居中）
        var valueText = PlotFmt.Number(Value);
        var valueFt = rc.Text(valueText, fontColor, rc.Layout.FontSize + 10, FontWeight.Bold);
        dc.DrawText(valueFt, new Point(cx - valueFt.Width / 2, cy + radius * 0.15));

        // 单位文本
        if (Unit is { Length: > 0 })
        {
            var unitFt = rc.Text(Unit, fontColor, rc.Layout.FontSize, FontWeight.Normal);
            dc.DrawText(unitFt, new Point(cx - unitFt.Width / 2, cy + radius * 0.15 + valueFt.Height + 2));
        }

        // 标题（在值下方）
        if (Title is { Length: > 0 })
        {
            var titleFt = rc.Text(Title, fontColor, rc.Layout.FontSize + 1, FontWeight.Medium);
            dc.DrawText(titleFt, new Point(cx - titleFt.Width / 2, cy + radius * 0.45));
        }
    }

    private static void DrawArc(DrawingContext dc, PlotRenderContext rc, double cx, double cy,
        double outerR, double innerR, double startAngle, double sweepAngle, Color color, double opacity)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var p0 = new Point(cx + outerR * Math.Cos(startAngle), cy - outerR * Math.Sin(startAngle));
            ctx.BeginFigure(p0, true);
            ctx.ArcTo(
                new Point(cx + outerR * Math.Cos(startAngle + sweepAngle), cy - outerR * Math.Sin(startAngle + sweepAngle)),
                new Size(outerR, outerR), 0, Math.Abs(sweepAngle) > Math.PI, sweepAngle < 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
            ctx.LineTo(new Point(cx + innerR * Math.Cos(startAngle + sweepAngle), cy - innerR * Math.Sin(startAngle + sweepAngle)));
            ctx.ArcTo(
                new Point(cx + innerR * Math.Cos(startAngle), cy - innerR * Math.Sin(startAngle)),
                new Size(innerR, innerR), 0, Math.Abs(sweepAngle) > Math.PI, sweepAngle < 0 ? SweepDirection.CounterClockwise : SweepDirection.Clockwise);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(rc.Brush(color, opacity), null, geo);
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (Title is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = Title, Color = ResolvedColor, Trace = this };
        else if (Name is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = Name, Color = ResolvedColor, Trace = this };
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        var rect = rc.PlotRect;
        var cx = rect.X + Center.X * rect.Width;
        var cy = rect.Y + Center.Y * rect.Height;
        var radius = Math.Min(rect.Width, rect.Height) * RadiusRatio;
        var dx = pt.X - cx; var dy = pt.Y - cy;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist <= radius * 1.1)
        {
            yield return new HoverTarget
            {
                ScreenPoint = pt, Trace = this, Color = ResolvedColor,
                Title = Title ?? Name,
                XText = $"值: {PlotFmt.Number(Value)}{Unit ?? ""}",
                YText = $"范围: {PlotFmt.Number(Min)} ~ {PlotFmt.Number(Max)}",
                Distance = dist
            };
        }
    }
}
