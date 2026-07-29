using Avalonia.Media;

namespace TinyPlot;

/// <summary>
/// A continuous color gradient used by heatmaps and marker color mapping.
/// Ships with the plotly.js built-in colorscales.
/// </summary>
public sealed class Colorscale
{
    private readonly (double t, Color c)[] _stops;

    public Colorscale(params (double t, Color c)[] stops)
    {
        _stops = stops.OrderBy(s => s.t).ToArray();
    }

    public string? Name { get; init; }

    public Color GetColor(double t)
    {
        t = double.IsNaN(t) ? 0 : Math.Clamp(t, 0, 1);
        var s = _stops;
        if (t <= s[0].t) return s[0].c;
        if (t >= s[^1].t) return s[^1].c;
        for (var i = 1; i < s.Length; i++)
        {
            if (t <= s[i].t)
            {
                var f = (t - s[i - 1].t) / (s[i].t - s[i - 1].t);
                var a = s[i - 1].c;
                var b = s[i].c;
                return Color.FromArgb(
                    (byte)(a.A + (b.A - a.A) * f),
                    (byte)(a.R + (b.R - a.R) * f),
                    (byte)(a.G + (b.G - a.G) * f),
                    (byte)(a.B + (b.B - a.B) * f));
            }
        }

        return s[^1].c;
    }

    private static Color H(string hex) => Color.Parse(hex);

    private static Colorscale Build(string name, params string[] hexes)
    {
        var stops = new (double, Color)[hexes.Length];
        for (var i = 0; i < hexes.Length; i++)
            stops[i] = (hexes.Length == 1 ? 0 : (double)i / (hexes.Length - 1), H(hexes[i]));
        return new Colorscale(stops) { Name = name };
    }

    public static Colorscale Viridis => Build(nameof(Viridis),
        "#440154", "#482878", "#3e4989", "#31688e", "#26828e", "#1f9e89", "#35b779", "#6ece58", "#b5de2b", "#fde725");

    public static Colorscale Plasma => Build(nameof(Plasma),
        "#0d0887", "#41049d", "#6a00a8", "#8f0da4", "#b12a90", "#cc4778", "#e16462", "#f2844b", "#fca636", "#fcce25", "#f0f921");

    public static Colorscale Inferno => Build(nameof(Inferno),
        "#000004", "#1b0c41", "#4a0c6b", "#781c6d", "#a52c60", "#cf4446", "#ed6925", "#fb9b06", "#f7d13d", "#fcffa4");

    public static Colorscale Magma => Build(nameof(Magma),
        "#000004", "#180f3d", "#440f76", "#721f81", "#9e2f7f", "#cd4071", "#f1605d", "#fd9668", "#feca8d", "#fcfdbf");

    public static Colorscale Cividis => Build(nameof(Cividis),
        "#00224e", "#35456c", "#666970", "#948e77", "#c0b47a", "#fee838");

    public static Colorscale Jet => Build(nameof(Jet),
        "#000083", "#003caa", "#05ffff", "#ffff00", "#fa0000", "#800000");

    public static Colorscale Hot => Build(nameof(Hot), "#000000", "#e60000", "#ffd200", "#ffffff");

    public static Colorscale Portland => Build(nameof(Portland), "#0c3383", "#0f88ba", "#f2d338", "#f28f38", "#d91e1e");

    public static Colorscale RdBu => Build(nameof(RdBu),
        "#67001f", "#b2182b", "#d6604d", "#f4a582", "#fddbc7", "#f7f7f7", "#d1e5f0", "#92c5de", "#4393c3", "#2166ac", "#053061");

    public static Colorscale Bluered => Build(nameof(Bluered), "#0000ff", "#ff0000");

    public static Colorscale Electric => Build(nameof(Electric), "#000000", "#1e0064", "#780064", "#a05a00", "#e6c800", "#fffadc");

    public static Colorscale Blackbody => Build(nameof(Blackbody), "#000000", "#e60000", "#e6d200", "#ffffff", "#a0c8ff");

    public static Colorscale Earth => Build(nameof(Earth), "#000082", "#00b5ad", "#40d27c", "#b4e632", "#fff78c", "#ffffff");

    public static Colorscale Greys => Build(nameof(Greys), "#000000", "#ffffff");

    public static Colorscale YlGnBu => Build(nameof(YlGnBu),
        "#081d58", "#1d91c0", "#41b6c4", "#7fcdbb", "#c7e9b4", "#edf8b1", "#ffffd9");

    public static Colorscale YlOrRd => Build(nameof(YlOrRd),
        "#800026", "#bd0026", "#e31a1c", "#fc4e2a", "#fd8d3c", "#feb24c", "#fed976", "#ffeda0", "#ffffcc");

    public static Colorscale Blues => Build(nameof(Blues),
        "#08306b", "#08519c", "#2171b5", "#4292c6", "#6baed6", "#9ecae1", "#c6dbef", "#deebf7", "#f7fbff");

    public static Colorscale Rainbow => Build(nameof(Rainbow), "#96005a", "#0000c8", "#0019ff", "#0098ff", "#2cff96", "#97ff00", "#ffea00", "#ff6e00", "#ff0000");

    public static Colorscale Picnic => Build(nameof(Picnic),
        "#0000ff", "#3399ff", "#66ccff", "#99ccff", "#ccccff", "#ffffff", "#ffccff", "#ff99ff", "#ff66cc", "#ff6666", "#ff0000");

    public static IReadOnlyList<(string Name, Func<Colorscale> Factory)> BuiltIn { get; } =
    [
        (nameof(Viridis), () => Viridis),
        (nameof(Plasma), () => Plasma),
        (nameof(Inferno), () => Inferno),
        (nameof(Magma), () => Magma),
        (nameof(Cividis), () => Cividis),
        (nameof(Jet), () => Jet),
        (nameof(Hot), () => Hot),
        (nameof(Portland), () => Portland),
        (nameof(RdBu), () => RdBu),
        (nameof(Bluered), () => Bluered),
        (nameof(Electric), () => Electric),
        (nameof(Blackbody), () => Blackbody),
        (nameof(Earth), () => Earth),
        (nameof(Greys), () => Greys),
        (nameof(YlGnBu), () => YlGnBu),
        (nameof(YlOrRd), () => YlOrRd),
        (nameof(Blues), () => Blues),
        (nameof(Rainbow), () => Rainbow),
        (nameof(Picnic), () => Picnic)
    ];

    public static Colorscale? Find(string name)
    {
        foreach (var (n, f) in BuiltIn)
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                return f();
        return null;
    }
}
