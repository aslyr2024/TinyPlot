using Avalonia.Media;

namespace TinyPlot;

/// <summary>图表标题（layout.title）。</summary>
public sealed class LayoutTitle
{
    /// <summary>标题文本。</summary>
    public string? Text { get; set; }

    /// <summary>水平锚点，0=左边缘，0.5=居中（plotly.js 默认值）。</summary>
    public double X { get; set; } = 0.5;

    /// <summary>标题字号。</summary>
    public double FontSize { get; set; } = 17;

    /// <summary>标题颜色。</summary>
    public Color? Color { get; set; }
}

/// <summary>外边距（设备无关像素），对应 plotly.js layout.margin。</summary>
public sealed class Margin
{
    public double Left { get; set; } = 0;
    public double Right { get; set; } = 0;
    public double Top { get; set; } = 0;
    public double Bottom { get; set; } = 0;
}

/// <summary>图例配置（layout.legend）。</summary>
public sealed class LegendModel
{
    /// <summary>水平位置（相对绘图区），1=右边缘（plotly.js 默认值 1.02）。</summary>
    public double X { get; set; } = 1.02;

    /// <summary>垂直位置（相对绘图区），1=顶部（plotly.js 默认值）。</summary>
    public double Y { get; set; } = 1;

    /// <summary>图例排列方向。</summary>
    public Orientation Orientation { get; set; } = Orientation.Vertical;

    /// <summary>背景色。</summary>
    public Color? Background { get; set; }

    /// <summary>边框色。</summary>
    public Color? BorderColor { get; set; }
}

/// <summary>
/// 布局对象，对应 plotly.js 的 layout。控制除数据以外的一切：
/// 坐标轴、标题、图例、颜色、悬停行为等。
/// </summary>
public sealed class Layout
{
    /// <summary>图表标题。</summary>
    public LayoutTitle Title { get; } = new();

    /// <summary>X 轴配置。</summary>
    public PlotAxis XAxis { get; } = new();

    /// <summary>Y 轴配置。</summary>
    public PlotAxis YAxis { get; } = new();

    /// <summary>是否显示图例。</summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>图例配置。</summary>
    public LegendModel Legend { get; } = new();

    /// <summary>外边距。</summary>
    public Margin Margin { get; } = new();

    /// <summary>悬停模式。</summary>
    public HoverMode HoverMode { get; set; } = HoverMode.Closest;

    /// <summary>多柱状图排列方式。</summary>
    public BarMode BarMode { get; set; } = BarMode.Group;

    /// <summary>相邻类目柱状图间距（类目宽度的比例，plotly.js bargap 默认 0.2）。</summary>
    public double BarGap { get; set; } = 0.2;

    /// <summary>分组模式下同类目内柱间距（plotly.js bargroupgap 默认 0）。</summary>
    public double BarGroupGap { get; set; } = 0.0;

    /// <summary>纸张背景色。</summary>
    public Color? PaperBackground { get; set; }

    /// <summary>绘图区背景色。</summary>
    public Color? PlotBackground { get; set; }

    /// <summary>颜色循环（layout.colorway），为 null 时使用主题默认色板。</summary>
    public IList<Color>? Colorway { get; set; }

    /// <summary>刻度标签基础字号。</summary>
    public double FontSize { get; set; } = 12;

    /// <summary>全局字体颜色。</summary>
    public Color? FontColor { get; set; }

    /// <summary>饼图尺寸比例（0..1，相对绘图区）。</summary>
    public double PieScale { get; set; } = 0.85;

    public Layout Clone() => (Layout)MemberwiseClone();
}

/// <summary>交互配置，对应 plotly.js 的 config。</summary>
public sealed class PlotConfig
{
    /// <summary>鼠标滚轮缩放（config.scrollZoom）。</summary>
    public bool ScrollZoom { get; set; } = true;

    /// <summary>双击重置坐标轴（config.doubleClick）。</summary>
    public bool DoubleClickReset { get; set; } = true;

    /// <summary>拖拽模式，默认为 Pan（平移）。</summary>
    public DragMode DragMode { get; set; } = DragMode.Pan;

    /// <summary>绘制从悬停点到坐标轴的虚线指引线。</summary>
    public bool ShowSpikes { get; set; } = true;

    /// <summary>点击图例条目切换序列可见性。</summary>
    public bool LegendClickToggle { get; set; } = true;
}
