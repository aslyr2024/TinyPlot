using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Media;
using Avalonia.Threading;
using TinyPlot.Samples;

namespace TinyPlot.RenderTests;

public static class Program
{
    public static int Main(string[] args)
    {
        // --benchmark 参数运行性能测试
        if (args.Length > 0 && args[0] == "--benchmark")
            return PerformanceBenchmark.Run(args);

        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .SetupWithoutStarting();

        var outDir = args.Length > 0
            ? args[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts"));
        Directory.CreateDirectory(outDir);

        var failures = 0;
        foreach (var (name, category, factory) in ChartSamples.All)
        {
            try
            {
                var plot = factory();
                var window = new Window { Width = 960, Height = 640, Content = plot, Background = Brushes.White };
                window.Show();
                PumpFrames(window);

                var frame = window.CaptureRenderedFrame();
                var safeName = name.Replace(" ", "_").Replace("/", "_");
                var path = Path.Combine(outDir, safeName + ".png");
                if (frame == null)
                {
                    Console.Error.WriteLine($"FAIL {name}: 无法捕获帧");
                    failures++;
                }
                else
                {
#pragma warning disable CS0618
                    frame.Save(path);
#pragma warning restore CS0618
                    Console.WriteLine($"OK   [{category}] {name} -> {safeName}.png");
                }

                window.Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {name}: {ex}");
                failures++;
            }
        }

        // ---- 交互测试：悬停和框选缩放 ----
        try
        {
            var plot = ChartSamples.UnifiedHoverChart();
            var window = new Window { Width = 960, Height = 640, Content = plot, Background = Brushes.White };
            window.Show();
            PumpFrames(window);

            window.MouseMove(new Point(420, 300));
            PumpFrames(window);
            SaveFrame(window, Path.Combine(outDir, "interaction-hover.png"));

            window.MouseDown(new Point(300, 220), MouseButton.Left);
            window.MouseMove(new Point(650, 420), RawInputModifiers.LeftMouseButton);
            PumpFrames(window);
            SaveFrame(window, Path.Combine(outDir, "interaction-zoomrect.png"));

            window.MouseUp(new Point(650, 420), MouseButton.Left);
            PumpFrames(window);
            SaveFrame(window, Path.Combine(outDir, "interaction-zoomed.png"));
            Console.WriteLine("OK   交互测试渲染完成");
            window.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL 交互测试: {ex}");
            failures++;
        }

        Console.WriteLine(failures == 0 ? $"全部渲染成功 ({ChartSamples.All.Length} 张图表)." : $"{failures} 张图表渲染失败.");
        return failures == 0 ? 0 : 1;
    }

    private static void PumpFrames(Window window)
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        }
    }

    private static void SaveFrame(Window window, string path)
    {
        var frame = window.CaptureRenderedFrame();
        if (frame == null)
        {
            Console.Error.WriteLine($"FAIL: 无法捕获帧 {path}");
            return;
        }
#pragma warning disable CS0618
        frame.Save(path);
#pragma warning restore CS0618
        Console.WriteLine($"OK   {Path.GetFileName(path)}");
    }

    private sealed class TestApp : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
    }
}
