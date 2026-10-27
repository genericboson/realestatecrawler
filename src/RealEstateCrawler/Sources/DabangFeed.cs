using System.Globalization;
using System.Text.Json;
using RealEstateCrawler.Geo;

namespace RealEstateCrawler.Sources;

public enum DabangCategory { OneTwoRoom, Officetel, Apartment }

/// <summary>
/// 다방 웹 지도(dabangapp.com/map)가 쓰는 v5 room-list API.
/// 필터 JSON 은 웹이 보내는 값을 그대로 쓰고 거래유형만 월세로 좁혔다.
/// </summary>
internal sealed class DabangFeed(IHttpClientFactory httpFactory, DabangCategory category) : IListingFeed
{
    public const string HttpClientName = "dabang";
    const int MaxPages = 30;

    public string Name => category switch
    {
        DabangCategory.OneTwoRoom => "다방 원룸·투룸",
        DabangCategory.Officetel => "다방 오피스텔",
        _ => "다방 아파트",
    };

    string CategoryPath => category switch
    {
        DabangCategory.OneTwoRoom => "one-two",
        DabangCategory.Officetel => "officetel",
        _ => "apt",
    };

    // 원룸·투룸은 서버에서 2층 이상(+옥탑)만 받는다. 오피스텔·아파트는 층 필터가 없어 받은 뒤 거른다.
    string Filters => category switch
    {
        DabangCategory.OneTwoRoom => """{"sellingTypeList":["MONTHLY_RENT"],"depositRange":{"min":0,"max":999999},"priceRange":{"min":0,"max":999999},"isIncludeMaintenance":false,"pyeongRange":{"min":0,"max":999999},"useApprovalDateRange":{"min":0,"max":999999},"roomFloorList":["GROUND_SECOND_OVER","ROOFTOP"],"roomTypeList":["ONE_ROOM","TWO_ROOM"],"dealTypeList":["AGENT","DIRECT"],"canParking":false,"isShortLease":false,"hasElevator":false,"hasPano":false,"isDivision":false,"isDuplex":false}""",
        DabangCategory.Officetel => """{"sellingTypeList":["MONTHLY_RENT"],"tradeRange":{"min":0,"max":999999},"depositRange":{"min":0,"max":999999},"priceRange":{"min":0,"max":999999},"isIncludeMaintenance":false,"pyeongRange":{"min":0,"max":999999},"useApprovalDateRange":{"min":0,"max":999999},"dealTypeList":["AGENT","DIRECT"],"parkingNumRange":{"min":0,"max":999999},"canParking":false,"isShortLease":false,"hasElevator":false,"hasPano":false,"roomCountList":["ONE_ROOM","TWO_ROOM","THREE_ROOM","FOUR_ROOM"]}""",
        _ => """{"sellingTypeList":["MONTHLY_RENT"],"tradeRange":{"min":0,"max":999999},"depositRange":{"min":0,"max":999999},"priceRange":{"min":0,"max":999999},"isIncludeMaintenance":false,"pyeongRange":{"min":0,"max":999999},"useApprovalDateRange":{"min":0,"max":999999},"dealTypeList":["AGENT","DIRECT"],"householdNumRange":{"min":0,"max":999999},"parkingNumRange":{"min":0,"max":999999},"isShortLease":false,"hasTakeTenant":false,"roomCountList":["ONE_ROOM","TWO_ROOM","THREE_ROOM","FOUR_ROOM"]}""",
    };

    public async Task<IReadOnlyList<Listing>> FetchAsync(SearchArea area, CancellationToken ct)
    {
        var http = httpFactory.CreateClient(HttpClientName);
        var b = area.Bounds;
        var bbox = string.Create(CultureInfo.InvariantCulture,
            $$$"""{"sw":{"lat":{{{b.South:F7}}},"lng":{{{b.West:F7}}}},"ne":{"lat":{{{b.North:F7}}},"lng":{{{b.East:F7}}}}}""");

        var listings = new Dictionary<string, Listing>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"api/v5/room-list/category/{CategoryPath}/bbox?filters={Uri.EscapeDataString(Filters)}" +
                      $"&bbox={Uri.EscapeDataString(bbox)}&zoom=15&useMap=naver&page={page}";
            using var doc = await HttpJson.GetAsync(http, url, ct);
            var result = doc.RootElement.GetProperty("result");

            var rooms = result.Array("roomList")
                .Concat(result.Array("premiumList").SelectMany(premium => premium.Array("roomList")));
            foreach (var room in rooms)
            {
                if (Map(room) is { } listing) listings.TryAdd(listing.Id, listing);
            }

            if (!result.TryGetProperty("hasMore", out var hasMore) || !hasMore.GetBoolean()) break;
            await Task.Delay(HttpJson.PoliteDelay, ct);
        }
        return [.. listings.Values];
    }

    internal static Listing? Map(JsonElement room)
    {
        if (room.Str("priceTypeName") != "월세") return null;

        var id = room.Str("id");
        if (id.Length == 0) return null;

        // roomDesc 예: "3층, 24.45m², 관리비 0.8만"
        var desc = room.Str("roomDesc").Split(',', StringSplitOptions.TrimEntries);
        var floor = desc.Length > 0 ? desc[0] : "";
        var areaText = desc.Length > 1 ? desc[1].Replace("m²", "㎡") : "";

        var location = room.TryGetProperty("randomLocation", out var loc) ? loc : default;
        var place = string.Join(' ', new[] { room.Str("dongName"), room.Str("complexName") }.Where(s => s.Length > 0));

        return new Listing(
            Source: "다방",
            Id: id,
            RoomType: room.Str("roomTypeName").Replace(" • ", "·"),
            Price: room.Str("priceTitle"),
            Rent: ParseRent(room.Str("priceTitle")),
            Floor: FloorRule.Normalize(floor),
            Area: areaText,
            Location: place,
            Title: room.Str("roomTitle"),
            Lat: location.ValueKind == JsonValueKind.Object ? location.Num("lat") ?? 0 : 0,
            Lng: location.ValueKind == JsonValueKind.Object ? location.Num("lng") ?? 0 : 0,
            Url: $"https://www.dabangapp.com/room/{id}");
    }

    /// <summary>priceTitle "보증금/월세" 에서 월세(만원)만 읽는다. 예: "1억/50" → 50.</summary>
    internal static int? ParseRent(string priceTitle)
    {
        var slash = priceTitle.LastIndexOf('/');
        return slash >= 0 && int.TryParse(priceTitle.AsSpan(slash + 1).Trim(), out var rent) ? rent : null;
    }
}
