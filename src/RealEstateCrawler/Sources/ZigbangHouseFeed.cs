using System.Globalization;
using System.Text.Json;
using RealEstateCrawler.Geo;

namespace RealEstateCrawler.Sources;

public enum ZigbangHouseKind { Oneroom, Villa, Officetel }

/// <summary>
/// 직방 원룸·빌라·오피스텔 지도. geohash 셀마다 매물 번호와 좌표를 받고,
/// 반경 안의 번호만 상세 목록 API(한 번에 15건)로 조회한다.
/// </summary>
internal sealed class ZigbangHouseFeed(IHttpClientFactory httpFactory, ZigbangGeohashCipher cipher, ZigbangHouseKind kind)
    : IListingFeed
{
    public const string HttpClientName = "zigbang";
    const int GeohashPrecision = 5;
    const int DetailBatchSize = 15;

    public string Name => kind switch
    {
        ZigbangHouseKind.Oneroom => "직방 원룸",
        ZigbangHouseKind.Villa => "직방 빌라·투룸",
        _ => "직방 오피스텔",
    };

    string ApiPath => kind switch
    {
        ZigbangHouseKind.Oneroom => "onerooms",
        ZigbangHouseKind.Villa => "villas",
        _ => "officetels",
    };

    string WebPath => kind switch
    {
        ZigbangHouseKind.Oneroom => "oneroom",
        ZigbangHouseKind.Villa => "villa",
        _ => "officetel",
    };

    public async Task<IReadOnlyList<Listing>> FetchAsync(SearchArea area, CancellationToken ct)
    {
        var http = httpFactory.CreateClient(HttpClientName);

        var ids = new List<long>();
        var seenIds = new HashSet<long>();
        foreach (var cell in Geohash.Covering(area.Bounds, GeohashPrecision))
        {
            var url = $"house/property/v1/items/{ApiPath}?geohash={Uri.EscapeDataString(cipher.Encrypt(cell))}" +
                      $"&depositMin=0&rentMin=0&salesTypes={Uri.EscapeDataString("월세")}";
            using var doc = await HttpJson.GetAsync(http, url, ct);
            foreach (var item in doc.RootElement.Array("items"))
            {
                if (item.Num("id") is not { } id) continue;
                // 좌표가 있으면 상세 조회 전에 반경 밖을 미리 버린다
                if (item.Num("lat") is { } lat && item.Num("lng") is { } lng && !area.Contains(lat, lng)) continue;
                if (seenIds.Add((long)id)) ids.Add((long)id);
            }
            await Task.Delay(HttpJson.PoliteDelay, ct);
        }

        var listings = new List<Listing>();
        foreach (var batch in ids.Chunk(DetailBatchSize))
        {
            var body = $"{{\"itemIds\":[{string.Join(',', batch)}]}}";
            using var doc = await HttpJson.PostAsync(http, "house/property/v1/items/list", body, ct);
            foreach (var item in doc.RootElement.Array("items"))
            {
                if (Map(item, WebPath) is { } listing) listings.Add(listing);
            }
            await Task.Delay(HttpJson.PoliteDelay, ct);
        }
        return listings;
    }

    internal static Listing? Map(JsonElement item, string webPath)
    {
        if (item.Str("sales_type") != "월세") return null;
        if (RoomTypeLabel(item.Str("service_type"), item.Str("room_type")) is not { } roomType) return null;

        var id = item.Str("item_id");
        var size = item.TryGetProperty("전용면적", out var exclusive) && exclusive.ValueKind == JsonValueKind.Object
            ? exclusive.Num("m2")
            : item.Num("size_m2");
        var (lat, lng) = Coordinates(item, "location") ?? Coordinates(item, "random_location") ?? (0, 0);
        var floor = item.Str("floor_string") is { Length: > 0 } floorString ? floorString : item.Str("floor");

        return new Listing(
            Source: "직방",
            Id: id,
            RoomType: roomType,
            Price: $"{FormatManwon((long)(item.Num("deposit") ?? 0))}/{(long)(item.Num("rent") ?? 0)}",
            Rent: item.Num("rent") is { } rent ? (int)rent : null,
            Floor: FloorRule.Normalize(floor),
            Area: size is { } m2 ? string.Create(CultureInfo.InvariantCulture, $"{m2:0.##}㎡") : "",
            Location: item.Str("address"),
            Title: item.Str("title"),
            Lat: lat,
            Lng: lng,
            Url: $"https://www.zigbang.com/home/{webPath}/items/{id}");
    }

    static (double Lat, double Lng)? Coordinates(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var loc) || loc.ValueKind != JsonValueKind.Object) return null;
        return loc.Num("lat") is { } lat && loc.Num("lng") is { } lng ? (lat, lng) : null;
    }

    /// <summary>room_type 01~03 은 원룸(오픈형·분리형·복층), 04 는 투룸, 05 는 쓰리룸 이상이라 뺀다.</summary>
    internal static string? RoomTypeLabel(string serviceType, string roomType)
    {
        if (serviceType == "오피스텔") return "오피스텔";
        return roomType switch
        {
            "01" or "02" or "03" => "원룸",
            "04" => "투룸",
            _ => null,
        };
    }

    /// <summary>만원 단위 금액을 사이트 표기로: 19400 → 1억9400, 10000 → 1억.</summary>
    internal static string FormatManwon(long manwon)
    {
        if (manwon < 10000) return manwon.ToString(CultureInfo.InvariantCulture);
        var eok = manwon / 10000;
        var rest = manwon % 10000;
        return rest == 0 ? $"{eok}억" : $"{eok}억{rest}";
    }
}
