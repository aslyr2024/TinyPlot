namespace TinyPlot.Animation;

/// <summary>
/// 缓动函数类型，对应 ECharts 的 easing 函数。
/// </summary>
public enum Easing
{
    /// <summary>线性。</summary>
    Linear,
    /// <summary>缓入（加速）。</summary>
    EaseIn,
    /// <summary>缓出（减速）。</summary>
    EaseOut,
    /// <summary>缓入缓出（平滑）。</summary>
    EaseInOut,
    /// <summary>弹性缓出。</summary>
    ElasticOut,
    /// <summary>回弹缓出。</summary>
    BackOut,
    /// <summary>弹跳缓出。</summary>
    BounceOut
}

/// <summary>
/// 动画缓动函数工具类。
/// </summary>
public static class EasingFunctions
{
    /// <summary>获取缓动函数。</summary>
    public static Func<double, double> Get(Easing easing) => easing switch
    {
        Easing.Linear => Linear,
        Easing.EaseIn => EaseIn,
        Easing.EaseOut => EaseOut,
        Easing.EaseInOut => EaseInOut,
        Easing.ElasticOut => ElasticOut,
        Easing.BackOut => BackOut,
        Easing.BounceOut => BounceOut,
        _ => Linear
    };

    /// <summary>线性: t</summary>
    public static double Linear(double t) => t;

    /// <summary>缓入: t²</summary>
    public static double EaseIn(double t) => t * t;

    /// <summary>缓出: 1 - (1-t)²</summary>
    public static double EaseOut(double t) => 1 - (1 - t) * (1 - t);

    /// <summary>缓入缓出: 三次贝塞尔</summary>
    public static double EaseInOut(double t) => t < 0.5
        ? 4 * t * t * t
        : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    /// <summary>弹性缓出。</summary>
    public static double ElasticOut(double t)
    {
        if (t == 0 || t == 1) return t;
        return Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1;
    }

    /// <summary>回弹缓出。</summary>
    public static double BackOut(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }

    /// <summary>弹跳缓出。</summary>
    public static double BounceOut(double t)
    {
        const double n1 = 7.5625;
        const double d1 = 2.75;
        if (t < 1 / d1) return n1 * t * t;
        if (t < 2 / d1) return n1 * (t -= 1.5 / d1) * t + 0.75;
        if (t < 2.5 / d1) return n1 * (t -= 2.25 / d1) * t + 0.9375;
        return n1 * (t -= 2.625 / d1) * t + 0.984375;
    }
}

/// <summary>
/// 数组插值器。在两组数值之间平滑过渡。
/// </summary>
public static class Interpolator
{
    /// <summary>在两个 double 数组之间线性插值。</summary>
    public static double[] Lerp(double[] from, double[] to, double t)
    {
        var len = Math.Max(from.Length, to.Length);
        var result = new double[len];
        for (var i = 0; i < len; i++)
        {
            var a = i < from.Length ? from[i] : 0;
            var b = i < to.Length ? to[i] : 0;
            result[i] = a + (b - a) * t;
        }
        return result;
    }

    /// <summary>在两个颜色之间线性插值。</summary>
    public static Avalonia.Media.Color Lerp(Avalonia.Media.Color from, Avalonia.Media.Color to, double t)
    {
        return Avalonia.Media.Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
    }

    /// <summary>在两个颜色数组之间线性插值。</summary>
    public static Avalonia.Media.Color[] Lerp(Avalonia.Media.Color[] from, Avalonia.Media.Color[] to, double t)
    {
        var len = Math.Max(from.Length, to.Length);
        var result = new Avalonia.Media.Color[len];
        for (var i = 0; i < len; i++)
        {
            var a = i < from.Length ? from[i] : Avalonia.Media.Colors.Transparent;
            var b = i < to.Length ? to[i] : Avalonia.Media.Colors.Transparent;
            result[i] = Lerp(a, b, t);
        }
        return result;
    }

    /// <summary>在两个 double 值之间线性插值。</summary>
    public static double Lerp(double from, double to, double t) => from + (to - from) * t;
}
