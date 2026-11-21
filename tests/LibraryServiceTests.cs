using System.Text;
using AstroDay.Models;
using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class LibraryServiceTests
{
    private static ApodItem Item(string date) => new()
    {
        Date = date,
        Title = "Title " + date,
        ImageUrl = "https://assets.science.nasa.gov/content/dam/x/" + date + ".jpg"
    };

    private static MemoryStream Bytes(string content) => new(Encoding.UTF8.GetBytes(content));

    private static (LibraryService Library, string Directory) Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AstroDayTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var library = new LibraryService(Path.Combine(directory, "lib"), Path.Combine(directory, "settings.json"), new AppSettings());
        return (library, directory);
    }

    [Fact]
    public async Task RemoveAsync_DeletesFileAndCatalogueEntry()
    {
        var token = TestContext.Current.CancellationToken;
        var (library, directory) = Create();
        try
        {
            var saved = await library.SaveDownloadAsync(Item("2026-10-01"), Bytes("one"), token);
            await library.SaveDownloadAsync(Item("2026-10-02"), Bytes("two"), token);

            await library.RemoveAsync("2026-10-01");

            Assert.False(File.Exists(saved.FilePath));
            var remaining = await library.GetItemsAsync();
            Assert.Equal(["2026-10-02"], remaining.Select(item => item.Date));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task RemoveAsync_IgnoresUnknownDate()
    {
        var token = TestContext.Current.CancellationToken;
        var (library, directory) = Create();
        try
        {
            await library.SaveDownloadAsync(Item("2026-10-01"), Bytes("one"), token);

            await library.RemoveAsync("2000-01-01");

            Assert.Single(await library.GetItemsAsync());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task SaveDownload_Reload_KeepsFavoriteAndReplacesContent()
    {
        var token = TestContext.Current.CancellationToken;
        var (library, directory) = Create();
        try
        {
            await library.SaveDownloadAsync(Item("2026-10-01"), Bytes("old"), token);
            await library.ToggleFavoriteAsync("2026-10-01");

            var reloaded = await library.SaveDownloadAsync(Item("2026-10-01"), Bytes("new content"), token);

            Assert.True(reloaded.IsFavorite);
            Assert.Equal("new content", await File.ReadAllTextAsync(reloaded.FilePath, token));
            Assert.Single(await library.GetItemsAsync());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task SaveDownload_Reload_WithDifferentExtensionRemovesStaleFile()
    {
        var token = TestContext.Current.CancellationToken;
        var (library, directory) = Create();
        try
        {
            var first = await library.SaveDownloadAsync(Item("2026-10-01"), Bytes("a"), token);
            var png = Item("2026-10-01");
            png.ImageUrl = "https://assets.science.nasa.gov/content/dam/x/pic.png";

            var second = await library.SaveDownloadAsync(png, Bytes("b"), token);

            Assert.NotEqual(first.FilePath, second.FilePath);
            Assert.False(File.Exists(first.FilePath));
            Assert.True(File.Exists(second.FilePath));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task SaveDownload_RejectsNonNasaHostAndEmptyImage()
    {
        var token = TestContext.Current.CancellationToken;
        var (library, directory) = Create();
        try
        {
            var foreign = Item("2026-10-01");
            foreign.ImageUrl = "https://example.com/a.jpg";

            await Assert.ThrowsAsync<InvalidDataException>(() => library.SaveDownloadAsync(foreign, Bytes("x"), token));
            await Assert.ThrowsAsync<InvalidDataException>(() => library.SaveDownloadAsync(Item("2026-10-02"), Bytes(""), token));
            Assert.Empty(await library.GetItemsAsync());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task SetLibraryDirectory_PreservesSourceAndExistingTargetContent()
    {
        var token = TestContext.Current.CancellationToken;
        var (library, directory) = Create();
        try
        {
            var saved = await library.SaveDownloadAsync(Item("2026-10-01"), Bytes("source image"), token);
            var target = Path.Combine(directory, "target");
            Directory.CreateDirectory(target);
            var targetImage = Path.Combine(target, Path.GetFileName(saved.FilePath));
            await File.WriteAllTextAsync(targetImage, "target image", token);
            await File.WriteAllTextAsync(Path.Combine(target, "library.json"), "target catalogue", token);

            await library.SetLibraryDirectoryAsync(target);

            Assert.True(File.Exists(saved.FilePath));
            Assert.Equal("source image", await File.ReadAllTextAsync(saved.FilePath, token));
            Assert.Equal("source image", await File.ReadAllTextAsync(targetImage, token));
            Assert.Equal("target image", await File.ReadAllTextAsync(Directory.GetFiles(target, "*.jpg.preserved-*").Single(), token));
            Assert.Equal("target catalogue", await File.ReadAllTextAsync(Directory.GetFiles(target, "library.json.preserved-*").Single(), token));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
