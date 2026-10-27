using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace RealEstateCrawler.Notify;

public interface IMailSender
{
    /// <summary>받는 주소가 설정돼 있는지. 없으면 메일 대신 로그에만 남긴다.</summary>
    bool IsEnabled { get; }
    Task SendAsync(MailContent mail, CancellationToken ct);
}

/// <summary>
/// SMTP 로 알림 메일을 보낸다. 받는 주소가 있는데 보내지 못하면 예외를 던져,
/// 호출한 쪽이 그 매물을 본 것으로 기록하지 않고 다음 회차에 다시 보내게 한다.
/// </summary>
internal sealed class SmtpMailSender(IOptions<MailOptions> options, ILogger<SmtpMailSender> logger) : IMailSender
{
    public bool IsEnabled => options.Value.IsEnabled;

    public async Task SendAsync(MailContent mail, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.IsEnabled)
        {
            logger.LogWarning("메일 받는 주소(Mail:To)가 비어 있어 보내지 않음:\n{Text}", mail.Text);
            return;
        }

        var password = o.ResolvePassword();
        if (string.IsNullOrWhiteSpace(o.SmtpHost) || string.IsNullOrWhiteSpace(o.UserName) || password is null)
            throw new InvalidOperationException(
                "메일 설정이 비어 있음: Mail:SmtpHost, Mail:UserName, SMTP 비밀번호(Mail:Password 또는 Mail:PasswordFile 의 파일)를 확인");

        var message = new MimeMessage { Subject = mail.Subject };
        message.From.Add(new MailboxAddress("부동산 크롤러", string.IsNullOrWhiteSpace(o.From) ? o.To : o.From));
        message.To.Add(MailboxAddress.Parse(o.To));
        message.Body = new BodyBuilder { TextBody = mail.Text, HtmlBody = mail.Html }.ToMessageBody();

        using var client = new SmtpClient();
        var security = o.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
        await client.ConnectAsync(o.SmtpHost, o.SmtpPort, security, ct);
        await client.AuthenticateAsync(o.UserName, password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
        logger.LogInformation("메일 발송: {Subject} → {To}", mail.Subject, o.To);
    }
}
