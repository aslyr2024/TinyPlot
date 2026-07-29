using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

internal sealed class PlotBuild
{
    public required Rect PaperRect { get; init; }

    public Rect PlotRect { get; set; }

    public PlotRenderContext? Context { get; set; }

    public List<LegendItem> Legend { get; } = [];

    public double LegendWidth { get; set; }

    public double LegendHeight { get; set; }

    public List<HeatmapTrace> Colorbars { get; } = [];

    public List<PieTrace> Pies { get; } = [];

    public bool HasCartesian => Context != null;

    public Typeface Typeface { get; set; } = default!;
}

/// <summary>
/// The calc engine: resolves axis types, categories, ranges and ticks,
/// positions bars and measures everything needed to lay out the plot.
/// Mirrors the plotly.js "calc" + "supplyDefaults" pipeline.
/// </summary>
internal static class PlotCalculator
{
    public static PlotBuild Build(IReadOnlyList<Trace> traces, Layout layout, PlotTheme theme, Rect paper)
    {
        var build = new PlotBuild { PaperRect = paper, Typeface = new Typeface(new FontFamily(theme.FontFamily)) };
        var colorway = layout.Colorway as IReadOnlyList<Color> ?? theme.Colorway;

        // ---- assign colors, split pies / cartesian ------------------------
        var cartesian = new List<Trace>();
        var colorIdx = 0;
        foreach (var t in traces)
        {
            t.ResolvedColor = t.Color ?? colorway[colorIdx % colorway.Count];
            t.ColorIndex = colorIdx++;
            if (t is PieTrace pie)
            {
                pie.SetColorway(colorway);
                if (pie.Visible) build.Pies.Add(pie);
            }
            else if (t.IsCartesian)
            {
                cartesian.Add(t);
            }
        }

        var visibleCartesian = cartesian.Where(t => t.Visible).ToList();

        // ---- axis type detection ------------------------------------------
        var xAxis = new AxisState { Source = layout.XAxis };
        var yAxis = new AxisState { Source = layout.YAxis };
        var xSeries = visibleCartesian.Select(t => t.GetAxesData().x).Where(s => s != null).Cast<DataSeries>().ToList();
        var ySeries = visibleCartesian.Select(t => t.GetAxesData().y).Where(s => s != null).Cast<DataSeries>().ToList();
        xAxis.EffectiveType = DetectType(layout.XAxis, xSeries);
        yAxis.EffectiveType = DetectType(layout.YAxis, ySeries);

        // ---- category registration ----------------------------------------
        if (xAxis.EffectiveType == AxisType.Category)
            xAxis.Categories = CollectCategories(xSeries);
        if (yAxis.EffectiveType == AxisType.Category)
            yAxis.Categories = CollectCategories(ySeries);

        // ---- calc context --------------------------------------------------
        var ctx = new PlotCalcContext { XAxis = xAxis, YAxis = yAxis, Layout = layout };

        // ---- bar layout (needs axes, runs before trace Prepare) ------------
        var bars = visibleCartesian.OfType<BarTrace>().ToList();
        if (bars.Count > 0)
        {
            var vBars = bars.Where(b => b.Orientation == Orientation.Vertical).ToList();
            var hBars = bars.Where(b => b.Orientation == Orientation.Horizontal).ToList();
            var state = new BarLayoutState
            {
                Orientation = vBars.Count >= hBars.Count ? Orientation.Vertical : Orientation.Horizontal,
                Mode = layout.BarMode
            };
            if (vBars.Count > 0)
            {
                state.Orientation = Orientation.Vertical;
                ComputeBars(vBars, Orientation.Vertical, ctx, state, layout);
            }

            if (hBars.Count > 0)
            {
                state.Orientation = Orientation.Horizontal;
                ComputeBars(hBars, Orientation.Horizontal, ctx, state, layout);
            }

            ctx.Bars = state;
        }

        // ---- per-trace calc ------------------------------------------------
        foreach (var t in visibleCartesian)
            t.Prepare(ctx);
        foreach (var pie in build.Pies)
            pie.Prepare(ctx);
        // 地图和雷达图也需要 Prepare
        foreach (var t in traces)
        {
            if (t.Visible && !t.IsCartesian && t is not PieTrace)
                t.Prepare(ctx);
        }

        // ---- ranges ---------------------------------------------------------
        var hasVBars = bars.Any(b => b.Orientation == Orientation.Vertical) || visibleCartesian.Any(t => t is HistogramTrace { X: not null });
        var hasHBars = bars.Any(b => b.Orientation == Orientation.Horizontal) || visibleCartesian.Any(t => t is HistogramTrace { Y: not null, X: null });
        FinalizeRange(xAxis, ctx.XMin, ctx.XMax, ctx.XMinPositive, layout.XAxis.RangeToZero || hasHBars);
        FinalizeRange(yAxis, ctx.YMin, ctx.YMax, ctx.YMinPositive, layout.YAxis.RangeToZero || hasVBars);

        // ---- ticks ----------------------------------------------------------
        var estPlotW = Math.Max(100, paper.Width - 200);
        var estPlotH = Math.Max(80, paper.Height - 150);
        xAxis.Ticks = BuildTicks(xAxis, (int)(estPlotW / 75), estPlotW);
        yAxis.Ticks = BuildTicks(yAxis, (int)(estPlotH / 45), estPlotH);

        // ---- measure labels, auto margins ----------------------------------
        var fontColor = layout.FontColor ?? theme.FontColor;
        double left = 12, right = 14, top = 14, bottom = 12;

        FormattedText Measure(string s, double? size = null, FontWeight w = FontWeight.Normal)
            => new(s, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, build.Typeface, size ?? layout.FontSize, Brushes.Black)
            {
                MaxTextWidth = double.PositiveInfinity,
                MaxTextHeight = double.PositiveInfinity
            };

        if (layout.Title.Text is { Length: > 0 })
            top += layout.Title.FontSize + 18;

        if (xAxis.Source.ShowTickLabels)
        {
            var h = 0.0;
            foreach (var tick in xAxis.Ticks.Take(200))
                h = Math.Max(h, Measure(tick.Label).Height);
            if (xAxis.Ticks.Count > 0) bottom += h + 6;
        }

        if (xAxis.Source.Title is { Length: > 0 })
            bottom += xAxis.Source.TitleFontSize + 12;

        if (yAxis.Source.ShowTickLabels)
        {
            var w = 0.0;
            foreach (var tick in yAxis.Ticks.Take(200))
                w = Math.Max(w, Measure(tick.Label).Width);
            if (yAxis.Ticks.Count > 0) left += w + 8;
        }

        if (yAxis.Source.Title is { Length: > 0 })
            left += yAxis.Source.TitleFontSize + 12;

        // ---- legend measure -------------------------------------------------
        var legend = new List<LegendItem>();
        if (layout.ShowLegend)
        {
            foreach (var t in traces)
            {
                foreach (var item in t.GetLegendItems())
                {
                    if (t is not PieTrace)
                        item.IsHidden = !t.Visible;
                    legend.Add(item);
                }
            }
        }

        build.Legend.AddRange(legend);

        if (legend.Count > 0)
        {
            var maxText = 0.0;
            var totalH = 10.0;
            foreach (var item in legend)
            {
                var ft = Measure(item.Label);
                maxText = Math.Max(maxText, ft.Width);
                totalH += Math.Max(18, ft.Height) + 4;
            }

            build.LegendWidth = maxText + 44;
            build.LegendHeight = totalH;
            if (layout.Legend.X >= 1) right += build.LegendWidth + 8;
            else if (layout.Legend.X <= 0) left += build.LegendWidth + 8;
        }

        // ---- colorbars -------------------------------------------------------
        foreach (var hm in visibleCartesian.OfType<HeatmapTrace>())
        {
            if (!hm.ShowScale || hm.Calc == null) continue;
            build.Colorbars.Add(hm);
            right += 74;
        }

        left = Math.Max(left, layout.Margin.Left);
        right = Math.Max(right, layout.Margin.Right);
        top = Math.Max(top, layout.Margin.Top);
        bottom = Math.Max(bottom, layout.Margin.Bottom);

        var plotRect = new Rect(
            paper.X + left,
            paper.Y + top,
            Math.Max(40, paper.Width - left - right),
            Math.Max(40, paper.Height - top - bottom));

        // shrink if paper too small
        if (paper.Width < left + right + 40 || paper.Height < top + bottom + 40)
            plotRect = new Rect(paper.X + left, paper.Y + top, Math.Max(10, paper.Width - left - right), Math.Max(10, paper.Height - top - bottom));

        build.PlotRect = plotRect;

        if (visibleCartesian.Count > 0)
        {
            var rc = new PlotRenderContext
            {
                PlotRect = plotRect,
                XAxis = xAxis,
                YAxis = yAxis,
                Theme = theme,
                Layout = layout,
                Typeface = build.Typeface,
                Bars = ctx.Bars
            };
            foreach (var kv in ctx.CalcData)
                rc.CalcData[kv.Key] = kv.Value;
            build.Context = rc;
        }

        return build;
    }

    private static AxisType DetectType(PlotAxis axis, IReadOnlyList<DataSeries> series)
    {
        if (axis.Type != AxisType.Auto) return axis.Type;
        foreach (var s in series)
        {
            switch (s.Kind)
            {
                case SeriesKind.Text: return AxisType.Category;
                case SeriesKind.Date: return AxisType.Date;
                case SeriesKind.Number: return AxisType.Linear;
            }
        }

        return AxisType.Linear;
    }

    private static List<string> CollectCategories(IReadOnlyList<DataSeries> series)
    {
        var cats = new List<string>();
        var seen = new HashSet<string>();
        foreach (var s in series)
        {
            if (s.Kind != SeriesKind.Text) continue;
            foreach (var v in s)
            {
                var text = v?.ToString() ?? "";
                if (seen.Add(text)) cats.Add(text);
            }
        }

        return cats;
    }

    private static void ComputeBars(List<BarTrace> traces, Orientation orientation, PlotCalcContext ctx, BarLayoutState state, Layout layout)
    {
        var posAxis = orientation == Orientation.Vertical ? ctx.XAxis : ctx.YAxis;

        // collect centers
        var centers = new Dictionary<BarTrace, List<double>>();
        var values = new Dictionary<BarTrace, List<double>>();
        var allCenters = new SortedSet<double>();
        foreach (var t in traces)
        {
            var posSeries = orientation == Orientation.Vertical ? t.X : t.Y;
            var valSeries = orientation == Orientation.Vertical ? t.Y : t.X;
            var n = Math.Max(posSeries?.Count ?? 0, valSeries?.Count ?? 0);
            var cList = new List<double>(n);
            var vList = new List<double>(n);
            for (var i = 0; i < n; i++)
            {
                double c;
                if (posSeries != null && i < posSeries.Count)
                    c = posAxis.EffectiveType == AxisType.Category
                        ? posAxis.CategoryIndex(posSeries.AsText(i) ?? "")
                        : posSeries.AsNumber(i);
                else
                    c = i;
                var v = valSeries != null && i < valSeries.Count ? valSeries.AsNumber(i) : 0;
                cList.Add(c);
                vList.Add(v);
                if (!double.IsNaN(c)) allCenters.Add(c);
            }

            centers[t] = cList;
            values[t] = vList;
        }

        // slot size
        double slot = 1;
        if (posAxis.EffectiveType == AxisType.Category)
        {
            slot = 1;
        }
        else if (allCenters.Count > 1)
        {
            var arr = allCenters.ToArray();
            var minDelta = double.PositiveInfinity;
            for (var i = 1; i < arr.Length; i++)
                minDelta = Math.Min(minDelta, arr[i] - arr[i - 1]);
            if (!double.IsInfinity(minDelta) && minDelta > 0) slot = minDelta;
        }

        state.SlotSize = slot;
        var groupWidth = slot * (1 - layout.BarGap);
        var tCount = traces.Count;
        var stackBase = new Dictionary<double, (double pos, double neg)>();

        for (var t = 0; t < tCount; t++)
        {
            var trace = traces[t];
            var cList = centers[trace];
            var vList = values[trace];
            var slots = new BarSlot[cList.Count];
            for (var i = 0; i < cList.Count; i++)
            {
                var c = cList[i];
                var v = vList[i];
                if (state.Mode == BarMode.Stack)
                {
                    if (!stackBase.TryGetValue(c, out var acc)) acc = (0, 0);
                    double baseVal;
                    if (v >= 0)
                    {
                        baseVal = acc.pos;
                        acc.pos += v;
                    }
                    else
                    {
                        baseVal = acc.neg;
                        acc.neg += v;
                    }

                    stackBase[c] = acc;
                    slots[i] = new BarSlot { Center = c, Width = groupWidth, Base = baseVal, Value = v };
                }
                else
                {
                    var step = groupWidth / tCount;
                    var barWidth = step * (1 - layout.BarGroupGap);
                    var center = c - groupWidth / 2 + step * t + step / 2;
                    slots[i] = new BarSlot { Center = center, Width = barWidth, Base = 0, Value = v };
                }
            }

            state.Slots[trace] = slots;
        }
    }

    private static void FinalizeRange(AxisState axis, double dataMin, double dataMax, double minPositive, bool toZero)
    {
        var src = axis.Source;

        if (axis.EffectiveType == AxisType.Category)
        {
            if (src.Range is { Length: 2 } cr)
            {
                axis.Min = cr[0];
                axis.Max = cr[1];
            }
            else
            {
                axis.Min = -0.5;
                axis.Max = Math.Max(0.5, axis.Categories.Count - 0.5);
            }

            return;
        }

        if (src.Range is { Length: 2 } r)
        {
            axis.Min = axis.Transform(r[0]);
            axis.Max = axis.Transform(r[1]);
            if (axis.Min > axis.Max) (axis.Min, axis.Max) = (axis.Max, axis.Min);
            return;
        }

        if (double.IsInfinity(dataMin) || double.IsInfinity(dataMax))
        {
            dataMin = axis.EffectiveType == AxisType.Date ? DateTime.Today.ToOADate() : 0;
            dataMax = axis.EffectiveType == AxisType.Date ? dataMin + 1 : 1;
        }

        if (axis.EffectiveType == AxisType.Log)
        {
            var lo = double.IsInfinity(minPositive) ? 1 : minPositive;
            var hi = dataMax <= 0 ? 10 : dataMax;
            var llo = Math.Log10(lo);
            var lhi = Math.Log10(hi);
            if (llo == lhi)
            {
                llo -= 1;
                lhi += 1;
            }

            var pad = (lhi - llo) * 0.03;
            axis.Min = llo - pad;
            axis.Max = lhi + pad;
            return;
        }

        var span = dataMax - dataMin;
        if (span <= 0)
        {
            span = Math.Abs(dataMax) > 0 ? Math.Abs(dataMax) * 0.1 : 1;
            dataMin -= span / 2;
            dataMax += span / 2;
            span = dataMax - dataMin;
        }

        // 智能自适应：当数据跨度跨越多个数量级且全为正数时，自动切换到对数轴
        if (axis.EffectiveType == AxisType.Linear && src.Type == AxisType.Auto
            && dataMin > 0 && dataMax > 0 && dataMax / dataMin > 1000)
        {
            axis.EffectiveType = AxisType.Log;
            var llo = Math.Log10(dataMin);
            var lhi = Math.Log10(dataMax);
            if (llo == lhi) { llo -= 1; lhi += 1; }
            var pad = (lhi - llo) * 0.03;
            axis.Min = llo - pad;
            axis.Max = lhi + pad;
            // 重新生成刻度
            axis.Ticks = TickGenerator.Log(axis.Min, axis.Max, src.TickPrefix, src.TickSuffix);
            return;
        }

        var padFrac = src.AutoRangePadding;
        var pLo = dataMin - span * padFrac;
        var pHi = dataMax + span * padFrac;
        if (toZero)
        {
            if (dataMin >= 0) pLo = Math.Min(0, dataMin);
            if (dataMax <= 0) pHi = Math.Max(0, dataMax);
        }

        axis.Min = pLo;
        axis.Max = pHi;
    }

    private static IReadOnlyList<Tick> BuildTicks(AxisState axis, int targetCount, double plotSize = 600)
    {
        var src = axis.Source;
        var n = src.NTicks > 0 ? src.NTicks : Math.Max(2, targetCount);
        switch (axis.EffectiveType)
        {
            case AxisType.Category:
            {
                var ticks = new List<Tick>();
                var lo = (int)Math.Ceiling(axis.Min + 0.499);
                var hi = (int)Math.Floor(axis.Max + 0.499);
                var visible = Math.Max(0, hi - lo + 1);
                // 根据可用宽度动态计算最大标签数（每个标签至少 50px）
                var maxLabels = Math.Max(2, Math.Min(targetCount, (int)(plotSize / 50)));
                var step = Math.Max(1, (int)Math.Ceiling((double)visible / maxLabels));
                for (var i = lo; i <= hi; i += step)
                    if (i >= 0 && i < axis.Categories.Count)
                        ticks.Add(new Tick(i, axis.Categories[i]));
                return ticks;
            }
            case AxisType.Log:
                return TickGenerator.Log(axis.Min, axis.Max, src.TickPrefix, src.TickSuffix);
            case AxisType.Date:
                return TickGenerator.Date(axis.Min, axis.Max, n);
            default:
                return TickGenerator.Linear(axis.Min, axis.Max, n, src.TickFormat, src.TickPrefix, src.TickSuffix);
        }
    }
}
