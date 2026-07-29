using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// 视觉主题。<see cref="PlotTheme.Plotly"/> 还原 plotly.js 默认模板，
/// <see cref="PlotTheme.PlotlyDark"/> 对应 "plotly_dark" 模板。
/// </summary>
public sealed class PlotTheme
{
    /// <summary>主题名称。</summary>
    public required string Name { get; init; }

    /// <summary>纸张背景色。</summary>
    public Color PaperBackground { get; init; }

    /// <summary>绘图区背景色。</summary>
    public Color PlotBackground { get; init; }

    /// <summary>默认字体颜色。</summary>
    public Color FontColor { get; init; }

    /// <summary>网格线颜色。</summary>
    public Color GridColor { get; init; }

    /// <summary>零线颜色。</summary>
    public Color ZeroLineColor { get; init; }

    /// <summary>坐标轴线颜色。</summary>
    public Color AxisLineColor { get; init; }

    /// <summary>指引线颜色。</summary>
    public Color SpikeColor { get; init; }

    /// <summary>悬停标签边框颜色。</summary>
    public Color HoverBorderColor { get; init; }

    /// <summary>框选缩放矩形填充色。</summary>
    public Color SelectionFill { get; init; }

    /// <summary>框选缩放矩形边框色。</summary>
    public Color SelectionStroke { get; init; }

    /// <summary>颜色循环。</summary>
    public required IReadOnlyList<Color> Colorway { get; init; }

    /// <summary>字体族列表（逗号分隔的回退链）。</summary>
    public string FontFamily { get; init; } = "Open Sans, Segoe UI, Helvetica, Arial, sans-serif";

    /// <summary>plotly.js 默认颜色循环。</summary>
    public static IReadOnlyList<Color> PlotlyColorway { get; } =
    [
        Color.Parse("#636efa"), Color.Parse("#EF553B"), Color.Parse("#00cc96"), Color.Parse("#ab63fa"),
        Color.Parse("#FFA15A"), Color.Parse("#19d3f3"), Color.Parse("#FF6692"), Color.Parse("#B6E880"),
        Color.Parse("#FF97FF"), Color.Parse("#FECB52")
    ];

    /// <summary>plotly.js 亮色主题。</summary>
    public static PlotTheme Plotly { get; } = new()
    {
        Name = "plotly",
        PaperBackground = Colors.White,
        PlotBackground = Colors.White,
        FontColor = Color.Parse("#2a3f5f"),
        GridColor = Color.Parse("#EBF0F8"),
        ZeroLineColor = Color.Parse("#EBF0F8"),
        AxisLineColor = Color.Parse("#2a3f5f"),
        SpikeColor = Color.Parse("#3c3c3c"),
        HoverBorderColor = Color.Parse("#2a3f5f"),
        SelectionFill = Color.FromArgb(28, 99, 110, 250),
        SelectionStroke = Color.Parse("#636efa"),
        Colorway = PlotlyColorway
    };

    /// <summary>plotly.js 暗色主题。</summary>
    public static PlotTheme PlotlyDark { get; } = new()
    {
        Name = "plotly_dark",
        PaperBackground = Color.Parse("#111111"),
        PlotBackground = Color.Parse("#111111"),
        FontColor = Color.Parse("#f2f5fa"),
        GridColor = Color.Parse("#283442"),
        ZeroLineColor = Color.Parse("#283442"),
        AxisLineColor = Color.Parse("#f2f5fa"),
        SpikeColor = Color.Parse("#c8d4e3"),
        HoverBorderColor = Color.Parse("#f2f5fa"),
        SelectionFill = Color.FromArgb(40, 99, 110, 250),
        SelectionStroke = Color.Parse("#636efa"),
        Colorway = PlotlyColorway
    };
}
