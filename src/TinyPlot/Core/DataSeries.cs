using System.Collections;
using System.Globalization;

namespace TinyPlot;

public enum SeriesKind
{
    Empty,
    Number,
    Text,
    Date
}

/// <summary>
/// Flexible trace data container. Accepts numbers, strings (categories) or
/// <see cref="DateTime"/> values, like a plotly.js data array.
/// Implicit conversions exist from common array/list types.
/// </summary>
public sealed class DataSeries : IReadOnlyList<object?>
{
    private readonly IReadOnlyList<object?> _items;

    public DataSeries(IReadOnlyList<object?> items)
    {
        _items = items;
        Kind = DetectKind(items);
    }

    public SeriesKind Kind { get; }

    public int Count => _items.Count;

    public object? this[int index] => _items[index];

    private static SeriesKind DetectKind(IReadOnlyList<object?> items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case null:
                    continue;
                case DateTime:
                    return SeriesKind.Date;
                case string:
                    return SeriesKind.Text;
                case sbyte or byte or short or ushort or int or uint or long or ulong
                    or float or double or decimal:
                    return SeriesKind.Number;
                default:
                    return SeriesKind.Text;
            }
        }

        return SeriesKind.Empty;
    }

    /// <summary>Numeric value at index. Dates map to OLE Automation dates, text to its hash index (do not use for categories; use axis mapping instead).</summary>
    public double AsNumber(int index)
    {
        var v = _items[index];
        return v switch
        {
            null => double.NaN,
            DateTime dt => dt.ToOADate(),
            string s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : double.NaN,
            IConvertible c => c.ToDouble(CultureInfo.InvariantCulture),
            _ => double.NaN
        };
    }

    public string? AsText(int index)
    {
        var v = _items[index];
        return v switch
        {
            null => null,
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            string s => s,
            IConvertible c => c.ToString(CultureInfo.InvariantCulture),
            _ => v.ToString()
        };
    }

    public IEnumerator<object?> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- factories / implicit conversions --------------------------------

    public static DataSeries From<T>(IEnumerable<T> values) where T : notnull
        => new(values.Cast<object?>().ToArray());

    public static DataSeries From(params object?[] values) => new(values);

    public static implicit operator DataSeries(double[] v) => From(v);
    public static implicit operator DataSeries(double?[] v) => new(v.Select(x => (object?)x).ToArray());
    public static implicit operator DataSeries(float[] v) => From(v);
    public static implicit operator DataSeries(int[] v) => From(v);
    public static implicit operator DataSeries(long[] v) => From(v);
    public static implicit operator DataSeries(decimal[] v) => From(v);
    public static implicit operator DataSeries(string[] v) => new(v);
    public static implicit operator DataSeries(DateTime[] v) => From(v);
    public static implicit operator DataSeries(List<double> v) => From(v);
    public static implicit operator DataSeries(List<int> v) => From(v);
    public static implicit operator DataSeries(List<string> v) => new(v);
    public static implicit operator DataSeries(List<DateTime> v) => From(v);
}
