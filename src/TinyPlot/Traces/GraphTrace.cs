using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 关系图节点。
/// </summary>
public sealed class GraphNode
{
    /// <summary>节点 ID。</summary>
    public required string Id { get; set; }

    /// <summary>显示名称。</summary>
    public string? Name { get; set; }

    /// <summary>节点大小。</summary>
    public double Size { get; set; } = 20;

    /// <summary>节点颜色。</summary>
    public Color? Color { get; set; }

    /// <summary>X 坐标（0..1 相对绘图区，NaN=自动布局）。</summary>
    public double X { get; set; } = double.NaN;

    /// <summary>Y 坐标（0..1 相对绘图区，NaN=自动布局）。</summary>
    public double Y { get; set; } = double.NaN;
}

/// <summary>
/// 关系图边。
/// </summary>
public sealed class GraphEdge
{
    /// <summary>源节点 ID。</summary>
    public required string Source { get; set; }

    /// <summary>目标节点 ID。</summary>
    public required string Target { get; set; }

    /// <summary>边的权重/粗细。</summary>
    public double Weight { get; set; } = 1;

    /// <summary>边颜色。</summary>
    public Color? Color { get; set; }
}

/// <summary>
/// 关系图序列，对应 ECharts 的 graph 图。
/// 支持力导向布局和固定坐标布局。
/// </summary>
public class GraphTrace : Trace
{
    /// <summary>节点列表。</summary>
    public IReadOnlyList<GraphNode> Nodes { get; set; } = [];

    /// <summary>边列表。</summary>
    public IReadOnlyList<GraphEdge> Edges { get; set; } = [];

    /// <summary>是否使用力导向自动布局。</summary>
    public bool ForceLayout { get; set; } = true;

    /// <summary>力导向迭代次数。</summary>
    public int ForceIterations { get; set; } = 200;

    /// <summary>节点间斥力。</summary>
    public double Repulsion { get; set; } = 500;

    /// <summary>边的引力。</summary>
    public double Attraction { get; set; } = 0.1;

    /// <summary>是否显示节点标签。</summary>
    public bool ShowLabels { get; set; } = true;

    /// <summary>是否显示箭头（有向图）。</summary>
    public bool Directed { get; set; } = false;

    /// <summary>视图 X 偏移（像素，用于平移）。</summary>
    public double ViewX { get; set; }

    /// <summary>视图 Y 偏移（像素，用于平移）。</summary>
    public double ViewY { get; set; }

    /// <summary>视图缩放比例（用于缩放）。</summary>
    public double ViewScale { get; set; } = 1.0;

    internal override bool IsCartesian => false;
    internal override bool SupportsPanZoom => true;

    private List<(Point pos, GraphNode node, Color color)>? _nodePositions;
    private List<(Point from, Point to, Color color, double weight)>? _edgeLines;

    internal override void Prepare(PlotCalcContext ctx)
    {
        ctx.SetCalc(this, null);
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Nodes.Count == 0) return;

        var rect = rc.PlotRect;
        var colorway = rc.Colorway;
        var fontColor = rc.Layout.FontColor ?? rc.Theme.FontColor;

        // 应用视图变换
        var scaledRect = new Rect(
            rect.X + ViewX, rect.Y + ViewY,
            rect.Width * ViewScale, rect.Height * ViewScale);

        // 力导向布局（如果需要）
        var positions = new Dictionary<string, Point>();
        if (ForceLayout)
            ForceDirectedLayout(positions, scaledRect);
        else
        {
            foreach (var node in Nodes)
            {
                var x = double.IsNaN(node.X) ? 0.5 : node.X;
                var y = double.IsNaN(node.Y) ? 0.5 : node.Y;
                positions[node.Id] = new Point(scaledRect.X + x * scaledRect.Width, scaledRect.Y + y * scaledRect.Height);
            }
        }

        // 绘制边
        _edgeLines = [];
        var edgePen = rc.Pen(rc.Theme.GridColor, 1);
        foreach (var edge in Edges)
        {
            if (!positions.TryGetValue(edge.Source, out var sp) || !positions.TryGetValue(edge.Target, out var tp))
                continue;
            var color = edge.Color ?? rc.Theme.GridColor;
            var pen = rc.Pen(color, Math.Max(0.5, edge.Weight), opacity: 0.6);
            dc.DrawLine(pen, sp, tp);
            _edgeLines.Add((sp, tp, color, edge.Weight));

            // 箭头
            if (Directed)
            {
                var angle = Math.Atan2(tp.Y - sp.Y, tp.X - sp.X);
                var arrowLen = 8;
                var a1 = new Point(tp.X - arrowLen * Math.Cos(angle - 0.4), tp.Y - arrowLen * Math.Sin(angle - 0.4));
                var a2 = new Point(tp.X - arrowLen * Math.Cos(angle + 0.4), tp.Y - arrowLen * Math.Sin(angle + 0.4));
                dc.DrawLine(pen, tp, a1);
                dc.DrawLine(pen, tp, a2);
            }
        }

        // 绘制节点
        _nodePositions = [];
        for (var i = 0; i < Nodes.Count; i++)
        {
            var node = Nodes[i];
            if (!positions.TryGetValue(node.Id, out var p)) continue;
            var color = node.Color ?? colorway[i % colorway.Count];
            var r = node.Size / 2;
            dc.DrawEllipse(rc.Brush(color), rc.Pen(Colors.White, 1.5), p, r, r);
            _nodePositions.Add((p, node, color));

            if (ShowLabels && node.Name is { Length: > 0 })
            {
                var ft = rc.Text(node.Name, fontColor, rc.Layout.FontSize - 1);
                dc.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y + r + 3));
            }
        }
    }

    private void ForceDirectedLayout(Dictionary<string, Point> positions, Rect rect)
    {
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 初始化随机位置
        var rng = new Random(42);
        var vel = new Dictionary<string, (double dx, double dy)>();
        foreach (var node in Nodes)
        {
            if (!double.IsNaN(node.X) && !double.IsNaN(node.Y))
            {
                positions[node.Id] = new Point(rect.X + node.X * rect.Width, rect.Y + node.Y * rect.Height);
            }
            else
            {
                positions[node.Id] = new Point(cx + (rng.NextDouble() - 0.5) * rect.Width * 0.6, cy + (rng.NextDouble() - 0.5) * rect.Height * 0.6);
            }
            vel[node.Id] = (0, 0);
        }

        // 迭代力导向
        for (var iter = 0; iter < ForceIterations; iter++)
        {
            var forces = new Dictionary<string, (double fx, double fy)>();
            foreach (var node in Nodes) forces[node.Id] = (0, 0);

            // 斥力（所有节点对）
            for (var i = 0; i < Nodes.Count; i++)
            {
                for (var j = i + 1; j < Nodes.Count; j++)
                {
                    var p1 = positions[Nodes[i].Id];
                    var p2 = positions[Nodes[j].Id];
                    var dx = p1.X - p2.X;
                    var dy = p1.Y - p2.Y;
                    var dist = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                    var force = Repulsion / (dist * dist);
                    var fx = dx / dist * force;
                    var fy = dy / dist * force;
                    forces[Nodes[i].Id] = (forces[Nodes[i].Id].fx + fx, forces[Nodes[i].Id].fy + fy);
                    forces[Nodes[j].Id] = (forces[Nodes[j].Id].fx - fx, forces[Nodes[j].Id].fy - fy);
                }
            }

            // 引力（相连节点）
            foreach (var edge in Edges)
            {
                if (!positions.TryGetValue(edge.Source, out var sp) || !positions.TryGetValue(edge.Target, out var tp))
                    continue;
                var dx = tp.X - sp.X;
                var dy = tp.Y - sp.Y;
                var dist = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                var force = dist * Attraction;
                var fx = dx / dist * force;
                var fy = dy / dist * force;
                forces[edge.Source] = (forces[edge.Source].fx + fx, forces[edge.Source].fy + fy);
                forces[edge.Target] = (forces[edge.Target].fx - fx, forces[edge.Target].fy - fy);
            }

            // 中心引力
            foreach (var node in Nodes)
            {
                var p = positions[node.Id];
                forces[node.Id] = (forces[node.Id].fx + (cx - p.X) * 0.001, forces[node.Id].fy + (cy - p.Y) * 0.001);
            }

            // 更新位置
            var damping = 0.9 * (1 - (double)iter / ForceIterations);
            foreach (var node in Nodes)
            {
                var f = forces[node.Id];
                var v = vel[node.Id];
                v = (v.dx * damping + f.fx * 0.1, v.dy * damping + f.fy * 0.1);
                vel[node.Id] = v;
                var p = positions[node.Id];
                positions[node.Id] = new Point(
                    Math.Clamp(p.X + v.dx, rect.X + 20, rect.Right - 20),
                    Math.Clamp(p.Y + v.dy, rect.Y + 20, rect.Bottom - 20));
            }
        }
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (Name is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = Name, Color = ResolvedColor, Trace = this };
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (_nodePositions == null) yield break;
        foreach (var (pos, node, color) in _nodePositions)
        {
            var dx = pt.X - pos.X; var dy = pt.Y - pos.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist <= node.Size / 2 + 5)
            {
                yield return new HoverTarget
                {
                    ScreenPoint = pt, Trace = this, Color = color,
                    Title = node.Name ?? node.Id,
                    XText = $"大小: {PlotFmt.Number(node.Size)}",
                    YText = $"连接: {Edges.Count(e => e.Source == node.Id || e.Target == node.Id)}",
                    Distance = dist
                };
            }
        }
    }
}
