using RealEstateCrawler.Geo;

namespace RealEstateCrawler.Tests;

public class GeoTests
{
    static readonly SearchArea Area = new(37.540131, 127.05679, 1.0);

    [Theory]
    [InlineData(37.540131, 127.05679, 5, "wydme")]
    [InlineData(37.540131, 127.05679, 9, "wydme1zzj")]
    [InlineData(37.5446, 127.0559, 7, "wydme4z")]
    public void Encode_MatchesReferenceImplementation(double lat, double lng, int precision, string expected) =>
        Assert.Equal(expected, Geohash.Encode(lat, lng, precision));

    [Fact]
    public void Contains_UsesGreatCircleDistance()
    {
        Assert.True(Area.Contains(37.540131, 127.05679));
        // 성수역 (약 0.5km)
        Assert.True(Area.Contains(37.544569, 127.055974));
        // 건대입구역 (약 1.2km)
        Assert.False(Area.Contains(37.5398956, 127.0706737));
    }

    [Fact]
    public void Bounds_EncloseTheWholeCircle()
    {
        var b = Area.Bounds;
        for (var bearing = 0; bearing < 360; bearing += 15)
        {
            var rad = bearing * Math.PI / 180;
            var lat = Area.CenterLat + Math.Cos(rad) * 1.0 / 110.574;
            var lng = Area.CenterLng + Math.Sin(rad) * 1.0 / (111.320 * Math.Cos(Area.CenterLat * Math.PI / 180));
            Assert.InRange(lat, b.South, b.North);
            Assert.InRange(lng, b.West, b.East);
        }
    }

    [Fact]
    public void Covering_IncludesEveryPointOfTheBounds()
    {
        var b = Area.Bounds;
        var cells = Geohash.Covering(b, 5).ToHashSet();
        for (var i = 0; i <= 40; i++)
        for (var j = 0; j <= 40; j++)
        {
            var lat = b.South + (b.North - b.South) * i / 40;
            var lng = b.West + (b.East - b.West) * j / 40;
            Assert.Contains(Geohash.Encode(lat, lng, 5), cells);
        }
    }
}
