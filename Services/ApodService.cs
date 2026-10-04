using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AstroDay.Models;
using HtmlAgilityPack;
using static AstroDay.Services.ApodParsing;

namespace AstroDay.Services;

public sealed class ApodService(LibraryService library, AppSettings settings)
{
    private static readonly HttpClient Client = CreateHttpClient();
    private const int MaxAttempts = 3;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AstroDay/1.0");
        return client;
    }

    public Task<ApodItem?> DownloadTodayAsync(CancellationToken cancellationToken) =>
        DownloadDateAsync(ApodToday(DateTime.UtcNow), cancellationToken);

    public async Task<ApodItem?> DownloadDateAsync(DateOnly date, CancellationToken cancellationToken, bool forceRefresh = false)
    {
        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!forceRefresh)
        {
            var cached = await library.GetByDateAsync(dateText);
            if (cached is not null) return cached;
        }

        var errors = new List<string>();
        foreach (var source in settings.SourceOrder.Distinct())
        {
            try
            {
                var item = source switch
                {
                    ApodSources.Api => await DownloadFromApiAsync(date, cancellationToken),
                    ApodSources.Article => await DownloadFromArticleSearchAsync(date, cancellationToken),
                    ApodSources.Page => await DownloadFromPageAsync(date, forceRefresh, cancellationToken),
                    _ => null
                };
                if (item is not null) return item;
                if (source != ApodSources.Page) return null;
            }
            catch (NoStillImageException noImage)
            {
                return await library.SaveTextOnlyAsync(noImage.Item);
            }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested && error is HttpRequestException or JsonException or InvalidDataException)
            {
                errors.Add($"{ApodSources.DisplayName(source)}: {error.Message}");
            }
        }

        if (errors.Count == 0)
            throw new InvalidOperationException("No image source is enabled. Enable at least one source in Settings.");
        throw new HttpRequestException("All enabled image sources failed.\n\n" + string.Join("\n\n", errors));
    }

    private sealed class NoStillImageException(ApodItem item) : Exception
    {
        public ApodItem Item { get; } = item;
    }

    private async Task<ApodItem?> DownloadFromApiAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var apiKey = Uri.EscapeDataString(string.IsNullOrWhiteSpace(settings.ApiKey) ? "DEMO_KEY" : settings.ApiKey.Trim());
        var metadataUrl = $"https://api.nasa.gov/planetary/apod?api_key={apiKey}&date={dateText}&thumbs=true";
        using var response = await GetWithRetryAsync(metadataUrl, "NASA APOD API", cancellationToken);
        await using var metadataStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var metadata = await JsonSerializer.DeserializeAsync<ApodResponse>(metadataStream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("NASA returned an empty image record.");
        if (!DateOnly.TryParseExact(metadata.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var returnedDate) || returnedDate != date)
            throw new InvalidDataException("NASA returned APOD metadata for an unexpected date.");

        if (!string.Equals(metadata.MediaType, "image", StringComparison.OrdinalIgnoreCase))
            throw new NoStillImageException(new ApodItem
            {
                Date = metadata.Date,
                Title = metadata.Title ?? "NASA Astronomy Picture of the Day",
                Explanation = metadata.Explanation ?? ""
            });

        return await DownloadAndSaveAsync(new ApodItem
        {
            Date = metadata.Date,
            Title = metadata.Title ?? "NASA Astronomy Picture of the Day",
            Explanation = metadata.Explanation ?? ""
        }, [metadata.HdUrl, metadata.Url], cancellationToken);
    }

    private async Task<ApodItem?> DownloadFromPageAsync(DateOnly requestedDate, bool forceRefresh, CancellationToken cancellationToken)
    {
        // The page only shows the current day; apod.nasa.gov/apod/apYYMMDD.html now redirects to it.
        if (requestedDate < ApodToday(DateTime.UtcNow))
            throw new InvalidDataException("The APOD webpage only shows today's picture.");
        var pageUrl = "https://science.nasa.gov/apod/";
        {
            using var response = await GetWithRetryAsync(pageUrl, "NASA APOD webpage", cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = new HtmlAgilityPack.HtmlDocument();
            document.LoadHtml(html);
            var pageText = Regex.Replace(HtmlEntity.DeEntitize(document.DocumentNode.InnerText), @"\s+", " ").Trim();
            var actualDate = ExtractPageDate(pageText) ?? requestedDate;

            // NASA may still show yesterday's picture; don't re-download or overwrite a day we already have.
            if (!forceRefresh && actualDate != requestedDate &&
                await library.GetByDateAsync(actualDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) is { } existing)
                return existing;

            var imageUrls = FindApodImageUrls(html, actualDate).ToList();
            string title = "";
            if (imageUrls.Count > 0)
            {
                title = HtmlEntity.DeEntitize(document.DocumentNode
                    .SelectSingleNode("//h1[contains(., 'Astronomy Picture of the Day')]/following::h2[1]")?.InnerText ?? "").Trim();
            }
            else
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(title) || title.StartsWith("Image Credit", StringComparison.OrdinalIgnoreCase))
                title = "NASA Astronomy Picture of the Day";

            return await DownloadAndSaveAsync(new ApodItem
            {
                Date = actualDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Title = title,
                Explanation = ExtractExplanation(pageText)
            }, imageUrls, cancellationToken);
        }
    }

    public async Task<string> FindApodPageUrlAsync(DateOnly date, CancellationToken cancellationToken)
    {
        if (date >= ApodToday(DateTime.UtcNow)) return ApodParsing.ApodHomeUrl;
        try
        {
            using var response = await GetWithRetryAsync(ApodParsing.ArticleSearchUrl(date, "title,link"), "NASA APOD article search", cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            foreach (var entry in json.RootElement.EnumerateArray())
            {
                var rawTitle = HtmlEntity.DeEntitize(entry.GetProperty("title").GetProperty("rendered").GetString() ?? "");
                var link = entry.GetProperty("link").GetString();
                if (ApodParsing.MatchArticleTitle(rawTitle, date) is not null && ApodParsing.IsNasaHttpsUrl(link))
                    return link!;
            }
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested && error is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        return ApodParsing.ApodArchiveUrl;
    }

    private async Task<ApodItem> DownloadFromArticleSearchAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var label = ApodParsing.DateLabel(date);
        var searchUrl = ApodParsing.ArticleSearchUrl(date, "title,content");
        using var response = await GetWithRetryAsync(searchUrl, "NASA APOD article search", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        foreach (var entry in json.RootElement.EnumerateArray())
        {
            var rawTitle = HtmlEntity.DeEntitize(entry.GetProperty("title").GetProperty("rendered").GetString() ?? "");
            var titleMatch = ApodParsing.MatchArticleTitle(rawTitle, date);
            if (titleMatch is null) continue;

            var content = entry.GetProperty("content").GetProperty("rendered").GetString() ?? "";
            var document = new HtmlAgilityPack.HtmlDocument();
            document.LoadHtml(content);
            var text = Regex.Replace(HtmlEntity.DeEntitize(document.DocumentNode.InnerText), @"\s+", " ").Trim();
            var imageUrls = ApodParsing.FindApodImageUrls(content, date).ToList();
            if (imageUrls.Count == 0)
            {
                if (ApodParsing.IsVideoContent(content))
                    throw new NoStillImageException(new ApodItem
                    {
                        Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Title = titleMatch,
                        Explanation = ExtractExplanation(text)
                    });
                continue;
            }

            return await DownloadAndSaveAsync(new ApodItem
            {
                Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Title = titleMatch,
                Explanation = ExtractExplanation(text)
            }, imageUrls, cancellationToken);
        }

        throw new InvalidDataException($"NASA has no APOD article with a still image for {label}.");
    }

    private async Task<ApodItem> DownloadAndSaveAsync(ApodItem item, IEnumerable<string?> imageUrls, CancellationToken cancellationToken)
    {
        Exception? lastDownloadError = null;
        foreach (var imageUrl in imageUrls)
        {
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var parsedUrl) || parsedUrl.Scheme != Uri.UriSchemeHttps || IsPlaceholderImageUrl(imageUrl))
                continue;
            if (!(parsedUrl.Host.Equals("nasa.gov", StringComparison.OrdinalIgnoreCase) || parsedUrl.Host.EndsWith(".nasa.gov", StringComparison.OrdinalIgnoreCase)))
                continue;
            try
            {
                using var imageResponse = await GetWithRetryAsync(parsedUrl.ToString(), "NASA image download", cancellationToken);
                if (imageResponse.Content.Headers.ContentLength is > 100 * 1024 * 1024)
                    throw new InvalidDataException("The APOD image exceeds AstroDay's 100 MB download limit.");
                var mediaType = imageResponse.Content.Headers.ContentType?.MediaType;
                if (mediaType is not null && (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || mediaType.Contains("svg", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"NASA image URL returned unexpected content type '{mediaType}'.");
                await using var imageStream = await imageResponse.Content.ReadAsStreamAsync(cancellationToken);
                item.ImageUrl = parsedUrl.ToString();
                return await library.SaveDownloadAsync(item, imageStream, cancellationToken);
            }
            catch (HttpRequestException exception) when (!cancellationToken.IsCancellationRequested)
            {
                lastDownloadError = exception;
            }
            catch (InvalidDataException exception) when (!cancellationToken.IsCancellationRequested)
            {
                lastDownloadError = exception;
            }
        }

        throw new InvalidDataException("NASA returned no downloadable image. The high-resolution and standard image URLs both failed.", lastDownloadError);
    }

    private static async Task<HttpResponseMessage> GetWithRetryAsync(string url, string stage, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                continue;
            }

            if (response.IsSuccessStatusCode) return response;

            var statusCode = response.StatusCode;
            var reason = response.ReasonPhrase;
            if (attempt < MaxAttempts && IsTransient(statusCode) && statusCode != System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;
                response.Dispose();
                var delay = retryAfter is { } requestedDelay && requestedDelay <= TimeSpan.FromSeconds(15)
                    ? requestedDelay
                    : TimeSpan.FromSeconds(attempt);
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            if (details.Length > 300) details = details[..300];
            var message = $"{stage} failed with HTTP {(int)statusCode} {reason}.";
            if (!string.IsNullOrWhiteSpace(details)) message += $" {details}";
            throw new HttpRequestException(message, null, statusCode);
        }

        throw new HttpRequestException($"{stage} failed after {MaxAttempts} attempts.");
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.RequestTimeout ||
        statusCode == System.Net.HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private sealed class ApodResponse
    {
        [JsonPropertyName("date")] public string Date { get; set; } = "";
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
        [JsonPropertyName("media_type")] public string? MediaType { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("hdurl")] public string? HdUrl { get; set; }
    }
}
