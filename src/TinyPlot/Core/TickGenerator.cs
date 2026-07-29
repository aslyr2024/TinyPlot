namespace TinyPlot;

public readonly record struct Tick(double Value, string Label, bool Major = true);

/// <summary>
/// Generates "nice" axis ticks (1/2/5 × 10^n steps) the way d3 / plotly.js do.
/// </summary>
public static class TickGenerator
{
    /// <summary>Numeric ticks covering [min, max]. Returns ticks and the step used.</summary>
    public static List<Tick> Linear(double min, double max, int targetCount, string? format = null, string? prefix = null, string? suffix = null)
    {
        var ticks = new List<Tick>();
        if (max < min) (min, max) = (max, min);
        var span = max - min;
        if (span <= 0 || double.IsNaN(span) || double.IsInfinity(span))
        {
            ticks.Add(new Tick(min, FormatValue(min, span / 5, format, prefix, suffix)));
            return ticks;
        }

        var count = Math.Max(2, targetCount);
        var step = NiceStep(span / count);
        var start = Math.Ceiling(min / step) * step;

        for (var v = start; v <= max + step * 1e-9; v += step)
        {
            var snapped = Math.Abs(v) < step * 1e-9 ? 0 : v;
            ticks.Add(new Tick(snapped, FormatValue(snapped, step, format, prefix, suffix)));
            if (ticks.Count > 1000) break;
        }

        if (ticks.Count == 0) ticks.Add(new Tick(min, FormatValue(min, step, format, prefix, suffix)));
        return ticks;
    }

    public static double NiceStep(double rawStep)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var error = rawStep / power;
        double factor;
        if (error >= 7.5) factor = 10;
        else if (error >= 3.5) factor = 5;
        else if (error >= 1.5) factor = 2;
        else factor = 1;
        return factor * power;
    }

    private static string FormatValue(double v, double step, string? format, string? prefix, string? suffix)
    {
        var text = format != null
            ? v.ToString(format, System.Globalization.CultureInfo.InvariantCulture)
            : PlotFmt.Number(v, step);
        return PlotFmt.ApplyAffixes(text, prefix, suffix);
    }

    /// <summary>Ticks for a log10-scaled axis whose visible range is [minLog, maxLog].</summary>
    public static List<Tick> Log(double minLog, double maxLog, string? prefix = null, string? suffix = null)
    {
        var ticks = new List<Tick>();
        var lo = (int)Math.Ceiling(minLog - 1e-9);
        var hi = (int)Math.Floor(maxLog + 1e-9);
        var decades = hi - lo + 1;

        if (decades <= 0) return ticks;

        if (decades <= 12)
        {
            // every decade, plus minor 2..9 ticks when zoomed in
            var minors = decades <= 3;
            for (var d = lo; d <= hi; d++)
            {
                ticks.Add(new Tick(d, PlotFmt.ApplyAffixes(PlotFmt.PowerOfTen(d), prefix, suffix)));
                if (minors)
                {
                    for (var m = 2; m <= 9; m++)
                    {
                        var v = d + Math.Log10(m);
                        if (v >= minLog && v <= maxLog)
                            ticks.Add(new Tick(v, PlotFmt.ApplyAffixes(PlotFmt.Number(m * Math.Pow(10, d)), prefix, suffix), Major: false));
                    }
                }
            }
        }
        else
        {
            var step = (int)Math.Ceiling(decades / 10.0);
            for (var d = lo; d <= hi; d += step)
                ticks.Add(new Tick(d, PlotFmt.ApplyAffixes(PlotFmt.PowerOfTen(d), prefix, suffix)));
        }

        return ticks;
    }

    /// <summary>Ticks for a date axis whose range is given as OLE Automation dates.</summary>
    public static List<Tick> Date(double minOa, double maxOa, int targetCount)
    {
        var ticks = new List<Tick>();
        DateTime min, max;
        try
        {
            min = DateTime.FromOADate(minOa);
            max = DateTime.FromOADate(maxOa);
        }
        catch
        {
            return ticks;
        }

        var span = max - min;
        if (span <= TimeSpan.Zero) span = TimeSpan.FromDays(1);
        var target = TimeSpan.FromTicks(span.Ticks / Math.Max(2, targetCount));

        // candidate steps
        (TimeSpan? span, int months, DateTickUnit unit)[] steps =
        [
            (TimeSpan.FromSeconds(1), 0, DateTickUnit.Second),
            (TimeSpan.FromSeconds(5), 0, DateTickUnit.Second),
            (TimeSpan.FromSeconds(15), 0, DateTickUnit.Second),
            (TimeSpan.FromSeconds(30), 0, DateTickUnit.Second),
            (TimeSpan.FromMinutes(1), 0, DateTickUnit.Minute),
            (TimeSpan.FromMinutes(5), 0, DateTickUnit.Minute),
            (TimeSpan.FromMinutes(15), 0, DateTickUnit.Minute),
            (TimeSpan.FromMinutes(30), 0, DateTickUnit.Minute),
            (TimeSpan.FromHours(1), 0, DateTickUnit.Hour),
            (TimeSpan.FromHours(3), 0, DateTickUnit.Hour),
            (TimeSpan.FromHours(6), 0, DateTickUnit.Hour),
            (TimeSpan.FromHours(12), 0, DateTickUnit.Hour),
            (TimeSpan.FromDays(1), 0, DateTickUnit.Day),
            (TimeSpan.FromDays(2), 0, DateTickUnit.Day),
            (TimeSpan.FromDays(7), 0, DateTickUnit.Day),
            (TimeSpan.FromDays(14), 0, DateTickUnit.Day),
            (null, 1, DateTickUnit.Month),
            (null, 3, DateTickUnit.Month),
            (null, 6, DateTickUnit.Month),
            (null, 12, DateTickUnit.Year),
            (null, 24, DateTickUnit.Year),
            (null, 60, DateTickUnit.Year),
            (null, 120, DateTickUnit.Year)
        ];

        var chosen = steps[^1];
        foreach (var s in steps)
        {
            var stepSpan = s.span ?? TimeSpan.FromDays(30.44 * s.months);
            if (stepSpan >= target)
            {
                chosen = s;
                break;
            }
        }

        if (chosen.months > 0)
        {
            var y = min.Year;
            var m = min.Month;
            var stepMonths = chosen.months;
            var first = new DateTime(y, m, 1);
            if (first < min) first = first.AddMonths(stepMonths);
            // align to step boundary
            while (true)
            {
                var t = first;
                if (t > max) break;
                ticks.Add(new Tick(t.ToOADate(), PlotFmt.Date(t, chosen.unit)));
                first = first.AddMonths(stepMonths);
                if (ticks.Count > 500) break;
            }
        }
        else
        {
            var step = chosen.span!.Value;
            var startTicks = (long)Math.Ceiling((double)min.Ticks / step.Ticks) * step.Ticks;
            for (var t = new DateTime(startTicks, min.Kind); t <= max; t = t.Add(step))
            {
                ticks.Add(new Tick(t.ToOADate(), PlotFmt.Date(t, chosen.unit)));
                if (ticks.Count > 500) break;
            }
        }

        return ticks;
    }
}
