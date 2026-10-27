using System.Text.RegularExpressions;

namespace RealEstateCrawler;

internal static partial class FloorRule
{
    /// <summary>
    /// 층 조건 판정. 숫자 층은 그대로 비교하고, 지하/반지하는 뺀다.
    /// 옥탑은 지상 위층이라 통과시키고, 사이트가 정확한 층을 숨긴 저층/중층/고층과
    /// 층 정보가 없는 매물은 놓치지 않도록 통과시킨다 (문자에 층 표기가 그대로 나간다).
    /// </summary>
    public static bool IsAllowed(string floor, int minFloor)
    {
        var f = floor.Trim();
        if (f.Length == 0) return true;
        if (f.Contains("지하") || f.StartsWith('B') || f.StartsWith('b') || f.StartsWith('-')) return false;
        if (f.Contains("옥탑")) return true;

        var number = LeadingNumber().Match(f);
        return !number.Success || int.Parse(number.Value) >= minFloor;
    }

    /// <summary>"7" → "7층", "저" → "저층" 처럼 사이트마다 다른 표기를 맞춘다.</summary>
    public static string Normalize(string floor)
    {
        var f = floor.Trim();
        if (f.Length == 0) return "층수 미상";
        if (LeadingNumber().Match(f) is { Success: true } m && m.Length == f.Length) return f + "층";
        return f is "저" or "중" or "고" ? f + "층" : f;
    }

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingNumber();
}
