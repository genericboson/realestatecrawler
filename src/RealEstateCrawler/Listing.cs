namespace RealEstateCrawler;

/// <summary>
/// 매물 한 건. <see cref="Key"/> 로 크롤링 회차 사이에서 같은 매물을 알아본다.
/// </summary>
/// <param name="Source">사이트 이름 (다방/직방)</param>
/// <param name="Id">사이트 안에서 유일한 매물 번호</param>
/// <param name="RoomType">원룸/투룸/오피스텔/아파트</param>
/// <param name="Price">보증금/월세 (만원), 예: 1000/65, 1억/50</param>
/// <param name="Floor">층 표기, 예: 3층, 저층, 옥탑</param>
/// <param name="Area">전용면적, 예: 24.5㎡</param>
/// <param name="Location">동 이름과 단지명</param>
/// <param name="Rent">월세 (만원). 가격 범위로 묶인 매물은 최저값, 읽지 못하면 null</param>
public sealed record Listing(
    string Source,
    string Id,
    string RoomType,
    string Price,
    string Floor,
    string Area,
    string Location,
    string Title,
    double Lat,
    double Lng,
    string Url,
    int? Rent = null)
{
    public string Key => $"{Source}:{Id}";
}
