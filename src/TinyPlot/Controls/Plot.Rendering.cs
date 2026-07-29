using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TinyPlot;

public partial class Plot
{
    public override void Render(DrawingContext dc)
    {
        var paper = new Rect(Bounds.Size);
        if (paper.Width < 10 || paper.Height < 10) return;

        var build = PlotCalculator.Build(Data.ToArray(), Layout, Theme, paper);
        _build = build;

        dc.FillRectangle(new SolidColorBrush(Layout.PaperBackground ?? Theme.PaperBackground), paper);

        if (build.HasCartesian)
        {
            var rc = build.Context!;
            dc.FillRectangle(new SolidColorBrush(Layout.PlotBackground ?? Theme.PlotBackground), rc.PlotRect);
            DrawGrid(dc, rc);

            using (dc.PushClip(rc.PlotRect))
            {
                foreach (var trace in Data)
                    if (trace.Visible && trace.IsCartesian)
                        trace.Render(dc, rc);
            }

            DrawTicksAndTitles(dc, rc, paper);
            DrawColorbars(dc, build);
        }
        else if (build.Pies.Count > 0)
        {
            var rc = PieRenderContext(build);
            var hovered = _hasHover && _hoverTargets.Count > 0 ? _hoverTargets[0] : null;
            foreach (var (pie, cell) in PieCells(build))
            {
                var hoveredSlice = hovered?.Trace == pie && hovered.Tag is int si ? si : -1;
                pie.RenderIn(dc, rc, cell, hoveredSlice);
            }
        }

        // 通用渲染上下文（用于雷达图、地图、仪表盘、树图、关系图、3D 等非笛卡尔类型）
        var generalRc = build.Context ?? PieRenderContext(build);

        // 雷达图渲染
        var radarTraces = Data.OfType<RadarTrace>().Where(t => t.Visible).ToList();
        if (radarTraces.Count > 0)
        {
            var radarAxis = _radarAxis ?? new RadarAxis
            {
                Indicators = Enumerable.Range(0, radarTraces.Max(t => t.Values.Count))
                    .Select(i => $"维度{i + 1}").ToArray()
            };
            RadarRenderer.Render(dc, generalRc, radarAxis, radarTraces, build.PlotRect);
        }

        // 地图渲染
        var mapTraces = Data.OfType<MapTrace>().Where(t => t.Visible).ToList();
        if (mapTraces.Count > 0)
        {
            foreach (var trace in mapTraces)
                trace.Render(dc, generalRc);
        }

        // 仪表盘、树图、关系图、3D 等非笛卡尔类型统一渲染
        foreach (var trace in Data)
        {
            if (!trace.Visible || trace.IsCartesian || trace is PieTrace or RadarTrace or MapTrace) continue;
            trace.Render(dc, generalRc);
        }

        DrawTitle(dc, build, paper);
        DrawLegend(dc, build);

        if (_hasHover && Layout.HoverMode != HoverMode.None)
            DrawHover(dc, build);

        if (_zoomRect is { } zr)
        {
            dc.FillRectangle(new SolidColorBrush(Theme.SelectionFill), zr);
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Theme.SelectionStroke), 1, DashStyle.Dash), zr);
        }
    }

    private PlotRenderContext PieRenderContext(PlotBuild build) => new()
    {
        PlotRect = build.PlotRect,
        XAxis = new AxisState { Source = Layout.XAxis },
        YAxis = new AxisState { Source = Layout.YAxis },
        Theme = Theme,
        Layout = Layout,
        Typeface = build.Typeface
    };

    internal static IEnumerable<(PieTrace pie, Rect cell)> PieCells(PlotBuild build)
    {
        var n = build.Pies.Count;
        if (n == 0) yield break;
        var cols = (int)Math.Ceiling(Math.Sqrt(n));
        var rows = (int)Math.Ceiling(n / (double)cols);
        var r = build.PlotRect;
        for (var i = 0; i < n; i++)
        {
            var col = i % cols;
            var row = i / cols;
            yield return (build.Pies[i], new Rect(
                r.X + col * r.Width / cols,
                r.Y + row * r.Height / rows,
                r.Width / cols,
                r.Height / rows));
        }
    }

    // ------------------------------------------------------------------ axes

    private void DrawGrid(DrawingContext dc, PlotRenderContext rc)
    {
        var gridColor = Layout.XAxis.GridColor ?? Theme.GridColor;
        var gridPen = rc.Pen(gridColor, 1);
        var gridPenY = rc.Pen(Layout.YAxis.GridColor ?? Theme.GridColor, 1);
        var r = rc.PlotRect;

        if (Layout.XAxis.ShowGrid)
            foreach (var tick in rc.XAxis.Ticks)
            {
                if (!tick.Major) continue;
                var px = r.X + tickFraction(rc.XAxis, tick.Value) * r.Width;
                if (double.IsNaN(px)) continue;
                dc.DrawLine(gridPen, new Point(px, r.Top), new Point(px, r.Bottom));
            }

        if (Layout.YAxis.ShowGrid)
            foreach (var tick in rc.YAxis.Ticks)
            {
                if (!tick.Major) continue;
                var py = r.Bottom - tickFraction(rc.YAxis, tick.Value) * r.Height;
                if (double.IsNaN(py)) continue;
                dc.DrawLine(gridPenY, new Point(r.Left, py), new Point(r.Right, py));
            }

        // zero lines
        if (Layout.XAxis.ZeroLine && rc.XAxis.EffectiveType != AxisType.Log && rc.XAxis.EffectiveType != AxisType.Category)
            DrawZeroLine(dc, rc, true);
        if (Layout.YAxis.ZeroLine && rc.YAxis.EffectiveType != AxisType.Log && rc.YAxis.EffectiveType != AxisType.Category)
            DrawZeroLine(dc, rc, false);
    }

    private static double tickFraction(AxisState axis, double tickValue)
    {
        var span = axis.Max - axis.Min;
        return span == 0 ? double.NaN : (tickValue - axis.Min) / span;
    }

    private void DrawZeroLine(DrawingContext dc, PlotRenderContext rc, bool xAxis)
    {
        var axis = xAxis ? rc.XAxis : rc.YAxis;
        var f = axis.Fraction(0);
        if (double.IsNaN(f) || f < 0 || f > 1) return;
        var pen = rc.Pen(axis.Source.ZeroLineColor ?? Theme.ZeroLineColor, 1.4);
        var r = rc.PlotRect;
        if (xAxis)
        {
            var px = r.X + f * r.Width;
            dc.DrawLine(pen, new Point(px, r.Top), new Point(px, r.Bottom));
        }
        else
        {
            var py = r.Bottom - f * r.Height;
            dc.DrawLine(pen, new Point(r.Left, py), new Point(r.Right, py));
        }
    }

    private void DrawTicksAndTitles(DrawingContext dc, PlotRenderContext rc, Rect paper)
    {
        var r = rc.PlotRect;
        var fontColor = Layout.FontColor ?? Theme.FontColor;
        var tickPen = rc.Pen(fontColor, 1, opacity: 0.35);

        // x 刻度标签（自动跳过重叠标签）
        if (rc.XAxis.Source.ShowTickLabels)
        {
            var lastLabelRight = double.MinValue;
            var labelSpacing = 4.0; // 标签间最小间距
            foreach (var tick in rc.XAxis.Ticks)
            {
                if (!tick.Major) continue;
                var px = r.X + tickFraction(rc.XAxis, tick.Value) * r.Width;
                if (double.IsNaN(px)) continue;
                dc.DrawLine(tickPen, new Point(px, r.Bottom), new Point(px, r.Bottom + 5));
                var ft = rc.Text(tick.Label, rc.XAxis.Source.Color ?? fontColor);
                ft.TextAlignment = TextAlignment.Center;
                var labelLeft = px - ft.Width / 2;
                // 跳过会与上一个标签重叠的标签
                if (labelLeft < lastLabelRight + labelSpacing) continue;
                dc.DrawText(ft, new Point(labelLeft, r.Bottom + 7));
                lastLabelRight = labelLeft + ft.Width;
            }
        }

        // y 刻度标签（自动跳过重叠标签）
        if (rc.YAxis.Source.ShowTickLabels)
        {
            var lastLabelTop = double.MaxValue;
            var labelSpacing = 2.0;
            foreach (var tick in rc.YAxis.Ticks)
            {
                if (!tick.Major) continue;
                var py = r.Bottom - tickFraction(rc.YAxis, tick.Value) * r.Height;
                if (double.IsNaN(py)) continue;
                dc.DrawLine(tickPen, new Point(r.Left - 5, py), new Point(r.Left, py));
                var ft = rc.Text(tick.Label, rc.YAxis.Source.Color ?? fontColor);
                var labelTop = py - ft.Height / 2;
                // 跳过会与上一个标签重叠的标签
                if (labelTop > lastLabelTop - labelSpacing) continue;
                dc.DrawText(ft, new Point(r.Left - 8 - ft.Width, labelTop));
                lastLabelTop = labelTop;
            }
        }

        // x 轴标题
        if (rc.XAxis.Source.Title is { Length: > 0 } xTitle)
        {
            var ft = rc.Text(xTitle, fontColor, rc.XAxis.Source.TitleFontSize);
            var titleY = r.Bottom + 7 + rc.Layout.FontSize + 6; // 在刻度标签下方
            // 如果有刻度标签，测量最大标签高度来定位
            if (rc.XAxis.Ticks.Count > 0)
            {
                var maxH = 0.0;
                foreach (var tick in rc.XAxis.Ticks.Where(t => t.Major).Take(5))
                    maxH = Math.Max(maxH, rc.Text(tick.Label, fontColor).Height);
                titleY = r.Bottom + 7 + maxH + 8;
            }
            dc.DrawText(ft, new Point(r.Center.X - ft.Width / 2, titleY));
        }

        // y 轴标题（旋转）
        if (rc.YAxis.Source.Title is { Length: > 0 } yTitle)
        {
            var ft = rc.Text(yTitle, fontColor, rc.YAxis.Source.TitleFontSize);
            var cx = paper.Left + 14;
            var cy = r.Center.Y;
            using (dc.PushTransform(Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(cx, cy)))
                dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }
    }

    private void DrawTitle(DrawingContext dc, PlotBuild build, Rect paper)
    {
        if (Layout.Title.Text is not { Length: > 0 } text) return;
        var fontColor = Layout.Title.Color ?? Layout.FontColor ?? Theme.FontColor;
        var rc = build.Context ?? PieRenderContext(build);
        var ft = rc.Text(text, fontColor, Layout.Title.FontSize, FontWeight.SemiBold);
        var x = paper.Left + Layout.Title.X * paper.Width;
        dc.DrawText(ft, new Point(x - ft.Width / 2, paper.Top + 10));
    }

    // ---------------------------------------------------------------- legend

    private void DrawLegend(DrawingContext dc, PlotBuild build)
    {
        if (build.Legend.Count == 0 || !Layout.ShowLegend)
        {
            _legendHitRects.Clear();
            return;
        }

        var rc = build.Context ?? PieRenderContext(build);
        var r = build.PlotRect;
        var fontColor = Layout.FontColor ?? Theme.FontColor;

        double left;
        if (Layout.Legend.X >= 1) left = r.Right + 8;
        else if (Layout.Legend.X <= 0) left = r.Left - build.LegendWidth - 8;
        else left = r.Left + Layout.Legend.X * (r.Width - build.LegendWidth);

        double top;
        if (Layout.Legend.Orientation == Orientation.Vertical)
            top = r.Top + (1 - Layout.Legend.Y) * (r.Height - build.LegendHeight);
        else
            top = r.Top + (1 - Layout.Legend.Y) * r.Height + (Layout.Legend.Y >= 1 ? -build.LegendHeight : 0);

        // panel background (only drawn when explicitly set, like plotly.js legend.bgcolor)
        if (Layout.Legend.Background is { } bg)
        {
            dc.FillRectangle(rc.Brush(bg), new Rect(left - 6, top - 4, build.LegendWidth + 12, build.LegendHeight + 8));
            if (Layout.Legend.BorderColor is { } bc)
                dc.DrawRectangle(null, rc.Pen(bc, 1), new Rect(left - 6, top - 4, build.LegendWidth + 12, build.LegendHeight + 8));
        }

        _legendHitRects.Clear();
        var y = top;
        var x = left;
        foreach (var item in build.Legend)
        {
            var ft = rc.Text(item.Label, fontColor);
            var rowH = Math.Max(18, ft.Height) + 4;
            var alpha = item.IsHidden ? 0.35 : 1.0;

            using (dc.PushOpacity(alpha))
            {
                var cy = y + rowH / 2 - 1;
                if (item.IsLine)
                {
                    dc.DrawLine(rc.Pen(item.Color, 2.5), new Point(x, cy), new Point(x + 26, cy));
                    if (item.Symbol is { } sym)
                    {
                        var geo = MarkerGeometry.Build(sym, new Point(x + 13, cy), 9);
                        dc.DrawGeometry(rc.Brush(item.Color), null, geo);
                    }
                }
                else
                {
                    dc.FillRectangle(rc.Brush(item.Color), new Rect(x + 6, cy - 6, 12, 12));
                }

                dc.DrawText(ft, new Point(x + 32, y + (rowH - ft.Height) / 2));
            }

            _legendHitRects.Add((new Rect(x, y, 32 + ft.Width + 6, rowH), item));

            if (Layout.Legend.Orientation == Orientation.Vertical) y += rowH;
            else x += 32 + ft.Width + 16;
        }
    }

    // -------------------------------------------------------------- colorbar

    private void DrawColorbars(DrawingContext dc, PlotBuild build)
    {
        var rc = build.Context!;
        var r = rc.PlotRect;
        var fontColor = Layout.FontColor ?? Theme.FontColor;
        var x0 = r.Right + 10 + build.LegendWidth + (build.Legend.Count > 0 && Layout.Legend.X >= 1 ? 8 : 0);

        foreach (var hm in build.Colorbars)
        {
            if (hm.Calc is not { } calc) continue;
            var barRect = new Rect(x0, r.Top, 14, r.Height);
            var bmp = GetGradientBitmap(hm.Colorscale);
            dc.DrawImage(bmp, new Rect(0, 0, 1, 64), barRect);
            dc.DrawRectangle(null, rc.Pen(fontColor, 1, opacity: 0.3), barRect);

            var ticks = TickGenerator.Linear(calc.ZMin, calc.ZMax, 6);
            foreach (var tick in ticks)
            {
                var f = (tick.Value - calc.ZMin) / (calc.ZMax - calc.ZMin);
                if (f < -0.001 || f > 1.001) continue;
                var py = barRect.Bottom - f * barRect.Height;
                var ft = rc.Text(tick.Label, fontColor, Layout.FontSize - 1);
                dc.DrawText(ft, new Point(barRect.Right + 4, py - ft.Height / 2));
            }

            x0 += 74;
        }
    }

    private static readonly Dictionary<string, WriteableBitmap> GradientCache = new();

    private static WriteableBitmap GetGradientBitmap(Colorscale cs)
    {
        var key = cs.Name ?? cs.GetHashCode().ToString();
        if (GradientCache.TryGetValue(key, out var cached)) return cached;

        const int h = 64;
        var bmp = new WriteableBitmap(new PixelSize(1, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        var pixels = new byte[h * 4];
        for (var i = 0; i < h; i++)
        {
            // top of the bar = high values
            var c = cs.GetColor(1 - (double)i / (h - 1));
            pixels[i * 4] = c.B;
            pixels[i * 4 + 1] = c.G;
            pixels[i * 4 + 2] = c.R;
            pixels[i * 4 + 3] = c.A;
        }

        using (var fb = bmp.Lock())
            Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
        GradientCache[key] = bmp;
        return bmp;
    }

    // ----------------------------------------------------------------- hover

    private void DrawHover(DrawingContext dc, PlotBuild build)
    {
        if (_hoverTargets.Count == 0) return;

        if (build.HasCartesian)
        {
            var rc = build.Context!;
            var r = rc.PlotRect;

            // spikes
            if (Config.ShowSpikes)
            {
                var spikePen = rc.Pen(Theme.SpikeColor, 1, DashStyle.Dash, 0.45);
                var snapX = _hoverTargets[0].ScreenPoint.X;
                if (Layout.HoverMode is HoverMode.X or HoverMode.XUnified)
                {
                    dc.DrawLine(spikePen, new Point(snapX, r.Top), new Point(snapX, r.Bottom));
                }
                else
                {
                    var p = _hoverTargets[0].ScreenPoint;
                    if (Layout.XAxis.ShowSpikes)
                        dc.DrawLine(spikePen, new Point(p.X, Math.Min(p.Y, r.Bottom)), new Point(p.X, r.Bottom));
                    if (Layout.YAxis.ShowSpikes)
                        dc.DrawLine(spikePen, new Point(Math.Max(p.X, r.Left), p.Y), new Point(r.Left, p.Y));
                }
            }

            // point highlights
            foreach (var t in _hoverTargets)
            {
                if (t.Trace is ScatterTrace or CandlestickTrace)
                {
                    dc.DrawEllipse(Brushes.White, null, t.ScreenPoint, 7, 7);
                    dc.DrawEllipse(new SolidColorBrush(t.Color), null, t.ScreenPoint, 5, 5);
                }
                else if (t.Trace is HeatmapTrace && t.Tag is ValueTuple<int, int> cell && t.Trace is HeatmapTrace hm && hm.Calc is { } calc)
                {
                    var x0 = rc.XToPixels(calc.Xs[cell.Item2] - calc.Dx / 2);
                    var x1 = rc.XToPixels(calc.Xs[cell.Item2] + calc.Dx / 2);
                    var y0 = rc.YToPixels(calc.Ys[cell.Item1] - calc.Dy / 2);
                    var y1 = rc.YToPixels(calc.Ys[cell.Item1] + calc.Dy / 2);
                    var rect = new Rect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                    dc.DrawRectangle(null, rc.Pen(Theme.SpikeColor, 1.5), rect);
                }
            }
        }

        // labels
        if (Layout.HoverMode == HoverMode.XUnified && _hoverTargets.Count > 1)
            DrawUnifiedLabel(dc, build);
        else if (Layout.HoverMode is HoverMode.X or HoverMode.XUnified)
            DrawColumnLabels(dc, build);
        else
            DrawClosestLabel(dc, build, _hoverTargets[0]);
    }

    private void DrawClosestLabel(DrawingContext dc, PlotBuild build, HoverTarget t)
    {
        var rc = build.Context ?? PieRenderContext(build);
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(t.Title) && t.Title != t.XText) lines.Add(t.Title!);
        if (!string.IsNullOrEmpty(t.XText)) lines.Add(t.XText!);
        if (!string.IsNullOrEmpty(t.YText)) lines.Add(t.YText!);
        if (!string.IsNullOrEmpty(t.ExtraText)) lines.Add(t.ExtraText!);
        if (lines.Count == 0) return;

        var textColor = rc.ContrastColor(t.Color);
        var fts = lines.Select((l, i) => rc.Text(l, textColor, Layout.FontSize, i == 0 && lines.Count > 1 ? FontWeight.SemiBold : FontWeight.Normal)).ToArray();
        var w = fts.Max(f => f.Width) + 16;
        var h = fts.Sum(f => f.Height) + 10;

        var box = PlaceLabel(new Size(w, h), t.ScreenPoint, build.PaperRect);
        dc.FillRectangle(new SolidColorBrush(t.Color), box, 4);
        dc.DrawRectangle(null, rc.Pen(Theme.HoverBorderColor, 1, opacity: 0.4), box, 4);

        var ty = box.Y + 5;
        foreach (var ft in fts)
        {
            dc.DrawText(ft, new Point(box.X + 8, ty));
            ty += ft.Height;
        }
    }

    private void DrawColumnLabels(DrawingContext dc, PlotBuild build)
    {
        var rc = build.Context!;
        foreach (var t in _hoverTargets)
            DrawClosestLabel(dc, build, t);
    }

    private void DrawUnifiedLabel(DrawingContext dc, PlotBuild build)
    {
        var rc = build.Context!;
        var fontColor = Layout.FontColor ?? Theme.FontColor;
        var header = _hoverTargets[0].XText ?? "";

        var headerFt = rc.Text(header, fontColor, Layout.FontSize, FontWeight.SemiBold);
        var rows = _hoverTargets
            .OrderBy(t => t.Trace.ColorIndex)
            .Select(t => (
                target: t,
                name: rc.Text(t.Trace.Name ?? "trace", fontColor),
                value: rc.Text(t.YText ?? t.ExtraText ?? "", fontColor, Layout.FontSize, FontWeight.SemiBold)))
            .ToList();

        var nameW = rows.Max(r => r.name.Width);
        var valW = rows.Max(r => r.value.Width);
        var w = 16 + 12 + 6 + nameW + 14 + valW + 8;
        var h = headerFt.Height + 6 + rows.Sum(r => Math.Max(16, r.name.Height) + 4) + 8;

        var anchor = new Point(_hoverTargets[0].ScreenPoint.X, _hoverPoint.Y);
        var box = PlaceLabel(new Size(w, h), anchor, build.PaperRect);
        dc.FillRectangle(new SolidColorBrush(Layout.PaperBackground ?? Theme.PaperBackground), box, 4);
        dc.DrawRectangle(null, rc.Pen(Theme.HoverBorderColor, 1, opacity: 0.5), box, 4);

        var ty = box.Y + 6;
        dc.DrawText(headerFt, new Point(box.X + 8, ty));
        ty += headerFt.Height + 6;
        foreach (var (target, name, value) in rows)
        {
            var rowH = Math.Max(16, name.Height);
            dc.FillRectangle(new SolidColorBrush(target.Color), new Rect(box.X + 8, ty + rowH / 2 - 5, 10, 10));
            dc.DrawText(name, new Point(box.X + 24, ty + (rowH - name.Height) / 2));
            dc.DrawText(value, new Point(box.X + 24 + nameW + 14, ty + (rowH - value.Height) / 2));
            ty += rowH + 4;
        }
    }

    /// <summary>Place a hover label near an anchor, flipping at paper edges (plotly.js style).</summary>
    private static Rect PlaceLabel(Size size, Point anchor, Rect paper)
    {
        var x = anchor.X + 14;
        var y = anchor.Y - size.Height - 10;
        if (x + size.Width > paper.Right - 4) x = anchor.X - size.Width - 14;
        if (x < paper.Left + 4) x = paper.Left + 4;
        if (y < paper.Top + 4) y = anchor.Y + 16;
        if (y + size.Height > paper.Bottom - 4) y = paper.Bottom - size.Height - 4;
        return new Rect(x, y, size.Width, size.Height);
    }
}
