# TinyPlot

基于 **Avalonia 12.1.0** 的交互式可视化库，API 设计全面对标 plotly.js，图表类型参照 ECharts。

## 特性一览

- **14 种图表类型**：折线、柱状、饼图、散点、热力图、箱线图、K线图、直方图、雷达图、地图、仪表盘、树图、关系图、3D曲面/散点
- **plotly.js 风格 API**：`data` / `layout` / `config` 三件套
- **丰富交互**：悬停标签、框选缩放、平移、滚轮缩放、图例切换、3D 旋转
- **平滑动画**：所有图表支持动态数据更新 + 7 种缓动函数（Elastic/Back/Bounce...）
- **19 种内置色阶**：Viridis、Plasma、RdBu、Jet 等
- **双主题**：plotly（亮色）/ plotly_dark（暗色）

---

## 快速开始

```csharp
using TinyPlot;

var plot = new Plot();
plot.Data.Add(new ScatterTrace
{
    X = new double[] { 1, 2, 3, 4, 5 },
    Y = new double[] { 10, 15, 13, 17, 20 },
    Mode = ScatterMode.LinesMarkers,
    Name = "示例数据"
});
plot.Layout.Title.Text = "我的第一个图表";
```

## 交互方式

| 图表类型 | 左键拖拽 | 右键拖拽 | 滚轮 | 双击 |
|---|---|---|---|---|
| 笛卡尔图表 | 平移 | 框选缩放 | 缩放 | 重置坐标轴 |
| 3D 图表 | 旋转 | 缩放 | 缩放 | 重置视角 |
| 树图 / 关系图 | 平移 | — | 缩放 | 重置视图 |

---

## 折线图 (ScatterTrace)

### 基础折线图

```csharp
var plot = new Plot();
plot.Layout.Title.Text = "基础折线图";
plot.Layout.XAxis.Title = "天数";
plot.Layout.YAxis.Title = "访问量";

double[] x = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
double[] y = { 120, 132, 101, 134, 90, 230, 210, 182, 192, 234 };

plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.LinesMarkers,
    Name = "直接访问"
});
```

### 平滑折线图

```csharp
plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.Lines,
    Name = "平滑线",
    Line = { Shape = LineShape.Spline, Width = 2.5 }  // Catmull-Rom 样条
});
```

### 阶梯折线图

```csharp
plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.Lines,
    Name = "阶梯线",
    Line = { Shape = LineShape.Hv }  // Hv / Vh / Hvh / Vhv
});
```

### 虚线样式

```csharp
plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.Lines,
    Name = "虚线",
    Line = { Dash = new double[] { 6, 3 } }  // 6px 实线 + 3px 间隔
});
```

### 面积图

```csharp
// 填充到 y=0
plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.Lines,
    Name = "面积",
    Fill = ScatterFill.ToZeroY,
    Line = { Shape = LineShape.Spline }
});

// 堆叠面积（填充到前一条线）
plot.Data.Add(new ScatterTrace { X = x, Y = y1, Mode = ScatterMode.Lines, Name = "系列A", Fill = ScatterFill.ToZeroY });
plot.Data.Add(new ScatterTrace { X = x, Y = y2, Mode = ScatterMode.Lines, Name = "系列B", Fill = ScatterFill.ToNextY });
```

### 置信带

```csharp
double[] upper = ..., lower = ..., mean = ...;
plot.Data.Add(new ScatterTrace { X = x, Y = lower, Mode = ScatterMode.Lines, Line = { Width = 1 }, ShowLegend = false });
plot.Data.Add(new ScatterTrace
{
    X = x, Y = upper,
    Mode = ScatterMode.Lines,
    Fill = ScatterFill.ToNextY,
    FillColor = Color.Parse("#636efa"),
    Name = "置信带"
});
plot.Data.Add(new ScatterTrace { X = x, Y = mean, Mode = ScatterMode.Lines, Name = "均值", Line = { Width = 2.5 } });
```

### 标记符号

```csharp
plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.Markers,
    Marker = {
        Size = 10,
        Symbol = MarkerSymbol.Diamond,  // Circle/Square/Diamond/Cross/X/TriangleUp/Star...
        Color = Color.Parse("#636efa"),
        OutlineColor = Colors.White,
        OutlineWidth = 1
    }
});
```

### 按值着色（色阶映射）

```csharp
plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.Markers,
    Marker = {
        Size = 12,
        ColorValues = values,           // 按此数组映射颜色
        Colorscale = Colorscale.Plasma, // Viridis/Plasma/RdBu/Jet...
        OutlineColor = Colors.White
    }
});
```

### 文本标签

```csharp
plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.LinesMarkersText,
    Text = labels,  // 每个点的标签
    TextPosition = TraceTextPosition.TopCenter
});
```

---

## 柱状图 (BarTrace)

### 基础柱状图

```csharp
plot.Data.Add(new BarTrace
{
    X = new string[] { "Q1", "Q2", "Q3", "Q4" },
    Y = new double[] { 120, 160, 140, 190 },
    Name = "EMEA",
    TextPosition = TraceTextPosition.Outside  // 显示数值标签
});
```

### 分组柱状图

```csharp
plot.Data.Add(new BarTrace { X = quarters, Y = emea, Name = "EMEA" });
plot.Data.Add(new BarTrace { X = quarters, Y = amer, Name = "AMER" });
plot.Data.Add(new BarTrace { X = quarters, Y = apac, Name = "APAC" });
// 默认 BarMode.Group
```

### 堆叠柱状图

```csharp
plot.Layout.BarMode = BarMode.Stack;
plot.Data.Add(new BarTrace { X = months, Y = infra, Name = "基建", TextPosition = TraceTextPosition.Inside });
plot.Data.Add(new BarTrace { X = months, Y = rd,    Name = "研发", TextPosition = TraceTextPosition.Inside });
```

### 水平条形图

```csharp
plot.Data.Add(new BarTrace
{
    Y = new string[] { "Rust", "Python", "TypeScript", "Go" },
    X = new double[] { 86, 78, 71, 63 },
    Orientation = Orientation.Horizontal,
    Name = "喜爱度"
});
```

### 自定义颜色

```csharp
plot.Data.Add(new BarTrace
{
    X = items, Y = values,
    Marker = {
        Colors = new Color[] { Colors.Green, Colors.Red, Colors.Blue, ... }  // 每柱独立颜色
    }
});
```

### 正负条形图

```csharp
double[] vals = { -120, -80, 45, 200 };
var colors = vals.Select(v => v >= 0 ? Color.Parse("#00cc96") : Color.Parse("#EF553B")).ToArray();
plot.Data.Add(new BarTrace { X = items, Y = vals, Marker = { Colors = colors } });
```

---

## 饼图 (PieTrace)

### 基础饼图

```csharp
plot.Data.Add(new PieTrace
{
    Labels = new string[] { "直接访问", "邮件营销", "联盟广告", "搜索引擎" },
    Values = new double[] { 335, 310, 234, 948 }
});
```

### 环形图

```csharp
plot.Data.Add(new PieTrace
{
    Labels = labels, Values = values,
    Hole = 0.5  // 内圈半径比例
});
```

### 半环形图

```csharp
plot.Data.Add(new PieTrace
{
    Labels = labels, Values = values,
    Hole = 0.5,
    Rotation = 225,    // 起始角度
    Sort = false,
    TextInfo = PieTextInfo.LabelPercent
});
```

### 南丁格尔玫瑰图

```csharp
plot.Data.Add(new PieTrace
{
    Labels = labels, Values = values,
    Sort = true,
    TextInfo = PieTextInfo.LabelPercent
});
```

### 自定义颜色

```csharp
plot.Data.Add(new PieTrace
{
    Labels = labels, Values = values,
    Colors = new Color[] { Color.Parse("#636efa"), Color.Parse("#EF553B"), ... }
});
```

---

## 散点图 (ScatterTrace)

### 基础散点图

```csharp
plot.Data.Add(new ScatterTrace
{
    X = x, Y = y,
    Mode = ScatterMode.Markers,
    Name = "数据点",
    Marker = { Size = 8, Opacity = 0.8 }
});
```

### 气泡图（大小 + 颜色映射）

```csharp
plot.Data.Add(new ScatterTrace
{
    X = power, Y = mpg,
    Mode = ScatterMode.Markers,
    Marker = {
        Size = 12,
        ColorValues = weight,              // 气泡大小由外部控制（这里用颜色映射）
        Colorscale = Colorscale.Plasma,
        OutlineColor = Colors.White,
        OutlineWidth = 1
    }
});
```

---

## 热力图 (HeatmapTrace)

### 笛卡尔热力图

```csharp
double[,] z = new double[6, 6];  // [行, 列]
string[] labels = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta" };

plot.Data.Add(new HeatmapTrace
{
    Z = z,
    X = labels, Y = labels,     // 类目轴
    Colorscale = Colorscale.RdBu,
    ZMin = -1, ZMax = 1,
    ShowScale = true             // 显示色阶条
});
```

### 数值坐标热力图

```csharp
double[] xCoords = { 0, 1, 2, 3, 4 };
double[] yCoords = { 0, 1, 2, 3 };
double[,] z = new double[4, 5];

plot.Data.Add(new HeatmapTrace { Z = z, X = xCoords, Y = yCoords, Colorscale = Colorscale.Viridis });
```

---

## 直方图 (HistogramTrace)

```csharp
double[] values = ...;  // 原始数据

plot.Data.Add(new HistogramTrace
{
    X = values,
    NBins = 32,  // 0 = 自动（平方根法则）
    Name = "分布"
});
plot.Layout.XAxis.Title = "值";
plot.Layout.YAxis.Title = "频次";
```

---

## 箱线图 (BoxTrace)

```csharp
plot.Data.Add(new BoxTrace { Y = engineering, Category = "工程" });
plot.Data.Add(new BoxTrace { Y = marketing,   Category = "市场" });
plot.Data.Add(new BoxTrace { Y = operations,  Category = "运营" });
plot.Layout.YAxis.Title = "月薪 [k]";
```

---

## K线图 (CandlestickTrace)

```csharp
DateTime[] dates = ...;
double[] open = ..., high = ..., low = ..., close = ...;

plot.Data.Add(new CandlestickTrace
{
    X = dates, Open = open, High = high, Low = low, Close = close,
    Name = "ACME",
    IncreasingColor = Color.Parse("#3D9970"),  // 涨色
    DecreasingColor = Color.Parse("#FF4136")   // 跌色
});
```

---

## 函数绘图 (FunctionTrace)

### sin(x) 和 cos(x)

```csharp
plot.Data.Add(new FunctionTrace
{
    Function = Math.Sin,           // y = sin(x)
    XMin = -2 * Math.PI,
    XMax = 2 * Math.PI,
    SampleCount = 2000,
    FormulaLabel = "sin(x)",
    Line = { Shape = LineShape.Linear, Width = 2.5 }
});

plot.Data.Add(new FunctionTrace
{
    Function = x => Math.Cos(x),
    XMin = -2 * Math.PI, XMax = 2 * Math.PI,
    FormulaLabel = "cos(x)",
    Line = { Dash = new double[] { 6, 3 } }
});
```

### 带填充的函数

```csharp
plot.Data.Add(new FunctionTrace
{
    Function = x => 0.5 * Math.Sin(2 * x + 1),
    XMin = -6, XMax = 6,
    Fill = ScatterFill.ToZeroY,
    FormulaLabel = "0.5·sin(2x+1)"
});
```

---

## 坐标轴配置

### 类目轴

```csharp
// 当 X 或 Y 为 string[] 时自动识别为类目轴
plot.Data.Add(new ScatterTrace { X = new string[] { "周一", "周二", "周三" }, Y = values });
```

### 对数轴

```csharp
plot.Layout.YAxis.Type = AxisType.Log;
```

### 日期轴

```csharp
// 当 X 为 DateTime[] 时自动识别为日期轴
plot.Data.Add(new ScatterTrace { X = dates, Y = values });
```

### 显式范围

```csharp
plot.Layout.XAxis.Range = new double[] { 0, 100 };
plot.Layout.YAxis.Range = new double[] { -10, 10 };
```

### 格式化刻度

```csharp
plot.Layout.YAxis.TickPrefix = "$";
plot.Layout.YAxis.TickSuffix = "%";
plot.Layout.YAxis.TickFormat = "0.0";  // .NET 数字格式
```

### 强制包含零

```csharp
plot.Layout.YAxis.RangeToZero = true;
```

---

## 雷达图 (RadarTrace)

```csharp
plot.RadarAxis = new RadarAxis
{
    Indicators = new string[] { "销售", "管理", "技术", "客服", "研发", "市场" },
    MaxValues = new double[] { 100, 100, 100, 100, 100, 100 }  // 可选
};

plot.Data.Add(new RadarTrace
{
    Values = new double[] { 90, 80, 85, 70, 95, 75 },
    Name = "预算分配",
    FillColor = Color.Parse("#636efa")
});

plot.Data.Add(new RadarTrace
{
    Values = new double[] { 70, 90, 60, 85, 80, 90 },
    Name = "实际开销",
    FillColor = Color.Parse("#EF553B")
});
```

---

## 仪表盘 (GaugeTrace)

### 基础仪表盘

```csharp
plot.Data.Add(new GaugeTrace
{
    Value = 67, Min = 0, Max = 100,
    Title = "完成率", Unit = "%"
});
```

### 速度仪表盘

```csharp
plot.Data.Add(new GaugeTrace
{
    Value = 72, Min = 0, Max = 200,
    Title = "km/h",
    StartAngle = 225, EndAngle = -45,
    Segments = new (double, Color)[] {
        (0.3, Color.Parse("#91cc75")),   // 绿色安全区
        (0.7, Color.Parse("#fac858")),   // 黄色警告区
        (1.0, Color.Parse("#ee6666"))    // 红色危险区
    }
});
```

### 得分环

```csharp
plot.Data.Add(new GaugeTrace
{
    Value = 85, Min = 0, Max = 100,
    Title = "得分", Unit = "分",
    Style = GaugeStyle.Ring,
    RadiusRatio = 0.6
});
```

### 进度仪表盘

```csharp
plot.Data.Add(new GaugeTrace
{
    Value = 45, Min = 0, Max = 100,
    Title = "任务进度", Unit = "%",
    Style = GaugeStyle.Progress,
    ShowTickLabels = false
});
```

---

## 树图 (TreeTrace)

```csharp
var root = new TreeNode { Name = "公司", Value = 100 };

var eng = new TreeNode { Name = "工程部", Value = 50 };
eng.Children.Add(new TreeNode { Name = "前端组", Value = 20 });
eng.Children.Add(new TreeNode { Name = "后端组", Value = 20 });
eng.Children.Add(new TreeNode { Name = "测试组", Value = 10 });

var mkt = new TreeNode { Name = "市场部", Value = 30 };
mkt.Children.Add(new TreeNode { Name = "品牌组", Value = 15 });
mkt.Children.Add(new TreeNode { Name = "渠道组", Value = 15 });

root.Children.Add(eng);
root.Children.Add(mkt);

plot.Data.Add(new TreeTrace
{
    Root = root,
    Name = "组织架构",
    Layout = TreeLayout.LeftToRight  // LeftToRight / TopToBottom
});
```

---

## 关系图 (GraphTrace)

```csharp
var nodes = new GraphNode[] {
    new() { Id = "A", Name = "中心", Size = 30 },
    new() { Id = "B", Name = "节点B", Size = 20 },
    new() { Id = "C", Name = "节点C", Size = 20 },
    new() { Id = "D", Name = "节点D", Size = 15 },
};

var edges = new GraphEdge[] {
    new() { Source = "A", Target = "B" },
    new() { Source = "A", Target = "C" },
    new() { Source = "B", Target = "D" },
    new() { Source = "B", Target = "C" },
};

plot.Data.Add(new GraphTrace
{
    Nodes = nodes,
    Edges = edges,
    ForceLayout = true,      // 力导向自动布局
    ForceIterations = 200,   // 迭代次数
    ShowLabels = true,
    Directed = false         // true = 有向图（显示箭头）
});
```

---

## 地图 (MapTrace)

### 等值区划图

```csharp
string[] codes = { "USA", "CHN", "JPN", "DEU", "GBR", "IND", "FRA", "BRA" };
double[] gdp  = { 25000, 18000, 4200, 4100, 3100, 3500, 2800, 1900 };

plot.Data.Add(new MapTrace
{
    Regions = codes,
    Values = gdp,
    Labels = codes,
    Colorscale = Colorscale.YlGnBu,
    ShowScale = true
});
```

### 纯底图

```csharp
plot.Data.Add(new MapTrace
{
    Colorscale = Colorscale.Greys,
    ShowScale = false,
    BorderColor = Color.Parse("#cccccc")
});
```

---

## 3D 曲面图 (SurfaceTrace)

```csharp
plot.Data.Add(new SurfaceTrace
{
    Function = (x, y) => Math.Sin(Math.Sqrt(x * x + y * y)) * 2,
    XMin = -6, XMax = 6,
    YMin = -6, YMax = 6,
    Resolution = 50,
    Colorscale = Colorscale.Viridis,
    RotationX = 30,    // 初始 X 旋转角度
    RotationZ = -60,   // 初始 Z 旋转角度
    Scale = 0.7        // 初始缩放
});
```

**交互**：左键拖拽旋转，滚轮缩放，双击重置。

---

## 3D 散点图 (Scatter3DTrace)

```csharp
plot.Data.Add(new Scatter3DTrace
{
    X = xCoords, Y = yCoords, Z = zCoords,
    MarkerSize = 5,
    Colorscale = Colorscale.Plasma,  // 按 Z 值着色
    RotationX = 25,
    RotationZ = -50,
    Name = "3D 散点"
});
```

---

## 悬停模式

```csharp
// 最近点悬停（默认）
plot.Layout.HoverMode = HoverMode.Closest;

// 统一悬停标签（所有序列按 x 对齐）
plot.Layout.HoverMode = HoverMode.XUnified;

// 按 x 分别显示
plot.Layout.HoverMode = HoverMode.X;

// 禁用
plot.Layout.HoverMode = HoverMode.None;
```

---

## 主题

```csharp
// 亮色主题（默认）
plot.Theme = PlotTheme.Plotly;

// 暗色主题
plot.Theme = PlotTheme.PlotlyDark;
```

---

## 动态数据 + 时间轴 (TimelinePlot)

```csharp
var tl = new TimelinePlot();
tl.Layout.Title.Text = "实时监控";
tl.Window = TimeSpan.FromSeconds(30);  // 显示 30 秒窗口
tl.Interval = TimeSpan.FromMilliseconds(100);  // 10Hz 更新

tl.AddSeries("温度", t => 20 + 5 * Math.Sin(t * 0.05) + rng.NextDouble() * 2);
tl.AddSeries("湿度", t => 60 + 10 * Math.Cos(t * 0.03) + rng.NextDouble() * 3);

tl.Play();   // 开始播放
tl.Pause();  // 暂停
tl.Reset();  // 重置
```

---

## 动态更新 + 平滑过渡 (AnimatedPlot)

所有图表类型均支持数据动态更新，新旧数据之间自动平滑过渡。

### 基本用法

```csharp
using TinyPlot.Animation;

var plot = new AnimatedPlot();
plot.Animation.Duration = TimeSpan.FromMilliseconds(750);  // 过渡时长
plot.Animation.Easing = Easing.EaseInOut;                  // 缓动函数

plot.Data.Add(new ScatterTrace { X = x, Y = y, Mode = ScatterMode.Lines, Name = "系列" });

// 平滑过渡到新数据
plot.AnimateTo(() => {
    ((ScatterTrace)plot.Data[0]).Y = newY;
});
```

### 支持动画的图表类型

| 类型 | 可动画属性 |
|---|---|
| **ScatterTrace** | X, Y |
| **BarTrace** | Y |
| **PieTrace** | Values |
| **GaugeTrace** | Value |
| **RadarTrace** | Values |
| **CandlestickTrace** | Open, High, Low, Close |

### 缓动函数

```csharp
plot.Animation.Easing = Easing.Linear;      // 线性
plot.Animation.Easing = Easing.EaseIn;      // 缓入（加速）
plot.Animation.Easing = Easing.EaseOut;     // 缓出（减速）
plot.Animation.Easing = Easing.EaseInOut;   // 缓入缓出（默认，平滑）
plot.Animation.Easing = Easing.ElasticOut;  // 弹性
plot.Animation.Easing = Easing.BackOut;     // 回弹
plot.Animation.Easing = Easing.BounceOut;   // 弹跳
```

### 链式 API

```csharp
plot.BeginUpdate()
    .SetY(0, newY1)
    .SetY(1, newY2)
    .SetValue(2, 85)
    .Animate();
```

### 动态仪表盘示例

```csharp
var plot = new AnimatedPlot();
plot.Animation.Duration = TimeSpan.FromMilliseconds(900);
plot.Animation.Easing = Easing.ElasticOut;

plot.Data.Add(new GaugeTrace { Value = 50, Min = 0, Max = 100, Title = "实时指标", Unit = "%" });

// 每秒更新
var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
timer.Tick += (_, _) => {
    var newValue = 20 + new Random().NextDouble() * 80;
    plot.AnimateTo(() => ((GaugeTrace)plot.Data[0]).Value = newValue);
};
timer.Start();
```

### 动态雷达图示例

```csharp
var plot = new AnimatedPlot();
plot.Animation.Duration = TimeSpan.FromMilliseconds(600);

plot.RadarAxis = new RadarAxis { Indicators = new[] { "销售", "管理", "技术", "客服", "研发", "市场" } };
plot.Data.Add(new RadarTrace { Values = new[] { 90, 80, 85, 70, 95, 75 }, Name = "上月" });
plot.Data.Add(new RadarTrace { Values = new[] { 70, 90, 60, 85, 80, 90 }, Name = "本月" });

// 平滑过渡到新数据
plot.AnimateTo(() => {
    ((RadarTrace)plot.Data[0]).Values = new[] { 85, 75, 90, 80, 70, 95 };
    ((RadarTrace)plot.Data[1]).Values = new[] { 65, 95, 55, 90, 85, 80 };
});
```

### 动态饼图示例

```csharp
var plot = new AnimatedPlot();
plot.Data.Add(new PieTrace {
    Labels = new[] { "直达", "邮件", "联盟", "视频", "搜索" },
    Values = new[] { 335.0, 310, 234, 135, 948 },
    Hole = 0.4
});

plot.AnimateTo(() => {
    ((PieTrace)plot.Data[0]).Values = new[] { 400.0, 250, 300, 200, 800 };
});
```

---

## 性能基准测试

```bash
dotnet run --project tests/TinyPlot.RenderTests -- --benchmark
```

| 场景 | 平均耗时 |
|---|---|
| 100 万散点 | ~11s |
| 10 万线段 | ~144ms |
| 1 万柱状图 | ~66ms |
| 200×200 热力图 | ~34ms |
| 1000 条 × 100 点 | ~391ms |

---

## 内置色阶

Viridis · Plasma · Inferno · Magma · Cividis · Jet · Hot · Portland · RdBu · Bluered · Electric · Blackbody · Earth · Greys · YlGnBu · YlOrRd · Blues · Rainbow · Picnic

```csharp
plot.Data.Add(new HeatmapTrace { Z = z, Colorscale = Colorscale.Plasma });
```

---

## 图例交互

```csharp
// 点击图例条目切换序列可见性
plot.Config.LegendClickToggle = true;

// 禁用图例
plot.Layout.ShowLegend = false;
```

---

## 事件

```csharp
// 数据点点击
plot.PlotClick += (s, e) => {
    Console.WriteLine($"点击了 {e.Trace.Name} 的第 {e.PointIndex} 个点");
};

// 悬停变化
plot.PlotHover += (s, e) => {
    foreach (var target in e.Targets)
        Console.WriteLine($"悬停: {target.Title} x={target.XText} y={target.YText}");
};
```

---

## 完整示例

```csharp
using TinyPlot;

var plot = new Plot();

// 数据
var rng = new Random(42);
var x = Enumerable.Range(0, 50).Select(i => (double)i).ToArray();
var y1 = x.Select(v => 400 + rng.NextDouble() * 100).ToArray();
var y2 = x.Select(v => 300 + rng.NextDouble() * 80).ToArray();

// 序列
plot.Data.Add(new ScatterTrace { X = x, Y = y1, Mode = ScatterMode.LinesMarkers, Name = "系列 A" });
plot.Data.Add(new ScatterTrace { X = x, Y = y2, Mode = ScatterMode.Lines, Name = "系列 B", Line = { Shape = LineShape.Spline } });

// 布局
plot.Layout.Title.Text = "完整示例";
plot.Layout.XAxis.Title = "X 轴";
plot.Layout.YAxis.Title = "Y 轴";
plot.Layout.HoverMode = HoverMode.XUnified;
plot.Theme = PlotTheme.Plotly;

// 交互
plot.PlotClick += (s, e) => Console.WriteLine($"点击: {e.Text}");

// 渲染到窗口
var window = new Window { Width = 960, Height = 640, Content = plot };
window.Show();
```
