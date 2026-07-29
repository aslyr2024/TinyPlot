using Avalonia.Media;

namespace TinyPlot.Animation;

/// <summary>
/// 序列数据快照。捕获一个 Trace 在某一时刻的所有可动画化数据。
/// </summary>
internal sealed class TraceSnapshot
{
    public double[]? X { get; init; }
    public double[]? Y { get; init; }
    public double[]? Values { get; init; }
    public double SingleValue { get; init; }
    public Color[]? Colors { get; init; }
    public double[]? RadarValues { get; init; }
    public double[]? Open { get; init; }
    public double[]? High { get; init; }
    public double[]? Low { get; init; }
    public double[]? Close { get; init; }

    /// <summary>捕获当前 Trace 的快照。</summary>
    public static TraceSnapshot? Capture(Trace trace)
    {
        return trace switch
        {
            ScatterTrace s => new TraceSnapshot
            {
                X = ToDoubleArray(s.X),
                Y = ToDoubleArray(s.Y)
            },
            BarTrace b => new TraceSnapshot
            {
                X = ToDoubleArray(b.X),
                Y = ToDoubleArray(b.Y)
            },
            PieTrace p => new TraceSnapshot
            {
                Values = p.Values.ToArray()
            },
            GaugeTrace g => new TraceSnapshot
            {
                SingleValue = g.Value
            },
            RadarTrace r => new TraceSnapshot
            {
                RadarValues = r.Values.ToArray()
            },
            FunctionTrace => null,
            HistogramTrace => null,
            BoxTrace => null,
            CandlestickTrace c => new TraceSnapshot
            {
                X = ToDoubleArray(c.X),
                Open = ToDoubleArray(c.Open),
                High = ToDoubleArray(c.High),
                Low = ToDoubleArray(c.Low),
                Close = ToDoubleArray(c.Close)
            },
            HeatmapTrace => null,
            _ => null
        };
    }

    private static double[] ToDoubleArray(DataSeries? ds)
    {
        if (ds == null) return [];
        var result = new double[ds.Count];
        for (var i = 0; i < ds.Count; i++) result[i] = ds.AsNumber(i);
        return result;
    }
}

/// <summary>
/// 序列动画任务。在快照和目标之间插值并应用到 Trace。
/// </summary>
internal sealed class TraceAnimation
{
    private readonly Trace _trace;
    private readonly TraceSnapshot _from;
    private readonly TraceSnapshot _to;
    private readonly Func<double, double> _easing;

    private TraceAnimation(Trace trace, TraceSnapshot from, TraceSnapshot to, Func<double, double> easing)
    {
        _trace = trace;
        _from = from;
        _to = to;
        _easing = easing;
    }

    /// <summary>在 t=0..1 的进度下应用插值。</summary>
    public void Apply(double t)
    {
        var et = _easing(t);

        switch (_trace)
        {
            case ScatterTrace s:
                if (_from.Y != null || _to.Y != null)
                    s.Y = Interpolator.Lerp(_from.Y ?? [], _to.Y ?? [], et);
                if (_from.X != null || _to.X != null)
                    s.X = Interpolator.Lerp(_from.X ?? [], _to.X ?? [], et);
                break;

            case BarTrace b:
                if (_from.Y != null || _to.Y != null)
                    b.Y = Interpolator.Lerp(_from.Y ?? [], _to.Y ?? [], et);
                break;

            case PieTrace p:
                if (_from.Values != null || _to.Values != null)
                    p.Values = Interpolator.Lerp(_from.Values ?? [], _to.Values ?? [], et);
                break;

            case GaugeTrace g:
                g.Value = Interpolator.Lerp(_from.SingleValue, _to.SingleValue, et);
                break;

            case RadarTrace r:
                if (_from.RadarValues != null || _to.RadarValues != null)
                    r.Values = Interpolator.Lerp(_from.RadarValues ?? [], _to.RadarValues ?? [], et);
                break;

            case CandlestickTrace c:
                if (_from.Open != null && _to.Open != null)
                    c.Open = Interpolator.Lerp(_from.Open, _to.Open, et);
                if (_from.High != null && _to.High != null)
                    c.High = Interpolator.Lerp(_from.High, _to.High, et);
                if (_from.Low != null && _to.Low != null)
                    c.Low = Interpolator.Lerp(_from.Low, _to.Low, et);
                if (_from.Close != null && _to.Close != null)
                    c.Close = Interpolator.Lerp(_from.Close, _to.Close, et);
                break;
        }
    }

    /// <summary>创建动画任务（如果该类型支持动画）。</summary>
    public static TraceAnimation? Create(Trace trace, TraceSnapshot from, TraceSnapshot to, Func<double, double> easing)
    {
        // 只有数据实际变化时才创建动画
        if (!HasChanged(from, to)) return null;
        return new TraceAnimation(trace, from, to, easing);
    }

    private static bool HasChanged(TraceSnapshot from, TraceSnapshot to)
    {
        if (from.SingleValue != to.SingleValue) return true;
        if (!ArraysEqual(from.Y, to.Y)) return true;
        if (!ArraysEqual(from.X, to.X)) return true;
        if (!ArraysEqual(from.Values, to.Values)) return true;
        if (!ArraysEqual(from.RadarValues, to.RadarValues)) return true;
        if (!ArraysEqual(from.Open, to.Open)) return true;
        if (!ArraysEqual(from.Close, to.Close)) return true;
        return false;
    }

    private static bool ArraysEqual(double[]? a, double[]? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > 1e-9) return false;
        return true;
    }
}
