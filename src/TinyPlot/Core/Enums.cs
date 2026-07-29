namespace TinyPlot;

/// <summary>轴值类型，对应 plotly.js 的 axis.type。</summary>
public enum AxisType
{
    /// <summary>自动检测。</summary>
    Auto,
    /// <summary>线性数值轴。</summary>
    Linear,
    /// <summary>类目轴。</summary>
    Category,
    /// <summary>对数轴。</summary>
    Log,
    /// <summary>日期时间轴。</summary>
    Date
}

/// <summary>悬停模式，对应 plotly.js 的 layout.hovermode。</summary>
public enum HoverMode
{
    /// <summary>最近点（跨所有序列）。</summary>
    Closest,
    /// <summary>统一悬停标签（按 x 值对齐所有序列）。</summary>
    XUnified,
    /// <summary>按 x 值分别显示悬停标签。</summary>
    X,
    /// <summary>禁用悬停。</summary>
    None
}

/// <summary>多柱状图排列方式，对应 plotly.js 的 layout.barmode。</summary>
public enum BarMode
{
    /// <summary>分组排列。</summary>
    Group,
    /// <summary>堆叠排列。</summary>
    Stack
}

/// <summary>左键拖拽行为。</summary>
public enum DragMode
{
    /// <summary>框选缩放。</summary>
    Zoom,
    /// <summary>平移拖拽（默认）。</summary>
    Pan
}

[Flags]
public enum ScatterMode
{
    Markers = 1,
    Lines = 2,
    Text = 4,
    LinesMarkers = Lines | Markers,
    LinesText = Lines | Text,
    MarkersText = Markers | Text,
    LinesMarkersText = Lines | Markers | Text
}

/// <summary>标记符号形状。</summary>
public enum MarkerSymbol
{
    Circle,
    Square,
    Diamond,
    Cross,
    X,
    TriangleUp,
    TriangleDown,
    Star,
    Pentagon,
    Hexagon
}

/// <summary>折线形状。</summary>
public enum LineShape
{
    /// <summary>直线段（默认）。</summary>
    Linear,
    /// <summary>样条平滑曲线（Catmull-Rom）。</summary>
    Spline,
    /// <summary>水平阶梯线（先水平后垂直）。</summary>
    Hv,
    /// <summary>垂直阶梯线（先垂直后水平）。</summary>
    Vh,
    /// <summary>水平-垂直-水平阶梯。</summary>
    Hvh,
    /// <summary>垂直-水平-垂直阶梯。</summary>
    Vhv
}

/// <summary>折线填充模式。</summary>
public enum ScatterFill
{
    /// <summary>无填充。</summary>
    None,
    /// <summary>填充至 y=0 基线。</summary>
    ToZeroY,
    /// <summary>填充至前一条折线（堆叠面积）。</summary>
    ToNextY
}

/// <summary>方向。</summary>
public enum Orientation
{
    Vertical,
    Horizontal
}

/// <summary>文本标签位置。</summary>
public enum TraceTextPosition
{
    None,
    Auto,
    Inside,
    Outside,
    TopCenter,
    TopLeft,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomCenter,
    BottomLeft,
    BottomRight
}

public enum TickLabelMode
{
    Auto,
    Number,
    Percent
}
