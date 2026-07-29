using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>Marker styling (trace.marker).</summary>
public sealed class Marker
{
    public Color? Color { get; set; }

    /// <summary>Per-point colors (takes precedence over <see cref="Color"/>).</summary>
    public IReadOnlyList<Color>? Colors { get; set; }

    /// <summary>Per-point values mapped through <see cref="Colorscale"/>.</summary>
    public IReadOnlyList<double>? ColorValues { get; set; }

    public Colorscale? Colorscale { get; set; }

    /// <summary>Diameter in pixels (plotly.js marker.size).</summary>
    public double Size { get; set; } = 6;

    public MarkerSymbol Symbol { get; set; } = MarkerSymbol.Circle;

    public Color? OutlineColor { get; set; }

    public double OutlineWidth { get; set; }

    public double Opacity { get; set; } = 1.0;

    internal Color PointColor(int index, Color fallback)
    {
        if (Colors != null && index < Colors.Count) return Colors[index];
        if (ColorValues != null && index < ColorValues.Count)
        {
            var cs = Colorscale ?? TinyPlot.Colorscale.Viridis;
            var vals = ColorValues;
            var min = double.PositiveInfinity;
            var max = double.NegativeInfinity;
            foreach (var v in vals)
            {
                if (double.IsNaN(v)) continue;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }

            var t = max > min ? (vals[index] - min) / (max - min) : 0.5;
            return cs.GetColor(t);
        }

        return Color ?? fallback;
    }
}

/// <summary>Line styling (trace.line).</summary>
public sealed class LineOptions
{
    public Color? Color { get; set; }

    public double Width { get; set; } = 2;

    public LineShape Shape { get; set; } = LineShape.Linear;

    /// <summary>Dash pattern in pixels, e.g. [4,2] for dash, [1,2] for dot.</summary>
    public IReadOnlyList<double>? Dash { get; set; }

    internal DashStyle? DashStyle => Dash is { Count: > 0 } d ? new DashStyle(d, 0) : null;
}

/// <summary>Bar fill styling (trace.marker for bars).</summary>
public sealed class BarMarker
{
    public Color? Color { get; set; }

    /// <summary>Per-bar colors.</summary>
    public IReadOnlyList<Color>? Colors { get; set; }

    public Color? LineColor { get; set; }

    public double LineWidth { get; set; }

    public double Opacity { get; set; } = 1.0;

    internal Color BarColor(int index, Color fallback)
        => Colors != null && index < Colors.Count ? Colors[index] : Color ?? fallback;
}

/// <summary>Static geometry builders shared by traces.</summary>
internal static class MarkerGeometry
{
    public static StreamGeometry Build(MarkerSymbol symbol, Point center, double diameter)
    {
        var r = diameter / 2;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            switch (symbol)
            {
                case MarkerSymbol.Circle:
                    ctx.BeginFigure(new Point(center.X + r, center.Y), true);
                    ctx.ArcTo(center, new Size(r, r), 0, true, SweepDirection.Clockwise);
                    ctx.EndFigure(true);
                    break;
                case MarkerSymbol.Square:
                    ctx.BeginFigure(new Point(center.X - r, center.Y - r), true);
                    ctx.LineTo(new Point(center.X + r, center.Y - r));
                    ctx.LineTo(new Point(center.X + r, center.Y + r));
                    ctx.LineTo(new Point(center.X - r, center.Y + r));
                    ctx.EndFigure(true);
                    break;
                case MarkerSymbol.Diamond:
                    ctx.BeginFigure(new Point(center.X, center.Y - r), true);
                    ctx.LineTo(new Point(center.X + r, center.Y));
                    ctx.LineTo(new Point(center.X, center.Y + r));
                    ctx.LineTo(new Point(center.X - r, center.Y));
                    ctx.EndFigure(true);
                    break;
                case MarkerSymbol.Cross:
                {
                    var a = r * 0.45;
                    ctx.BeginFigure(new Point(center.X - a, center.Y - r), true);
                    ctx.LineTo(new Point(center.X + a, center.Y - r));
                    ctx.LineTo(new Point(center.X + a, center.Y - a));
                    ctx.LineTo(new Point(center.X + r, center.Y - a));
                    ctx.LineTo(new Point(center.X + r, center.Y + a));
                    ctx.LineTo(new Point(center.X + a, center.Y + a));
                    ctx.LineTo(new Point(center.X + a, center.Y + r));
                    ctx.LineTo(new Point(center.X - a, center.Y + r));
                    ctx.LineTo(new Point(center.X - a, center.Y + a));
                    ctx.LineTo(new Point(center.X - r, center.Y + a));
                    ctx.LineTo(new Point(center.X - r, center.Y - a));
                    ctx.LineTo(new Point(center.X - a, center.Y - a));
                    ctx.EndFigure(true);
                    break;
                }
                case MarkerSymbol.X:
                {
                    var a = r * 0.35;
                    ctx.BeginFigure(new Point(center.X - r, center.Y - r + a), true);
                    ctx.LineTo(new Point(center.X - a, center.Y));
                    ctx.LineTo(new Point(center.X - r, center.Y + r - a));
                    ctx.LineTo(new Point(center.X - r + a, center.Y + r));
                    ctx.LineTo(new Point(center.X, center.Y + a));
                    ctx.LineTo(new Point(center.X + r - a, center.Y + r));
                    ctx.LineTo(new Point(center.X + r, center.Y + r - a));
                    ctx.LineTo(new Point(center.X + a, center.Y));
                    ctx.LineTo(new Point(center.X + r, center.Y - r + a));
                    ctx.LineTo(new Point(center.X + r - a, center.Y - r));
                    ctx.LineTo(new Point(center.X, center.Y - a));
                    ctx.LineTo(new Point(center.X - r + a, center.Y - r));
                    ctx.EndFigure(true);
                    break;
                }
                case MarkerSymbol.TriangleUp:
                    ctx.BeginFigure(new Point(center.X, center.Y - r), true);
                    ctx.LineTo(new Point(center.X + r, center.Y + r * 0.8));
                    ctx.LineTo(new Point(center.X - r, center.Y + r * 0.8));
                    ctx.EndFigure(true);
                    break;
                case MarkerSymbol.TriangleDown:
                    ctx.BeginFigure(new Point(center.X, center.Y + r), true);
                    ctx.LineTo(new Point(center.X + r, center.Y - r * 0.8));
                    ctx.LineTo(new Point(center.X - r, center.Y - r * 0.8));
                    ctx.EndFigure(true);
                    break;
                case MarkerSymbol.Star:
                case MarkerSymbol.Pentagon:
                case MarkerSymbol.Hexagon:
                {
                    var star = symbol == MarkerSymbol.Star;
                    var corners = symbol == MarkerSymbol.Hexagon ? 6 : 5;
                    var total = star ? corners * 2 : corners;
                    var angleStep = star ? Math.PI / corners : 2 * Math.PI / corners;
                    for (var i = 0; i < total; i++)
                    {
                        var rr = star && i % 2 == 1 ? r * 0.45 : r;
                        var angle = -Math.PI / 2 + i * angleStep;
                        var p = new Point(center.X + rr * Math.Cos(angle), center.Y + rr * Math.Sin(angle));
                        if (i == 0) ctx.BeginFigure(p, true);
                        else ctx.LineTo(p);
                    }

                    ctx.EndFigure(true);
                    break;
                }
            }
        }

        return g;
    }
}
