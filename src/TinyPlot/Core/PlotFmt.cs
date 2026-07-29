using System.Globalization;
using System.Text;

namespace TinyPlot;

/// <summary>Number / date formatting helpers used by axes and hover labels.</summary>
public static class PlotFmt
{
    private static readonly string[] Suffixes = ["", "k", "M", "G", "T", "P", "E"];

    /// <summary>Format a value for an axis tick. Large magnitudes get SI suffixes (1k, 2.5M) like plotly.js.</summary>
    public static string Number(double value, double step = 0)
    {
        if (double.IsNaN(value)) return "";
        if (value == 0) return "0";

        var abs = Math.Abs(value);
        if (abs >= 1e15) return value.ToString("0.###e+0", CultureInfo.InvariantCulture);

        if (abs >= 1000)
        {
            var mag = (int)Math.Floor(Math.Log10(abs) / 3);
            mag = Math.Clamp(mag, 0, Suffixes.Length - 1);
            var scaled = value / Math.Pow(1000, mag);
            return Trim(scaled, DecimalsFor(step / Math.Pow(1000, mag))) + Suffixes[mag];
        }

        return Trim(value, DecimalsFor(step));
    }

    /// <summary>Precision derived from the tick step so labels never show redundant zeros.</summary>
    public static int DecimalsFor(double step)
    {
        if (step <= 0 || double.IsNaN(step)) return 6;
        var d = (int)Math.Ceiling(-Math.Log10(step));
        return Math.Clamp(d, 0, 10);
    }

    public static string Trim(double value, int maxDecimals)
    {
        var s = value.ToString("F" + maxDecimals, CultureInfo.InvariantCulture);
        if (s.Contains('.'))
        {
            s = s.TrimEnd('0').TrimEnd('.');
        }

        return s == "-0" ? "0" : s;
    }

    /// <summary>Hover-friendly value formatting with ~6 significant digits, like plotly.js.</summary>
    public static string HoverValue(double value)
    {
        if (double.IsNaN(value)) return "";
        var abs = Math.Abs(value);
        if (abs > 0 && abs < 1e-4) return value.ToString("0.###e+0", CultureInfo.InvariantCulture);
        return value.ToString("G6", CultureInfo.InvariantCulture);
    }

    public static string ApplyAffixes(string text, string? prefix, string? suffix)
    {
        if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix)) return text;
        return $"{prefix}{text}{suffix}";
    }

    /// <summary>Format a DateTime tick according to the chosen step magnitude.</summary>
    public static string Date(DateTime value, DateTickUnit unit)
    {
        var c = CultureInfo.InvariantCulture;
        return unit switch
        {
            DateTickUnit.Year => value.ToString("yyyy", c),
            DateTickUnit.Month => value.ToString("MMM yyyy", c),
            DateTickUnit.Day => value.ToString("MMM d", c),
            DateTickUnit.Hour => value.ToString("HH:mm\nMMM d", c),
            DateTickUnit.Minute => value.ToString("HH:mm", c),
            _ => value.ToString("HH:mm:ss", c)
        };
    }

    /// <summary>"10^n" using unicode superscripts, for log axes.</summary>
    public static string PowerOfTen(int exponent)
    {
        if (exponent == 0) return "1";
        if (exponent == 1) return "10";
        var sb = new StringBuilder("10");
        foreach (var ch in exponent.ToString(CultureInfo.InvariantCulture))
        {
            sb.Append(ch switch
            {
                '-' => '⁻',
                '0' => '⁰',
                '1' => '¹',
                '2' => '²',
                '3' => '³',
                '4' => '⁴',
                '5' => '⁵',
                '6' => '⁶',
                '7' => '⁷',
                '8' => '⁸',
                '9' => '⁹',
                _ => ch
            });
        }

        return sb.ToString();
    }
}

public enum DateTickUnit
{
    Second,
    Minute,
    Hour,
    Day,
    Month,
    Year
}
