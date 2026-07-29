using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TinyPlot;

public sealed class PlotClickEventArgs : EventArgs
{
    /// <summary>被点击的序列。</summary>
    public required Trace Trace { get; init; }

    /// <summary>数据点索引。</summary>
    public int PointIndex { get; init; }

    /// <summary>悬停文本。</summary>
    public string? Text { get; init; }
}

public sealed class PlotHoverEventArgs : EventArgs
{
    /// <summary>当前悬停目标列表。</summary>
    public required IReadOnlyList<HoverTarget> Targets { get; init; }
}

/// <summary>
/// 图表控件。向 Data 添加序列，配置 Layout —— 完全对标 plotly.js 的 data/layout/config 模型。
/// 
/// 交互方式：
///   左键拖拽 = 平移（丝滑直接修改范围）
///   右键拖拽 = 框选缩放
///   滚轮 = 缩放
///   双击 = 重置坐标轴
///   点击图例 = 切换序列可见性
/// </summary>
public partial class Plot : Control
{
    private PlotBuild? _build;
    private Point? _pressPoint;
    private Point _lastPointer;
    private bool _dragging;
    private bool _rightDragging;
    private Rect? _zoomRect;
    private List<HoverTarget> _hoverTargets = [];
    private Point _hoverPoint;
    private bool _hasHover;
    private readonly List<(Rect rect, LegendItem item)> _legendHitRects = [];
    private RadarAxis? _radarAxis;

    public Plot()
    {
        Data = new List<Trace>();
        Layout = new Layout();
        Config = new PlotConfig();
        Theme = PlotTheme.Plotly;
        ClipToBounds = true;
    }

    /// <summary>序列列表（plotly.js "data"）。</summary>
    public IList<Trace> Data { get; }

    /// <summary>布局对象（plotly.js "layout"）。</summary>
    public Layout Layout { get; set; }

    /// <summary>交互配置（plotly.js "config"）。</summary>
    public PlotConfig Config { get; }

    /// <summary>视觉主题。默认 <see cref="PlotTheme.Plotly"/>。</summary>
    public new PlotTheme Theme { get; set; }

    /// <summary>雷达图配置。当 Data 包含 RadarTrace 时使用。</summary>
    public RadarAxis? RadarAxis { get => _radarAxis; set => _radarAxis = value; }

    /// <summary>数据点被点击时触发。</summary>
    public event EventHandler<PlotClickEventArgs>? PlotClick;

    /// <summary>悬停目标变化时触发。</summary>
    public event EventHandler<PlotHoverEventArgs>? PlotHover;

    /// <summary>数据变更后调用此方法重新计算并重绘。</summary>
    public void Refresh() => InvalidateVisual();

    /// <summary>重置两个坐标轴为自动范围（plotly.js 双击行为）。</summary>
    public void ResetAxes()
    {
        Layout.XAxis.Range = null;
        Layout.YAxis.Range = null;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height;
        return new Size(Math.Min(w, 100000), Math.Min(h, 100000));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }

    // ================================================================ 指针交互

    /// <summary>当前是否有 3D 图表需要旋转交互。</summary>
    private bool Has3DTraces => Data.Any(t => t.Visible && t.Is3D);

    /// <summary>当前是否有支持缩放平移的非笛卡尔图表。</summary>
    private bool HasPanZoomTraces => Data.Any(t => t.Visible && t.SupportsPanZoom);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetPosition(this);
        _pressPoint = pt;
        _lastPointer = pt;
        _dragging = false;
        _rightDragging = false;

        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed || props.IsMiddleButtonPressed || props.IsRightButtonPressed)
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        var dx = pt.X - _lastPointer.X;
        var dy = pt.Y - _lastPointer.Y;

        // 3D 图表：左键拖拽 = 旋转，右键拖拽 = 缩放
        if (_pressPoint != null && Has3DTraces)
        {
            if (props.IsLeftButtonPressed)
            {
                if (!_dragging && Distance(pt, _pressPoint.Value) > 2) _dragging = true;
                if (_dragging)
                {
                    Rotate3D(dx, dy);
                    _lastPointer = pt;
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }
            if (props.IsRightButtonPressed)
            {
                if (!_rightDragging && Distance(pt, _pressPoint.Value) > 2) _rightDragging = true;
                if (_rightDragging)
                {
                    Zoom3D(dy);
                    _lastPointer = pt;
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }
        }

        // 树图/关系图：左键拖拽 = 平移，滚轮 = 缩放（在 WheelChanged 中处理）
        if (_pressPoint != null && HasPanZoomTraces && !Has3DTraces)
        {
            if (props.IsLeftButtonPressed)
            {
                if (!_dragging && Distance(pt, _pressPoint.Value) > 2) _dragging = true;
                if (_dragging)
                {
                    PanView(dx, dy);
                    _lastPointer = pt;
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }
        }

        // 笛卡尔图表：左键拖拽 = 平移，右键拖拽 = 框选缩放
        if (_pressPoint != null && props.IsLeftButtonPressed && !Has3DTraces && !HasPanZoomTraces)
        {
            if (!_dragging && Distance(pt, _pressPoint.Value) > 3) _dragging = true;
            if (_dragging)
            {
                PanByDirect(dx, dy);
                _lastPointer = pt;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        // 右键拖拽 = 框选缩放（笛卡尔图表）
        if (_pressPoint != null && props.IsRightButtonPressed && !Has3DTraces)
        {
            if (!_rightDragging && Distance(pt, _pressPoint.Value) > 4) _rightDragging = true;
            if (_rightDragging)
            {
                var p0 = _pressPoint.Value;
                _zoomRect = new Rect(Math.Min(p0.X, pt.X), Math.Min(p0.Y, pt.Y), Math.Abs(pt.X - p0.X), Math.Abs(pt.Y - p0.Y));
                _lastPointer = pt;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        // 中键拖拽 = 平移
        if (_pressPoint != null && props.IsMiddleButtonPressed)
        {
            if (Has3DTraces) Zoom3D(dy);
            else if (HasPanZoomTraces) PanView(dx, dy);
            else PanByDirect(dx, dy);
            _lastPointer = pt;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        _lastPointer = pt;
        UpdateHover(pt);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pt = e.GetPosition(this);

        if (_rightDragging && _zoomRect is { } zr && _build?.Context != null)
        {
            ApplyZoomRect(zr);
            _zoomRect = null;
        }
        else if (!_dragging && !_rightDragging && _pressPoint != null)
        {
            HandleClick(pt);
        }

        _dragging = false;
        _rightDragging = false;
        _pressPoint = null;
        e.Pointer.Capture(null);
        UpdateHover(pt);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hasHover = false;
        _hoverTargets = [];
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        // 3D 图表：滚轮缩放
        if (Has3DTraces)
        {
            Zoom3D(-e.Delta.Y * 30);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // 树图/关系图：滚轮缩放
        if (HasPanZoomTraces)
        {
            var factor = Math.Pow(1.15, -e.Delta.Y);
            ZoomView(factor);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // 笛卡尔图表：滚轮缩放
        if (!Config.ScrollZoom || _build?.Context is not { } rc) return;
        if (!rc.PlotRect.Contains(e.GetPosition(this))) return;

        var pt = e.GetPosition(this);
        var f = Math.Pow(1.25, -e.Delta.Y);
        ZoomAxis(Layout.XAxis, rc.XAxis, (pt.X - rc.PlotRect.X) / rc.PlotRect.Width, f);
        ZoomAxis(Layout.YAxis, rc.YAxis, (rc.PlotRect.Bottom - pt.Y) / rc.PlotRect.Height, f);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        if (Config.DoubleClickReset)
        {
            // 重置 3D 视图
            foreach (var trace in Data)
            {
                if (trace is SurfaceTrace s) { s.RotationX = 30; s.RotationZ = -60; s.Scale = 0.7; }
                else if (trace is Scatter3DTrace sc) { sc.RotationX = 30; sc.RotationZ = -60; sc.Scale = 0.6; }
                else if (trace is TreeTrace t) { t.ViewX = 0; t.ViewY = 0; t.ViewScale = 1; }
                else if (trace is GraphTrace g) { g.ViewX = 0; g.ViewY = 0; g.ViewScale = 1; }
            }
            ResetAxes();
            e.Handled = true;
        }
    }

    // ================================================================ 辅助方法

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void ApplyZoomRect(Rect zr)
    {
        var rc = _build!.Context!;
        if (zr.Width > 10)
        {
            var lo = rc.XAxis.RawValueAt((zr.Left - rc.PlotRect.X) / rc.PlotRect.Width);
            var hi = rc.XAxis.RawValueAt((zr.Right - rc.PlotRect.X) / rc.PlotRect.Width);
            Layout.XAxis.Range = [Math.Min(lo, hi), Math.Max(lo, hi)];
        }
        if (zr.Height > 10)
        {
            var hi = rc.YAxis.RawValueAt((rc.PlotRect.Bottom - zr.Top) / rc.PlotRect.Height);
            var lo = rc.YAxis.RawValueAt((rc.PlotRect.Bottom - zr.Bottom) / rc.PlotRect.Height);
            Layout.YAxis.Range = [Math.Min(lo, hi), Math.Max(lo, hi)];
        }
    }

    /// <summary>
    /// 直接平移轴范围（不经过完整 PlotCalculator 重算，丝滑响应）。
    /// 1:1 像素映射到数据空间，与 plotly.js 平移手感一致。
    /// </summary>
    private void PanByDirect(double dxPx, double dyPx)
    {
        if (_build?.Context is not { } rc) return;
        // X 轴：像素正方向 = 数据负方向
        var xSpan = rc.XAxis.Max - rc.XAxis.Min;
        var ySpan = rc.YAxis.Max - rc.YAxis.Min;
        var xDelta = -dxPx / rc.PlotRect.Width * xSpan;
        var yDelta = dyPx / rc.PlotRect.Height * ySpan;

        Layout.XAxis.Range = [rc.XAxis.Untransform(rc.XAxis.Min + xDelta), rc.XAxis.Untransform(rc.XAxis.Max + xDelta)];
        Layout.YAxis.Range = [rc.YAxis.Untransform(rc.YAxis.Min + yDelta), rc.YAxis.Untransform(rc.YAxis.Max + yDelta)];
    }

    private static void ZoomAxis(PlotAxis axis, AxisState state, double fraction, double factor)
    {
        var center = state.ValueAt(fraction);
        var lo = center + (state.Min - center) * factor;
        var hi = center + (state.Max - center) * factor;
        axis.Range = [state.Untransform(lo), state.Untransform(hi)];
    }

    /// <summary>3D 图表旋转（修改 RotationX/RotationZ）。</summary>
    private void Rotate3D(double dxPx, double dyPx)
    {
        foreach (var trace in Data)
        {
            if (!trace.Visible || !trace.Is3D) continue;
            if (trace is SurfaceTrace s)
            {
                s.RotationZ += dxPx * 0.5;
                s.RotationX = Math.Clamp(s.RotationX - dyPx * 0.5, -89, 89);
            }
            else if (trace is Scatter3DTrace sc)
            {
                sc.RotationZ += dxPx * 0.5;
                sc.RotationX = Math.Clamp(sc.RotationX - dyPx * 0.5, -89, 89);
            }
        }
    }

    /// <summary>3D 图表缩放（修改 Scale）。</summary>
    private void Zoom3D(double dyPx)
    {
        foreach (var trace in Data)
        {
            if (!trace.Visible || !trace.Is3D) continue;
            if (trace is SurfaceTrace s)
                s.Scale = Math.Clamp(s.Scale * (1 - dyPx * 0.005), 0.1, 3);
            else if (trace is Scatter3DTrace sc)
                sc.Scale = Math.Clamp(sc.Scale * (1 - dyPx * 0.005), 0.1, 3);
        }
    }

    /// <summary>树图/关系图视图平移。</summary>
    private void PanView(double dxPx, double dyPx)
    {
        foreach (var trace in Data)
        {
            if (!trace.Visible || !trace.SupportsPanZoom) continue;
            if (trace is TreeTrace t) { t.ViewX += dxPx; t.ViewY += dyPx; }
            else if (trace is GraphTrace g) { g.ViewX += dxPx; g.ViewY += dyPx; }
        }
    }

    /// <summary>树图/关系图视图缩放。</summary>
    private void ZoomView(double factor)
    {
        foreach (var trace in Data)
        {
            if (!trace.Visible || !trace.SupportsPanZoom) continue;
            if (trace is TreeTrace t) t.ViewScale = Math.Clamp(t.ViewScale * factor, 0.1, 5);
            else if (trace is GraphTrace g) g.ViewScale = Math.Clamp(g.ViewScale * factor, 0.1, 5);
        }
    }

    private void HandleClick(Point pt)
    {
        if (Config.LegendClickToggle)
        {
            foreach (var (rect, item) in _legendHitRects)
            {
                if (!rect.Contains(pt) || item.Trace == null) continue;
                if (item.Trace is PieTrace pie && item.Tag is string label)
                {
                    if (!pie.HiddenLabels.Remove(label)) pie.HiddenLabels.Add(label);
                }
                else
                {
                    item.Trace.Visible = !item.Trace.Visible;
                }
                InvalidateVisual();
                return;
            }
        }

        UpdateHover(pt, suppressEvent: true);
        var target = _hoverTargets.OrderBy(t => t.Distance).FirstOrDefault();
        if (target != null && target.Distance < 20)
        {
            PlotClick?.Invoke(this, new PlotClickEventArgs
            {
                Trace = target.Trace,
                PointIndex = target.Tag is int i ? i : -1,
                Text = target.Title
            });
        }
    }

    private void UpdateHover(Point pt, bool suppressEvent = false)
    {
        if (Layout.HoverMode == HoverMode.None) { _hasHover = false; return; }

        _hoverPoint = pt;
        var targets = new List<HoverTarget>();

        // 通用渲染上下文（用于所有非笛卡尔类型）
        var generalRc = _build?.Context ?? (_build != null ? PieRenderContext(_build) : null);

        if (generalRc != null)
        {
            // 笛卡尔图表
            if (generalRc.PlotRect.Contains(pt))
            {
                foreach (var trace in Data)
                {
                    if (!trace.Visible) continue;
                    if (trace.IsCartesian || trace is MapTrace or GaugeTrace or RadarTrace
                        or TreeTrace or GraphTrace or SurfaceTrace or Scatter3DTrace)
                    {
                        targets.AddRange(trace.HitTest(generalRc, pt, Layout.HoverMode));
                    }
                }
            }

            // 饼图（可能在独立的 cell 区域）
            if (_build is { Pies.Count: > 0 } b)
            {
                foreach (var (pie, cell) in PieCells(b))
                {
                    if (!cell.Contains(pt)) continue;
                    targets.AddRange(pie.HitTest(generalRc, pt, Layout.HoverMode));
                }
            }
        }

        _hoverTargets = Layout.HoverMode == HoverMode.Closest
            ? targets.OrderBy(t => t.Distance).Take(1).ToList()
            : targets.GroupBy(t => t.Trace).Select(g => g.OrderBy(t => t.Distance).First()).ToList();
        _hasHover = _hoverTargets.Count > 0;

        if (!suppressEvent && _hasHover)
            PlotHover?.Invoke(this, new PlotHoverEventArgs { Targets = _hoverTargets });

        InvalidateVisual();
    }
}
