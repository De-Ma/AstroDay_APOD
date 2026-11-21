using System.Net;
using System.Text;
using AstroDay.Models;
using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v2.0.0-beta.1", "2.0.0")]
    public void TryParseVersion_AcceptsReleaseTags(string tag, string expected)
    {
        Assert.True(UpdateService.TryParseVersion(tag, out var version));
        Assert.Equal(new Version(expected), version);
    }

    [Fact]
    public async Task CheckAsync_ReportsNewerGithubRelease()
    {
        var handler = new StaticResponseHandler("""
            {
              "tag_name":"v1.2.0",
              "html_url":"https://github.com/De-Ma/AstroDay_APOD/releases/tag/v1.2.0",
              "name":"AstroDay 1.2.0",
              "assets":[
                {
                  "name":"AstroDay-Setup-v1.2.0.exe",
                  "browser_download_url":"https://github.com/De-Ma/AstroDay_APOD/releases/download/v1.2.0/AstroDay-Setup-v1.2.0.exe"
                }
              ]
            }
            """);
        using var client = new HttpClient(handler);
        var service = new UpdateService(client);

        var result = await service.CheckAsync(new Version(1, 1, 0), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(1, 2, 0), result.LatestVersion);
        Assert.Equal("AstroDay 1.2.0", result.ReleaseName);
        Assert.EndsWith("AstroDay-Setup-v1.2.0.exe", result.InstallerUrl);
    }

    [Fact]
    public async Task Settings_RoundTripWallpaperPlacement()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "AstroDay-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            await SettingsService.SaveAsync(path, new AppSettings { WallpaperPlacement = "Fit" }, cancellationToken);

            var loaded = await SettingsService.LoadAsync(path, cancellationToken);

            Assert.Equal("Fit", loaded.WallpaperPlacement);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class StaticResponseHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
            return Task.FromResult(response);
        }
    }
}
