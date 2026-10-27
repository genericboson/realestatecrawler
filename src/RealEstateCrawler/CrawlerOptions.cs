using RealEstateCrawler.Geo;

namespace RealEstateCrawler;

public sealed class CrawlerOptions
{
    public const string Section = "Crawler";

    public string CenterName { get; set; } = "에이스하이엔드 성수타워";
    public double CenterLat { get; set; } = 37.540131;
    public double CenterLng { get; set; } = 127.05679;
    public double RadiusKm { get; set; } = 1.0;
    public int IntervalMinutes { get; set; } = 60;
    public int MinFloor { get; set; } = 2;

    /// <summary>이 기간 동안 다시 보이지 않은 매물은 기억에서 지운다.</summary>
    public int SeenRetentionDays { get; set; } = 30;

    /// <summary>한 회차에 보내는 문자 수 상한. 넘치는 매물은 "외 N건"으로 줄인다.</summary>
    public int MaxMessagesPerRun { get; set; } = 5;

    /// <summary>문자 머리말에 붙는 지역 이름.</summary>
    public string AlertLabel { get; set; } = "성수";

    /// <summary>비우면 %ProgramData%\RealEstateCrawler 를 쓴다.</summary>
    public string DataDirectory { get; set; } = "";

    /// <summary>직방 웹이 geohash 를 암호화할 때 쓰는 키. 직방이 키를 바꾸면 여기만 고친다.</summary>
    public string ZigbangGeohashKey { get; set; } = "osZDx4zzfVX/iHB9SpOZXOWYZDjWW/PtXxdkR4L0nns=";

    public SearchArea Area => new(CenterLat, CenterLng, RadiusKm);

    public string ResolveDataDirectory() =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RealEstateCrawler")
            : Environment.ExpandEnvironmentVariables(DataDirectory);
}

public sealed class SmsOptions
{
    public const string Section = "Sms";

    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";

    /// <summary>SOLAPI 에 등록된 발신번호. 비우면 수신번호를 그대로 쓴다.</summary>
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret) && !string.IsNullOrWhiteSpace(To);
}
