using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RealEstateCrawler.Notify;

public interface ISmsSender
{
    bool IsConfigured { get; }
    Task SendAsync(IReadOnlyList<SmsMessage> messages, CancellationToken ct);
}

public sealed class SmsSendException(string message) : Exception(message);

/// <summary>SOLAPI(구 CoolSMS) 문자 발송. https://solapi.com</summary>
internal sealed class SolapiSmsSender(
    IHttpClientFactory httpFactory,
    IOptions<SmsOptions> options,
    TimeProvider time,
    ILogger<SolapiSmsSender> logger) : ISmsSender
{
    public const string HttpClientName = "solapi";

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task SendAsync(IReadOnlyList<SmsMessage> messages, CancellationToken ct)
    {
        if (messages.Count == 0) return;

        var o = options.Value;
        if (!o.IsConfigured)
        {
            foreach (var m in messages) logger.LogWarning("SMS 설정이 비어 있어 보내지 않음:\n{Text}", m.Text);
            return;
        }

        var to = Digits(o.To);
        var from = string.IsNullOrWhiteSpace(o.From) ? to : Digits(o.From);
        var payload = new
        {
            messages = messages.Select(m =>
            {
                var message = new Dictionary<string, string> { ["to"] = to, ["from"] = from, ["text"] = m.Text, ["type"] = m.Type };
                if (m.Subject is not null) message["subject"] = m.Subject;
                return message;
            }).ToList(),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "messages/v4/send-many/detail")
        {
            Content = JsonContent.Create(payload),
        };
        var salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        request.Headers.TryAddWithoutValidation("Authorization", BuildAuthorization(o.ApiKey, o.ApiSecret, time.GetUtcNow(), salt));

        var http = httpFactory.CreateClient(HttpClientName);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new SmsSendException($"SOLAPI {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("failedMessageList", out var failed) &&
            failed.ValueKind == JsonValueKind.Array && failed.GetArrayLength() > 0)
            throw new SmsSendException($"SOLAPI 발송 실패 목록: {failed}");
    }

    internal static string BuildAuthorization(string apiKey, string apiSecret, DateTimeOffset now, string salt)
    {
        var date = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(apiSecret), Encoding.UTF8.GetBytes(date + salt));
        return $"HMAC-SHA256 apiKey={apiKey}, date={date}, salt={salt}, signature={Convert.ToHexStringLower(signature)}";
    }

    static string Digits(string phone) => new(phone.Where(char.IsDigit).ToArray());
}
