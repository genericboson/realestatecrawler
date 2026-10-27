using Microsoft.Extensions.Options;
using SkiaSharp;

namespace RealEstateCrawler.Geo;

public interface IMapRenderer
{
    /// <summary>매물 위치(빨강)와 기준 건물(파랑)을 함께 담은 JPEG 지도. 그리지 못하면 null.</summary>
    Task<byte[]?> RenderAsync(double lat, double lng, CancellationToken ct);
}

/// <summary>웹 메르카토르(지도 타일 좌표계) 변환.</summary>
internal static class WebMercator
{
    public const int TileSize = 256;

    /// <summary>위경도를 해당 줌의 전역 픽셀 좌표로.</summary>
    public static (double X, double Y) ToPixel(double lat, double lng, int zoom)
    {
        var scale = TileSize * Math.Pow(2, zoom);
        var sin = Math.Sin(lat * Math.PI / 180);
        return ((lng + 180) / 360 * scale, (0.5 - Math.Log((1 + sin) / (1 - sin)) / (4 * Math.PI)) * scale);
    }
}

/// <summary>지도 한 장이 보여 주는 영역: 줌과 왼쪽 위 모서리의 전역 픽셀 좌표.</summary>
internal readonly record struct MapView(int Zoom, double Left, double Top)
{
    public SKPoint Project(double lat, double lng)
    {
        var (x, y) = WebMercator.ToPixel(lat, lng, Zoom);
        return new SKPoint((float)(x - Left), (float)(y - Top));
    }
}

/// <summary>
/// OpenStreetMap 타일을 이어 붙여 매물 위치 지도를 그린다.
/// 타일 이용 정책(https://operations.osmfoundation.org/policies/tiles/)에 따라 앱을 밝히는 User-Agent 를 보내고,
/// 받은 타일은 30일 동안 디스크에 캐시하며, 지도 오른쪽 아래에 출처를 적는다.
/// </summary>
internal sealed class OsmStaticMapRenderer(
    IHttpClientFactory httpFactory,
    IOptions<CrawlerOptions> options,
    ILogger<OsmStaticMapRenderer> logger) : IMapRenderer
{
    public const string HttpClientName = "osm-tiles";
    public const int Width = 600;
    public const int Height = 360;
    const int Padding = 48;
    const int MinZoom = 13;
    const int MaxZoom = 16;
    static readonly TimeSpan TileCacheAge = TimeSpan.FromDays(30);

    readonly string _cacheDirectory = Path.Combine(options.Value.ResolveDataDirectory(), "tiles");

    public async Task<byte[]?> RenderAsync(double lat, double lng, CancellationToken ct)
    {
        var o = options.Value;
        try
        {
            return await DrawAsync(lat, lng, o.CenterLat, o.CenterLng, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // 지도는 덧붙이는 정보라 실패해도 알림 자체는 지도 없이 나간다
            logger.LogWarning(ex, "지도를 그리지 못함 ({Lat}, {Lng}) — 지도 없이 보냄", lat, lng);
            return null;
        }
    }

    /// <summary>매물과 기준 건물이 여백 안에 함께 들어가는 가장 큰 줌을 고르고, 두 점의 가운데를 지도 중심에 둔다.</summary>
    internal static MapView Fit(double lat, double lng, double refLat, double refLng)
    {
        for (var zoom = MaxZoom; ; zoom--)
        {
            var a = WebMercator.ToPixel(lat, lng, zoom);
            var b = WebMercator.ToPixel(refLat, refLng, zoom);
            var fits = Math.Abs(a.X - b.X) <= Width - 2 * Padding && Math.Abs(a.Y - b.Y) <= Height - 2 * Padding;
            if (fits || zoom == MinZoom)
                return new MapView(zoom, (a.X + b.X) / 2 - Width / 2.0, (a.Y + b.Y) / 2 - Height / 2.0);
        }
    }

    async Task<byte[]> DrawAsync(double lat, double lng, double refLat, double refLng, CancellationToken ct)
    {
        var view = Fit(lat, lng, refLat, refLng);
        var tileCount = 1 << view.Zoom;

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var firstX = (int)Math.Floor(view.Left / WebMercator.TileSize);
        var firstY = (int)Math.Floor(view.Top / WebMercator.TileSize);
        for (var ty = firstY; ty * WebMercator.TileSize < view.Top + Height; ty++)
        {
            for (var tx = firstX; tx * WebMercator.TileSize < view.Left + Width; tx++)
            {
                if (ty < 0 || ty >= tileCount || tx < 0 || tx >= tileCount) continue;
                var bytes = await GetTileAsync(view.Zoom, tx, ty, ct);
                using var tile = SKImage.FromEncodedData(bytes) ?? throw new InvalidDataException($"타일 {view.Zoom}/{tx}/{ty} 를 읽지 못함");
                canvas.DrawImage(tile, (float)(tx * WebMercator.TileSize - view.Left), (float)(ty * WebMercator.TileSize - view.Top),
                    SKSamplingOptions.Default);
            }
        }

        DrawDot(canvas, view.Project(refLat, refLng), new SKColor(0x1A, 0x5F, 0xB4), radius: 7);
        DrawDot(canvas, view.Project(lat, lng), new SKColor(0xE0, 0x1B, 0x24), radius: 9);
        DrawAttribution(canvas);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 82);
        return data.ToArray();
    }

    static void DrawDot(SKCanvas canvas, SKPoint at, SKColor color, float radius)
    {
        using var halo = new SKPaint { Color = color.WithAlpha(70), IsAntialias = true };
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        using var ring = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
        canvas.DrawCircle(at, radius * 2.2f, halo);
        canvas.DrawCircle(at, radius, fill);
        canvas.DrawCircle(at, radius, ring);
    }

    static void DrawAttribution(SKCanvas canvas)
    {
        const string Text = "© OpenStreetMap contributors";
        using var font = new SKFont(SKTypeface.Default, 12);
        using var textPaint = new SKPaint { Color = new SKColor(0x33, 0x33, 0x33), IsAntialias = true };
        using var boxPaint = new SKPaint { Color = SKColors.White.WithAlpha(210) };
        var textWidth = font.MeasureText(Text);
        canvas.DrawRect(Width - textWidth - 12, Height - 20, textWidth + 12, 20, boxPaint);
        canvas.DrawText(Text, Width - 6, Height - 6, SKTextAlign.Right, font, textPaint);
    }

    async Task<byte[]> GetTileAsync(int zoom, int x, int y, CancellationToken ct)
    {
        var file = new FileInfo(Path.Combine(_cacheDirectory, zoom.ToString(), x.ToString(), $"{y}.png"));
        if (file.Exists && DateTime.UtcNow - file.LastWriteTimeUtc < TileCacheAge)
            return await File.ReadAllBytesAsync(file.FullName, ct);

        byte[] bytes;
        try
        {
            bytes = await httpFactory.CreateClient(HttpClientName).GetByteArrayAsync($"{zoom}/{x}/{y}.png", ct);
        }
        catch (Exception ex) when (file.Exists && ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // 타일 서버가 안 될 때는 오래된 캐시라도 쓴다
            return await File.ReadAllBytesAsync(file.FullName, ct);
        }

        Directory.CreateDirectory(file.DirectoryName!);
        await File.WriteAllBytesAsync(file.FullName, bytes, ct);
        return bytes;
    }
}
