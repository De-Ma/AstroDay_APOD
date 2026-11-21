using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstroDay.Services;

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version LatestVersion,
    string ReleaseUrl,
    string ReleaseName,
    string? InstallerUrl)
{
    public bool IsUpdateAvailable => LatestVersion > CurrentVersion;
    public static string RepositoryUrl => "https://github.com/De-Ma/AstroDay_APOD";
}

public sealed class UpdateService
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/De-Ma/AstroDay_APOD/releases/latest");
    private readonly HttpClient _client;

    public UpdateService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AstroDay", "1.0"));
        if (!_client.DefaultRequestHeaders.Accept.Any())
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(LatestReleaseUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<ReleaseResponse>(stream, cancellationToken: cancellationToken);
        if (release is null || !TryParseVersion(release.TagName, out var latestVersion) ||
            string.IsNullOrWhiteSpace(release.HtmlUrl))
            throw new InvalidDataException("The GitHub release response did not contain a valid version and release URL.");
        var installerUrl = release.Assets.FirstOrDefault(asset =>
            asset.Name.StartsWith("AstroDay-Setup-", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        return new UpdateCheckResult(currentVersion, latestVersion, release.HtmlUrl,
            string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            installerUrl);
    }

    public static Version GetCurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        var candidate = text.Trim();
        if (candidate.StartsWith('v') || candidate.StartsWith('V'))
            candidate = candidate[1..];
        var suffixIndex = candidate.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0) candidate = candidate[..suffixIndex];
        if (!Version.TryParse(candidate, out var parsed))
            return false;
        version = parsed;
        return true;
    }

    private sealed class ReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("assets")]
        public List<ReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class ReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }
}
