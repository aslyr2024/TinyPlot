using Avalonia;
using Avalonia.Media;
using TinyPlot.Geo;

namespace TinyPlot;

/// <summary>
/// 地图序列。渲染地理边界（国家/地区）并支持按数据值着色（等值区划图）。
/// 对应 ECharts 的 map 和 geo 组件。
/// </summary>
public class MapTrace : Trace
{
    /// <summary>要显示的国家/地区代码列表。为空时显示全部。</summary>
    public IReadOnlyList<string>? Regions { get; set; }

    /// <summary>各区域的值（与 Regions 一一对应，用于着色）。</summary>
    public IReadOnlyList<double>? Values { get; set; }

    /// <summary>区域名称（与 Values 对应，用于悬停标签）。</summary>
    public IReadOnlyList<string>? Labels { get; set; }

    /// <summary>颜色比例尺。</summary>
    public Colorscale Colorscale { get; set; } = Colorscale.Blues;

    /// <summary>显式最小值（NaN=自动）。</summary>
    public double MinValue { get; set; } = double.NaN;

    /// <summary>显式最大值（NaN=自动）。</summary>
    public double MaxValue { get; set; } = double.NaN;

    /// <summary>边界线颜色。</summary>
    public Color BorderColor { get; set; } = Avalonia.Media.Colors.White;

    /// <summary>边界线宽度。</summary>
    public double BorderWidth { get; set; } = 0.8;

    /// <summary>无数据区域的颜色。</summary>
    public Color NoDataColor { get; set; } = Avalonia.Media.Color.Parse("#eeeeee");

    /// <summary>是否显示色阶条。</summary>
    public bool ShowScale { get; set; } = true;

    internal override bool IsCartesian => false;

    internal MapCalc? Calc { get; private set; }

    internal override void Prepare(PlotCalcContext ctx)
    {
        var countries = WorldData.Countries;

        // 筛选要显示的区域
        IEnumerable<WorldData.Country> selected;
        if (Regions != null && Regions.Count > 0)
        {
            var set = new HashSet<string>(Regions, StringComparer.OrdinalIgnoreCase);
            selected = countries.Where(c => set.Contains(c.Code) || set.Contains(c.Name));
        }
        else
        {
            // 默认显示所有国家（排除轮廓辅助条目）
            selected = countries.Where(c => !c.Code.StartsWith('_'));
        }

        var list = selected.ToList();

        // 构建区域→值映射
        var valueMap = new Dictionary<string, double>();
        if (Values != null && Regions != null)
        {
            for (var i = 0; i < Math.Min(Regions.Count, Values.Count); i++)
                valueMap[Regions[i]] = Values[i];
        }

        // 计算颜色范围
        var vmin = double.PositiveInfinity;
        var vmax = double.NegativeInfinity;
        foreach (var (_, _, v) in list.Select(c => (c.Code, c.Name, valueMap.GetValueOrDefault(c.Code, double.NaN)))
                     .Where(t => !double.IsNaN(t.Item3)))
        {
            vmin = Math.Min(vmin, v);
            vmax = Math.Max(vmax, v);
        }

        if (double.IsInfinity(vmin)) (vmin, vmax) = (0, 1);
        if (!double.IsNaN(MinValue)) vmin = MinValue;
        if (!double.IsNaN(MaxValue)) vmax = MaxValue;
        if (vmin == vmax) vmax = vmin + 1;

        Calc = new MapCalc
        {
            Countries = list,
            ValueMap = valueMap,
            VMin = vmin,
            VMax = vmax
        };
        ctx.SetCalc(this, Calc);
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Calc is not { } calc) return;
        var rect = rc.PlotRect;

        foreach (var country in calc.Countries)
        {
            var pts = MapProjection.ToPixels(country.Boundary, rect);
            if (pts.Length < 3) continue;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(pts[0], true);
                for (var i = 1; i < pts.Length; i++) ctx.LineTo(pts[i]);
                ctx.EndFigure(true);
            }

            // 着色
            Color fill;
            if (calc.ValueMap.TryGetValue(country.Code, out var v))
            {
                var t = (v - calc.VMin) / (calc.VMax - calc.VMin);
                fill = Colorscale.GetColor(t);
            }
            else
            {
                fill = NoDataColor;
            }

            dc.DrawGeometry(rc.Brush(fill), rc.Pen(BorderColor, BorderWidth), geo);
        }
    }

    internal override IEnumerable<LegendItem> GetLegendItems() => [];

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (Calc is not { } calc) yield break;
        var rect = rc.PlotRect;

        // 反算经纬度
        var fracX = (pt.X - rect.X) / rect.Width;
        var fracY = (pt.Y - rect.Y) / rect.Height;
        if (fracX < 0 || fracX > 1 || fracY < 0 || fracY > 1) yield break;

        // 简化：逐国家判断点是否在多边形内
        foreach (var country in calc.Countries)
        {
            var pixels = MapProjection.ToPixels(country.Boundary, rect);
            if (pixels.Length < 3) continue;
            if (!PointInPolygon(pt, pixels)) continue;

            calc.ValueMap.TryGetValue(country.Code, out var v);
            yield return new HoverTarget
            {
                ScreenPoint = pt,
                Trace = this,
                Color = double.IsNaN(v) ? NoDataColor : Colorscale.GetColor((v - calc.VMin) / (calc.VMax - calc.VMin)),
                Title = country.Name,
                XText = $"代码: {country.Code}",
                YText = double.IsNaN(v) ? null : $"值: {PlotFmt.HoverValue(v)}",
                Distance = 0
            };
            yield break;
        }
    }

    private static bool PointInPolygon(Point p, Point[] poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) &&
                p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        }
        return inside;
    }

    internal sealed class MapCalc
    {
        public List<WorldData.Country> Countries { get; init; } = [];
        public Dictionary<string, double> ValueMap { get; init; } = new();
        public double VMin { get; init; }
        public double VMax { get; init; }
    }
}
