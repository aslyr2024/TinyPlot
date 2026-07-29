using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TinyPlot;

/// <summary>
/// Heatmap, the counterpart of plotly.js type "heatmap". Z is indexed [row, col]
/// (row = y, col = x), like plotly.js z: [[...], [...]].
/// </summary>
public class HeatmapTrace : Trace
{
    /// <summary>Cell values, indexed [row, col].</summary>
    public double[,]? Z { get; set; }

    /// <summary>X coordinates of columns (numbers, dates or categories). Defaults to 0..cols-1.</summary>
    public DataSeries? X { get; set; }

    /// <summary>Y coordinates of rows. Defaults to 0..rows-1.</summary>
    public DataSeries? Y { get; set; }

    public Colorscale Colorscale { get; set; } = Colorscale.Viridis;

    public bool ShowScale { get; set; } = true;

    /// <summary>Explicit z range; NaN = auto.</summary>
    public double ZMin { get; set; } = double.NaN;

    public double ZMax { get; set; } = double.NaN;

    internal HeatmapCalc? Calc { get; private set; }

    internal override (DataSeries? x, DataSeries? y) GetAxesData() => (X, Y);

    internal override void Prepare(PlotCalcContext ctx)
    {
        if (Z == null)
        {
            Calc = null;
            return;
        }

        var rows = Z.GetLength(0);
        var cols = Z.GetLength(1);
        var xs = new double[cols];
        var ys = new double[rows];
        for (var j = 0; j < cols; j++) xs[j] = X != null && j < X.Count ? ctx.XValue(X, j) : j;
        for (var i = 0; i < rows; i++) ys[i] = Y != null && i < Y.Count ? ctx.YValue(Y, i) : i;

        var zmin = double.PositiveInfinity;
        var zmax = double.NegativeInfinity;
        foreach (var z in Z)
        {
            if (double.IsNaN(z)) continue;
            zmin = Math.Min(zmin, z);
            zmax = Math.Max(zmax, z);
        }

        if (double.IsInfinity(zmin)) (zmin, zmax) = (0, 1);
        if (!double.IsNaN(ZMin)) zmin = ZMin;
        if (!double.IsNaN(ZMax)) zmax = ZMax;
        if (zmin == zmax) zmax = zmin + 1;

        var dx = CellDelta(xs);
        var dy = CellDelta(ys);
        Calc = new HeatmapCalc
        {
            Xs = xs,
            Ys = ys,
            ZMin = zmin,
            ZMax = zmax,
            Dx = dx,
            Dy = dy
        };
        ctx.SetCalc(this, Calc);

        ctx.ExtendXRange(xs.Min() - dx / 2, xs.Max() + dx / 2);
        ctx.ExtendYRange(ys.Min() - dy / 2, ys.Max() + dy / 2);
    }

    private static double CellDelta(double[] coords)
    {
        var min = double.PositiveInfinity;
        for (var i = 1; i < coords.Length; i++)
            min = Math.Min(min, Math.Abs(coords[i] - coords[i - 1]));
        return double.IsInfinity(min) ? 1 : min;
    }

    internal override void Render(DrawingContext dc, PlotRenderContext rc)
    {
        if (Z == null || Calc is not { } calc) return;
        var rect = rc.PlotRect;
        var w = Math.Max(1, (int)Math.Round(rect.Width));
        var h = Math.Max(1, (int)Math.Round(rect.Height));

        var key = (w, h, calc.ZMin, calc.ZMax, Colorscale.Name, Z.GetHashCode());
        if (calc.CachedKey == null || !calc.CachedKey.Value.Equals(key) || calc.Bitmap == null)
        {
            calc.Bitmap?.Dispose();
            calc.Bitmap = RenderBitmap(rc, calc, w, h);
            calc.CachedKey = key;
        }

        if (calc.Bitmap != null)
        {
            dc.DrawImage(calc.Bitmap, new Rect(0, 0, w, h), rect);
        }
    }

    private WriteableBitmap? RenderBitmap(PlotRenderContext rc, HeatmapCalc calc, int w, int h)
    {
        var z = Z!;
        var rows = z.GetLength(0);
        var cols = z.GetLength(1);
        var pixels = new byte[w * h * 4];

        for (var py = 0; py < h; py++)
        {
            var yRaw = rc.YAxis.RawValueAt((double)(h - 1 - py) / (h - 1 == 0 ? 1 : h - 1));
            var row = FindCell(calc.Ys, yRaw);
            for (var px = 0; px < w; px++)
            {
                var xRaw = rc.XAxis.RawValueAt(w == 1 ? 0 : (double)px / (w - 1));
                var col = FindCell(calc.Xs, xRaw);
                var off = (py * w + px) * 4;
                if (row < 0 || col < 0)
                {
                    var paper = rc.Layout.PlotBackground ?? rc.Theme.PlotBackground;
                    pixels[off] = paper.B;
                    pixels[off + 1] = paper.G;
                    pixels[off + 2] = paper.R;
                    pixels[off + 3] = 255;
                    continue;
                }

                var zv = z[row, col];
                var c = double.IsNaN(zv) ? Avalonia.Media.Color.FromArgb(0, 0, 0, 0) : Colorscale.GetColor((zv - calc.ZMin) / (calc.ZMax - calc.ZMin));
                pixels[off] = c.B;
                pixels[off + 1] = c.G;
                pixels[off + 2] = c.R;
                pixels[off + 3] = double.IsNaN(zv) ? (byte)0 : c.A;
            }
        }

        var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = bmp.Lock())
        {
            var stride = fb.RowBytes;
            for (var row = 0; row < h; row++)
                Marshal.Copy(pixels, row * w * 4, fb.Address + row * stride, w * 4);
        }

        return bmp;
    }

    private static int FindCell(double[] coords, double value)
    {
        if (coords.Length == 0 || double.IsNaN(value)) return -1;
        if (coords.Length == 1) return Math.Abs(value - coords[0]) <= 0.5 ? 0 : -1;
        var best = 0;
        var bestD = double.PositiveInfinity;
        for (var i = 0; i < coords.Length; i++)
        {
            var d = Math.Abs(coords[i] - value);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        // reject when outside the outermost cell edges
        var delta = coords.Length > 1 ? Math.Abs(coords[coords.Length > 1 ? 1 : 0] - coords[0]) : 1;
        return bestD <= delta / 2 + 1e-9 ? best : -1;
    }

    internal override IEnumerable<HoverTarget> HitTest(PlotRenderContext rc, Point pt, HoverMode mode)
    {
        if (Z == null || Calc is not { } calc) yield break;
        var xRaw = rc.XAxis.RawValueAt((pt.X - rc.PlotRect.X) / rc.PlotRect.Width);
        var yRaw = rc.YAxis.RawValueAt((rc.PlotRect.Bottom - pt.Y) / rc.PlotRect.Height);
        var col = FindCell(calc.Xs, xRaw);
        var row = FindCell(calc.Ys, yRaw);
        if (row < 0 || col < 0) yield break;

        var zv = Z[row, col];
        yield return new HoverTarget
        {
            ScreenPoint = pt,
            Trace = this,
            Color = Colorscale.GetColor((zv - calc.ZMin) / (calc.ZMax - calc.ZMin)),
            Title = Name,
            XText = $"x: {rc.XAxis.FormatHover(calc.Xs[col])}",
            YText = $"y: {rc.YAxis.FormatHover(calc.Ys[row])}",
            ExtraText = $"z: {PlotFmt.HoverValue(zv)}",
            Distance = 0,
            Tag = (row, col)
        };
    }

    internal sealed class HeatmapCalc
    {
        public double[] Xs { get; init; } = [];

        public double[] Ys { get; init; } = [];

        public double ZMin { get; init; }

        public double ZMax { get; init; }

        public double Dx { get; init; }

        public double Dy { get; init; }

        public WriteableBitmap? Bitmap { get; set; }

        public (int w, int h, double zmin, double zmax, string? cs, int zhash)? CachedKey { get; set; }
    }
}
