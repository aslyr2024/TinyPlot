using Avalonia.Controls;
using Avalonia.Threading;
using TinyPlot.Animation;
using TinyPlot.Samples;

namespace TinyPlot.Demo;

public partial class MainWindow : Window
{
    private bool _dark;
    private DispatcherTimer? _streamTimer;
    private DispatcherTimer? _animTimer;

    public MainWindow()
    {
        InitializeComponent();

        // 按类别分组添加图表标签页
        var categories = ChartSamples.All.GroupBy(c => c.Category).OrderBy(g => g.Key);
        foreach (var group in categories)
        {
            foreach (var (name, _, factory) in group)
            {
                var plot = factory();
                Tabs.Items.Add(new TabItem { Header = $"[{group.Key}] {name}", Content = plot });
            }
        }

        // 流式数据标签页
        Tabs.Items.Add(new TabItem { Header = "[实时] 动态数据 + 时间轴", Content = BuildStreamingTab() });
        Tabs.Items.Add(new TabItem { Header = "[实时] TimelinePlot", Content = BuildTimelineTab() });

        // 启动动画示例
        StartAnimations();

        ThemeToggle.Click += (_, _) =>
        {
            _dark = !_dark;
            foreach (var item in Tabs.Items)
            {
                if (item is TabItem { Content: Plot p })
                {
                    p.Theme = _dark ? PlotTheme.PlotlyDark : PlotTheme.Plotly;
                    p.Refresh();
                }
            }
        };
    }

    private Control BuildStreamingTab()
    {
        var plot = new Plot();
        plot.Layout.Title.Text = "动态数据 + 时间坐标轴 (10 Hz)";
        plot.Layout.YAxis.Title = "数值";
        var a = new ChartSamples.StreamingSeries("传感器 A");
        var b = new ChartSamples.StreamingSeries("传感器 B");
        plot.Data.Add(a.Trace);
        plot.Data.Add(b.Trace);

        _streamTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _streamTimer.Tick += (_, _) => { a.Push(); b.Push(); plot.Refresh(); };
        _streamTimer.Start();
        return plot;
    }

    private Control BuildTimelineTab()
    {
        var tl = new TimelinePlot();
        tl.Layout.Title.Text = "TimelinePlot 控件演示";
        tl.AddSeries("温度", t => 20 + 5 * Math.Sin(t * 0.05) + new Random().NextDouble() * 2);
        tl.AddSeries("湿度", t => 60 + 10 * Math.Cos(t * 0.03) + new Random().NextDouble() * 3);
        tl.Play();
        return tl;
    }

    private void StartAnimations()
    {
        var rng = new Random(42);

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _animTimer.Tick += (_, _) =>
        {
            foreach (var item in Tabs.Items)
            {
                if (item is not TabItem { Content: AnimatedPlot plot }) continue;

                var title = plot.Layout.Title.Text ?? "";

                // 动态折线图
                if (title.Contains("动态折线"))
                {
                    var s0 = (ScatterTrace)plot.Data[0];
                    var s1 = (ScatterTrace)plot.Data[1];
                    var x = Enumerable.Range(0, 50).Select(i => (double)i).ToArray();
                    double[] Gen() { var v = 200.0; return x.Select(_ => v += (rng.NextDouble() - 0.5) * 30).ToArray(); }
                    plot.AnimateTo(() => { s0.Y = Gen(); s1.Y = Gen(); });
                }
                // 动态柱状图
                else if (title.Contains("动态柱状"))
                {
                    double[] R() => Enumerable.Range(0, 4).Select(_ => 80 + rng.NextDouble() * 140).ToArray();
                    plot.AnimateTo(() =>
                    {
                        ((BarTrace)plot.Data[0]).Y = R();
                        ((BarTrace)plot.Data[1]).Y = R();
                    });
                }
                // 动态饼图
                else if (title.Contains("动态饼图"))
                {
                    double[] R() => Enumerable.Range(0, 5).Select(_ => 100 + rng.NextDouble() * 800).ToArray();
                    plot.AnimateTo(() => { ((PieTrace)plot.Data[0]).Values = R(); });
                }
                // 动态仪表盘
                else if (title.Contains("动态仪表盘"))
                {
                    var val = 20 + rng.NextDouble() * 80;
                    plot.AnimateTo(() => { ((GaugeTrace)plot.Data[0]).Value = val; });
                }
                // 动态雷达图
                else if (title.Contains("动态雷达"))
                {
                    double[] R() => Enumerable.Range(0, 6).Select(_ => 40 + rng.NextDouble() * 60).ToArray();
                    plot.AnimateTo(() =>
                    {
                        ((RadarTrace)plot.Data[0]).Values = R();
                        ((RadarTrace)plot.Data[1]).Values = R();
                    });
                }
            }
        };
        _animTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _streamTimer?.Stop();
        _animTimer?.Stop();
        base.OnClosed(e);
    }
}
