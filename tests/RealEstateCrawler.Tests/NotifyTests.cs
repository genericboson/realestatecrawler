using RealEstateCrawler.Notify;

namespace RealEstateCrawler.Tests;

public class NotifyTests
{
    static Listing Sample(int i, string title = "제목") => new(
        "직방", i.ToString(), "원룸", "1000/65", "3층", "19.83㎡", "성동구 성수동2가", title, 37.54, 127.05,
        $"https://www.zigbang.com/home/oneroom/items/{50000000 + i}");

    [Fact]
    public void Build_PutsEveryListingInOneMail()
    {
        var listings = Enumerable.Range(1, 30).Select(i => Sample(i)).ToList();
        var mail = MailFormatter.Build(listings, "성수");

        Assert.Equal("[성수] 신규 월세 30건", mail.Subject);
        Assert.StartsWith("[성수] 신규 월세 30건\n\n[직방] 원룸 월세 1000/65\n성동구 성수동2가 · 3층 · 19.83㎡\n", mail.Text);
        Assert.All(listings, l => Assert.Contains(l.Url, mail.Text));
        Assert.All(listings, l => Assert.Contains($"<a href=\"{l.Url}\"", mail.Html));
    }

    [Fact]
    public void Build_EncodesListingTextInHtml()
    {
        var mail = MailFormatter.Build([Sample(1, "<b>풀옵션</b> & 역세권")], "성수");

        Assert.Contains("&lt;b&gt;풀옵션&lt;/b&gt; &amp; 역세권", mail.Html);
        Assert.DoesNotContain("<b>풀옵션", mail.Html);
    }

    [Fact]
    public void Build_SkipsEmptyTitle()
    {
        var mail = MailFormatter.Build([Sample(1, "")], "성수");

        Assert.DoesNotContain("color:#666", mail.Html);
    }

    [Fact]
    public void ResolvePassword_PrefersInlinePasswordThenFile()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "from-file\r\n");

            Assert.Equal("inline", new MailOptions { Password = "inline", PasswordFile = file }.ResolvePassword());
            Assert.Equal("from-file", new MailOptions { PasswordFile = file }.ResolvePassword());
            Assert.Null(new MailOptions { PasswordFile = file + ".missing" }.ResolvePassword());
            Assert.Null(new MailOptions().ResolvePassword());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void MailIsEnabledOnlyWithRecipient()
    {
        Assert.False(new MailOptions().IsEnabled);
        Assert.True(new MailOptions { To = "someone@example.com" }.IsEnabled);
    }
}
