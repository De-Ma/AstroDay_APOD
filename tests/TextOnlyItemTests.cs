using System.Text;
using System.Text.Json;
using AstroDay.Models;
using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class TextOnlyItemTests
{
    [Fact]
    public void ExtractPageDate_IgnoresYesterdaysImageAndExplanationDates()
    {
        const string text = "Astronomy Picture of the Day Selfie Explanation: On Earth date August 26, 2026 it grew. " +
            "DATE October 3, 2026 CREDIT: x YESTERDAY'S IMAGE APOD: 2026 October 2 \u2013 The Complete Sharpless Catalog";

        Assert.Equal(new DateOnly(2026, 10, 3), ApodParsing.ExtractPageDate(text));
    }

    [Fact]
    public void ExtractPageDate_DoesNotPickRandomDatesFromBody()
    {
        var filler = new string('x', 300);

        Assert.Null(ApodParsing.ExtractPageDate(filler + " YESTERDAY'S IMAGE APOD: 2026 October 2 \u2013 Title"));
    }

    [Fact]
    public void FavoriteSelector_IgnoresFavoritesWithoutImage()
    {
        var textOnly = new ApodItem { Date = "2026-08-31", IsFavorite = true };

        Assert.Null(FavoriteSelector.ChooseRandom([textOnly], null, new Random(1)));
    }

    [Fact]
    public void HasImage_IsNotWrittenToTheCatalogue()
    {
        var json = JsonSerializer.Serialize(new ApodItem { Date = "2026-08-31", FilePath = "x.jpg" });

        Assert.DoesNotContain("HasImage", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveTextOnly_PersistsTextAndSurvivesReload()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "AstroDayTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var settings = new AppSettings();
            var library = new LibraryService(Path.Combine(directory, "lib"), Path.Combine(directory, "s.json"), settings);

            await library.SaveTextOnlyAsync(new ApodItem { Date = "2026-08-31", Title = "Launch", Explanation = "A video." });
            var reopened = new LibraryService(Path.Combine(directory, "lib"), Path.Combine(directory, "s.json"), settings);
            var item = Assert.Single(await reopened.GetItemsAsync());

            Assert.False(item.HasImage);
            Assert.Equal("Launch", item.Title);
            Assert.Equal("A video.", item.Explanation);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task SaveTextOnly_ReplacesExistingImageAndKeepsFavorite()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "AstroDayTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var library = new LibraryService(Path.Combine(directory, "lib"), Path.Combine(directory, "s.json"), new AppSettings());
            var saved = await library.SaveDownloadAsync(
                new ApodItem { Date = "2026-08-31", ImageUrl = "https://assets.science.nasa.gov/x/logo.png" },
                new MemoryStream(Encoding.UTF8.GetBytes("logo")), token);
            await library.ToggleFavoriteAsync("2026-08-31");

            var replaced = await library.SaveTextOnlyAsync(new ApodItem { Date = "2026-08-31", Title = "Launch" });

            Assert.True(replaced.IsFavorite);
            Assert.False(File.Exists(saved.FilePath));
            Assert.Single(await library.GetItemsAsync());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task RemoveAsync_RemovesTextOnlyItem()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AstroDayTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var library = new LibraryService(Path.Combine(directory, "lib"), Path.Combine(directory, "s.json"), new AppSettings());
            await library.SaveTextOnlyAsync(new ApodItem { Date = "2026-08-31", Title = "Launch" });

            await library.RemoveAsync("2026-08-31");

            Assert.Empty(await library.GetItemsAsync());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
