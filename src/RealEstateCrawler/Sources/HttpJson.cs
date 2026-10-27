using System.Net;
using System.Text;
using System.Text.Json;

namespace RealEstateCrawler.Sources;

internal static class HttpJson
{
    static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15)];

    /// <summary>사이트에 부담을 주지 않도록 요청 사이에 두는 간격.</summary>
    public static readonly TimeSpan PoliteDelay = TimeSpan.FromMilliseconds(250);

    public static Task<JsonDocument> GetAsync(HttpClient http, string url, CancellationToken ct) =>
        SendAsync(http, () => new HttpRequestMessage(HttpMethod.Get, url), ct);

    public static Task<JsonDocument> PostAsync(HttpClient http, string url, string json, CancellationToken ct) =>
        SendAsync(http, () => new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }, ct);

    static async Task<JsonDocument> SendAsync(HttpClient http, Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = createRequest();
            try
            {
                using var response = await http.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"{(int)response.StatusCode} {request.Method} {request.RequestUri}: {Truncate(body)}",
                        null, response.StatusCode);
                }
                return JsonDocument.Parse(body);
            }
            catch (Exception ex) when (attempt < RetryDelays.Length && IsTransient(ex) && !ct.IsCancellationRequested)
            {
                await Task.Delay(RetryDelays[attempt], ct);
            }
        }
    }

    static bool IsTransient(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => true,
        HttpRequestException { StatusCode: var code } => (int)code! >= 500,
        TaskCanceledException => true,
        _ => false,
    };

    static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "…";
}

internal static class JsonElementExtensions
{
    public static string Str(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : "";

    public static double? Num(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    public static IEnumerable<JsonElement> Array(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];
}
