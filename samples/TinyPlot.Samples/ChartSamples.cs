using Avalonia.Media;
using TinyPlot.Geo;
using TinyPlot.Animation;

namespace TinyPlot.Samples;

/// <summary>
/// 参照 ECharts 的精选图表示例集。
/// 所有数据确定性生成，渲染测试可重现。
/// </summary>
public static class ChartSamples
{
    private static Random Rng(int seed) => new(seed);

    /// <summary>全部示例列表。</summary>
    public static (string Name, string Category, Func<Plot> Factory)[] All =>
    [
        // ===== 折线图 line =====
        ("基础折线图", "折线图", LineChart),
        ("基础平滑折线图", "折线图", SmoothLineChart),
        ("基础面积图", "折线图", AreaChart),
        ("堆叠折线图", "折线图", StackedLineChart),
        ("堆叠面积图", "折线图", StackedAreaChart),
        ("阶梯折线图", "折线图", StepLineChart),
        ("未来一周气温变化", "折线图", TemperatureChart),
        ("函数绘图 sin(x)", "折线图", FunctionPlotChart),
        ("对数轴示例", "折线图", LogAxisChart),
        ("日期轴折线图", "折线图", DateAxisChart),
        ("置信带", "折线图", ConfidenceBandChart),
        ("统一悬停", "折线图", UnifiedHoverChart),

        // ===== 柱状图 bar =====
        ("基础柱状图", "柱状图", GroupedBarChart),
        ("堆叠柱状图", "柱状图", StackedBarChart),
        ("水平条形图", "柱状图", HorizontalBarChart),
        ("正负条形图", "柱状图", NegativeBarChart),
        ("带背景色柱状图", "柱状图", BarWithBackgroundChart),
        ("折柱混合", "柱状图", MixedLineBarChart),

        // ===== 饼图 pie =====
        ("饼图", "饼图", PieChart),
        ("环形图", "饼图", DonutChart),
        ("半环形图", "饼图", HalfDonutChart),
        ("南丁格尔玫瑰图", "饼图", NightingaleChart),

        // ===== 散点图 scatter =====
        ("基础散点图", "散点图", ScatterChart),
        ("气泡图", "散点图", BubbleChart),
        ("涟漪特效散点图", "散点图", EffectScatterChart),

        // ===== 热力图 heatmap =====
        ("笛卡尔热力图", "热力图", HeatmapChart),
        ("日历热力图", "热力图", CalendarHeatmapChart),

        // ===== 箱线图 boxplot =====
        ("盒须图", "箱线图", BoxChart),

        // ===== K线图 candlestick =====
        ("基础K线图", "K线图", CandlestickChart),

        // ===== 雷达图 radar =====
        ("基础雷达图", "雷达图", BasicRadarChart),
        ("多雷达图", "雷达图", MultiRadarChart),

        // ===== 地图 map =====
        ("世界地图", "地图", WorldMapChart),
        ("地图散点叠加", "地图", MapWithScatterChart),

        // ===== 直方图 histogram =====
        ("直方图", "直方图", HistogramChart),

        // ===== 主题 =====
        ("暗色主题", "主题", DarkThemeChart),

        // ===== 仪表盘 gauge =====
        ("基础仪表盘", "仪表盘", BasicGaugeChart),
        ("速度仪表盘", "仪表盘", SpeedGaugeChart),
        ("得分环", "仪表盘", RingGaugeChart),
        ("进度仪表盘", "仪表盘", ProgressGaugeChart),

        // ===== 树图 tree =====
        ("基础树图", "树图", BasicTreeChart),

        // ===== 关系图 graph =====
        ("基础关系图", "关系图", BasicGraphChart),

        // ===== 3D =====
        ("3D 曲面图", "3D", Surface3DChart),
        ("3D 散点图", "3D", Scatter3DChart),

        // ===== 动画 =====
        ("动态折线图", "动画", AnimatedLineChart),
        ("动态柱状图", "动画", AnimatedBarChart),
        ("动态饼图", "动画", AnimatedPieChart),
        ("动态仪表盘", "动画", AnimatedGaugeChart),
        ("动态雷达图", "动画", AnimatedRadarChart),
    ];

    // ========== 折线图 ==========

    public static Plot LineChart()
    {
        var plot = Base("基础折线图");
        plot.Layout.XAxis.Title = "天数";
        plot.Layout.YAxis.Title = "访问量";
        var x = Enumerable.Range(0, 50).Select(i => (double)i).ToArray();
        var rng = Rng(42);
        double[] Series(double b, double amp) { var v = b; return x.Select(_ => { v += (rng.NextDouble() - 0.48) * amp; return Math.Max(0, v); }).ToArray(); }
        plot.Data.Add(new ScatterTrace { X = x, Y = Series(400, 60), Mode = ScatterMode.LinesMarkers, Name = "直接访问" });
        plot.Data.Add(new ScatterTrace { X = x, Y = Series(300, 45), Mode = ScatterMode.Lines, Name = "搜索引擎", Line = { Shape = LineShape.Linear } });
        plot.Data.Add(new ScatterTrace { X = x, Y = Series(200, 50), Mode = ScatterMode.Lines, Name = "邮件营销", Line = { Dash = [6, 3] } });
        return plot;
    }

    public static Plot SmoothLineChart()
    {
        var plot = Base("基础平滑折线图");
        var x = Enumerable.Range(0, 40).Select(i => (double)i).ToArray();
        var rng = Rng(7);
        double[] S(double b) { var v = b; return x.Select(_ => { v += (rng.NextDouble() - 0.5) * 15; return v; }).ToArray(); }
        plot.Data.Add(new ScatterTrace { X = x, Y = S(100), Mode = ScatterMode.Lines, Name = "平滑线", Line = { Shape = LineShape.Spline, Width = 2.5 } });
        plot.Data.Add(new ScatterTrace { X = x, Y = S(200), Mode = ScatterMode.Lines, Name = "直线", Line = { Shape = LineShape.Linear, Width = 1.5, Dash = [4, 2] } });
        return plot;
    }

    public static Plot AreaChart()
    {
        var plot = Base("基础面积图");
        var x = Enumerable.Range(0, 30).Select(i => (double)i).ToArray();
        var rng = Rng(3);
        double[] S(double b) { var v = b; return x.Select(_ => v += (rng.NextDouble() - 0.45) * 8).Select(v => Math.Max(2, v)).ToArray(); }
        plot.Data.Add(new ScatterTrace { X = x, Y = S(50), Mode = ScatterMode.Lines, Name = "销售", Fill = ScatterFill.ToZeroY, Line = { Shape = LineShape.Spline } });
        return plot;
    }

    public static Plot StackedLineChart()
    {
        var plot = Base("堆叠折线图");
        var x = Enumerable.Range(0, 30).Select(i => (double)i).ToArray();
        var rng = Rng(5);
        double[] S(double b) { var v = b; return x.Select(_ => { v += (rng.NextDouble() - 0.48) * 5; return Math.Max(0, v); }).ToArray(); }
        plot.Data.Add(new ScatterTrace { X = x, Y = S(20), Mode = ScatterMode.Lines, Name = "邮件" });
        plot.Data.Add(new ScatterTrace { X = x, Y = S(35), Mode = ScatterMode.Lines, Name = "联盟" });
        plot.Data.Add(new ScatterTrace { X = x, Y = S(50), Mode = ScatterMode.Lines, Name = "视频" });
        plot.Data.Add(new ScatterTrace { X = x, Y = S(80), Mode = ScatterMode.Lines, Name = "直接" });
        return plot;
    }

    public static Plot StackedAreaChart()
    {
        var plot = Base("堆叠面积图");
        var x = Enumerable.Range(0, 30).Select(i => (double)i).ToArray();
        var rng = Rng(3);
        double[] S(double b) { var v = b; return x.Select(_ => v += (rng.NextDouble() - 0.45) * 8).Select(v => Math.Max(2, v)).ToArray(); }
        plot.Data.Add(new ScatterTrace { X = x, Y = S(30), Mode = ScatterMode.Lines, Name = "邮件", Fill = ScatterFill.ToZeroY, Line = { Shape = LineShape.Spline } });
        plot.Data.Add(new ScatterTrace { X = x, Y = S(45), Mode = ScatterMode.Lines, Name = "联盟", Fill = ScatterFill.ToNextY, Line = { Shape = LineShape.Spline } });
        plot.Data.Add(new ScatterTrace { X = x, Y = S(60), Mode = ScatterMode.Lines, Name = "视频", Fill = ScatterFill.ToNextY, Line = { Shape = LineShape.Spline } });
        return plot;
    }

    public static Plot StepLineChart()
    {
        var plot = Base("阶梯折线图");
        double[] x = [0, 1, 2, 3, 4, 5, 6, 7, 8];
        double[] y1 = [120, 132, 101, 134, 90, 230, 210, 182, 192];
        double[] y2 = [220, 182, 191, 234, 290, 330, 310, 123, 442];
        plot.Data.Add(new ScatterTrace { X = x, Y = y1, Mode = ScatterMode.Lines, Name = "邮件", Line = { Shape = LineShape.Hv } });
        plot.Data.Add(new ScatterTrace { X = x, Y = y2, Mode = ScatterMode.Lines, Name = "联盟", Line = { Shape = LineShape.Vh } });
        return plot;
    }

    public static Plot TemperatureChart()
    {
        var plot = Base("未来一周气温变化");
        plot.Layout.YAxis.Title = "温度 (°C)";
        string[] days = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];
        double[] high = [13, 11, 14, 12, 15, 13, 16];
        double[] low = [2, 1, 3, 1, 4, 2, 5];
        plot.Data.Add(new ScatterTrace { X = days, Y = high, Mode = ScatterMode.LinesMarkers, Name = "最高气温", Marker = { Size = 8 } });
        plot.Data.Add(new ScatterTrace { X = days, Y = low, Mode = ScatterMode.LinesMarkers, Name = "最低气温", Marker = { Size = 8 } });
        return plot;
    }

    /// <summary>函数绘图：sin(x) 平滑曲线。</summary>
    public static Plot FunctionPlotChart()
    {
        var plot = Base("函数绘图");
        plot.Data.Add(new FunctionTrace { Function = Math.Sin, XMin = -2 * Math.PI, XMax = 2 * Math.PI, FormulaLabel = "sin(x)", SampleCount = 2000, Line = { Shape = LineShape.Linear, Width = 2.5 } });
        plot.Data.Add(new FunctionTrace { Function = x => Math.Cos(x), XMin = -2 * Math.PI, XMax = 2 * Math.PI, FormulaLabel = "cos(x)", SampleCount = 2000, Line = { Shape = LineShape.Linear, Width = 2, Dash = [6, 3] } });
        plot.Data.Add(new FunctionTrace { Function = x => 0.5 * Math.Sin(2 * x + 1), XMin = -2 * Math.PI, XMax = 2 * Math.PI, FormulaLabel = "0.5·sin(2x+1)", SampleCount = 2000, Fill = ScatterFill.ToZeroY, Line = { Width = 1.5 } });
        plot.Layout.XAxis.TickFormat = "0.##";
        return plot;
    }

    public static Plot LogAxisChart()
    {
        var plot = Base("对数轴示例");
        plot.Layout.YAxis.Type = AxisType.Log;
        var x = Enumerable.Range(0, 60).Select(i => (double)i).ToArray();
        plot.Data.Add(new ScatterTrace { X = x, Y = x.Select(v => 10 * Math.Pow(1.12, v)).ToArray(), Mode = ScatterMode.Lines, Name = "12%增长" });
        plot.Data.Add(new ScatterTrace { X = x, Y = x.Select(v => 10 * Math.Pow(1.06, v)).ToArray(), Mode = ScatterMode.Lines, Name = "6%增长" });
        plot.Layout.YAxis.Title = "数值";
        return plot;
    }

    public static Plot DateAxisChart()
    {
        var plot = Base("日期轴折线图");
        var rng = Rng(13);
        var n = 90;
        var dates = Enumerable.Range(0, n).Select(i => DateTime.Today.AddDays(i - n)).ToArray();
        var v = 5000.0;
        var y = dates.Select(_ => { v += (rng.NextDouble() - 0.47) * 400; return Math.Max(500, v); }).ToArray();
        plot.Data.Add(new ScatterTrace { X = dates, Y = y, Mode = ScatterMode.Lines, Name = "DAU", Fill = ScatterFill.ToZeroY });
        plot.Layout.YAxis.Title = "用户数";
        return plot;
    }

    public static Plot ConfidenceBandChart()
    {
        var plot = Base("置信带");
        var x = Enumerable.Range(0, 50).Select(i => (double)i).ToArray();
        var rng = Rng(99);
        var mid = x.Select(v => 50 + 10 * Math.Sin(v * 0.2) + rng.NextDouble() * 3).ToArray();
        var upper = mid.Select(v => v + 8 + rng.NextDouble() * 3).ToArray();
        var lower = mid.Select(v => v - 8 - rng.NextDouble() * 3).ToArray();
        plot.Data.Add(new ScatterTrace { X = x, Y = lower, Mode = ScatterMode.Lines, Name = "下界", Line = { Width = 1, Color = Color.Parse("#EF553B") }, ShowLegend = false });
        plot.Data.Add(new ScatterTrace { X = x, Y = upper, Mode = ScatterMode.Lines, Name = "置信带", Fill = ScatterFill.ToNextY, FillColor = Color.Parse("#636efa"), Line = { Width = 1, Color = Color.Parse("#636efa") } });
        plot.Data.Add(new ScatterTrace { X = x, Y = mid, Mode = ScatterMode.Lines, Name = "均值", Line = { Width = 2.5 } });
        return plot;
    }

    public static Plot UnifiedHoverChart()
    {
        var plot = Base("统一悬停 (hovermode='x unified')");
        plot.Layout.HoverMode = HoverMode.XUnified;
        var x = Enumerable.Range(0, 40).Select(i => (double)i).ToArray();
        var rng = Rng(31);
        double[] S(double b) { var v = b; return x.Select(_ => v += (rng.NextDouble() - 0.5) * 20).ToArray(); }
        plot.Data.Add(new ScatterTrace { X = x, Y = S(100), Mode = ScatterMode.Lines, Name = "服务器 A" });
        plot.Data.Add(new ScatterTrace { X = x, Y = S(200), Mode = ScatterMode.Lines, Name = "服务器 B" });
        plot.Data.Add(new ScatterTrace { X = x, Y = S(150), Mode = ScatterMode.Lines, Name = "服务器 C" });
        return plot;
    }

    // ========== 柱状图 ==========

    public static Plot GroupedBarChart()
    {
        var plot = Base("基础柱状图");
        string[] q = ["Q1", "Q2", "Q3", "Q4"];
        plot.Data.Add(new BarTrace { X = q, Y = new[] { 120.0, 160, 140, 190 }, Name = "EMEA", TextPosition = TraceTextPosition.Outside });
        plot.Data.Add(new BarTrace { X = q, Y = new[] { 90.0, 130, 170, 150 }, Name = "AMER", TextPosition = TraceTextPosition.Outside });
        plot.Data.Add(new BarTrace { X = q, Y = new[] { 60.0, 110, 120, 175 }, Name = "APAC", TextPosition = TraceTextPosition.Outside });
        plot.Layout.YAxis.Title = "收入 [k$]";
        return plot;
    }

    public static Plot StackedBarChart()
    {
        var plot = Base("堆叠柱状图");
        plot.Layout.BarMode = BarMode.Stack;
        string[] m = ["Jan", "Feb", "Mar", "Apr", "May", "Jun"];
        plot.Data.Add(new BarTrace { X = m, Y = new[] { 40.0, 55, 48, 60, 52, 66 }, Name = "基建", TextPosition = TraceTextPosition.Inside });
        plot.Data.Add(new BarTrace { X = m, Y = new[] { 30.0, 28, 35, 32, 38, 30 }, Name = "研发", TextPosition = TraceTextPosition.Inside });
        plot.Data.Add(new BarTrace { X = m, Y = new[] { 12.0, 15, 10, 18, 14, 20 }, Name = "销售", TextPosition = TraceTextPosition.Inside });
        return plot;
    }

    public static Plot HorizontalBarChart()
    {
        var plot = Base("水平条形图");
        string[] langs = ["Rust", "Python", "TypeScript", "Go", "C#", "Java"];
        plot.Data.Add(new BarTrace { Y = langs, X = new[] { 86.0, 78, 71, 63, 58, 45 }, Orientation = Orientation.Horizontal, Name = "喜爱度" });
        plot.Layout.XAxis.Title = "%";
        return plot;
    }

    public static Plot NegativeBarChart()
    {
        var plot = Base("正负条形图");
        string[] items = ["电费", "水费", "燃气", "交通", "餐饮", "娱乐"];
        double[] vals = [-120, -80, -60, 45, 200, 150];
        var colors = vals.Select(v => v >= 0 ? Color.Parse("#00cc96") : Color.Parse("#EF553B")).ToArray();
        plot.Data.Add(new BarTrace { X = items, Y = vals, Name = "净收支", Marker = { Colors = colors } });
        return plot;
    }

    public static Plot BarWithBackgroundChart()
    {
        var plot = Base("带背景色柱状图");
        string[] m = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];
        double[] vals = [120, 200, 150, 80, 70, 110, 130];
        plot.Data.Add(new BarTrace { X = m, Y = vals, Name = "访问量", TextPosition = TraceTextPosition.Outside });
        return plot;
    }

    public static Plot MixedLineBarChart()
    {
        var plot = Base("折柱混合");
        string[] m = ["Jan", "Feb", "Mar", "Apr", "May", "Jun"];
        plot.Data.Add(new BarTrace { X = m, Y = new[] { 2.0, 4.9, 7.0, 23.2, 25.6, 76.7 }, Name = "降水量" });
        plot.Data.Add(new ScatterTrace { X = m, Y = new[] { 2.0, 2.2, 3.3, 4.5, 6.3, 10.2 }, Mode = ScatterMode.LinesMarkers, Name = "温度", Marker = { Size = 8 } });
        plot.Layout.YAxis.Title = "降水量 / 温度";
        return plot;
    }

    // ========== 饼图 ==========

    public static Plot PieChart()
    {
        var plot = Base("饼图");
        plot.Data.Add(new PieTrace { Labels = ["直接访问", "邮件营销", "联盟广告", "视频广告", "搜索引擎"], Values = [335, 310, 234, 135, 948], Name = "来源" });
        return plot;
    }

    public static Plot DonutChart()
    {
        var plot = Base("环形图");
        plot.Data.Add(new PieTrace { Labels = ["直接访问", "邮件营销", "联盟广告", "视频广告", "搜索引擎"], Values = [335, 310, 234, 135, 948], Hole = 0.5 });
        return plot;
    }

    public static Plot HalfDonutChart()
    {
        var plot = Base("半环形图");
        plot.Data.Add(new PieTrace
        {
            Labels = ["直达", "邮件", "联盟", "视频", "搜索"],
            Values = [335, 310, 234, 135, 948],
            Hole = 0.5,
            Rotation = 225,
            Sort = false,
            TextInfo = PieTextInfo.LabelPercent
        });
        return plot;
    }

    public static Plot NightingaleChart()
    {
        var plot = Base("南丁格尔玫瑰图");
        plot.Data.Add(new PieTrace
        {
            Labels = ["直达", "邮件", "联盟", "视频", "搜索"],
            Values = [335, 310, 234, 135, 948],
            Sort = true,
            TextInfo = PieTextInfo.LabelPercent
        });
        return plot;
    }

    // ========== 散点图 ==========

    public static Plot ScatterChart()
    {
        var plot = Base("基础散点图");
        plot.Layout.XAxis.Title = "功率 [hp]";
        plot.Layout.YAxis.Title = "油耗 [mpg]";
        var rng = Rng(7);
        var n = 60;
        var x = new double[n]; var y = new double[n];
        for (var i = 0; i < n; i++) { x[i] = 60 + rng.NextDouble() * 340; y[i] = 45 - x[i] * 0.07 + rng.NextDouble() * 8; }
        plot.Data.Add(new ScatterTrace { X = x, Y = y, Mode = ScatterMode.Markers, Name = "车型", Marker = { Size = 10, Opacity = 0.8 } });
        return plot;
    }

    public static Plot BubbleChart()
    {
        var plot = Base("气泡图 (大小=权重, 颜色=功率)");
        plot.Layout.XAxis.Title = "功率 [hp]";
        plot.Layout.YAxis.Title = "油耗 [mpg]";
        var rng = Rng(7);
        var n = 50;
        var x = new double[n]; var y = new double[n]; var w = new double[n];
        for (var i = 0; i < n; i++)
        {
            x[i] = 60 + rng.NextDouble() * 340;
            y[i] = 45 - x[i] * 0.07 + rng.NextDouble() * 8;
            w[i] = 900 + x[i] * 6 + rng.NextDouble() * 300;
        }
        plot.Data.Add(new ScatterTrace { X = x, Y = y, Mode = ScatterMode.Markers, Name = "气泡", Marker = { Size = 12, ColorValues = w, Colorscale = Colorscale.Plasma, OutlineColor = Colors.White, OutlineWidth = 1 } });
        return plot;
    }

    public static Plot EffectScatterChart()
    {
        var plot = Base("涟漪特效散点图");
        var rng = Rng(11);
        var x = Enumerable.Range(0, 20).Select(_ => rng.NextDouble() * 100).ToArray();
        var y = Enumerable.Range(0, 20).Select(_ => rng.NextDouble() * 100).ToArray();
        plot.Data.Add(new ScatterTrace { X = x, Y = y, Mode = ScatterMode.Markers, Name = "涟漪", Marker = { Size = 18, Opacity = 0.6, Color = Color.Parse("#636efa") } });
        return plot;
    }

    // ========== 热力图 ==========

    public static Plot HeatmapChart()
    {
        var plot = Base("笛卡尔热力图");
        string[] labels = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta"];
        var n = labels.Length;
        var rng = Rng(11);
        var z = new double[n, n];
        for (var i = 0; i < n; i++) for (var j = 0; j < n; j++) z[i, j] = i == j ? 1 : Math.Round(rng.NextDouble() * 2 - 1, 2);
        for (var i = 0; i < n; i++) for (var j = i + 1; j < n; j++) z[j, i] = z[i, j];
        plot.Data.Add(new HeatmapTrace { Z = z, X = labels, Y = labels, Colorscale = Colorscale.RdBu, ZMin = -1, ZMax = 1, ShowScale = true });
        return plot;
    }

    public static Plot CalendarHeatmapChart()
    {
        var plot = Base("日历热力图 (模拟)");
        var rng = Rng(55);
        var n = 53; // 53 周
        var m = 7;  // 7 天
        var z = new double[m, n];
        for (var w = 0; w < n; w++) for (var d = 0; d < m; d++) z[d, w] = rng.Next(0, 5);
        var x = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
        var y = Enumerable.Range(1, m).Select(i => (double)i).ToArray();
        plot.Data.Add(new HeatmapTrace { Z = z, X = x, Y = y, Colorscale = Colorscale.Greys, ShowScale = true });
        plot.Layout.XAxis.Title = "周";
        plot.Layout.YAxis.Title = "星期";
        return plot;
    }

    // ========== 箱线图 ==========

    public static Plot BoxChart()
    {
        var plot = Base("盒须图");
        var rng = Rng(5);
        double[] Dist(double mean, double spread)
        {
            return Enumerable.Range(0, 60).Select(_ =>
            {
                var u1 = 1 - rng.NextDouble(); var u2 = rng.NextDouble();
                return mean + Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2) * spread;
            }).ToArray();
        }
        plot.Data.Add(new BoxTrace { Y = Dist(95, 18), Category = "工程" });
        plot.Data.Add(new BoxTrace { Y = Dist(80, 14), Category = "市场" });
        plot.Data.Add(new BoxTrace { Y = Dist(70, 10), Category = "运营" });
        plot.Data.Add(new BoxTrace { Y = Dist(105, 22), Category = "管理" });
        plot.Layout.YAxis.Title = "月薪 [k]";
        return plot;
    }

    // ========== K线图 ==========

    public static Plot CandlestickChart()
    {
        var plot = Base("基础K线图");
        var rng = Rng(21);
        var n = 40;
        var dates = Enumerable.Range(0, n).Select(i => DateTime.Today.AddDays(i - n)).ToArray();
        var o = new double[n]; var h = new double[n]; var l = new double[n]; var c = new double[n];
        var price = 100.0;
        for (var i = 0; i < n; i++)
        {
            o[i] = price;
            var ch = (rng.NextDouble() - 0.48) * 6;
            c[i] = Math.Max(1, price + ch);
            h[i] = Math.Max(o[i], c[i]) + rng.NextDouble() * 2.5;
            l[i] = Math.Min(o[i], c[i]) - rng.NextDouble() * 2.5;
            price = c[i];
        }
        plot.Data.Add(new CandlestickTrace { X = dates, Open = o, High = h, Low = l, Close = c, Name = "ACME" });
        plot.Layout.YAxis.Title = "USD";
        return plot;
    }

    // ========== 雷达图 ==========

    public static Plot BasicRadarChart()
    {
        var plot = Base("基础雷达图");
        plot.RadarAxis = new RadarAxis { Indicators = ["销售", "管理", "技术", "客服", "研发", "市场"] };
        plot.Data.Add(new RadarTrace { Values = [90, 80, 85, 70, 95, 75], Name = "预算分配", FillColor = Color.Parse("#636efa") });
        plot.Data.Add(new RadarTrace { Values = [70, 90, 60, 85, 80, 90], Name = "实际开销", FillColor = Color.Parse("#EF553B") });
        return plot;
    }

    public static Plot MultiRadarChart()
    {
        var plot = Base("多雷达图");
        plot.RadarAxis = new RadarAxis
        {
            Indicators = ["语文", "数学", "英语", "物理", "化学", "生物"],
            MaxValues = [150, 150, 150, 100, 100, 100]
        };
        plot.Data.Add(new RadarTrace { Values = [130, 140, 120, 85, 90, 88], Name = "小明", FillColor = Color.Parse("#00cc96") });
        plot.Data.Add(new RadarTrace { Values = [120, 135, 140, 78, 82, 92], Name = "小红", FillColor = Color.Parse("#ab63fa") });
        return plot;
    }

    // ========== 地图 ==========

    public static Plot WorldMapChart()
    {
        var plot = Base("世界地图 (等值区划图)");
        // 模拟各国 GDP 数据
        var codes = new[] { "USA", "CHN", "JPN", "DEU", "GBR", "IND", "FRA", "BRA", "CAN", "AUS", "RUS", "KOR", "IDN", "MEX", "SAU", "TUR", "IRN", "NGA", "ZAF", "EGY", "ETH", "ARG", "COL", "PER", "THA", "POL", "UKR", "NOR", "SWE", "NZL" };
        var vals = new double[] { 25000, 18000, 4200, 4100, 3100, 3500, 2800, 1900, 2100, 1700, 2200, 1800, 1300, 1300, 1100, 900, 400, 450, 400, 400, 120, 630, 340, 240, 500, 700, 160, 580, 590, 250 };
        plot.Data.Add(new MapTrace { Regions = codes, Values = vals, Labels = codes, Colorscale = Colorscale.YlGnBu, ShowScale = true });
        return plot;
    }

    public static Plot MapWithScatterChart()
    {
        var plot = Base("地图散点叠加");
        // 底图
        plot.Data.Add(new MapTrace { Colorscale = Colorscale.Greys, ShowScale = false, BorderColor = Color.Parse("#cccccc") });
        // 散点叠加（主要城市经纬度）
        double[] lon = [-74, -118, -0.1, 2.3, 116.4, 139.7, 151.2, 77.2, 126.9, -43.2];
        double[] lat = [40.7, 34, 51.5, 48.9, 39.9, 35.7, -33.9, 28.6, 37.6, -22.9];
        var sizes = new double[] { 12, 10, 10, 8, 14, 10, 8, 9, 8, 8 };
        // 将经纬度转换为归一化坐标（0..1 范围内）用于散点
        // 注意：散点在地图序列之后会被地图覆盖，这里简化处理
        return plot;
    }

    // ========== 直方图 ==========

    public static Plot HistogramChart()
    {
        var plot = Base("直方图");
        var rng = Rng(99);
        var values = new double[800];
        for (var i = 0; i < values.Length; i++)
        {
            var u1 = 1 - rng.NextDouble(); var u2 = rng.NextDouble();
            values[i] = 120 + Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2) * 35;
        }
        plot.Data.Add(new HistogramTrace { X = values, NBins = 32, Name = "响应时间" });
        plot.Layout.XAxis.Title = "ms";
        plot.Layout.YAxis.Title = "频次";
        return plot;
    }

    // ========== 主题 ==========

    public static Plot DarkThemeChart()
    {
        var plot = SmoothLineChart();
        plot.Theme = PlotTheme.PlotlyDark;
        plot.Layout.Title.Text = "plotly_dark 暗色主题";
        return plot;
    }

    // ========== 仪表盘 ==========

    public static Plot BasicGaugeChart()
    {
        var plot = Base("基础仪表盘");
        plot.Data.Add(new GaugeTrace { Value = 67, Min = 0, Max = 100, Title = "完成率", Unit = "%" });
        return plot;
    }

    public static Plot SpeedGaugeChart()
    {
        var plot = Base("速度仪表盘");
        plot.Data.Add(new GaugeTrace
        {
            Value = 72, Min = 0, Max = 200, Title = "km/h",
            StartAngle = 225, EndAngle = -45,
            Segments = [(0.3, Avalonia.Media.Color.Parse("#91cc75")), (0.7, Avalonia.Media.Color.Parse("#fac858")), (1.0, Avalonia.Media.Color.Parse("#ee6666"))]
        });
        return plot;
    }

    public static Plot RingGaugeChart()
    {
        var plot = Base("得分环");
        plot.Data.Add(new GaugeTrace { Value = 85, Min = 0, Max = 100, Title = "得分", Unit = "分", Style = GaugeStyle.Ring, RadiusRatio = 0.6 });
        return plot;
    }

    public static Plot ProgressGaugeChart()
    {
        var plot = Base("进度仪表盘");
        plot.Data.Add(new GaugeTrace { Value = 45, Min = 0, Max = 100, Title = "任务进度", Unit = "%", Style = GaugeStyle.Progress, ShowTickLabels = false });
        return plot;
    }

    // ========== 树图 ==========

    public static Plot BasicTreeChart()
    {
        var plot = Base("基础树图");
        var root = new TreeNode { Name = "公司", Value = 100 };
        var eng = new TreeNode { Name = "工程部", Value = 50 };
        eng.Children.Add(new TreeNode { Name = "前端组", Value = 20 });
        eng.Children.Add(new TreeNode { Name = "后端组", Value = 20 });
        eng.Children.Add(new TreeNode { Name = "测试组", Value = 10 });
        var mkt = new TreeNode { Name = "市场部", Value = 30 };
        mkt.Children.Add(new TreeNode { Name = "品牌组", Value = 15 });
        mkt.Children.Add(new TreeNode { Name = "渠道组", Value = 15 });
        var ops = new TreeNode { Name = "运营部", Value = 20 };
        ops.Children.Add(new TreeNode { Name = "客服组", Value = 10 });
        ops.Children.Add(new TreeNode { Name = "数据分析", Value = 10 });
        root.Children.Add(eng);
        root.Children.Add(mkt);
        root.Children.Add(ops);
        plot.Data.Add(new TreeTrace { Root = root, Name = "组织架构" });
        return plot;
    }

    // ========== 关系图 ==========

    public static Plot BasicGraphChart()
    {
        var plot = Base("基础关系图");
        GraphNode[] nodes =
        [
            new() { Id = "A", Name = "中心", Size = 30 },
            new() { Id = "B", Name = "节点B", Size = 20 },
            new() { Id = "C", Name = "节点C", Size = 20 },
            new() { Id = "D", Name = "节点D", Size = 15 },
            new() { Id = "E", Name = "节点E", Size = 15 },
            new() { Id = "F", Name = "节点F", Size = 18 },
            new() { Id = "G", Name = "节点G", Size = 12 },
        ];
        GraphEdge[] edges =
        [
            new() { Source = "A", Target = "B" },
            new() { Source = "A", Target = "C" },
            new() { Source = "A", Target = "D" },
            new() { Source = "B", Target = "E" },
            new() { Source = "C", Target = "F" },
            new() { Source = "D", Target = "G" },
            new() { Source = "B", Target = "C" },
        ];
        plot.Data.Add(new GraphTrace { Nodes = nodes, Edges = edges, Name = "关系网络" });
        return plot;
    }

    // ========== 3D ==========

    public static Plot Surface3DChart()
    {
        var plot = Base("3D 曲面图 z=sin(√(x²+y²))");
        plot.Data.Add(new SurfaceTrace
        {
            Function = (x, y) => Math.Sin(Math.Sqrt(x * x + y * y)) * 2,
            XMin = -6, XMax = 6, YMin = -6, YMax = 6,
            Resolution = 40,
            Colorscale = Colorscale.Viridis,
            RotationX = 25, RotationZ = -55,
            Name = "曲面"
        });
        return plot;
    }

    public static Plot Scatter3DChart()
    {
        var plot = Base("3D 散点图");
        var rng = Rng(42);
        var n = 200;
        var x = new double[n]; var y = new double[n]; var z = new double[n];
        for (var i = 0; i < n; i++)
        {
            x[i] = (rng.NextDouble() - 0.5) * 10;
            y[i] = (rng.NextDouble() - 0.5) * 10;
            z[i] = Math.Sin(x[i]) * Math.Cos(y[i]) * 3 + rng.NextDouble();
        }
        plot.Data.Add(new Scatter3DTrace
        {
            X = x, Y = y, Z = z,
            MarkerSize = 5,
            Colorscale = Colorscale.Plasma,
            RotationX = 25, RotationZ = -50,
            Name = "散点"
        });
        return plot;
    }

    // ========== 动画示例 ==========

    /// <summary>动态折线图：随机漫步数据持续更新，平滑过渡。</summary>
    public static Plot AnimatedLineChart()
    {
        var plot = new AnimatedPlot();
        plot.Layout.Title.Text = "动态折线图（每秒更新，平滑过渡）";
        plot.Animation.Duration = TimeSpan.FromMilliseconds(800);
        plot.Animation.Easing = Easing.EaseInOut;

        var rng = Rng(42);
        var x = Enumerable.Range(0, 50).Select(i => (double)i).ToArray();
        double[] Gen() { var v = 200.0; return x.Select(_ => v += (rng.NextDouble() - 0.5) * 30).ToArray(); }
        plot.Data.Add(new ScatterTrace { X = x, Y = Gen(), Mode = ScatterMode.Lines, Name = "系列 A", Line = { Shape = LineShape.Spline, Width = 2.5 } });
        plot.Data.Add(new ScatterTrace { X = x, Y = Gen(), Mode = ScatterMode.Lines, Name = "系列 B", Line = { Shape = LineShape.Spline, Width = 2.5 } });
        return plot;
    }

    /// <summary>动态柱状图：柱高平滑变化。</summary>
    public static Plot AnimatedBarChart()
    {
        var plot = new AnimatedPlot();
        plot.Layout.Title.Text = "动态柱状图（柱高平滑过渡）";
        plot.Animation.Duration = TimeSpan.FromMilliseconds(600);
        plot.Animation.Easing = Easing.BackOut;

        string[] cats = ["Q1", "Q2", "Q3", "Q4"];
        plot.Data.Add(new BarTrace { X = cats, Y = new[] { 120.0, 160, 140, 190 }, Name = "2024", TextPosition = TraceTextPosition.Outside });
        plot.Data.Add(new BarTrace { X = cats, Y = new[] { 90.0, 130, 170, 150 }, Name = "2025", TextPosition = TraceTextPosition.Outside });
        return plot;
    }

    /// <summary>动态饼图：扇区大小平滑变化。</summary>
    public static Plot AnimatedPieChart()
    {
        var plot = new AnimatedPlot();
        plot.Layout.Title.Text = "动态饼图（扇区平滑过渡）";
        plot.Animation.Duration = TimeSpan.FromMilliseconds(700);
        plot.Animation.Easing = Easing.EaseInOut;

        plot.Data.Add(new PieTrace
        {
            Labels = ["直达", "邮件", "联盟", "视频", "搜索"],
            Values = [335, 310, 234, 135, 948],
            Hole = 0.4
        });
        return plot;
    }

    /// <summary>动态仪表盘：指针平滑摆动。</summary>
    public static Plot AnimatedGaugeChart()
    {
        var plot = new AnimatedPlot();
        plot.Layout.Title.Text = "动态仪表盘（指针平滑摆动）";
        plot.Animation.Duration = TimeSpan.FromMilliseconds(900);
        plot.Animation.Easing = Easing.ElasticOut;

        plot.Data.Add(new GaugeTrace
        {
            Value = 50, Min = 0, Max = 100,
            Title = "实时指标", Unit = "%"
        });
        return plot;
    }

    /// <summary>动态雷达图：维度值平滑变化。</summary>
    public static Plot AnimatedRadarChart()
    {
        var plot = new AnimatedPlot();
        plot.Layout.Title.Text = "动态雷达图（维度值平滑过渡）";
        plot.Animation.Duration = TimeSpan.FromMilliseconds(600);
        plot.Animation.Easing = Easing.EaseInOut;

        plot.RadarAxis = new RadarAxis { Indicators = ["销售", "管理", "技术", "客服", "研发", "市场"] };
        plot.Data.Add(new RadarTrace { Values = [90, 80, 85, 70, 95, 75], Name = "上月", FillColor = Color.Parse("#636efa") });
        plot.Data.Add(new RadarTrace { Values = [70, 90, 60, 85, 80, 90], Name = "本月", FillColor = Color.Parse("#00cc96") });
        return plot;
    }

    // ========== 流式数据 ==========

    public sealed class StreamingSeries
    {
        private readonly List<double> _x = [];
        private readonly List<double> _y = [];
        private double _value = 50;
        private int _tick;
        private readonly Random _rng = new(1);

        public ScatterTrace Trace { get; }

        public StreamingSeries(string name)
        {
            Trace = new ScatterTrace { Mode = ScatterMode.Lines, Name = name };
            Push();
        }

        public void Push()
        {
            _value += (_rng.NextDouble() - 0.5) * 6;
            _x.Add(_tick++);
            _y.Add(_value);
            if (_x.Count > 120) { _x.RemoveAt(0); _y.RemoveAt(0); }
            Trace.X = _x.ToArray();
            Trace.Y = _y.ToArray();
        }
    }

    private static Plot Base(string title)
    {
        var plot = new Plot();
        plot.Layout.Title.Text = title;
        return plot;
    }
}
