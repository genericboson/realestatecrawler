using Microsoft.Extensions.Options;
using RealEstateCrawler.Notify;
using RealEstateCrawler.State;

namespace RealEstateCrawler;

public sealed class Worker(
    CrawlRunner runner,
    StateStore store,
    IMailSender mail,
    IOptions<CrawlerOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        logger.LogInformation(
            "시작: {Center} 반경 {Radius}km, {Floor}층 이상, 월세 {MaxRent}만원 이하, {Interval}분 간격, 상태 파일 {State}, 메일 {Mail}",
            o.CenterName, o.RadiusKm, o.MinFloor, o.MaxMonthlyRent, o.IntervalMinutes, store.Path, mail.IsEnabled ? "사용" : "미설정(로그만)");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(o.IntervalMinutes));
        do
        {
            try
            {
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "크롤링 회차 실패");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
