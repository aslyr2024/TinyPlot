using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TinyPlot;

/// <summary>
/// 动态数据时间轴控件。支持自动滚动的时间轴、实时数据追加，
/// 对应 ECharts 的"动态数据 + 时间坐标轴"功能。
/// </summary>
public class TimelinePlot : Plot
{
    private DispatcherTimer? _timer;
    private readonly List<TimelineSeries> _series = [];

    /// <summary>时间轴显示的时间窗口（默认 30 秒）。</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>数据更新间隔。</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>是否正在播放。</summary>
    public bool IsPlaying => _timer?.IsEnabled == true;

    /// <summary>添加一条时间序列。</summary>
    public TimelineSeries AddSeries(string name, Func<double, double>? generator = null)
    {
        var series = new TimelineSeries(this, name, generator ?? DefaultGenerator());
        _series.Add(series);
        Data.Add(series.Trace);
        return series;
    }

    /// <summary>开始自动播放。</summary>
    public void Play()
    {
        _timer ??= new DispatcherTimer { Interval = Interval };
        _timer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            foreach (var s in _series) s.Push(now);
            AutoScrollXAxis(now);
            Refresh();
        };
        _timer.Start();
    }

    /// <summary>暂停。</summary>
    public void Pause() => _timer?.Stop();

    /// <summary>重置所有数据。</summary>
    public void Reset()
    {
        _timer?.Stop();
        foreach (var s in _series) s.Clear();
        Refresh();
    }

    private void AutoScrollXAxis(DateTime now)
    {
        Layout.XAxis.Type = AxisType.Date;
        Layout.XAxis.Range = [now.Subtract(Window).ToOADate(), now.ToOADate()];
    }

    private static Func<double, double> DefaultGenerator()
    {
        var rng = new Random();
        var v = 50.0;
        return _ =>
        {
            v += (rng.NextDouble() - 0.5) * 5;
            return Math.Clamp(v, 0, 100);
        };
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}

/// <summary>
/// 时间轴序列。管理一条实时滚动的数据线。
/// </summary>
public sealed class TimelineSeries
{
    private readonly List<DateTime> _times = [];
    private readonly List<double> _values = [];
    private readonly TimelinePlot _plot;
    private readonly Func<double, double> _generator;
    private double _tick;

    internal ScatterTrace Trace { get; }

    internal TimelineSeries(TimelinePlot plot, string name, Func<double, double> generator)
    {
        _plot = plot;
        _generator = generator;
        Trace = new ScatterTrace
        {
            Mode = ScatterMode.Lines,
            Name = name,
            Line = { Shape = LineShape.Linear, Width = 2 }
        };
    }

    /// <summary>添加一个新数据点。</summary>
    public void Push(DateTime time)
    {
        var value = _generator(_tick++);
        _times.Add(time);
        _values.Add(value);

        // 保留窗口内的数据 + 少量余量
        var cutoff = time - _plot.Window - TimeSpan.FromSeconds(5);
        while (_times.Count > 0 && _times[0] < cutoff)
        {
            _times.RemoveAt(0);
            _values.RemoveAt(0);
        }

        Trace.X = _times.ToArray();
        Trace.Y = _values.ToArray();
    }

    /// <summary>清除所有数据。</summary>
    public void Clear()
    {
        _times.Clear();
        _values.Clear();
        _tick = 0;
        Trace.X = Array.Empty<DateTime>();
        Trace.Y = Array.Empty<double>();
    }
}
