using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AstroDay.Models;
using Microsoft.Win32;

namespace AstroDay.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string? LastLoadWarning { get; private set; }
    public const string PreferencesRegistryPath = @"Software\AstroDay";

    public static void RememberLibraryDirectory(string directory)
    {
        using var key = Registry.CurrentUser.CreateSubKey(PreferencesRegistryPath);
        key.SetValue("LibraryDirectory", Path.GetFullPath(directory), RegistryValueKind.String);
    }

    public static async Task<AppSettings> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        LastLoadWarning = null;
        try
        {
            if (File.Exists(path))
            {
                SettingsDocument? document;
                await using (var stream = File.OpenRead(path))
                    document = await JsonSerializer.DeserializeAsync<SettingsDocument>(stream, cancellationToken: cancellationToken);
                if (document is null) return new AppSettings();
                var settings = document.ToSettings();
                if (!string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
                {
                    try { settings.ApiKey = ApiKeyProtection.Unprotect(settings.ProtectedApiKey); }
                    catch (Exception exception) when (exception is CryptographicException or FormatException)
                    {
                        settings.ApiKey = "DEMO_KEY";
                        settings.ApiKeyUnprotectFailed = true;
                        LastLoadWarning = "The saved NASA API key could not be decrypted for this Windows user. The protected value was preserved; enter the key again in Settings.";
                    }
                }
                else if (!string.IsNullOrWhiteSpace(document.ApiKey))
                {
                    settings.ApiKey = document.ApiKey;
                    try { await SaveAsync(path, settings, cancellationToken); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
                    {
                        LastLoadWarning = $"The old API key format could not be migrated to protected storage: {exception.Message}";
                    }
                }
                return settings;
            }
        }
        catch (JsonException exception)
        {
            LastLoadWarning = $"Settings could not be read and defaults are in use: {exception.Message}";
            PreserveInvalidFile(path);
        }
        catch (IOException exception)
        {
            LastLoadWarning = $"Settings could not be read: {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            LastLoadWarning = $"Settings could not be accessed: {exception.Message}";
        }
        return new AppSettings();
    }

    public static Task SaveAsync(string path, AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.ApiKeyUnprotectFailed)
        {
            settings.ProtectedApiKey = ApiKeyProtection.Protect(settings.ApiKey);
        }
        return AtomicJsonFile.WriteAsync(path, SettingsDocument.FromSettings(settings), Options, cancellationToken);
    }

    private static void PreserveInvalidFile(string path)
    {
        if (!File.Exists(path)) return;
        var backupPath = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        try { File.Move(path, backupPath, false); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class SettingsDocument
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApiKey { get; set; }
        public string? ProtectedApiKey { get; set; }
        public string? LibraryDirectory { get; set; }
        public bool ApplyWallpaper { get; set; } = true;
        public string? WallpaperPlacement { get; set; }
        public bool ApplyLockScreen { get; set; }
        public bool FavoritesOnly { get; set; }
        public int FavoriteCycleIntervalMinutes { get; set; } = 60;
        public bool CycleFavoritesOnLockScreen { get; set; }
        public string? LastFavoritePath { get; set; }
        public List<string>? SourceOrder { get; set; }
        public bool RunAtStartup { get; set; }
        public bool CloseToTray { get; set; } = true;
        public string ThemeName { get; set; } = "Mint";
        public string? SelectedApodDate { get; set; }
        public DateTime? LastCheckedUtc { get; set; }
        public DateTime? LastUpdateCheckUtc { get; set; }
        public string? CurrentWallpaperPath { get; set; }

        public AppSettings ToSettings() => new()
        {
            LibraryDirectory = LibraryDirectory ?? new AppSettings().LibraryDirectory,
            ProtectedApiKey = ProtectedApiKey,
            ApplyWallpaper = ApplyWallpaper,
            WallpaperPlacement = WallpaperPlacement == "Fit" ? "Fit" : "Fill",
            ApplyLockScreen = ApplyLockScreen,
            FavoritesOnly = FavoritesOnly,
            FavoriteCycleIntervalMinutes = FavoriteCycleIntervalMinutes,
            CycleFavoritesOnLockScreen = CycleFavoritesOnLockScreen,
            LastFavoritePath = LastFavoritePath,
            SourceOrder = SourceOrder ?? new AppSettings().SourceOrder,
            RunAtStartup = RunAtStartup,
            CloseToTray = CloseToTray,
            ThemeName = ThemeName,
            SelectedApodDate = SelectedApodDate,
            LastCheckedUtc = LastCheckedUtc,
            LastUpdateCheckUtc = LastUpdateCheckUtc,
            CurrentWallpaperPath = CurrentWallpaperPath
        };

        public static SettingsDocument FromSettings(AppSettings settings) => new()
        {
            ProtectedApiKey = settings.ProtectedApiKey,
            LibraryDirectory = settings.LibraryDirectory,
            ApplyWallpaper = settings.ApplyWallpaper,
            WallpaperPlacement = settings.WallpaperPlacement == "Fit" ? "Fit" : "Fill",
            ApplyLockScreen = settings.ApplyLockScreen,
            FavoritesOnly = settings.FavoritesOnly,
            FavoriteCycleIntervalMinutes = settings.FavoriteCycleIntervalMinutes,
            CycleFavoritesOnLockScreen = settings.CycleFavoritesOnLockScreen,
            LastFavoritePath = settings.LastFavoritePath,
            SourceOrder = [.. settings.SourceOrder],
            RunAtStartup = settings.RunAtStartup,
            CloseToTray = settings.CloseToTray,
            ThemeName = settings.ThemeName,
            SelectedApodDate = settings.SelectedApodDate,
            LastCheckedUtc = settings.LastCheckedUtc,
            LastUpdateCheckUtc = settings.LastUpdateCheckUtc,
            CurrentWallpaperPath = settings.CurrentWallpaperPath
        };
    }
}
