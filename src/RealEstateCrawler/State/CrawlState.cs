using System.Text.Encodings.Web;
using System.Text.Json;

namespace RealEstateCrawler.State;

/// <summary>
/// 크롤링 회차 사이에 기억하는 것: 기준선을 잡은 카테고리, 지금까지 본 매물과 마지막으로 본 시각.
/// </summary>
public sealed class CrawlState
{
    public HashSet<string> BaselinedFeeds { get; set; } = [];
    public Dictionary<string, DateTimeOffset> Seen { get; set; } = [];

    public bool IsBaselined(string feed) => BaselinedFeeds.Contains(feed);

    public void MarkBaselined(string feed) => BaselinedFeeds.Add(feed);

    public IEnumerable<Listing> FindUnseen(IEnumerable<Listing> listings) =>
        listings.Where(l => !Seen.ContainsKey(l.Key));

    public void MarkSeen(IEnumerable<Listing> listings, DateTimeOffset now)
    {
        foreach (var listing in listings) Seen[listing.Key] = now;
    }

    /// <summary>오래 안 보인 매물을 지운다. 내려갔다 다시 올라온 매물은 그때 신규로 잡힌다.</summary>
    public void Prune(DateTimeOffset cutoff)
    {
        foreach (var key in Seen.Where(p => p.Value < cutoff).Select(p => p.Key).ToList()) Seen.Remove(key);
    }
}

public sealed class StateStore(string path, ILogger<StateStore> logger)
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Path => path;

    public CrawlState Load()
    {
        if (!File.Exists(path)) return new CrawlState();
        try
        {
            return JsonSerializer.Deserialize<CrawlState>(File.ReadAllText(path), JsonOptions) ?? new CrawlState();
        }
        catch (JsonException ex)
        {
            // 깨진 상태로 계속 돌면 매 회차가 실패한다. 옆으로 치우고 기준선부터 다시 잡는다(메일 폭주 없음).
            var backup = $"{path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(path, backup);
            logger.LogError(ex, "상태 파일이 깨져 {Backup} 로 옮기고 새로 시작", backup);
            return new CrawlState();
        }
    }

    public void Save(CrawlState state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }
}
