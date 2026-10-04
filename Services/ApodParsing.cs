using System.Globalization;
using System.Text.RegularExpressions;

namespace AstroDay.Services;

public static class ApodParsing
{
    public const string ApodHomeUrl = "https://science.nasa.gov/apod/";
    public const string ApodArchiveUrl = "https://science.nasa.gov/apod/archive/";

    // APOD days follow US Eastern time, which can be a day behind the local date.
    public static DateOnly ApodToday(DateTime utcNow)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone));
    }

    // The label used in NASA article titles, e.g. "2026 September 8".
    public static string DateLabel(DateOnly date) =>
        $"{date.Year} {date.ToString("MMMM", CultureInfo.InvariantCulture)} {date.Day}";

    public static string ArticleSearchUrl(DateOnly date, string fields) =>
        $"https://science.nasa.gov/wp-json/wp/v2/image-article?per_page=10&_fields={fields}&search=" +
        Uri.EscapeDataString($"APOD: {DateLabel(date)}");

    // Returns the title after "APOD: {date} – " or null when the article belongs to another date.
    public static string? MatchArticleTitle(string rawTitle, DateOnly date)
    {
        var match = Regex.Match(rawTitle, $@"^APOD:\s*{Regex.Escape(DateLabel(date))}\s*[\u2013\u2014-]\s*(?<title>.+)$");
        return match.Success ? match.Groups["title"].Value.Trim() : null;
    }

    public static bool IsVideoContent(string content) =>
        Regex.IsMatch(content, @"\.mp4|youtube\.com|youtu\.be|vimeo\.com", RegexOptions.IgnoreCase);

    // NASA's API and pages can return the site logo as a stand-in until the real picture is published.
    public static bool IsPlaceholderImageUrl(string? url) =>
        url is not null && (url.Contains("logo", StringComparison.OrdinalIgnoreCase) || url.Contains("/themes/", StringComparison.OrdinalIgnoreCase));

    public static bool IsNasaHttpsUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("nasa.gov", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".nasa.gov", StringComparison.OrdinalIgnoreCase));

    // science.nasa.gov serves APOD images from assets.science.nasa.gov/.../cds/apod/apod/{year}/{month}/.
    public static IEnumerable<string> FindApodImageUrls(string html, DateOnly date)
    {
        var month = date.ToString("MMMM", CultureInfo.InvariantCulture).ToLowerInvariant();
        var matches = Regex.Matches(html,
            $@"https://assets\.science\.nasa\.gov/dynamicimage/assets/science/cds/apod/apod/{date.Year}/{month}/[^""'\s<>?]+\.(?:jpe?g|png|gif|webp)",
            RegexOptions.IgnoreCase);
        if (matches.Count == 0)
            matches = Regex.Matches(html,
                $@"https://assets\.science\.nasa\.gov/dynamicimage/assets/science/cds/apod/apod/{date.Year}/[a-z]+/[^""'\s<>?]+\.(?:jpe?g|png|gif|webp)",
                RegexOptions.IgnoreCase);
        foreach (var path in matches.Select(match => match.Value).GroupBy(value => value).OrderByDescending(group => group.Count()).Select(group => group.Key))
        {
            // Original file first, then a large resized rendition.
            yield return path.Replace("/dynamicimage/assets/", "/content/dam/");
            yield return $"{path}?w=2400";
        }
    }

    // Page text lists several dates (explanation, "yesterday's image"); only the DATE row is reliable and it comes last.
    public static DateOnly? ExtractPageDate(string pageText)
    {
        var rows = Regex.Matches(pageText, @"\bDATE\s*\|?\s*(?<value>[A-Za-z]+ \d{1,2}, \d{4})", RegexOptions.IgnoreCase);
        for (var index = rows.Count - 1; index >= 0; index--)
            if (DateOnly.TryParseExact(rows[index].Groups["value"].Value, "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
        // Legacy layout puts "2026 September 28" in the page header.
        var header = pageText.Length > 150 ? pageText[..150] : pageText;
        var match = Regex.Match(header,
            @"\b(?<year>20\d{2})\s+(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2})\b",
            RegexOptions.IgnoreCase);
        var value = $"{match.Groups["year"].Value} {match.Groups["month"].Value} {match.Groups["day"].Value}";
        return match.Success && DateOnly.TryParseExact(value, "yyyy MMMM d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    public static string ExtractExplanation(string pageText)
    {
        const string marker = "Explanation:";
        var start = pageText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return "";
        start += marker.Length;
        var end = new[]
        {
            pageText.IndexOf("Tomorrow's picture:", start, StringComparison.OrdinalIgnoreCase),
            pageText.IndexOf("APOD's email for image submissions", start, StringComparison.OrdinalIgnoreCase),
            pageText.IndexOf("Authors & editors:", start, StringComparison.OrdinalIgnoreCase)
        }.Where(index => index >= 0).DefaultIfEmpty(pageText.Length).Min();
        var explanation = pageText[start..end].Trim();
        return explanation.Length > 6000 ? explanation[..6000] : explanation;
    }
}
