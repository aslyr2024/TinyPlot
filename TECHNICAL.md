# TinyPlot 技术架构文档

> 本文档详细介绍 TinyPlot 的 2D/3D 渲染原理、交互系统实现，以及动画引擎设计。
> 结合源码讲解，帮助理解可视化库的核心技术。

---

## 目录

1. [渲染管线总览](#1-渲染管线总览)
2. [2D 坐标系统与变换](#2-2d-坐标系统与变换)
3. [轴系统：刻度生成与范围计算](#3-轴系统刻度生成与范围计算)
4. [交互系统：平移与缩放](#4-交互系统平移与缩放)
5. [3D 投影与旋转](#5-3d-投影与旋转)
6. [动画引擎设计](#6-动画引擎设计)
7. [关键数据结构](#7-关键数据结构)

---

## 1. 渲染管线总览

TinyPlot 的渲染遵循 Avalonia 的即时模式渲染（Immediate Mode Rendering），
每次界面刷新时调用 `Plot.Render(DrawingContext)` 重新绘制所有内容。

```
┌─────────────────────────────────────────────────────────┐
│                    Plot.Render(dc)                       │
├─────────────────────────────────────────────────────────┤
│  1. PlotCalculator.Build()  ← 计算轴/范围/刻度/布局      │
│     ├─ 检测轴类型（Linear/Category/Log/Date）            │
│     ├─ 注册类目                                          │
│     ├─ 计算柱状图布局（Group/Stack）                      │
│     ├─ 调用每个 Trace.Prepare()  ← 计算数据范围          │
│     ├─ 确定轴范围（自动/用户指定）                        │
│     ├─ 生成刻度（Nice Numbers 算法）                     │
│     ├─ 测量标签 → 计算自动边距                           │
│     └─ 返回 PlotBuild（包含 PlotRenderContext）           │
│                                                         │
│  2. 绘制背景                                             │
│     ├─ 纸张背景（paper background）                      │
│     └─ 绘图区背景（plot background）                     │
│                                                         │
│  3. 绘制网格和坐标轴                                     │
│     ├─ 网格线（vertical/horizontal）                     │
│     ├─ 零线（zero line）                                 │
│     ├─ 刻度标签（自动跳过重叠）                          │
│     └─ 轴标题                                           │
│                                                         │
│  4. 绘制数据序列（Clip to plot rect）                    │
│     ├─ 笛卡尔图表（Scatter/Bar/Heatmap/Box/Candle...）  │
│     ├─ 饼图                                             │
│     ├─ 雷达图                                           │
│     ├─ 地图                                             │
│     └─ 仪表盘/树图/关系图/3D...                          │
│                                                         │
│  5. 绘制叠加层                                           │
│     ├─ 色阶条（colorbar）                                │
│     ├─ 标题                                             │
│     ├─ 图例                                             │
│     ├─ 悬停标签（hover tooltip）                         │
│     └─ 框选矩形（zoom rect）                            │
└─────────────────────────────────────────────────────────┘
```

**核心设计思想**：每次渲染都是完整重绘（无状态），数据变更后调用
`InvalidateVisual()` 触发下一帧渲染。这与游戏引擎的渲染循环一致。

---

## 2. 2D 坐标系统与变换

### 2.1 三层坐标系

TinyPlot 使用三层坐标系：

```
数据空间 (Data Space)     例: x=100, y=200 (业务数据)
      ↓ AxisState.Fraction()
归一化空间 (0..1)         例: x=0.3, y=0.6 (轴范围内的比例)
      ↓ PlotRect 映射
像素空间 (Pixel Space)     例: x=340px, y=180px (屏幕坐标)
```

**为什么需要三层？**
- 数据空间：用户关心的业务值（销售额、温度、日期...）
- 归一化空间：与具体像素无关，方便计算比例
- 像素空间：最终绘制坐标

### 2.2 数据 → 像素的转换

```csharp
// AxisState.cs - 数据到归一化的转换
public double Fraction(double raw)
{
    var t = Transform(raw);  // 对数轴: t = log10(raw); 其他: t = raw
    var span = Max - Min;
    return span == 0 ? double.NaN : (t - Min) / span;
}

// PlotRenderContext.cs - 数据到像素的转换
public double XToPixels(double xRaw)
    => PlotRect.X + XAxis.Fraction(xRaw) * PlotRect.Width;

public double YToPixels(double yRaw)
    => PlotRect.Bottom - YAxis.Fraction(yRaw) * PlotRect.Height;
    // 注意: Y 轴反转! 数据空间 Y 向上, 像素空间 Y 向下
```

**关键点**：Y 轴方向反转是 2D 图表的标准做法——数据的 "上" 对应像素的 "小 Y"。

### 2.3 对数轴变换

对数轴不是简单地取 log10，而是在 `Transform` 层统一处理：

```csharp
// 对数轴: 数据值 1000 → 归一化空间中 log10(1000) = 3
public double Transform(double raw)
    => EffectiveType == AxisType.Log ? Math.Log10(raw) : raw;

// 反变换: 归一化空间 3 → 数据值 10^3 = 1000
public double Untransform(double t)
    => EffectiveType == AxisType.Log ? Math.Pow(10, t) : t;
```

这样所有下游代码（Fraction、ToPixels）都不需要关心对数轴的特殊性。

### 2.4 类目轴映射

类目轴将字符串映射为整数索引：

```
["Q1", "Q2", "Q3", "Q4"]  →  [0, 1, 2, 3]
```

```csharp
// 类目轴的 Fraction 就是索引/总范围
// "Q2" → Fraction = (1 - (-0.5)) / (3.5 - (-0.5)) = 0.375
```

---

## 3. 轴系统：刻度生成与范围计算

### 3.1 Nice Numbers 算法

刻度生成的核心是 **Nice Numbers** 算法（与 d3-array / plotly.js 相同），
确保刻度值是"好看的"数字（1, 2, 5 的倍数）。

```csharp
// TickGenerator.cs
public static double NiceStep(double rawStep)
{
    var power = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
    var error = rawStep / power;
    double factor;
    if (error >= 7.5) factor = 10;      // 跳到下一个 10 的幂
    else if (error >= 3.5) factor = 5;   // 5 的倍数
    else if (error >= 1.5) factor = 2;   // 2 的倍数
    else factor = 1;                     // 1 的倍数
    return factor * power;
}
```

**例子**：
- 原始步长 3.7 → power=1, error=3.7 → factor=5 → nice step=5
- 原始步长 0.08 → power=0.01, error=8 → factor=10 → nice step=0.1
- 原始步长 150 → power=100, error=1.5 → factor=2 → nice step=200

### 3.2 自动范围计算

```csharp
// PlotCalculator.cs - FinalizeRange
// 1. 收集所有 Trace 的数据范围 (ExtendX/ExtendY)
// 2. 应用 padding (默认 5%)
// 3. 处理特殊情况:
//    - 柱状图: Y 轴强制包含 0
//    - 对数轴: 在 log 空间中 padding
//    - 类目轴: [-0.5, n-0.5]
//    - 数据全为正且跨 1000x: 自动切换对数轴
```

### 3.3 智能对数轴切换

当数据满足以下条件时自动切换到对数轴：
1. 轴类型为 Auto（用户未显式指定）
2. 所有值为正数
3. 最大值/最小值 > 1000

```csharp
if (axis.EffectiveType == AxisType.Linear && src.Type == AxisType.Auto
    && dataMin > 0 && dataMax > 0 && dataMax / dataMin > 1000)
{
    axis.EffectiveType = AxisType.Log;
    // ... 在 log 空间中计算范围
}
```

---

## 4. 交互系统：平移与缩放

### 4.1 平移原理

平移的本质是**等比例移动轴范围**：

```
用户拖拽 dx 像素 → 对应的数据偏移 = dx / plotWidth * (max - min)
```

```csharp
// Plot.cs - PanByDirect
private void PanByDirect(double dxPx, double dyPx)
{
    var rc = _build.Context;
    var xSpan = rc.XAxis.Max - rc.XAxis.Min;
    var ySpan = rc.YAxis.Max - rc.YAxis.Min;

    // 像素偏移 → 数据空间偏移
    var xDelta = -dxPx / rc.PlotRect.Width * xSpan;
    var yDelta =  dyPx / rc.PlotRect.Height * ySpan;

    // 直接修改轴范围（不触发完整重算，丝滑响应）
    Layout.XAxis.Range = [
        rc.XAxis.Untransform(rc.XAxis.Min + xDelta),
        rc.XAxis.Untransform(rc.XAxis.Max + xDelta)
    ];
    Layout.YAxis.Range = [
        rc.YAxis.Untransform(rc.YAxis.Min + yDelta),
        rc.YAxis.Untransform(rc.YAxis.Max + yDelta)
    ];
}
```

**为什么是丝滑的？** 因为 `PanByDirect` 只修改 Range 属性，
不调用 `PlotCalculator.Build()`（完整重算在下一帧 Render 中自动发生）。
而 Range 修改是 O(1) 操作，所以拖拽响应是即时的。

### 4.2 缩放原理

缩放以鼠标位置为中心，按比例缩放轴范围：

```csharp
// Plot.cs - ZoomAxis
private static void ZoomAxis(PlotAxis axis, AxisState state,
    double fraction, double factor)
{
    // fraction: 鼠标在轴上的位置比例 (0..1)
    // factor: 缩放因子 (>1 放大, <1 缩小)

    var center = state.ValueAt(fraction);  // 鼠标处的数据值
    var lo = center + (state.Min - center) * factor;
    var hi = center + (state.Max - center) * factor;
    axis.Range = [state.Untransform(lo), state.Untransform(hi)];
}
```

**数学原理**：
```
新范围 = 中心点 + (原范围 - 中心点) × 缩放因子

factor = 1.25^(-滚轮增量)
  滚轮向上 → factor < 1 → 范围缩小 → 放大
  滚轮向下 → factor > 1 → 范围扩大 → 缩小
```

### 4.3 框选缩放

框选缩放将像素矩形转换为数据范围：

```csharp
private void ApplyZoomRect(Rect zr)
{
    var rc = _build.Context;
    // 像素坐标 → 数据坐标
    var xLo = rc.XAxis.RawValueAt((zr.Left - rc.PlotRect.X) / rc.PlotRect.Width);
    var xHi = rc.XAxis.RawValueAt((zr.Right - rc.PlotRect.X) / rc.PlotRect.Width);
    Layout.XAxis.Range = [Math.Min(xLo, xHi), Math.Max(xLo, xHi)];
    // Y 轴类似（注意 Y 方向反转）
}
```

### 4.4 双击重置

双击恢复自动范围：

```csharp
public void ResetAxes()
{
    Layout.XAxis.Range = null;  // null = 自动范围
    Layout.YAxis.Range = null;
    InvalidateVisual();
}
```

---

## 5. 3D 投影与旋转

### 5.1 3D → 2D 投影

3D 图表使用**正交投影 + 旋转矩阵**将 3D 坐标映射到 2D 屏幕：

```
3D 坐标 (x, y, z)
    ↓ 归一化到 [-0.5, 0.5]
    ↓ 绕 Z 轴旋转 (RotationZ)
    ↓ 绕 X 轴旋转 (RotationX)
    ↓ 忽略深度 (正交投影)
2D 坐标 (screenX, screenY)
    ↓ 缩放 + 偏移
像素坐标
```

### 5.2 旋转矩阵

**绕 Z 轴旋转**（水平旋转，改变观察方位角）：

```
x' = x·cos(θ) - y·sin(θ)
y' = x·sin(θ) + y·cos(θ)
z' = z
```

**绕 X 轴旋转**（垂直旋转，改变观察俯仰角）：

```
x' = x
y' = y·cos(φ) - z·sin(φ)
z' = y·sin(φ) + z·cos(φ)
```

### 5.3 源码实现

```csharp
// SurfaceTrace.cs / Scatter3DTrace.cs - Render
var rotX = RotationX * Math.PI / 180;  // 度 → 弧度
var rotZ = RotationZ * Math.PI / 180;

for (var i = 0; i < n; i++)
{
    // 1. 归一化到 [-0.5, 0.5]
    var nx = (double)i / (n - 1) - 0.5;
    var ny = (double)j / (n - 1) - 0.5;
    var nz = (data[i,j] - zmin) / (zmax - zmin) - 0.5;

    // 2. 绕 Z 轴旋转（水平）
    var x1 = nx * Math.Cos(rotZ) - ny * Math.Sin(rotZ);
    var y1 = nx * Math.Sin(rotZ) + ny * Math.Cos(rotZ);
    var z1 = nz;

    // 3. 绕 X 轴旋转（垂直）
    var y2 = y1 * Math.Cos(rotX) - z1 * Math.Sin(rotX);
    // z2 = y1 * Math.Sin(rotX) + z1 * Math.Cos(rotX);  // 深度，正交投影时忽略

    // 4. 映射到像素
    var px = cx + x1 * size;
    var py = cy - y2 * size;  // Y 反转
}
```

### 5.4 交互旋转

用户拖拽直接修改旋转角度：

```csharp
// Plot.cs - Rotate3D
private void Rotate3D(double dxPx, double dyPx)
{
    foreach (var trace in Data)
    {
        if (trace is SurfaceTrace s)
        {
            s.RotationZ += dxPx * 0.5;  // 水平拖拽 → 绕 Z 轴旋转
            s.RotationX = Math.Clamp(
                s.RotationX - dyPx * 0.5,  // 垂直拖拽 → 绕 X 轴旋转
                -89, 89);  // 限制俯仰角避免万向锁
        }
    }
}
```

### 5.5 深度排序（3D 曲面）

3D 曲面需要按深度排序面片（从后到前绘制），否则前面的面会被后面的面覆盖。
当前实现使用简单的从后到前遍历（i, j 递增顺序），对于大多数视角足够。
更精确的实现需要计算每个面片的 Z 深度并排序。

---

## 6. 动画引擎设计

### 6.1 核心思路

动画 = 在两组数据之间按时间插值。

```
旧数据 ──────────────────────────→ 新数据
         ↑ 缓动函数控制进度
         t=0 ───────→ t=1
         (开始)       (结束)
```

### 6.2 缓动函数

缓动函数将线性时间 t ∈ [0,1] 映射为非线性进度：

```csharp
// 缓入缓出（最常用，平滑自然）
public static double EaseInOut(double t)
    => t < 0.5 ? 4*t*t*t : 1 - Math.Pow(-2*t + 2, 3) / 2;

// 弹性缓出（有弹性回弹效果，适合仪表盘指针）
public static double ElasticOut(double t)
{
    if (t == 0 || t == 1) return t;
    return Math.Pow(2, -10*t) * Math.Sin((t*10 - 0.75) * (2*PI/3)) + 1;
}

// 回弹缓出（超过目标后回弹，适合柱状图）
public static double BackOut(double t)
{
    const double c1 = 1.70158;
    return 1 + (c1+1) * Math.Pow(t-1, 3) + c1 * Math.Pow(t-1, 2);
}
```

### 6.3 数据插值

```csharp
// Interpolator.cs
public static double[] Lerp(double[] from, double[] to, double t)
{
    var len = Math.Max(from.Length, to.Length);
    var result = new double[len];
    for (var i = 0; i < len; i++)
    {
        var a = i < from.Length ? from[i] : 0;
        var b = i < to.Length ? to[i] : 0;
        result[i] = a + (b - a) * t;  // 线性插值
    }
    return result;
}
```

### 6.4 动画流程

```csharp
// AnimatedPlot.cs - AnimateTo
public void AnimateTo(Action updateAction)
{
    // 1. 快照当前数据
    var snapshots = CaptureSnapshots();

    // 2. 执行用户的数据修改（瞬间完成）
    updateAction();

    // 3. 快照目标数据
    var targets = CaptureSnapshots();

    // 4. 创建动画任务（比较快照，只为变化的数据创建动画）
    foreach (var (trace, snapshot) in snapshots)
    {
        var target = targets[trace];
        var anim = TraceAnimation.Create(trace, snapshot, target, easing);
        if (anim != null) animations.Add(anim);
    }

    // 5. 启动 ~60fps 定时器
    timer.Tick += (_, _) =>
    {
        var progress = elapsed / duration;  // 0 → 1
        var easedProgress = easing(progress);

        foreach (var anim in animations)
            anim.Apply(easedProgress);  // 将插值后的数据写回 Trace

        Refresh();  // 触发重绘

        if (progress >= 1) timer.Stop();  // 动画结束
    };
    timer.Start();
}
```

### 6.5 为什么动画是平滑的？

1. **60fps 定时器**：每 16ms 更新一帧，人眼感知为连续运动
2. **缓动函数**：EaseInOut 使开始和结束都减速，避免突兀
3. **只修改数据，不重算布局**：动画帧中只做插值赋值，布局重算在 Render 中自动发生
4. **增量更新**：Range 修改是 O(1)，完整重算是 O(n)，帧率足够高

---

## 7. 关键数据结构

### 7.1 Trace 继承体系

```
Trace (抽象基类)
├── ScatterTrace    折线/散点/面积
├── BarTrace        柱状图
├── PieTrace        饼图/环形图
├── HeatmapTrace    热力图
├── HistogramTrace  直方图
├── BoxTrace        箱线图
├── CandlestickTrace K线图
├── FunctionTrace   函数绘图
├── RadarTrace      雷达图
├── GaugeTrace      仪表盘
├── TreeTrace       树图
├── GraphTrace      关系图
├── MapTrace        地图
├── SurfaceTrace    3D曲面
└── Scatter3DTrace  3D散点
```

每个 Trace 实现：
- `Prepare(ctx)` — 计算数据范围，注册到轴系统
- `Render(dc, rc)` — 使用 DrawingContext 绘制
- `HitTest(rc, pt, mode)` — 悬停检测
- `GetLegendItems()` — 图例条目

### 7.2 PlotRenderContext

渲染上下文，包含绘制所需的一切：

```csharp
public sealed class PlotRenderContext
{
    public Rect PlotRect;        // 绘图区矩形
    public AxisState XAxis;      // X 轴状态（范围、刻度、类型）
    public AxisState YAxis;      // Y 轴状态
    public PlotTheme Theme;      // 主题颜色
    public Layout Layout;        // 布局配置
    public Typeface Typeface;    // 字体

    // 便捷方法
    public Point ToPixels(double xRaw, double yRaw);  // 数据→像素
    public SolidColorBrush Brush(Color c, double opacity);
    public Pen Pen(Color c, double width, ...);
    public FormattedText Text(string s, Color color, ...);
}
```

### 7.3 数据流

```
用户代码: plot.Data.Add(trace)
                ↓
plot.Refresh() → InvalidateVisual()
                ↓
Render() → PlotCalculator.Build()
                ↓
           trace.Prepare(ctx)  ← 每个 Trace 计算自己的数据范围
                ↓
           FinalizeRange()     ← 合并所有范围，确定轴范围
                ↓
           BuildTicks()        ← 生成刻度
                ↓
           Measure labels      ← 测量标签，计算边距
                ↓
           创建 PlotRenderContext
                ↓
           trace.Render(dc, rc) ← 每个 Trace 绘制自己
```

---

## 附录：性能优化技巧

1. **只在数据变更时调用 Refresh()**：避免不必要的重绘
2. **平移不触发完整重算**：`PanByDirect` 直接修改 Range，O(1) 操作
3. **热力图使用位图缓存**：`WriteableBitmap` 缓存，只在数据变化时重建
4. **悬停只在鼠标移动时计算**：`UpdateHover` 独立于渲染管线
5. **动画帧间跳过未变化的 Trace**：`TraceAnimation.Create` 比较快照
6. **大数据量散点图**：可进一步优化为 binning/culling（当前未实现）

---

## 附录：数学公式速查

| 概念 | 公式 |
|---|---|
| 线性插值 | `lerp(a, b, t) = a + (b - a) * t` |
| 归一化 | `frac = (value - min) / (max - min)` |
| 对数变换 | `logVal = log10(value)` |
| 2D 旋转 | `x' = x·cos(θ) - y·sin(θ)` |
| 3D→2D 投影 | 正交: 忽略 Z 分量 |
| 缓入缓出 | `t<0.5 ? 4t³ : 1-(-2t+2)³/2` |
| Nice Step | `factor × 10^floor(log10(step))` |
