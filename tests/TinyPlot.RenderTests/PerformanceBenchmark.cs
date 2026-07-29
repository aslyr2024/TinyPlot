using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace TinyPlot.RenderTests;

/// <summary>
/// 性能基准测试。测试百万数据量下的渲染性能。
/// 通过 Avalonia 无头平台进行实际渲染。
/// </summary>
public static class PerformanceBenchmark
{
    public static int Run(string[] args)
    {
        AppBuilder.Configure<BenchApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .SetupWithoutStarting();

        var results = new List<string>();

        // 百万散点图
        results.Add(RunScatter1M());

        // 10万线段
        results.Add(RunLine100K());

        // 大规模柱状图
        results.Add(RunBar10K());

        // 热力图 200x200
        results.Add(RunHeatmap20K());

        // 多线段（1000 条 × 100 点）
        results.Add(RunMultiLine());

        Console.WriteLine("\n===== 性能基准测试结果 =====");
        foreach (var r in results) Console.WriteLine(r);
        Console.WriteLine("============================\n");
        return 0;
    }

    private static string RunScatter1M()
    {
        var n = 1_000_000;
        var rng = new Random(42);
        var x = new double[n];
        var y = new double[n];
        for (var i = 0; i < n; i++)
        {
            x[i] = rng.NextDouble() * 1000;
            y[i] = rng.NextDouble() * 1000;
        }

        var plot = new Plot();
        plot.Layout.Title.Text = $"100万散点图 ({n:N0} 点)";
        plot.Layout.ShowLegend = false;
        plot.Data.Add(new ScatterTrace
        {
            X = x, Y = y,
            Mode = ScatterMode.Markers,
            Marker = { Size = 1.5, Opacity = 0.15, Color = Color.Parse("#636efa") }
        });

        return Measure("scatter-1m (100万点)", plot, 1200, 800);
    }

    private static string RunLine100K()
    {
        var n = 100_000;
        var x = new double[n];
        var y = new double[n];
        for (var i = 0; i < n; i++)
        {
            x[i] = i * 0.01;
            y[i] = Math.Sin(x[i]) * 50 + Math.Sin(x[i] * 7.3) * 10;
        }

        var plot = new Plot();
        plot.Layout.Title.Text = $"10万点折线图 ({n:N0} 点)";
        plot.Data.Add(new ScatterTrace { X = x, Y = y, Mode = ScatterMode.Lines, Line = { Width = 1 } });

        return Measure("line-100k (10万线段)", plot, 1200, 800);
    }

    private static string RunBar10K()
    {
        var n = 10_000;
        var x = Enumerable.Range(0, n).Select(i => $"C{i}").ToArray();
        var y = new double[n];
        var rng = new Random(42);
        for (var i = 0; i < n; i++) y[i] = rng.NextDouble() * 100;

        var plot = new Plot();
        plot.Layout.Title.Text = $"1万柱状图 ({n:N0} 柱)";
        plot.Layout.ShowLegend = false;
        plot.Data.Add(new BarTrace { X = x, Y = y });

        return Measure("bar-10k (1万柱)", plot, 1200, 800);
    }

    private static string RunHeatmap20K()
    {
        var size = 200;
        var z = new double[size, size];
        var rng = new Random(42);
        for (var i = 0; i < size; i++)
            for (var j = 0; j < size; j++)
                z[i, j] = Math.Sin(i * 0.1) * Math.Cos(j * 0.1) + rng.NextDouble() * 0.3;

        var plot = new Plot();
        plot.Layout.Title.Text = $"热力图 {size}x{size} ({size * size:N0} 单元)";
        plot.Data.Add(new HeatmapTrace { Z = z, Colorscale = Colorscale.Viridis });

        return Measure("heatmap-200x200 (4万单元)", plot, 1200, 800);
    }

    private static string RunMultiLine()
    {
        var rng = new Random(42);
        var plot = new Plot();
        plot.Layout.Title.Text = "多线段（1000 条 × 100 点 = 10万点）";
        plot.Layout.ShowLegend = false;
        for (var s = 0; s < 1000; s++)
        {
            var x = Enumerable.Range(0, 100).Select(i => (double)i).ToArray();
            var y = Enumerable.Range(0, 100).Select(i => Math.Sin(i * 0.1 + s * 0.05) * 50 + rng.NextDouble() * 5).ToArray();
            plot.Data.Add(new ScatterTrace { X = x, Y = y, Mode = ScatterMode.Lines, Line = { Width = 0.5 }, Opacity = 0.3 });
        }

        return Measure("multiline-1000x100 (10万点)", plot, 1200, 800);
    }

    private static string Measure(string label, Plot plot, int width, int height)
    {
        var window = new Window { Width = width, Height = height, Content = plot, Background = Brushes.White };
        window.Show();
        for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

        // 预热
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

        // 正式测量 5 次
        var times = new List<double>();
        for (var run = 0; run < 5; run++)
        {
            plot.Refresh();
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
            var sw = Stopwatch.StartNew();
            var frame = window.CaptureRenderedFrame();
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        window.Close();

        var avg = times.Average();
        var min = times.Min();
        var max = times.Max();
        return $"  {label,-35} 平均={avg,7:F1}ms  最小={min,7:F1}ms  最大={max,7:F1}ms  ({width}×{height})";
    }

    private sealed class BenchApp : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
    }
}
