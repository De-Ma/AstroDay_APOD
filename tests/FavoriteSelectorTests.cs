using System.Text.Json;
using AstroDay.Models;
using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class FavoriteSelectorTests
{
    [Fact]
    public void ChooseRandom_ReturnsNull_WhenThereAreNoFavorites()
    {
        var items = new[] { CreateItem("2026-09-30", false) };

        var selected = FavoriteSelector.ChooseRandom(items, null, new Random(1));

        Assert.Null(selected);
    }

    [Fact]
    public void ChooseRandom_ReturnsOnlyFavorite_WhenThereIsOne()
    {
        var favorite = CreateItem("2026-09-29", true);
        var items = new[] { CreateItem("2026-09-30", false), favorite };

        var selected = FavoriteSelector.ChooseRandom(items, favorite.FilePath, new Random(1));

        Assert.Same(favorite, selected);
    }

    [Fact]
    public void ChooseRandom_DoesNotImmediatelyRepeat_WhenMultipleFavoritesExist()
    {
        var first = CreateItem("2026-09-29", true);
        var second = CreateItem("2026-09-28", true);

        var selected = FavoriteSelector.ChooseRandom([first, second], first.FilePath, new Random(1));

        Assert.Same(second, selected);
    }

    [Fact]
    public void ApiKeyProtection_RoundTripsForCurrentUser()
    {
        const string apiKey = "unit-test-api-key";

        var protectedKey = ApiKeyProtection.Protect(apiKey);
        var restoredKey = ApiKeyProtection.Unprotect(protectedKey);

        Assert.Equal(apiKey, restoredKey);
        Assert.DoesNotContain(apiKey, protectedKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsLoad_MigratesLegacyPlaintextKeyToProtectedStorage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            await File.WriteAllTextAsync(settingsPath, "{\"ApiKey\":\"legacy-secret-value\"}", cancellationToken);

            var settings = await SettingsService.LoadAsync(settingsPath, cancellationToken);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath, cancellationToken));

            Assert.Equal("legacy-secret-value", settings.ApiKey);
            Assert.True(document.RootElement.TryGetProperty("ProtectedApiKey", out var protectedKey), "The migrated key should be present in protected form.");
            Assert.DoesNotContain("legacy-secret-value", protectedKey.GetString(), StringComparison.Ordinal);
            Assert.DoesNotContain("legacy-secret-value", await File.ReadAllTextAsync(settingsPath, cancellationToken), StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task SettingsLoad_DisablesStartupWhenSettingsFileIsMissing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = CreateTemporaryDirectory();
        try
        {
            var settings = await SettingsService.LoadAsync(Path.Combine(directory, "settings.json"), cancellationToken);

            Assert.False(settings.RunAtStartup);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task SettingsLoad_PreservesMalformedFileAndReturnsDefaults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            await File.WriteAllTextAsync(settingsPath, "{ invalid json", cancellationToken);

            var settings = await SettingsService.LoadAsync(settingsPath, cancellationToken);

            Assert.Equal("DEMO_KEY", settings.ApiKey);
            Assert.False(File.Exists(settingsPath));
            Assert.Single(Directory.GetFiles(directory, "settings.json.corrupt-*"));
            Assert.Contains("defaults", SettingsService.LastLoadWarning, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task AtomicJsonWrite_ReplacesExistingDocumentWithoutTemporaryFiles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "record.json");
            var options = new JsonSerializerOptions();
            await AtomicJsonFile.WriteAsync(path, new { Value = 1 }, options, cancellationToken);
            await AtomicJsonFile.WriteAsync(path, new { Value = 2 }, options, cancellationToken);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken));

            Assert.Equal(2, document.RootElement.GetProperty("Value").GetInt32());
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"AstroDay.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static ApodItem CreateItem(string date, bool favorite) => new()
    {
        Date = date,
        FilePath = $"C:\\AstroDay\\{date}.jpg",
        IsFavorite = favorite
    };
}
