using Avalonia;
using Avalonia.Threading;

namespace TinyPlot.Animation;

/// <summary>
/// 动画配置。
/// </summary>
public sealed class AnimationConfig
{
    /// <summary>动画持续时间（默认 750ms，与 ECharts 默认一致）。</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(750);

    /// <summary>缓动函数（默认 EaseInOutCubic，平滑过渡）。</summary>
    public Easing Easing { get; set; } = Easing.EaseInOut;

    /// <summary>动画延迟（默认 0）。</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
}

/// <summary>
/// 支持动态数据更新和平滑过渡的图表控件。
/// 所有图表类型均支持动画效果。
///
/// 用法：
/// <code>
/// var plot = new AnimatedPlot();
/// plot.Data.Add(new ScatterTrace { X = x, Y = y, Name = "系列" });
/// 
/// // 平滑过渡到新数据
/// plot.AnimateTo(anim => {
///     ((ScatterTrace)plot.Data[0]).X = newX;
///     ((ScatterTrace)plot.Data[0]).Y = newY;
/// });
/// </code>
/// </summary>
public class AnimatedPlot : Plot
{
    private readonly List<TraceAnimation> _animations = [];
    private DispatcherTimer? _timer;
    private DateTime _startTime;
    private bool _isAnimating;

    /// <summary>动画配置。</summary>
    public AnimationConfig Animation { get; } = new();

    /// <summary>是否正在播放动画。</summary>
    public new bool IsAnimating => _isAnimating;

    /// <summary>
    /// 执行带动画的数据更新。在回调中修改数据，动画引擎会自动
    /// 在旧数据和新数据之间平滑过渡。
    /// </summary>
    /// <param name="updateAction">在其中修改数据的回调。</param>
    /// <param name="config">可选的动画配置覆盖。</param>
    public void AnimateTo(Action updateAction, AnimationConfig? config = null)
    {
        var cfg = config ?? Animation;
        var easing = EasingFunctions.Get(cfg.Easing);

        // 1. 快照当前所有可动画化的数据
        var snapshots = CaptureSnapshots();

        // 2. 执行用户的数据修改
        updateAction();

        // 3. 捕获目标数据
        var targets = CaptureSnapshots();

        // 4. 创建动画任务
        _animations.Clear();
        foreach (var (trace, snapshot) in snapshots)
        {
            if (!targets.TryGetValue(trace, out var target)) continue;
            var anim = TraceAnimation.Create(trace, snapshot, target, easing);
            if (anim != null) _animations.Add(anim);
        }

        if (_animations.Count == 0) { Refresh(); return; }

        // 5. 启动动画定时器
        StartAnimation(cfg);
    }

    /// <summary>
    /// 快速更新单个序列的 Y 数据并动画过渡。
    /// </summary>
    public void UpdateY(int traceIndex, double[] newY, AnimationConfig? config = null)
    {
        if (traceIndex < 0 || traceIndex >= Data.Count) return;
        var trace = Data[traceIndex];
        AnimateTo(() =>
        {
            if (trace is ScatterTrace s) s.Y = newY;
            else if (trace is BarTrace b) b.Y = newY;
        }, config);
    }

    /// <summary>
    /// 快速更新单个序列的 X 和 Y 数据并动画过渡。
    /// </summary>
    public UpdateBuilder BeginUpdate() => new(this);

    private Dictionary<Trace, TraceSnapshot> CaptureSnapshots()
    {
        var result = new Dictionary<Trace, TraceSnapshot>();
        foreach (var trace in Data)
        {
            var snap = TraceSnapshot.Capture(trace);
            if (snap != null) result[trace] = snap;
        }
        return result;
    }

    private void StartAnimation(AnimationConfig cfg)
    {
        _timer?.Stop();
        _isAnimating = true;

        // 应用延迟
        if (cfg.Delay > TimeSpan.Zero)
        {
            var delayTimer = new DispatcherTimer { Interval = cfg.Delay };
            delayTimer.Tick += (_, _) =>
            {
                delayTimer.Stop();
                StartAnimationLoop(cfg);
            };
            delayTimer.Start();
        }
        else
        {
            StartAnimationLoop(cfg);
        }
    }

    private void StartAnimationLoop(AnimationConfig cfg)
    {
        _startTime = DateTime.UtcNow;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
        _timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - _startTime;
            var progress = Math.Clamp(elapsed.TotalMilliseconds / cfg.Duration.TotalMilliseconds, 0, 1);

            foreach (var anim in _animations)
                anim.Apply(progress);

            Refresh();

            if (progress >= 1)
            {
                _timer.Stop();
                _isAnimating = false;
                _animations.Clear();
            }
        };
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}

/// <summary>
/// 更新构建器，链式 API。
/// </summary>
public sealed class UpdateBuilder
{
    private readonly AnimatedPlot _plot;
    private Action? _action;

    internal UpdateBuilder(AnimatedPlot plot) { _plot = plot; }

    public UpdateBuilder SetY(int traceIndex, double[] y)
    {
        var trace = _plot.Data[traceIndex];
        _action += () =>
        {
            if (trace is ScatterTrace s) s.Y = y;
            else if (trace is BarTrace b) b.Y = y;
        };
        return this;
    }

    public UpdateBuilder SetX(int traceIndex, double[] x)
    {
        var trace = _plot.Data[traceIndex];
        _action += () =>
        {
            if (trace is ScatterTrace s) s.X = x;
            else if (trace is BarTrace b) b.X = x;
        };
        return this;
    }

    public UpdateBuilder SetValue(int traceIndex, double value)
    {
        var trace = _plot.Data[traceIndex];
        _action += () =>
        {
            if (trace is GaugeTrace g) g.Value = value;
        };
        return this;
    }

    public UpdateBuilder SetPieValues(int traceIndex, double[] values)
    {
        var trace = _plot.Data[traceIndex];
        _action += () =>
        {
            if (trace is PieTrace p) p.Values = values;
        };
        return this;
    }

    public UpdateBuilder SetRadarValues(int traceIndex, double[] values)
    {
        var trace = _plot.Data[traceIndex];
        _action += () =>
        {
            if (trace is RadarTrace r) r.Values = values;
        };
        return this;
    }

    public void Animate(AnimationConfig? config = null)
    {
        if (_action != null) _plot.AnimateTo(_action, config);
    }
}
