namespace TinyPlot.Geo;

/// <summary>
/// 地图投影工具。提供经纬度到像素坐标的转换。
/// 默认使用墨卡托投影（Mercator），与 ECharts 地图一致。
/// </summary>
public static class MapProjection
{
    /// <summary>将经度/纬度转换为墨卡托投影坐标（0..1 范围）。</summary>
    public static (double x, double y) Mercator(double lon, double lat)
    {
        var x = (lon + 180.0) / 360.0;
        lat = Math.Clamp(lat, -85.051129, 85.051129);
        var latRad = lat * Math.PI / 180.0;
        var y = 0.5 - Math.Log(Math.Tan(Math.PI / 4 + latRad / 2)) / (2 * Math.PI);
        return (x, y);
    }

    /// <summary>将墨卡托投影坐标（0..1）反算为经纬度。</summary>
    public static (double lon, double lat) InverseMercator(double x, double y)
    {
        var lon = x * 360.0 - 180.0;
        var latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y)));
        var lat = latRad * 180.0 / Math.PI;
        return (lon, lat);
    }

    /// <summary>将经纬度点数组转换为绘图区像素坐标。</summary>
    public static Avalonia.Point[] ToPixels(
        (double lon, double lat)[] coords,
        Avalonia.Rect plotRect)
    {
        var result = new Avalonia.Point[coords.Length];
        for (var i = 0; i < coords.Length; i++)
        {
            var (px, py) = Mercator(coords[i].lon, coords[i].lat);
            result[i] = new Avalonia.Point(
                plotRect.X + px * plotRect.Width,
                plotRect.Y + py * plotRect.Height);
        }
        return result;
    }
}
