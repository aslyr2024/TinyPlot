using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

[Flags]
public enum PieTextInfo
{
    None = 0,
    Label = 1,
    Percent = 2,
    Value = 4,
    LabelPercent = Label | Percent
}

/// <summary>
/// Pie / donut chart, the counterpart of plotly.js type "pie".
/// </summary>
public class PieTrace : Trace
{
    public IReadOnlyList<string> Labels { get; set; } = [];

    public IReadOnlyList<double> Values { get; set; } = [];

    /// <summary>Donut hole fraction, 0..0.95.</summary>
    public double Hole { get; set; }

    /// <summary>Per-slice colors; defaults to the colorway.</summary>
    public IReadOnlyList<Color>? Colors { get; set; }

    public PieTextInfo TextInfo { get; set; } = PieTextInfo.LabelPercent;

    /// <summary>Rotation of the first slice in degrees (0 = 12 o'clock, clockwise).</summary>
    public double Rotation { get; set; }

    /// <summary>Sort slices by value, largest first (plotly.js default).</summary>
    public bool Sort { get; set; } = true;

    /// <summary>Pull all slices out by this fraction of the radius.</summary>
    public double Pull { get; set; }

    /// <summary>Slices hidden through the legend.</summary>
    public HashSet<string> HiddenLabels { get; } = new();

    internal override bool IsCartesian => false;

    internal PieCalc? Calc { get; private set; }

    internal override void Prepare(PlotCalcContext ctx)
    {
        var entries = new List<(string label, double value, int src)>();
        for (var i = 0; i < Math.Min(Labels.Count, Values.Count); i++)
        {
            var v = Values[i];
            if (v < 0 || double.IsNaN(v)) continue;
            if (HiddenLabels.Contains(Labels[i])) continue;
            entries.Add((Labels[i], v, i));
        }

        if (Sort) entries = entries.OrderByDescending(e => e.value).ToList();
        var total = entries.Sum(e => e.value);

        var slices = new List<PieSlice>(entries.Count);
        var angle = Rotation * Math.PI / 180 - Math.PI / 2;
        foreach (var (label, value, src) in entries)
        {
            var sweep = total > 0 ? value / total * Math.PI * 2 : 0;
            slices.Add(new PieSlice
            {
                Label = label,
                Value = value,
                SourceIndex = src,
                StartAngle = angle,
                Sweep = sweep,
                Fraction = total > 0 ? value / total : 0
            });
            angle += sweep;
        }

        Calc = new PieCalc { Slices = slices, Total = total };
        ctx.SetCalc(this, Calc);
    }

    internal Color SliceColor(PlotRenderContext rc, PieSlice slice)
        => Colors != null && slice.SourceIndex < Colors.Count
            ? Colors[slice.SourceIndex]
            : rc.Colorway[slice.SourceIndex % rc.Colorway.Count];

    internal override void Render(DrawingContext dc, PlotRenderContext rc) => RenderIn(dc, rc, rc.PlotRect);

    internal void RenderIn(DrawingContext dc, PlotRenderContext rc, Rect cell, int hoveredSlice = -1)
    {
        if (Calc is not { } calc || calc.Slices.Count == 0) return;

        var center = cell.Center;
        var radius = Math.Min(cell.Width, cell.Height) / 2 * rc.Layout.PieScale;
        calc.Center = center;
        calc.Radius = radius;
        calc.HoleRadius = radius * Math.Clamp(Hole, 0, 0.95);

        for (var i = 0; i < calc.Slices.Count; i++)
        {
            var slice = calc.Slices[i];
            if (slice.Sweep <= 0) continue;
            var color = SliceColor(rc, slice);
            var pull = Pull * radius;
            if (i == hoveredSlice) pull += 8;
            var mid = slice.StartAngle + slice.Sweep / 2;
            var c = new Point(center.X + pull * Math.Cos(mid), center.Y + pull * Math.Sin(mid));

            var geo = SliceGeometry(c, radius, slice.StartAngle, slice.Sweep);
            using (dc.PushOpacity(Opacity))
                dc.DrawGeometry(rc.Brush(color), new Pen(Brushes.White, 1), geo);

            // slice text
            if (TextInfo != PieTextInfo.None && slice.Sweep > 0.22)
            {
                var parts = new List<string>();
                if (TextInfo.HasFlag(PieTextInfo.Label)) parts.Add(slice.Label);
                if (TextInfo.HasFlag(PieTextInfo.Value)) parts.Add(PlotFmt.HoverValue(slice.Value));
                if (TextInfo.HasFlag(PieTextInfo.Percent)) parts.Add(slice.Fraction.ToString("0.0%"));
                if (parts.Count == 0) continue;
                var textR = calc.HoleRadius + (radius - calc.HoleRadius) * 0.62;
                if (calc.HoleRadius <= 0) textR = radius * 0.72;
                var tp = new Point(c.X + textR * Math.Cos(mid), c.Y + textR * Math.Sin(mid));
                var ft = rc.Text(string.Join('\n', parts), rc.ContrastColor(color), rc.Layout.FontSize, FontWeight.SemiBold);
                ft.TextAlignment = TextAlignment.Center;
                dc.DrawText(ft, new Point(tp.X - ft.Width / 2, tp.Y - ft.Height / 2));
            }
        }

        // donut hole
        if (calc.HoleRadius > 1)
        {
            var paper = rc.Layout.PaperBackground ?? rc.Theme.PaperBackground;
            dc.DrawEllipse(rc.Brush(paper), null, center, calc.HoleRadius, calc.HoleRadius);
        }
    }

    private static StreamGeometry SliceGeometry(Point center, double radius, double start, double sweep)
    {
        var geo = new StreamGeometry();
        using var ctx = geo.Open();
        var p0 = new Point(center.X + radius * Math.Cos(start), center.Y + radius * Math.Sin(start));
        var p1 = new Point(center.X + radius * Math.Cos(start + sweep), center.Y + radius * Math.Sin(start + sweep));
        ctx.BeginFigure(center, true);
        ctx.LineTo(p0);
        ctx.ArcTo(p1, new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);
        ctx.EndFigure(true);
        return geo;
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (Calc is not { } calc || calc.Radius <= 0) yield break;
        var dx = pt.X - calc.Center.X;
        var dy = pt.Y - calc.Center.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > calc.Radius || dist < calc.HoleRadius) yield break;

        var angle = Math.Atan2(dy, dx);
        for (var i = 0; i < calc.Slices.Count; i++)
        {
            var s = calc.Slices[i];
            var a = angle;
            while (a < s.StartAngle) a += Math.PI * 2;
            if (a >= s.StartAngle && a <= s.StartAngle + s.Sweep)
            {
                yield return new HoverTarget
                {
                    ScreenPoint = pt,
                    Trace = this,
                    Color = SliceColor(rc, s),
                    Title = s.Label,
                    XText = PlotFmt.HoverValue(s.Value),
                    YText = s.Fraction.ToString("0.0%"),
                    Distance = 0,
                    Tag = i
                };
                yield break;
            }
        }
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (!ShowLegend) yield break;
        for (var i = 0; i < Math.Min(Labels.Count, Values.Count); i++)
        {
            var label = Labels[i];
            yield return new LegendItem
            {
                Label = label,
                Color = Colors != null && i < Colors.Count ? Colors[i] : ResolvedColorAt(i),
                Trace = this,
                Tag = label,
                IsHidden = HiddenLabels.Contains(label)
            };
        }
    }

    private Color ResolvedColorAt(int i) => Colors != null && i < Colors.Count ? Colors[i] : _colorwayFallback(i);

    private Func<int, Color> _colorwayFallback = _ => Colors2.Transparent;

    internal void SetColorway(IReadOnlyList<Color> colorway) => _colorwayFallback = i => colorway[i % colorway.Count];

    internal sealed class PieCalc
    {
        public List<PieSlice> Slices { get; init; } = [];

        public double Total { get; init; }

        public Point Center { get; set; }

        public double Radius { get; set; }

        public double HoleRadius { get; set; }
    }

    internal sealed class PieSlice
    {
        public required string Label { get; init; }

        public double Value { get; init; }

        public int SourceIndex { get; init; }

        public double StartAngle { get; init; }

        public double Sweep { get; init; }

        public double Fraction { get; init; }
    }
}

internal static class Colors2
{
    public static Color Transparent => Color.FromArgb(0, 0, 0, 0);
}
