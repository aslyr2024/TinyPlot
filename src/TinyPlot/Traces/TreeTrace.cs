using Avalonia;
using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 树图布局方向。
/// </summary>
public enum TreeLayout
{
    /// <summary>从左到右（水平）。</summary>
    LeftToRight,
    /// <summary>从上到下（垂直）。</summary>
    TopToBottom
}

/// <summary>
/// 树图节点。
/// </summary>
public sealed class TreeNode
{
    /// <summary>节点名称。</summary>
    public required string Name { get; set; }

    /// <summary>节点值（用于节点大小/颜色映射）。</summary>
    public double Value { get; set; }

    /// <summary>子节点列表。</summary>
    public List<TreeNode> Children { get; } = [];

    /// <summary>指定颜色（null=使用色板）。</summary>
    public Color? Color { get; set; }
}

/// <summary>
/// 树图序列，对应 ECharts 的 tree 图。
/// 支持水平/垂直布局，矩形节点+连线。
/// </summary>
public class TreeTrace : Trace
{
    /// <summary>根节点。</summary>
    public TreeNode? Root { get; set; }

    /// <summary>布局方向。</summary>
    public TreeLayout Layout { get; set; } = TreeLayout.LeftToRight;

    /// <summary>节点间距（像素）。</summary>
    public double NodeGap { get; set; } = 20;

    /// <summary>层级间距（像素）。</summary>
    public double LevelGap { get; set; } = 120;

    /// <summary>节点圆角。</summary>
    public double CornerRadius { get; set; } = 4;

    /// <summary>视图 X 偏移（像素，用于平移）。</summary>
    public double ViewX { get; set; }

    /// <summary>视图 Y 偏移（像素，用于平移）。</summary>
    public double ViewY { get; set; }

    /// <summary>视图缩放比例（用于缩放）。</summary>
    public double ViewScale { get; set; } = 1.0;

    internal override bool IsCartesian => false;
    internal override bool SupportsPanZoom => true;

    private List<(Rect rect, TreeNode node, Color color)>? _nodeRects;

    internal override void Prepare(PlotCalcContext ctx) { ctx.SetCalc(this, null); }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Root == null) return;
        _nodeRects = [];

        var rect = rc.PlotRect;
        var colorway = rc.Colorway;

        // 应用视图变换
        var transformedRect = new Rect(
            rect.X + ViewX, rect.Y + ViewY,
            rect.Width * ViewScale, rect.Height * ViewScale);

        // 计算树的深度和每层节点数
        var depths = new Dictionary<TreeNode, int>();
        var layerCounts = new Dictionary<int, int>();
        MeasureTree(Root, 0, depths, layerCounts);

        var maxDepth = depths.Values.Max();
        var horizontal = Layout == TreeLayout.LeftToRight;
        var nodeH = 24 * ViewScale;
        var nodeW = horizontal ? 80 * ViewScale : 60 * ViewScale;

        // 计算每个节点的位置
        var positions = new Dictionary<TreeNode, Point>();

        // 从根节点开始分配位置
        var startY = transformedRect.Y + 10;
        var startX = transformedRect.X + 10;
        LayoutNodes(Root, 0, horizontal ? startX : startY, horizontal ? startY : startX,
            depths, layerCounts, positions, colorway);

        // 绘制连线
        DrawConnections(dc, rc, Root, positions, horizontal);

        // 绘制节点
        DrawNodes(dc, rc, Root, positions, nodeW, nodeH, colorway);
    }

    private void MeasureTree(TreeNode node, int depth, Dictionary<TreeNode, int> depths, Dictionary<int, int> counts)
    {
        depths[node] = depth;
        counts[depth] = counts.GetValueOrDefault(depth) + 1;
        foreach (var child in node.Children)
            MeasureTree(child, depth + 1, depths, counts);
    }

    private void LayoutNodes(TreeNode node, int depth, double x, double y,
        Dictionary<TreeNode, int> depths, Dictionary<int, int> counts,
        Dictionary<TreeNode, Point> positions, IReadOnlyList<Color> colorway)
    {
        positions[node] = new Point(x, y);

        if (node.Children.Count == 0) return;

        var childY = y;
        var childX = Layout == TreeLayout.LeftToRight ? x + LevelGap : x;
        var nextPos = Layout == TreeLayout.LeftToRight ? y : x;

        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            var cx = Layout == TreeLayout.LeftToRight ? childX : childY + i * (24 + NodeGap);
            var cy = Layout == TreeLayout.LeftToRight ? childY + i * (24 + NodeGap) : childX;
            LayoutNodes(child, depth + 1, cx, cy, depths, counts, positions, colorway);
        }
    }

    private void DrawConnections(DrawingContext dc, PlotRenderContext rc, TreeNode node,
        Dictionary<TreeNode, Point> positions, bool horizontal)
    {
        if (!positions.TryGetValue(node, out var p)) return;
        var pen = rc.Pen(rc.Theme.GridColor, 1.2);

        foreach (var child in node.Children)
        {
            if (!positions.TryGetValue(child, out var cp)) continue;
            if (horizontal)
            {
                var midX = (p.X + cp.X) / 2;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(p.X + 40, p.Y + 12), false);
                    ctx.CubicBezierTo(new Point(midX, p.Y + 12), new Point(midX, cp.Y + 12), new Point(cp.X, cp.Y + 12));
                    ctx.EndFigure(false);
                }
                dc.DrawGeometry(null, pen, geo);
            }
            else
            {
                var midY = (p.Y + cp.Y) / 2;
                dc.DrawLine(pen, new Point(p.X + 30, p.Y + 24), new Point(cp.X + 30, cp.Y));
            }

            DrawConnections(dc, rc, child, positions, horizontal);
        }
    }

    private void DrawNodes(DrawingContext dc, PlotRenderContext rc, TreeNode node,
        Dictionary<TreeNode, Point> positions, double nodeW, double nodeH, IReadOnlyList<Color> colorway)
    {
        if (!positions.TryGetValue(node, out var p)) return;

        var depth = 0;
        var color = node.Color ?? colorway[positions.Keys.ToList().IndexOf(node) % colorway.Count];
        var rect = new Rect(p.X, p.Y, nodeW, nodeH);
        _nodeRects?.Add((rect, node, color));

        dc.DrawRectangle(rc.Brush(color, 0.85), rc.Pen(Colors.White, 1), rect, CornerRadius, CornerRadius);

        var ft = rc.Text(node.Name, Colors.White, rc.Layout.FontSize - 1, FontWeight.Medium);
        dc.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2, rect.Y + (rect.Height - ft.Height) / 2));

        foreach (var child in node.Children)
            DrawNodes(dc, rc, child, positions, nodeW, nodeH, colorway);
    }

    internal override IEnumerable<LegendItem> GetLegendItems()
    {
        if (Name is { Length: > 0 } && ShowLegend)
            yield return new LegendItem { Label = Name, Color = ResolvedColor, Trace = this };
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (_nodeRects == null) yield break;
        foreach (var (rect, node, color) in _nodeRects)
        {
            if (!rect.Contains(pt)) continue;
            yield return new HoverTarget
            {
                ScreenPoint = pt, Trace = this, Color = color,
                Title = node.Name,
                XText = $"值: {PlotFmt.Number(node.Value)}",
                YText = $"子节点: {node.Children.Count}",
                Distance = 0
            };
        }
    }
}
