namespace RealEstateCrawler.Tests;

public class FloorRuleTests
{
    [Theory]
    [InlineData("2층", true)]
    [InlineData("12층", true)]
    [InlineData("3", true)]
    [InlineData("1층", false)]
    [InlineData("1", false)]
    [InlineData("반지하", false)]
    [InlineData("지하1층", false)]
    [InlineData("B1", false)]
    [InlineData("-1", false)]
    [InlineData("옥탑", true)]
    [InlineData("옥탑방", true)]
    [InlineData("저층", true)]
    [InlineData("고층", true)]
    [InlineData("", true)]
    public void IsAllowed_AppliesSecondFloorRule(string floor, bool expected) =>
        Assert.Equal(expected, FloorRule.IsAllowed(floor, minFloor: 2));

    [Theory]
    [InlineData("7", "7층")]
    [InlineData("3층", "3층")]
    [InlineData("저", "저층")]
    [InlineData("고", "고층")]
    [InlineData("반지하", "반지하")]
    [InlineData("", "층수 미상")]
    public void Normalize_UnifiesSiteFormats(string raw, string expected) =>
        Assert.Equal(expected, FloorRule.Normalize(raw));
}
