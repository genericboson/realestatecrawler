using System.Text;
using RealEstateCrawler;
using RealEstateCrawler.Geo;
using RealEstateCrawler.Notify;
using RealEstateCrawler.Sources;
using RealEstateCrawler.State;
using Serilog;
using Serilog.Events;

// 실행 방법
//   (인자 없음)   서비스/콘솔로 계속 돌며 주기마다 크롤링
//   --dry-run     한 번 수집해서 조건에 맞는 매물을 출력만 한다 (상태 저장·메일 없음)
//   --test-mail   설정된 주소로 시험 메일 한 통을 보낸다
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
// 메일 주소와 SMTP 비밀번호(또는 그 파일 경로)는 저장소에 올리지 않는 이 파일에만 둔다
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddWindowsService(o => o.ServiceName = "RealEstateCrawler");
builder.Services.Configure<CrawlerOptions>(builder.Configuration.GetSection(CrawlerOptions.Section));
builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.Section));

var crawlerOptions = builder.Configuration.GetSection(CrawlerOptions.Section).Get<CrawlerOptions>() ?? new CrawlerOptions();
var dataDirectory = crawlerOptions.ResolveDataDirectory();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(dataDirectory, "logs", "crawler-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();
builder.Services.AddSerilog();

const string BrowserUserAgent =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";

builder.Services.AddHttpClient(DabangFeed.HttpClientName, c =>
{
    c.BaseAddress = new Uri("https://www.dabangapp.com/");
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
    c.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    c.DefaultRequestHeaders.Referrer = new Uri("https://www.dabangapp.com/map/onetwo");
    // 다방 웹 클라이언트가 붙이는 헤더. 없으면 400 을 돌려준다.
    c.DefaultRequestHeaders.Add("csrf", "token");
    c.DefaultRequestHeaders.Add("D-Api-Version", "5.0.0");
    c.DefaultRequestHeaders.Add("D-App-Version", "1");
    c.DefaultRequestHeaders.Add("D-Call-Type", "web");
});
builder.Services.AddHttpClient(ZigbangHouseFeed.HttpClientName, c =>
{
    c.BaseAddress = new Uri("https://apis.zigbang.com/");
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
    c.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    c.DefaultRequestHeaders.Referrer = new Uri("https://www.zigbang.com/");
    c.DefaultRequestHeaders.Add("X-Zigbang-Platform", "www");
});
builder.Services.AddHttpClient(OsmStaticMapRenderer.HttpClientName, c =>
{
    c.BaseAddress = new Uri("https://tile.openstreetmap.org/");
    c.Timeout = TimeSpan.FromSeconds(20);
    // OSM 타일 정책: 앱을 알아볼 수 있는 User-Agent 를 보내야 한다
    c.DefaultRequestHeaders.UserAgent.ParseAdd("RealEstateCrawler/1.0 (+https://github.com/genericboson/realestatecrawler)");
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new ZigbangGeohashCipher(crawlerOptions.ZigbangGeohashKey));
builder.Services.AddSingleton<IListingFeed>(sp => ActivatorUtilities.CreateInstance<DabangFeed>(sp, DabangCategory.OneTwoRoom));
builder.Services.AddSingleton<IListingFeed>(sp => ActivatorUtilities.CreateInstance<DabangFeed>(sp, DabangCategory.Officetel));
builder.Services.AddSingleton<IListingFeed>(sp => ActivatorUtilities.CreateInstance<DabangFeed>(sp, DabangCategory.Apartment));
builder.Services.AddSingleton<IListingFeed>(sp => ActivatorUtilities.CreateInstance<ZigbangHouseFeed>(sp, ZigbangHouseKind.Oneroom));
builder.Services.AddSingleton<IListingFeed>(sp => ActivatorUtilities.CreateInstance<ZigbangHouseFeed>(sp, ZigbangHouseKind.Villa));
builder.Services.AddSingleton<IListingFeed>(sp => ActivatorUtilities.CreateInstance<ZigbangHouseFeed>(sp, ZigbangHouseKind.Officetel));
builder.Services.AddSingleton<IListingFeed, ZigbangApartmentFeed>();
builder.Services.AddSingleton<IMailSender, SmtpMailSender>();
builder.Services.AddSingleton<IMapRenderer, OsmStaticMapRenderer>();
builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<StateStore>(sp, Path.Combine(dataDirectory, "state.json")));
builder.Services.AddSingleton<CrawlRunner>();
builder.Services.AddHostedService<Worker>();

try
{
    using var host = builder.Build();

    if (args.Contains("--dry-run"))
    {
        Console.OutputEncoding = Encoding.UTF8;
        await host.Services.GetRequiredService<CrawlRunner>().DryRunAsync(Console.Out, CancellationToken.None);
        return 0;
    }

    if (args.Contains("--test-mail"))
    {
        var mail = host.Services.GetRequiredService<IMailSender>();
        if (!mail.IsEnabled)
        {
            Log.Error("appsettings.Local.json 의 Mail:To 가 비어 있음");
            return 1;
        }
        const string Body = "부동산 크롤러의 메일 발송 시험입니다. 이 메일이 보이면 설정이 맞습니다.";
        await mail.SendAsync(new MailContent("[부동산 크롤러] 메일 발송 시험", Body, $"<p>{Body}</p>", []), CancellationToken.None);
        return 0;
    }

    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "비정상 종료");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
