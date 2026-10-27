using Microsoft.Extensions.Options;
using RealEstateCrawler.Notify;
using RealEstateCrawler.Sources;
using RealEstateCrawler.State;

namespace RealEstateCrawler;

/// <summary>
/// 한 회차: 모든 카테고리를 수집하고, 처음 보는 매물만 메일로 보낸다.
/// 카테고리의 첫 성공 수집은 기준선으로만 저장하고 메일을 보내지 않는다.
/// </summary>
public sealed class CrawlRunner(
    IEnumerable<IListingFeed> feeds,
    StateStore store,
    IMailSender mail,
    IOptions<CrawlerOptions> options,
    TimeProvider time,
    ILogger<CrawlRunner> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var opt = options.Value;
        var now = time.GetLocalNow();
        var state = store.Load();
        var fresh = new List<Listing>();

        foreach (var feed in feeds)
        {
            if (await TryFetchAsync(feed, opt, ct) is not { } listings) continue;

            if (!state.IsBaselined(feed.Name))
            {
                state.MarkSeen(listings, now);
                state.MarkBaselined(feed.Name);
                logger.LogInformation("{Feed}: 첫 수집 {Count}건을 기준선으로 저장 (메일 없음)", feed.Name, listings.Count);
                continue;
            }

            var unseen = state.FindUnseen(listings).Where(l => fresh.All(f => f.Key != l.Key)).ToList();
            var unseenKeys = unseen.Select(l => l.Key).ToHashSet();
            state.MarkSeen(listings.Where(l => !unseenKeys.Contains(l.Key)), now);
            fresh.AddRange(unseen);
            logger.LogInformation("{Feed}: {Count}건, 신규 {New}건", feed.Name, listings.Count, unseen.Count);
        }

        if (fresh.Count > 0)
        {
            foreach (var listing in fresh)
                logger.LogInformation("신규 매물: {Listing}", MailFormatter.FormatBlock(listing).ReplaceLineEndings(" | "));

            try
            {
                await mail.SendAsync(MailFormatter.Build(fresh, opt.AlertLabel), ct);
                // 발송에 성공한 뒤에만 본 것으로 기록한다. 실패하면 다음 회차에 다시 신규로 잡혀 재시도된다.
                state.MarkSeen(fresh, now);
                logger.LogInformation("신규 {Count}건 메일 처리", fresh.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "메일 발송 실패 — 다음 회차에 다시 보냄");
            }
        }

        state.Prune(now - TimeSpan.FromDays(opt.SeenRetentionDays));
        store.Save(state);
    }

    /// <summary>상태를 건드리지 않고 지금 조건에 맞는 매물을 모두 출력한다.</summary>
    public async Task DryRunAsync(TextWriter output, CancellationToken ct)
    {
        var opt = options.Value;
        output.WriteLine($"{opt.CenterName} ({opt.CenterLat}, {opt.CenterLng}) 반경 {opt.RadiusKm}km, {opt.MinFloor}층 이상 월세");
        var total = new HashSet<string>();
        foreach (var feed in feeds)
        {
            if (await TryFetchAsync(feed, opt, ct) is not { } listings) continue;
            output.WriteLine();
            output.WriteLine($"== {feed.Name}: {listings.Count}건");
            foreach (var listing in listings)
            {
                total.Add(listing.Key);
                output.WriteLine($"{MailFormatter.FormatBlock(listing).ReplaceLineEndings(" | ")} | {listing.Title}");
            }
        }
        output.WriteLine();
        output.WriteLine($"합계(중복 제외) {total.Count}건");
    }

    async Task<IReadOnlyList<Listing>?> TryFetchAsync(IListingFeed feed, CrawlerOptions opt, CancellationToken ct)
    {
        try
        {
            var area = opt.Area;
            var listings = await feed.FetchAsync(area, ct);
            return listings
                .Where(l => area.Contains(l.Lat, l.Lng) && FloorRule.IsAllowed(l.Floor, opt.MinFloor))
                .DistinctBy(l => l.Key)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // 실패한 카테고리는 이번 회차에서 빠질 뿐, 본 매물 기록은 그대로라 다음 회차에 메일 폭주가 없다
            logger.LogError(ex, "{Feed} 수집 실패 — 이번 회차는 건너뜀", feed.Name);
            return null;
        }
    }
}
