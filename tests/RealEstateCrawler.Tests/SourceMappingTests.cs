using System.Text.Json;
using RealEstateCrawler.Sources;

namespace RealEstateCrawler.Tests;

public class SourceMappingTests
{
    // 직방 웹이 보낸 실제 요청에서 잡은 값 (geohash → 암호문)
    [Theory]
    [InlineData("wydme", "haOH1X5eg6XUs39vXF6KEw+rcW0XbGs3Qsp7BeEY0NU=")]
    [InlineData("wydmd", "P5pYYguGZGDhjUOXaqPlam4eBg2Gtk6ceme50on1gmo=")]
    public void ZigbangCipher_MatchesWebClient(string geohash, string expected) =>
        Assert.Equal(expected, new ZigbangGeohashCipher(new CrawlerOptions().ZigbangGeohashKey).Encrypt(geohash));

    [Fact]
    public void Dabang_MapsMonthlyRent()
    {
        using var doc = JsonDocument.Parse("""
            {"id":"6ad76024bbe1fa51cd56e0a1","roomTypeName":"투룸 • 단기","randomLocation":{"lat":37.5413,"lng":127.0571},
             "complexName":"테라289","roomTitle":"신축급","roomDesc":"3층, 15.03m², 관리비 10만",
             "priceTypeName":"월세","priceTitle":"1000/125","dongName":"성수동2가"}
            """);
        var listing = DabangFeed.Map(doc.RootElement)!;

        Assert.Equal("다방:6ad76024bbe1fa51cd56e0a1", listing.Key);
        Assert.Equal("투룸·단기", listing.RoomType);
        Assert.Equal("1000/125", listing.Price);
        Assert.Equal("3층", listing.Floor);
        Assert.Equal("15.03㎡", listing.Area);
        Assert.Equal("성수동2가 테라289", listing.Location);
        Assert.Equal(37.5413, listing.Lat);
        Assert.Equal("https://www.dabangapp.com/room/6ad76024bbe1fa51cd56e0a1", listing.Url);
    }

    [Fact]
    public void Dabang_SkipsLease()
    {
        using var doc = JsonDocument.Parse("""{"id":"x","priceTypeName":"전세","priceTitle":"1억","roomDesc":"2층"}""");
        Assert.Null(DabangFeed.Map(doc.RootElement));
    }

    [Theory]
    [InlineData("원룸", "01", "원룸")]
    [InlineData("원룸", "03", "원룸")]
    [InlineData("빌라", "04", "투룸")]
    [InlineData("빌라", "05", null)]
    [InlineData("오피스텔", "05", "오피스텔")]
    public void Zigbang_RoomTypeLabel(string serviceType, string roomType, string? expected) =>
        Assert.Equal(expected, ZigbangHouseFeed.RoomTypeLabel(serviceType, roomType));

    [Fact]
    public void Zigbang_MapsHouseItem()
    {
        using var doc = JsonDocument.Parse("""
            {"item_id":50012620,"sales_type":"월세","deposit":19400,"rent":183,"size_m2":39.67,
             "전용면적":{"m2":24.48,"p":"7.4"},"floor":"고","floor_string":"고","title":"신축",
             "room_type":"04","service_type":"원룸","location":{"lat":37.5469,"lng":127.0673},"address":"광진구 화양동"}
            """);
        var listing = ZigbangHouseFeed.Map(doc.RootElement, "oneroom")!;

        Assert.Equal("직방:50012620", listing.Key);
        Assert.Equal("투룸", listing.RoomType);
        Assert.Equal("1억9400/183", listing.Price);
        Assert.Equal("고층", listing.Floor);
        Assert.Equal("24.48㎡", listing.Area);
        Assert.Equal("https://www.zigbang.com/home/oneroom/items/50012620", listing.Url);
    }

    [Fact]
    public void Zigbang_MapsApartmentCatalog()
    {
        using var doc = JsonDocument.Parse("""
            {"areaHoId":5480783,"tranType":"rental","areaDanjiId":36524,"areaDanjiName":"트리마제","local3":"성수동1가",
             "isPriceRange":false,"depositMin":5000,"rentMin":350,"sizeM2":25.32,"dong":"103동","floor":"저",
             "itemTitle":"월세","itemIdList":[{"itemSource":"zigbang","itemId":50014707}]}
            """);
        var listing = ZigbangApartmentFeed.Map(doc.RootElement, 37.54, 127.04)!;

        Assert.Equal("직방:apt-50014707", listing.Key);
        Assert.Equal("아파트", listing.RoomType);
        Assert.Equal("5000/350", listing.Price);
        Assert.Equal("저층", listing.Floor);
        Assert.Equal("성수동1가 트리마제 103동", listing.Location);
        Assert.Equal("https://www.zigbang.com/home/apt/danjis/36524", listing.Url);
    }

    [Theory]
    [InlineData(500, "500")]
    [InlineData(10000, "1억")]
    [InlineData(19400, "1억9400")]
    [InlineData(240000, "24억")]
    public void FormatManwon_UsesEokUnit(long manwon, string expected) =>
        Assert.Equal(expected, ZigbangHouseFeed.FormatManwon(manwon));
}
