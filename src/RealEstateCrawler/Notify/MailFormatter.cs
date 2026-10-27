using System.Net;
using System.Text;

namespace RealEstateCrawler.Notify;

public sealed record MailContent(string Subject, string Text, string Html);

/// <summary>
/// 한 회차의 신규 매물을 메일 한 통으로 만든다.
/// 본문은 HTML 과, HTML 을 못 보는 클라이언트용 텍스트 두 벌이다.
/// </summary>
public static class MailFormatter
{
    public static MailContent Build(IReadOnlyList<Listing> listings, string label)
    {
        var subject = $"[{label}] 신규 월세 {listings.Count}건";

        var text = new StringBuilder(subject);
        foreach (var l in listings) text.Append("\n\n").Append(FormatBlock(l));

        var html = new StringBuilder("<div style=\"font-family:'Malgun Gothic',sans-serif;font-size:14px;line-height:1.6;color:#222\">");
        html.Append($"<p style=\"margin:0 0 16px;font-size:16px\"><b>{E(subject)}</b></p>");
        foreach (var l in listings)
        {
            html.Append("<div style=\"margin:0 0 16px\">")
                .Append($"<a href=\"{E(l.Url)}\" style=\"font-weight:bold;color:#1a5fb4\">[{E(l.Source)}] {E(l.RoomType)} 월세 {E(l.Price)}</a><br>")
                .Append($"{E(l.Location)} · {E(l.Floor)} · {E(l.Area)}");
            if (!string.IsNullOrWhiteSpace(l.Title)) html.Append($"<br><span style=\"color:#666\">{E(l.Title)}</span>");
            html.Append("</div>");
        }
        html.Append("</div>");

        return new MailContent(subject, text.ToString(), html.ToString());
    }

    public static string FormatBlock(Listing l) =>
        $"[{l.Source}] {l.RoomType} 월세 {l.Price}\n{l.Location} · {l.Floor} · {l.Area}\n{l.Url}";

    static string E(string value) => WebUtility.HtmlEncode(value);
}
