using RealEstateCrawler.Geo;

namespace RealEstateCrawler.Sources;

/// <summary>
/// 사이트의 매물 카테고리 하나 (예: 다방 오피스텔). 첫 수집 기준선을 카테고리마다 따로 잡기 때문에
/// 한 카테고리가 실패해도 다른 카테고리의 신규 판정에는 영향이 없다.
/// </summary>
public interface IListingFeed
{
    string Name { get; }

    /// <summary>영역 근처의 월세 매물. 거리·층 조건은 호출하는 쪽에서 한 번 더 거른다.</summary>
    Task<IReadOnlyList<Listing>> FetchAsync(SearchArea area, CancellationToken ct);
}
