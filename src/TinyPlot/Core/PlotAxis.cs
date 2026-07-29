using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 坐标轴定义，对应 plotly.js 的 layout.xaxis / layout.yaxis。
/// </summary>
public sealed class PlotAxis
{
    /// <summary>坐标轴标题。</summary>
    public string? Title { get; set; }

    /// <summary>坐标轴标题字号。</summary>
    public double TitleFontSize { get; set; } = 14;

    /// <summary>坐标轴类型。</summary>
    public AxisType Type { get; set; } = AxisType.Auto;

    /// <summary>是否自动计算范围（默认 true）。</summary>
    public bool AutoRange { get; set; } = true;

    /// <summary>显式范围 [min, max]。对数轴传原始值（非 log10），日期轴传 OLE Automation 日期。</summary>
    public double[]? Range { get; set; }

    /// <summary>是否显示网格线。</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>网格线颜色。</summary>
    public Color? GridColor { get; set; }

    /// <summary>0 在范围内时高亮零线。</summary>
    public bool ZeroLine { get; set; } = true;

    /// <summary>零线颜色。</summary>
    public Color? ZeroLineColor { get; set; }

    /// <summary>期望刻度数量。</summary>
    public int NTicks { get; set; } = 0;

    /// <summary>是否显示刻度标签。</summary>
    public bool ShowTickLabels { get; set; } = true;

    /// <summary>坐标轴颜色。</summary>
    public Color? Color { get; set; }

    /// <summary>刻度标签标准 .NET 数字格式字符串（如 "0.0"、"P0"）。日期轴支持日期格式。</summary>
    public string? TickFormat { get; set; }

    /// <summary>刻度标签后缀（如 "%"）。</summary>
    public string? TickSuffix { get; set; }

    /// <summary>刻度标签前缀（如 "$"）。</summary>
    public string? TickPrefix { get; set; }

    /// <summary>散点类数据的自动范围两侧填充比例。</summary>
    public double AutoRangePadding { get; set; } = 0.05;

    /// <summary>从悬停点向此轴绘制虚线指引线。</summary>
    public bool ShowSpikes { get; set; } = true;

    /// <summary>强制自动范围包含零（plotly.js rangemode="tozero"）。</summary>
    public bool RangeToZero { get; set; }

    public PlotAxis Clone() => (PlotAxis)MemberwiseClone();
}
