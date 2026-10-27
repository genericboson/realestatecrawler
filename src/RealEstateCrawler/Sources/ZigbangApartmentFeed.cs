using System.Globalization;
using System.Text.Json;
using RealEstateCrawler.Geo;

namespace RealEstateCrawler.Sources;

/// <summary>
/// 직방 아파트는 단지 단위로 매물을 묶는다. 반경 안 단지를 찾은 뒤 단지마다 월세(rental) 매물을 읽는다.
/// </summary>
internal sealed class ZigbangApartmentFeed(IHttpClientFactory httpFactory) : IListingFeed
{
    const int MaxDanjis = 300;
    const int PageSize = 50;

    public string Name => "직방 아파트";

    public async Task<IReadOnlyList<Listing>> FetchAsync(SearchArea area, CancellationToken ct)
    {
        var http = httpFactory.CreateClient(ZigbangHouseFeed.HttpClientName);

        var danjiUrl = string.Create(CultureInfo.InvariantCulture,
            $"apt/nearby-danjis?lat={area.CenterLat}&lng={area.CenterLng}&radius={area.RadiusKm}&limit={MaxDanjis}");
        List<(long Id, double Lat, double Lng)> danjis;
        using (var doc = await HttpJson.GetAsync(http, danjiUrl, ct))
        {
            danjis = doc.RootElement.Array("list")
                .Select(d => (Id: (long)(d.Num("id") ?? 0), Lat: d.Num("lat") ?? 0, Lng: d.Num("lng") ?? 0))
                .Where(d => d.Id > 0 && area.Contains(d.Lat, d.Lng))
                .ToList();
        }

        var listings = new List<Listing>();
        foreach (var danji in danjis)
        {
            for (var offset = 0; ; offset += PageSize)
            {
                await Task.Delay(HttpJson.PoliteDelay, ct);
                var url = $"apt/danjis/{danji.Id}/item-catalogs?tranTypeIn%5B0%5D=rental&offset={offset}&limit={PageSize}";
                using var doc = await HttpJson.GetAsync(http, url, ct);
                var page = doc.RootElement.Array("list").ToList();
                listings.AddRange(page.Select(item => Map(item, danji.Lat, danji.Lng)).OfType<Listing>());

                var total = doc.RootElement.Num("totalCount") ?? 0;
                if (page.Count < PageSize || offset + PageSize >= total) break;
            }
        }
        return listings;
    }

    internal static Listing? Map(JsonElement item, double lat, double lng)
    {
        if (item.Str("tranType") != "rental") return null;

        var itemId = item.Array("itemIdList").Select(i => i.Str("itemId")).FirstOrDefault(i => i.Length > 0);
        var id = itemId ?? "ho" + item.Str("areaHoId");
        var danjiId = item.Str("areaDanjiId");
        var place = string.Join(' ', new[] { item.Str("local3"), item.Str("areaDanjiName"), item.Str("dong") }
            .Where(s => s.Length > 0));
        var deposit = ZigbangHouseFeed.FormatManwon((long)(item.Num("depositMin") ?? 0));
        var rent = (long)(item.Num("rentMin") ?? 0);

        return new Listing(
            Source: "직방",
            Id: "apt-" + id,
            RoomType: "아파트",
            Price: item.TryGetProperty("isPriceRange", out var range) && range.ValueKind == JsonValueKind.True
                ? $"{deposit}/{rent}~"
                : $"{deposit}/{rent}",
            Floor: FloorRule.Normalize(item.Str("floor")),
            Area: item.Num("sizeM2") is { } m2 ? string.Create(CultureInfo.InvariantCulture, $"{m2:0.##}㎡") : "",
            Location: place,
            Title: item.Str("itemTitle"),
            Lat: lat,
            Lng: lng,
            Url: $"https://www.zigbang.com/home/apt/danjis/{danjiId}");
    }
}
