using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealEstateCrawler.Geo;
using RealEstateCrawler.Notify;
using RealEstateCrawler.Sources;
using RealEstateCrawler.State;

namespace RealEstateCrawler.Tests;

public sealed class CrawlRunnerTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "rec-tests-" + Guid.NewGuid().ToString("N"));
    readonly FakeFeed _dabang = new("다방 원룸·투룸");
    readonly FakeFeed _zigbang = new("직방 원룸");
    readonly RecordingMail _mail = new();
    readonly CrawlRunner _runner;
    readonly StateStore _store;

    public CrawlRunnerTests()
    {
        _store = new StateStore(Path.Combine(_dir, "state.json"), NullLogger<StateStore>.Instance);
        _runner = new CrawlRunner(
            [_dabang, _zigbang], _store, _mail, Options.Create(new CrawlerOptions()), TimeProvider.System,
            NullLogger<CrawlRunner>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    static Listing L(string source, string id, string floor = "3층", double lat = 37.5405, double lng = 127.0570) =>
        new(source, id, "원룸", "1000/65", floor, "20㎡", "성수동2가", "제목", lat, lng, $"https://example/{id}");

    [Fact]
    public async Task FirstRunIsBaselineOnly()
    {
        _dabang.Next = [L("다방", "a"), L("다방", "b")];
        _zigbang.Next = [L("직방", "1")];

        await _runner.RunAsync(CancellationToken.None);

        Assert.Empty(_mail.Sent);
        Assert.Equal(3, _store.Load().Seen.Count);
    }

    [Fact]
    public async Task SecondRunSendsOnlyNewListings()
    {
        _dabang.Next = [L("다방", "a")];
        _zigbang.Next = [L("직방", "1")];
        await _runner.RunAsync(CancellationToken.None);

        _dabang.Next = [L("다방", "a"), L("다방", "new")];
        _zigbang.Next = [L("직방", "1"), L("직방", "2")];
        await _runner.RunAsync(CancellationToken.None);

        var text = Assert.Single(_mail.Sent).Text;
        Assert.Contains("https://example/new", text);
        Assert.Contains("https://example/2", text);
        Assert.DoesNotContain("https://example/a", text);
        Assert.Contains("신규 월세 2건", text);

        // 같은 매물은 다시 보내지 않는다
        await _runner.RunAsync(CancellationToken.None);
        Assert.Single(_mail.Sent);
    }

    [Fact]
    public async Task FiltersFloorAndDistanceBeforeDiffing()
    {
        _dabang.Next = [];
        _zigbang.Next = [];
        await _runner.RunAsync(CancellationToken.None);

        _dabang.Next = [L("다방", "ground", floor: "1층"), L("다방", "basement", floor: "반지하"), L("다방", "far", lat: 37.56)];
        _zigbang.Next = [L("직방", "ok", floor: "2층")];
        await _runner.RunAsync(CancellationToken.None);

        var text = Assert.Single(_mail.Sent).Text;
        Assert.Contains("https://example/ok", text);
        Assert.DoesNotContain("ground", text);
        Assert.DoesNotContain("basement", text);
        Assert.DoesNotContain("far", text);
    }

    [Fact]
    public async Task FailedFeedDoesNotTriggerFloodWhenItRecovers()
    {
        _dabang.Next = [L("다방", "a"), L("다방", "b")];
        _zigbang.Next = [L("직방", "1")];
        await _runner.RunAsync(CancellationToken.None);

        _dabang.Fail = true;
        await _runner.RunAsync(CancellationToken.None);

        _dabang.Fail = false;
        await _runner.RunAsync(CancellationToken.None);

        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task FeedFailingOnFirstRunGetsItsOwnBaselineLater()
    {
        _dabang.Fail = true;
        _zigbang.Next = [L("직방", "1")];
        await _runner.RunAsync(CancellationToken.None);

        _dabang.Fail = false;
        _dabang.Next = [L("다방", "a"), L("다방", "b")];
        await _runner.RunAsync(CancellationToken.None);

        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task FailedSendIsRetriedNextRun()
    {
        _dabang.Next = [];
        _zigbang.Next = [];
        await _runner.RunAsync(CancellationToken.None);

        _dabang.Next = [L("다방", "new")];
        _mail.Fail = true;
        await _runner.RunAsync(CancellationToken.None);
        Assert.Empty(_mail.Sent);

        _mail.Fail = false;
        await _runner.RunAsync(CancellationToken.None);
        Assert.Contains("https://example/new", Assert.Single(_mail.Sent).Text);
    }

    sealed class FakeFeed(string name) : IListingFeed
    {
        public string Name => name;
        public IReadOnlyList<Listing> Next { get; set; } = [];
        public bool Fail { get; set; }

        public Task<IReadOnlyList<Listing>> FetchAsync(SearchArea area, CancellationToken ct) =>
            Fail ? throw new HttpRequestException("down") : Task.FromResult(Next);
    }

    sealed class RecordingMail : IMailSender
    {
        public List<MailContent> Sent { get; } = [];
        public bool Fail { get; set; }
        public bool IsEnabled => true;

        public Task SendAsync(MailContent mail, CancellationToken ct)
        {
            if (Fail) throw new IOException("smtp down");
            Sent.Add(mail);
            return Task.CompletedTask;
        }
    }
}
