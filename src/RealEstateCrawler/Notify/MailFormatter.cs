using System.Net;
using System.Text;

namespace RealEstateCrawler.Notify;

/// <summary>본문 HTML 이 cid:{ContentId} 로 가리키는 첨부 이미지 (JPEG).</summary>
public sealed record MailImage(string ContentId, byte[] Jpeg);

public sealed record MailContent(string Subject, string Text, string Html, IReadOnlyList<MailImage> Images);

/// <summary>
/// 한 회차의 신규 매물을 메일 한 통으로 만든다.
/// 본문은 HTML 과, HTML 을 못 보는 클라이언트용 텍스트 두 벌이다. 지도는 HTML 에만 들어간다.
/// </summary>
public static class MailFormatter
{
    /// <param name="maps">매물 Key → 위치 지도 JPEG. 지도가 없는 매물은 글만 나간다.</param>
    public static MailContent Build(
        IReadOnlyList<Listing> listings, string label, string centerName, IReadOnlyDictionary<string, byte[]> maps)
    {
        var subject = $"[{label}] 신규 월세 {listings.Count}건";

        var text = new StringBuilder(subject);
        foreach (var l in listings) text.Append("\n\n").Append(Name(l)).Append('\n').Append(FormatBlock(l));

        var images = new List<MailImage>();
        var html = new StringBuilder("<div style=\"font-family:'Malgun Gothic',sans-serif;font-size:14px;line-height:1.6;color:#222\">");
        html.Append($"<p style=\"margin:0 0 4px;font-size:16px\"><b>{E(subject)}</b></p>");
        if (maps.Count > 0)
            html.Append("<p style=\"margin:0 0 16px;font-size:12px;color:#666\">지도: <span style=\"color:#e01b24\">●</span> 매물 대략 위치, ")
                .Append($"<span style=\"color:#1a5fb4\">●</span> {E(centerName)}. 지도 데이터 © OpenStreetMap 기여자</p>");
        foreach (var l in listings)
        {
            // 매물 이름이 곧 다방/직방 매물 페이지 링크다
            html.Append("<div style=\"margin:0 0 24px\">")
                .Append($"<a href=\"{E(l.Url)}\" style=\"font-size:15px;font-weight:bold;color:#1a5fb4\">{E(Name(l))}</a><br>")
                .Append($"[{E(l.Source)}] {E(l.RoomType)} · 월세 {E(l.Price)} · {E(l.Floor)} · {E(l.Area)} · {E(l.Location)}");
            if (maps.TryGetValue(l.Key, out var map))
            {
                var image = new MailImage($"map{images.Count + 1}@realestatecrawler", map);
                images.Add(image);
                html.Append($"<br><a href=\"{E(l.Url)}\"><img src=\"cid:{image.ContentId}\" width=\"600\" alt=\"매물 위치 지도\" ")
                    .Append("style=\"max-width:100%;height:auto;margin-top:6px;border:1px solid #ddd\"></a>");
            }
            html.Append("</div>");
        }
        html.Append("</div>");

        return new MailContent(subject, text.ToString(), html.ToString(), images);
    }

    /// <summary>사이트에 올라온 매물 제목. 비어 있으면 위치와 유형으로 대신한다.</summary>
    static string Name(Listing l) => string.IsNullOrWhiteSpace(l.Title) ? $"{l.Location} {l.RoomType}".Trim() : l.Title.Trim();

    public static string FormatBlock(Listing l) =>
        $"[{l.Source}] {l.RoomType} 월세 {l.Price}\n{l.Location} · {l.Floor} · {l.Area}\n{l.Url}";

    static string E(string value) => WebUtility.HtmlEncode(value);
}
