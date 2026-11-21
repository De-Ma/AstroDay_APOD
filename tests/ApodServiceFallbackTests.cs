using System.Net;
using System.Text;
using AstroDay.Models;
using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class ApodServiceFallbackTests
{
    [Fact]
    public async Task DownloadDate_FallsBackToWebsiteArticleAfterApiRateLimit()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "AstroDayTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var settings = new AppSettings
            {
                ApiKey = "DEMO_KEY",
                LibraryDirectory = Path.Combine(directory, "library"),
                SourceOrder = [ApodSources.Api, ApodSources.Article]
            };
            var library = new LibraryService(settings.LibraryDirectory, Path.Combine(directory, "settings.json"), settings);
            var handler = new RateLimitedApiHandler();
            using var client = new HttpClient(handler);
            var service = new ApodService(library, settings, client);

            var item = await service.DownloadDateAsync(new DateOnly(2026, 10, 31), token);

            Assert.NotNull(item);
            Assert.True(item.HasImage);
            Assert.Equal("Night on a Spooky Planet", item.Title);
            Assert.Contains("Night on a Spooky Planet", item.Explanation);
            Assert.Equal(1, handler.ApiRequests);
            Assert.Equal(1, handler.ArticleRequests);
            Assert.Equal(1, handler.ImageRequests);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class RateLimitedApiHandler : HttpMessageHandler
    {
        public int ApiRequests { get; private set; }
        public int ArticleRequests { get; private set; }
        public int ImageRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host.Equals("api.nasa.gov", StringComparison.OrdinalIgnoreCase))
            {
                ApiRequests++;
                return JsonResponse(HttpStatusCode.TooManyRequests, """{"error":{"code":"OVER_RATE_LIMIT"}}""");
            }
            if (request.RequestUri.Host.Equals("science.nasa.gov", StringComparison.OrdinalIgnoreCase))
            {
                ArticleRequests++;
                const string content = """
                    <a href="https://assets.science.nasa.gov/content/dam/science/cds/apod/apod/Hverir_Vetter_1300.jpg">
                      <img src="https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/Hverir_Vetter_1300.jpg?w=1300" />
                    </a>
                    <p><strong>Explanation:</strong> Night on a Spooky Planet over Iceland.</p>
                    """;
                var json = $$"""
                    [{
                      "title":{"rendered":"APOD: 2026 October 31 – Night on a Spooky Planet"},
                      "content":{"rendered":{{System.Text.Json.JsonSerializer.Serialize(content)}}}
                    }]
                    """;
                return JsonResponse(HttpStatusCode.OK, json);
            }
            if (request.RequestUri.Host.Equals("assets.science.nasa.gov", StringComparison.OrdinalIgnoreCase))
            {
                ImageRequests++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("jpeg-bytes")),
                    RequestMessage = request
                };
                response.Content.Headers.ContentType = new("image/jpeg");
                return Task.FromResult(response);
            }
            return JsonResponse(HttpStatusCode.NotFound, "");
        }

        private static Task<HttpResponseMessage> JsonResponse(HttpStatusCode statusCode, string content) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}
