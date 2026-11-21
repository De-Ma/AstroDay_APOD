using System.IO;

namespace AstroDay.Models;

public sealed class AppSettings
{
    public string LibraryDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AstroDay");
    public string ApiKey { get; set; } = "DEMO_KEY";
    public string? ProtectedApiKey { get; set; }
    public bool ApiKeyUnprotectFailed { get; set; }
    public bool ApplyWallpaper { get; set; } = true;
    public string WallpaperPlacement { get; set; } = "Fill";
    public bool ApplyLockScreen { get; set; }
    public bool FavoritesOnly { get; set; }
    public int FavoriteCycleIntervalMinutes { get; set; } = 60;
    public bool CycleFavoritesOnLockScreen { get; set; }
    public string? LastFavoritePath { get; set; }
    public List<string> SourceOrder { get; set; } = [ApodSources.Api, ApodSources.Article, ApodSources.Page];
    public bool RunAtStartup { get; set; }
    public bool CloseToTray { get; set; } = true;
    public string ThemeName { get; set; } = "Mint";
    public string? SelectedApodDate { get; set; }
    public DateTime? LastCheckedUtc { get; set; }
    public DateTime? LastUpdateCheckUtc { get; set; }
    public string? CurrentWallpaperPath { get; set; }
}
