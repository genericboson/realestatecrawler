using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealEstateCrawler.Geo;
using SkiaSharp;

namespace RealEstateCrawler.Tests;

public sealed class StaticMapTests : IDisposable
{
    const double CenterLat = 37.540131, CenterLng = 127.05679;

    readonly string _dir = Path.Combine(Path.GetTempPath(), "rec-map-tests-" + Guid.NewGuid().ToString("N"));
    readonly TileServer _tiles = new();
    readonly OsmStaticMapRenderer _renderer;

    public StaticMapTests()
    {
        var options = Options.Create(new CrawlerOptions { CenterLat = CenterLat, CenterLng = CenterLng, DataDirectory = _dir });
        _renderer = new OsmStaticMapRenderer(_tiles, options, NullLogger<OsmStaticMapRenderer>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void ToPixel_MatchesKnownTileCorners()
    {
        Assert.Equal((128, 128), WebMercator.ToPixel(0, 0, 0));
        var (x, y) = WebMercator.ToPixel(85.05112878, 180, 1);
        Assert.Equal(512, x, 6);
        Assert.Equal(0, y, 3);
    }

    [Theory]
    [InlineData(37.5402, 127.0569, 16)] // 바로 옆
    [InlineData(37.5491, 127.0568, 15)] // 북쪽 1km
    [InlineData(37.5401, 127.0681, 15)] // 동쪽 1km
    public void Fit_KeepsBothPointsInsideTheFrame(double lat, double lng, int expectedZoom)
    {
        var view = OsmStaticMapRenderer.Fit(lat, lng, CenterLat, CenterLng);

        Assert.Equal(expectedZoom, view.Zoom);
        foreach (var p in new[] { view.Project(lat, lng), view.Project(CenterLat, CenterLng) })
        {
            Assert.InRange(p.X, 40, OsmStaticMapRenderer.Width - 40);
            Assert.InRange(p.Y, 40, OsmStaticMapRenderer.Height - 40);
        }
    }

    [Fact]
    public async Task Render_DrawsMarkersOnTilesAndCachesTiles()
    {
        const double lat = 37.5445, lng = 127.0510;

        var jpeg = await _renderer.RenderAsync(lat, lng, CancellationToken.None);

        using var bitmap = SKBitmap.Decode(jpeg);
        Assert.Equal(OsmStaticMapRenderer.Width, bitmap.Width);
        Assert.Equal(OsmStaticMapRenderer.Height, bitmap.Height);

        var view = OsmStaticMapRenderer.Fit(lat, lng, CenterLat, CenterLng);
        var listing = bitmap.GetPixel((int)view.Project(lat, lng).X, (int)view.Project(lat, lng).Y);
        var center = bitmap.GetPixel((int)view.Project(CenterLat, CenterLng).X, (int)view.Project(CenterLat, CenterLng).Y);
        var background = bitmap.GetPixel(5, 5);
        Assert.True(listing.Red > 180 && listing.Green < 90, $"매물 점이 빨갛지 않음: {listing}");
        Assert.True(center.Blue > 130 && center.Red < 90, $"기준 건물 점이 파랗지 않음: {center}");
        Assert.True(Math.Abs(background.Red - 200) < 20 && Math.Abs(background.Blue - 200) < 20, $"타일이 깔리지 않음: {background}");

        var requests = _tiles.Requests;
        Assert.InRange(requests, 1, 12); // 600x360 은 타일 최대 4x3 장
        await _renderer.RenderAsync(lat, lng, CancellationToken.None);
        Assert.Equal(requests, _tiles.Requests);
    }

    [Fact]
    public async Task Render_ReturnsNullWhenTilesAreUnavailable()
    {
        _tiles.Fail = true;

        Assert.Null(await _renderer.RenderAsync(37.5445, 127.0510, CancellationToken.None));
    }

    /// <summary>회색 타일을 돌려주는 가짜 타일 서버.</summary>
    sealed class TileServer : HttpMessageHandler, IHttpClientFactory
    {
        static readonly byte[] GrayTile = MakeTile();

        public int Requests { get; private set; }
        public bool Fail { get; set; }

        public HttpClient CreateClient(string name) =>
            new(this, disposeHandler: false) { BaseAddress = new Uri("https://tile.example/") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(Fail
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(GrayTile) });
        }

        static byte[] MakeTile()
        {
            using var bitmap = new SKBitmap(256, 256);
            bitmap.Erase(new SKColor(200, 200, 200));
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}
