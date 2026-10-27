using RealEstateCrawler.Notify;

namespace RealEstateCrawler.Tests;

public class NotifyTests
{
    static Listing Sample(int i) => new(
        "직방", i.ToString(), "원룸", "1000/65", "3층", "19.83㎡", "성동구 성수동2가", "제목", 37.54, 127.05,
        $"https://www.zigbang.com/home/oneroom/items/{50000000 + i}");

    [Fact]
    public void Build_SingleListingIsOneLms()
    {
        var messages = MessageFormatter.Build([Sample(1)], "성수", maxMessages: 5);

        var message = Assert.Single(messages);
        Assert.Equal("LMS", message.Type);
        Assert.Equal("성수 신규 월세 1건", message.Subject);
        Assert.StartsWith("[성수 신규 월세 1건]\n\n[직방] 원룸 월세 1000/65", message.Text);
        Assert.Contains("https://www.zigbang.com/home/oneroom/items/50000001", message.Text);
    }

    [Fact]
    public void Build_SplitsWithinLmsLimitAndKeepsEveryListing()
    {
        var listings = Enumerable.Range(1, 30).Select(Sample).ToList();
        var messages = MessageFormatter.Build(listings, "성수", maxMessages: 5);

        Assert.True(messages.Count > 1);
        Assert.All(messages, m => Assert.InRange(MessageFormatter.ByteCount(m.Text), 1, MessageFormatter.LmsMaxBytes));
        Assert.All(listings, l => Assert.Contains(messages, m => m.Text.Contains(l.Url)));
        Assert.EndsWith($"(1/{messages.Count})", messages[0].Subject);
    }

    [Fact]
    public void Build_CapsMessageCountAndSummarizesTheRest()
    {
        var listings = Enumerable.Range(1, 100).Select(Sample).ToList();
        var messages = MessageFormatter.Build(listings, "성수", maxMessages: 2);

        Assert.Equal(2, messages.Count);
        var shown = listings.Count(l => messages.Any(m => m.Text.Contains(l.Url)));
        Assert.EndsWith($"외 {100 - shown}건은 로그 참고", messages[^1].Text);
        Assert.All(messages, m => Assert.InRange(MessageFormatter.ByteCount(m.Text), 1, MessageFormatter.LmsMaxBytes));
    }

    [Fact]
    public void ByteCount_CountsKoreanAsTwoBytes() =>
        Assert.Equal(6, MessageFormatter.ByteCount("원룸 a"));

    [Fact]
    public void SolapiAuthorization_SignsDateAndSaltWithSecret()
    {
        var header = SolapiSmsSender.BuildAuthorization(
            "KEY", "test-secret", new DateTimeOffset(2019, 7, 1, 9, 41, 48, TimeSpan.FromHours(9)), "jqsba2jxjnrjor");

        Assert.Equal(
            "HMAC-SHA256 apiKey=KEY, date=2019-07-01T00:41:48Z, salt=jqsba2jxjnrjor, " +
            "signature=24ec8a9aa1a9e5fe93c45654e888e652e61c9fe829cffb47c2b4566d63016f79",
            header);
    }
}
