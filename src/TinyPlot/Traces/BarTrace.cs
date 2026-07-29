using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// Bar chart, the counterpart of plotly.js type "bar". Supports vertical and
/// horizontal orientation, grouping and stacking (layout.barmode).
/// </summary>
public class BarTrace : Trace
{
    public DataSeries? X { get; set; }

    public DataSeries? Y { get; set; }

    public Orientation Orientation { get; set; } = Orientation.Vertical;

    public BarMarker Marker { get; } = new();

    /// <summary>Per-bar text labels.</summary>
    public IReadOnlyList<string>? Text { get; set; }

    public TraceTextPosition TextPosition { get; set; } = TraceTextPosition.Auto;

    internal BarSlot[]? Slots { get; set; }

    internal override (DataSeries? x, DataSeries? y) GetAxesData() => (X, Y);

    internal override void Prepare(PlotCalcContext ctx)
    {
        if (ctx.Bars != null && ctx.Bars.Orientation == Orientation && ctx.Bars.Slots.TryGetValue(this, out var slots))
        {
            Slots = slots;
            foreach (var s in slots)
            {
                var top = s.Base + s.Value;
                if (Orientation == Orientation.Vertical)
                {
                    ctx.ExtendXRange(s.Center - ctx.Bars.SlotSize / 2, s.Center + ctx.Bars.SlotSize / 2);
                    ctx.ExtendYRange(Math.Min(s.Base, top), Math.Max(s.Base, top));
                }
                else
                {
                    ctx.ExtendYRange(s.Center - ctx.Bars.SlotSize / 2, s.Center + ctx.Bars.SlotSize / 2);
                    ctx.ExtendXRange(Math.Min(s.Base, top), Math.Max(s.Base, top));
                }
            }
        }

        ctx.SetCalc(this, null);
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Slots == null) return;
        using var _ = dc.PushOpacity(Opacity * Marker.Opacity);
        var zero = Orientation == Orientation.Vertical
            ? (rc.YAxis.EffectiveType == AxisType.Log ? 1 : 0)
            : (rc.XAxis.EffectiveType == AxisType.Log ? 1 : 0);

        for (var i = 0; i < Slots.Length; i++)
        {
            var s = Slots[i];
            var rect = BarRect(rc, s, zero);
            if (rect == null) continue;
            var color = Marker.BarColor(i, ResolvedColor);
            dc.DrawRectangle(rc.Brush(color), Marker.LineWidth > 0 ? rc.Pen(Marker.LineColor ?? rc.Theme.PaperBackground, Marker.LineWidth) : null, rect.Value);
        }

        // 数值标签：像 plotly.js 一样，仅在设置 text 或显式 textposition 时显示
        // 柱子太窄时自动隐藏标签以避免重叠
        var showLabels = Text != null || TextPosition is TraceTextPosition.Inside or TraceTextPosition.Outside;
        if (!showLabels || TextPosition == TraceTextPosition.None) return;

        // 检查柱子宽度是否足够显示标签
        var sampleRect = Slots.Length > 0 ? BarRect(rc, Slots[0], 0) : null;
        if (sampleRect is { } sr && (Orientation == Orientation.Vertical ? sr.Width < 20 : sr.Height < 16))
            return; // 柱子太窄，隐藏标签
        for (var i = 0; i < Slots.Length; i++)
        {
            var s = Slots[i];
            var rect = BarRect(rc, s, zero);
            if (rect == null) continue;
            var label = Text != null && i < Text.Count ? Text[i] : PlotFmt.HoverValue(s.Value);
            if (string.IsNullOrEmpty(label)) continue;
            var color = Marker.BarColor(i, ResolvedColor);
            var r = rect.Value;
            var horizontal = Orientation == Orientation.Horizontal;
            var inside = TextPosition == TraceTextPosition.Inside ||
                         (TextPosition == TraceTextPosition.Auto && Fits(rc, label, r, horizontal));

            var ft = rc.Text(label, inside ? rc.ContrastColor(color) : rc.Theme.FontColor, weight: inside ? FontWeight.SemiBold : FontWeight.Normal);
            Point pos;
            if (!horizontal)
            {
                var top = Math.Min(r.Top, r.Bottom);
                pos = inside
                    ? new Point(r.Center.X - ft.Width / 2, top + 4)
                    : new Point(r.Center.X - ft.Width / 2, top - ft.Height - 3);
            }
            else
            {
                var right = Math.Max(r.Left, r.Right);
                pos = inside
                    ? new Point(right - ft.Width - 4, r.Center.Y - ft.Height / 2)
                    : new Point(right + 4, r.Center.Y - ft.Height / 2);
            }

            dc.DrawText(ft, pos);
        }
    }

    private static bool Fits(PlotRenderContext rc, string label, Rect r, bool horizontal)
    {
        var ft = rc.Text(label, Colors.Black);
        return horizontal ? ft.Width < r.Width - 8 : ft.Width < r.Width - 4 && ft.Height < r.Height - 6;
    }

    private Rect? BarRect(PlotRenderContext rc, BarSlot s, double zero)
    {
        var v0 = s.Base;
        var v1 = s.Base + s.Value;
        if (Orientation == Orientation.Vertical)
        {
            var x0 = rc.XToPixels(s.Center - s.Width / 2);
            var x1 = rc.XToPixels(s.Center + s.Width / 2);
            var y0 = rc.YToPixels(v0);
            var y1 = rc.YToPixels(v1);
            if (new[] { x0, x1, y0, y1 }.Any(double.IsNaN)) return null;
            return new Rect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Max(1, Math.Abs(y1 - y0)));
        }
        else
        {
            var y0 = rc.YToPixels(s.Center - s.Width / 2);
            var y1 = rc.YToPixels(s.Center + s.Width / 2);
            var x0 = rc.XToPixels(v0);
            var x1 = rc.XToPixels(v1);
            if (new[] { x0, x1, y0, y1 }.Any(double.IsNaN)) return null;
            return new Rect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(1, Math.Abs(x1 - x0)), Math.Abs(y1 - y0));
        }
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (Slots == null) yield break;
        var zero = 0;
        for (var i = 0; i < Slots.Length; i++)
        {
            var s = Slots[i];
            var rect = BarRect(rc, s, zero);
            if (rect == null) continue;
            var r = rect.Value;
            var hit = mode is HoverMode.X or HoverMode.XUnified
                ? (Orientation == Orientation.Vertical ? pt.X >= r.Left && pt.X <= r.Right : pt.Y >= r.Top && pt.Y <= r.Bottom)
                : r.Contains(pt);
            if (!hit) continue;

            var horizontal = Orientation == Orientation.Horizontal;
            var label = Text != null && i < Text.Count ? Text[i] : null;
            yield return new HoverTarget
            {
                ScreenPoint = new Point(Math.Clamp(pt.X, r.Left, r.Right), Math.Clamp(pt.Y, r.Top, r.Bottom)),
                Trace = this,
                Color = Marker.BarColor(i, ResolvedColor),
                Title = Name,
                XText = horizontal ? rc.XAxis.FormatHover(s.Base + s.Value) : rc.XAxis.FormatHover(s.Center),
                YText = horizontal ? rc.YAxis.FormatHover(s.Center) : rc.YAxis.FormatHover(s.Base + s.Value),
                ExtraText = label,
                Distance = 0,
                Tag = i
            };
        }
    }
}
