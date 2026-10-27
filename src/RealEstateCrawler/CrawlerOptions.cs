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

    /// <summary>메일 제목 앞에 붙는 지역 이름.</summary>
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

/// <summary>알림 메일. 주소와 비밀번호는 저장소에 올리지 않는 appsettings.Local.json 에만 둔다.</summary>
public sealed class MailOptions
{
    public const string Section = "Mail";

    /// <summary>받는 주소. 비우면 메일 대신 로그에만 남긴다.</summary>
    public string To { get; set; } = "";

    /// <summary>보내는 주소. 비우면 받는 주소를 쓴다. 네이버·지메일은 SMTP 계정 자신의 주소여야 한다.</summary>
    public string From { get; set; } = "";

    public string SmtpHost { get; set; } = "smtp.naver.com";
    public int SmtpPort { get; set; } = 465;

    /// <summary>true 면 STARTTLS(보통 587), false 면 접속부터 SSL(보통 465).</summary>
    public bool UseStartTls { get; set; }

    /// <summary>SMTP 로그인 아이디. 네이버는 메일 주소의 @ 앞부분.</summary>
    public string UserName { get; set; } = "";

    /// <summary>SMTP 비밀번호. 비우면 PasswordFile 에서 읽는다.</summary>
    public string Password { get; set; } = "";

    /// <summary>비밀번호 한 줄만 든 파일. 상대 경로면 실행 파일 폴더 기준이고, 보낼 때마다 다시 읽는다.</summary>
    public string PasswordFile { get; set; } = "";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(To);

    public string? ResolvePassword()
    {
        if (!string.IsNullOrWhiteSpace(Password)) return Password.Trim();
        if (string.IsNullOrWhiteSpace(PasswordFile)) return null;

        var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(PasswordFile), AppContext.BaseDirectory);
        return File.Exists(path) && File.ReadAllText(path).Trim() is { Length: > 0 } fromFile ? fromFile : null;
    }
}
