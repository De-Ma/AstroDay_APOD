using AstroDay.Models;
using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class ApodParsingTests
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    [Fact]
    public void DateLabel_UsesFullMonthAndNoLeadingZero()
    {
        Assert.Equal("2026 September 8", ApodParsing.DateLabel(new DateOnly(2026, 9, 8)));
    }

    [Fact]
    public void ArticleSearchUrl_EscapesSearchTermAndKeepsFields()
    {
        var url = ApodParsing.ArticleSearchUrl(Oct1, "title,link");

        Assert.Contains("_fields=title,link", url, StringComparison.Ordinal);
        Assert.EndsWith("search=APOD%3A%202026%20October%201", url, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("APOD: 2026 October 1 \u2013 Harvest Moon", "Harvest Moon")]
    [InlineData("APOD: 2026 October 1 - Harvest Moon", "Harvest Moon")]
    public void MatchArticleTitle_ReturnsTitleForMatchingDate(string raw, string expected)
    {
        Assert.Equal(expected, ApodParsing.MatchArticleTitle(raw, Oct1));
    }

    [Theory]
    [InlineData("APOD: 2026 October 11 \u2013 Other day")]
    [InlineData("APOD: 2026 September 1 \u2013 Other month")]
    [InlineData("Something else entirely")]
    public void MatchArticleTitle_RejectsOtherDates(string raw)
    {
        Assert.Null(ApodParsing.MatchArticleTitle(raw, Oct1));
    }

    [Theory]
    [InlineData("https://science.nasa.gov/wp-content/themes/nasa-child/assets/images/nasa-logo@2x.png", true)]
    [InlineData("https://assets.science.nasa.gov/content/dam/x/nasa-logo.svg", true)]
    [InlineData("https://assets.science.nasa.gov/content/dam/science/cds/apod/apod/2026/october/Rainbows.jpg", false)]
    [InlineData(null, false)]
    public void IsPlaceholderImageUrl_FlagsSiteLogos(string? url, bool expected)
    {
        Assert.Equal(expected, ApodParsing.IsPlaceholderImageUrl(url));
    }

    [Fact]
    public void FindApodImageUrls_PrefersOriginalThenRendition_AndIgnoresOtherImages()
    {
        const string html = """
            <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/october/Pic%20One.jpg?w=1600&#038;h=800" />
            <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/october/Pic%20One.jpg?w=300" />
            <img src="https://www.nasa.gov/wp-content/uploads/2026/09/other.jpg" />
            <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/june/old.jpg?w=300" />
            """;

        var urls = ApodParsing.FindApodImageUrls(html, Oct1).ToArray();

        Assert.Equal(
        [
            "https://assets.science.nasa.gov/content/dam/science/cds/apod/apod/2026/october/Pic%20One.jpg",
            "https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/october/Pic%20One.jpg?w=2400"
        ], urls);
    }

    [Fact]
    public void FindApodImageUrls_AcceptsReusedMissionAssetsOutsideDateFolders()
    {
        const string html = """
            <a href="https://assets.science.nasa.gov/content/dam/science/missions/hubble/galaxies/irregular/NGC%204449.jpg">
              <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/missions/hubble/galaxies/irregular/NGC%204449.jpg?w=2048&amp;h=1320" />
            </a>
            """;

        var urls = ApodParsing.FindApodImageUrls(html, new DateOnly(2026, 11, 11)).ToArray();

        Assert.Equal(
        [
            "https://assets.science.nasa.gov/content/dam/science/missions/hubble/galaxies/irregular/NGC%204449.jpg",
            "https://assets.science.nasa.gov/dynamicimage/assets/science/missions/hubble/galaxies/irregular/NGC%204449.jpg?w=2400"
        ], urls);
    }

    [Fact]
    public void FindApodImageUrls_AcceptsApodAssetsOutsideYearMonthFolders()
    {
        const string html = """
            <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/Hverir_Vetter_1300.jpg?w=1300" />
            """;

        Assert.Equal(
            "https://assets.science.nasa.gov/content/dam/science/cds/apod/apod/Hverir_Vetter_1300.jpg",
            ApodParsing.FindApodImageUrls(html, new DateOnly(2026, 10, 31)).First());
    }

    [Fact]
    public void FindApodImageUrls_AcceptsReusedPrimaryImageFromOlderApodYear()
    {
        const string html = """
            <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2021/october/DarkMatter_KipacAmnh_1200.jpg?w=1200" />
            <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2020/june/Other.jpg?w=300" />
            """;

        var urls = ApodParsing.FindApodImageUrls(html, new DateOnly(2026, 10, 25)).ToArray();

        Assert.Equal(
        [
            "https://assets.science.nasa.gov/content/dam/science/cds/apod/apod/2021/october/DarkMatter_KipacAmnh_1200.jpg",
            "https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2021/october/DarkMatter_KipacAmnh_1200.jpg?w=2400"
        ], urls);
    }

    [Fact]
    public void FindApodImageUrls_AcceptsWebbMissionImage()
    {
        const string html = """
            <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/missions/webb/science/2026/10/weic2620a.jpg?w=1280" />
            """;

        Assert.Equal(
            "https://assets.science.nasa.gov/content/dam/science/missions/webb/science/2026/10/weic2620a.jpg",
            ApodParsing.FindApodImageUrls(html, new DateOnly(2026, 11, 16)).First());
    }

    [Fact]
    public void FindApodImageUrls_UsesHeroBeforeImagesInArticleLinks()
    {
        const string html = """
            <div class="media-detail-hero__media">
              <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/november/NGC7771group_Marinoni_1179.jpg?w=1179" />
            </div>
            <p>Related: <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/november/Lynds1251_old.jpg?w=800" /></p>
            """;

        var urls = ApodParsing.FindApodImageUrls(html, new DateOnly(2026, 11, 24)).ToArray();

        Assert.Equal(2, urls.Length);
        Assert.Contains("NGC7771group_Marinoni_1179.jpg", urls[0], StringComparison.Ordinal);
        Assert.DoesNotContain(urls, url => url.Contains("Lynds1251_old", StringComparison.Ordinal));
    }

    [Fact]
    public void ArticleLookupSearchUrl_UsesWordPressSearchApi()
    {
        Assert.Equal(
            "https://science.nasa.gov/wp-json/wp/v2/search?per_page=100&subtype=image-article&search=APOD%3A%202026%20August%2012",
            ApodParsing.ArticleLookupSearchUrl(new DateOnly(2026, 8, 12)));
    }

    [Fact]
    public void FindApodImageUrls_ReturnsNothingForVideoOnlyContent()
    {
        const string html = "https://assets.science.nasa.gov/content/dam/science/cds/apod/apod/2026/august/Launch.mp4";

        Assert.Empty(ApodParsing.FindApodImageUrls(html, new DateOnly(2026, 8, 31)));
        Assert.True(ApodParsing.IsVideoContent(html));
    }

    [Fact]
    public void IsVideoContent_DetectsEmbeddedPlayers()
    {
        Assert.True(ApodParsing.IsVideoContent("<iframe src=\"https://www.youtube.com/embed/abc\"></iframe>"));
        Assert.True(ApodParsing.IsVideoContent("<iframe src=\"https://science.nasa.gov/embed/abc\"></iframe>"));
        Assert.True(ApodParsing.IsVideoContent("<video src=\"video.webm\"></video>"));
        Assert.False(ApodParsing.IsVideoContent("<img src=\"a.jpg\" />"));
    }

    [Theory]
    [InlineData("https://science.nasa.gov/image-article/x/", true)]
    [InlineData("https://nasa.gov/x", true)]
    [InlineData("http://science.nasa.gov/x", false)]
    [InlineData("https://nasa.gov.evil.example/x", false)]
    [InlineData("https://evilnasa.gov/x", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData(null, false)]
    public void IsNasaHttpsUrl_OnlyAllowsHttpsNasaHosts(string? url, bool expected)
    {
        Assert.Equal(expected, ApodParsing.IsNasaHttpsUrl(url));
    }

    [Fact]
    public void ExtractPageDate_PrefersDateRowOverDatesInExplanation()
    {
        const string text = "Explanation: On Earth date August 26, 2026 Curiosity... DATE October 3, 2026 CREDIT: someone";

        Assert.Equal(new DateOnly(2026, 10, 3), ApodParsing.ExtractPageDate(text));
    }

    [Fact]
    public void ExtractPageDate_SupportsLegacyYearMonthDayFormat()
    {
        Assert.Equal(new DateOnly(2026, 9, 28), ApodParsing.ExtractPageDate("Astronomy Picture of the Day 2026 September 28 Title"));
        Assert.Null(ApodParsing.ExtractPageDate("no date here"));
    }

    [Fact]
    public void ExtractExplanation_StopsAtFooterMarkers()
    {
        const string text = "Title Explanation: A nice picture. Tomorrow's picture: Something";

        Assert.Equal("A nice picture.", ApodParsing.ExtractExplanation(text));
        Assert.Equal("", ApodParsing.ExtractExplanation("No marker"));
    }

    [Fact]
    public void ExtractExplanation_TruncatesVeryLongText()
    {
        var text = "Explanation: " + new string('x', 7000);

        Assert.Equal(6000, ApodParsing.ExtractExplanation(text).Length);
    }

    [Fact]
    public void DefaultSourceOrder_IsApiThenArticleThenPage()
    {
        Assert.Equal([ApodSources.Api, ApodSources.Article, ApodSources.Page], new AppSettings().SourceOrder);
    }

    [Fact]
    public async Task SourceOrder_SurvivesSaveAndLoad_AndDefaultsWhenMissing()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "AstroDayTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            await SettingsService.SaveAsync(path, new AppSettings { SourceOrder = [ApodSources.Page, ApodSources.Article] }, token);

            Assert.Equal([ApodSources.Page, ApodSources.Article], (await SettingsService.LoadAsync(path, token)).SourceOrder);

            await File.WriteAllTextAsync(path, "{\"ThemeName\":\"Lime\"}", token);
            Assert.Equal(new AppSettings().SourceOrder, (await SettingsService.LoadAsync(path, token)).SourceOrder);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
