using System.Text;

namespace RealEstateCrawler.Notify;

public sealed record SmsMessage(string Text, string Type, string? Subject);

/// <summary>
/// 신규 매물을 문자 본문으로 만든다. 90바이트 이하는 SMS, 넘으면 LMS(2000바이트)로 나누고,
/// 문자 수 상한을 넘는 매물은 마지막 문자에 "외 N건"으로만 적는다.
/// </summary>
public static class MessageFormatter
{
    public const int SmsMaxBytes = 90;
    public const int LmsMaxBytes = 2000;
    const int HeaderFooterReserve = 80;

    public static IReadOnlyList<SmsMessage> Build(IReadOnlyList<Listing> listings, string label, int maxMessages)
    {
        if (listings.Count == 0) return [];

        var blocks = listings.Select(FormatBlock).ToList();
        var budget = LmsMaxBytes - HeaderFooterReserve;
        var pages = new List<List<string>>();
        var current = new List<string>();
        var used = 0;
        var consumed = 0;

        foreach (var block in blocks)
        {
            var size = ByteCount(block) + 2;
            if (current.Count > 0 && used + size > budget)
            {
                pages.Add(current);
                if (pages.Count == maxMessages) break;
                current = [];
                used = 0;
            }
            current.Add(block);
            used += size;
            consumed++;
        }
        if (pages.Count < maxMessages && current.Count > 0) pages.Add(current);

        var remaining = blocks.Count - consumed;
        var messages = new List<SmsMessage>(pages.Count);
        for (var i = 0; i < pages.Count; i++)
        {
            var subject = pages.Count > 1
                ? $"{label} 신규 월세 {listings.Count}건 ({i + 1}/{pages.Count})"
                : $"{label} 신규 월세 {listings.Count}건";
            var text = new StringBuilder($"[{subject}]");
            foreach (var block in pages[i]) text.Append("\n\n").Append(block);
            if (i == pages.Count - 1 && remaining > 0) text.Append($"\n\n외 {remaining}건은 로그 참고");

            var body = text.ToString();
            messages.Add(ByteCount(body) <= SmsMaxBytes
                ? new SmsMessage(body, "SMS", null)
                : new SmsMessage(body, "LMS", subject));
        }
        return messages;
    }

    public static string FormatBlock(Listing l) =>
        $"[{l.Source}] {l.RoomType} 월세 {l.Price}\n{l.Location} · {l.Floor} · {l.Area}\n{l.Url}";

    /// <summary>통신사 기준(EUC-KR) 길이: ASCII 1바이트, 그 밖은 2바이트.</summary>
    public static int ByteCount(string text) => text.Sum(c => c <= 0x7F ? 1 : 2);
}
