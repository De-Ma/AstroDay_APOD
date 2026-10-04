using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using AstroDay.Models;
using AstroDay.Services;
using Application = System.Windows.Application;

namespace AstroDay;

public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private NotifyIcon? _trayIcon;
    private System.Threading.Timer? _dailyTimer;
    private volatile bool _todayAvailable;
    private System.Threading.Timer? _favoriteTimer;
    private readonly SemaphoreSlim _rotationGate = new(1, 1);
    private readonly SemaphoreSlim _dailyCheckGate = new(1, 1);
    private MainWindow? _mainWindow;
    private LibraryService? _library;
    private ApodService? _apod;
    private AppSettings _settings = new();
    private bool _exiting;
    private bool _ownsMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            ErrorReporter.Log(args.Exception, "UI dispatcher");
            _trayIcon?.ShowBalloonTip(5000, "AstroDay recovered from an error", args.Exception.Message, ToolTipIcon.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                ErrorReporter.Log(exception, "AppDomain");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorReporter.Log(args.Exception, "Unobserved task");
            args.SetObserved();
        };

        try
        {
            _instanceMutex = new Mutex(true, "AstroDay.SingleInstance", out var isFirstInstance);
            if (!isFirstInstance)
            {
                _instanceMutex.Dispose();
                _instanceMutex = null;
                Shutdown();
                return;
            }
            _ownsMutex = true;

            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDay");
            Directory.CreateDirectory(appData);
            _settings = await SettingsService.LoadAsync(Path.Combine(appData, "settings.json"));
            ThemeService.Apply(_settings.ThemeName);
            try { StartupService.SetEnabled(_settings.RunAtStartup); }
            catch (Exception exception)
            {
                _settings.RunAtStartup = false;
                ErrorReporter.Log(exception, "Updating sign-in startup registration");
            }
            _library = new LibraryService(_settings.LibraryDirectory, Path.Combine(appData, "settings.json"), _settings);
            _apod = new ApodService(_library, _settings);
            SetupTrayIcon();
            ScheduleNextCheck();
            UpdateFavoriteRotation();
            if (_settings.FavoritesOnly)
                _ = RotateFavoriteAsync();

            if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase) || e.Args.Contains("--show", StringComparer.OrdinalIgnoreCase))
                ShowMainWindow();

            _ = CheckForTodayAsync();
        }
        catch (Exception exception)
        {
            ErrorReporter.Log(exception, "Startup");
            System.Windows.MessageBox.Show($"AstroDay could not start. Details were saved to the local log.\n\n{exception.Message}", "AstroDay startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void SetupTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open AstroDay", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        menu.Items.Add("Check for today's image", null, async (_, _) => await CheckForTodayAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _trayIcon = new NotifyIcon
        {
            Text = "AstroDay",
            Icon = AppIconService.CreateIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        if (SettingsService.LastLoadWarning is { } warning)
            _trayIcon.ShowBalloonTip(6000, "AstroDay settings recovery", warning, ToolTipIcon.Warning);
    }

    private void ScheduleNextCheck()
    {
        if (_exiting) return;
        _dailyTimer?.Dispose();
        // Check every 6 hours; retry every 30 minutes while today's APOD is not available yet.
        var delay = _todayAvailable ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(30);
        _dailyTimer = new System.Threading.Timer(async _ =>
        {
            await CheckForTodayAsync();
            ScheduleNextCheck();
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private async Task CheckForTodayAsync()
    {
        if (_apod is null || _exiting || !await _dailyCheckGate.WaitAsync(0)) return;
        try
        {
            if (_mainWindow is not null)
                await Dispatcher.InvokeAsync(() => _mainWindow?.SetImageLoading(true));
            try
            {
                var item = await _apod.DownloadTodayAsync(CancellationToken.None);
                _todayAvailable = item?.Date == ApodParsing.ApodToday(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                if (!_settings.FavoritesOnly)
                    await ApplyAutomaticSourceAsync(item);
                if (item is null or { HasImage: false })
                    _trayIcon?.ShowBalloonTip(3500, "AstroDay", "NASA's latest APOD post is not an image. Its description was saved.", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _todayAvailable = false;
                ErrorReporter.Log(ex, "Daily APOD check");
                _trayIcon?.ShowBalloonTip(4000, "AstroDay", ex.Message, ToolTipIcon.Warning);
            }
            finally
            {
                if (_mainWindow is not null)
                    await Dispatcher.InvokeAsync(() => _mainWindow?.SetImageLoading(false));
                _settings.LastCheckedUtc = DateTime.UtcNow;
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDay");
                try { await SettingsService.SaveAsync(Path.Combine(appData, "settings.json"), _settings); }
                catch (Exception exception)
                {
                    ErrorReporter.Log(exception, "Saving settings after daily check");
                    _trayIcon?.ShowBalloonTip(5000, "Could not save AstroDay settings", exception.Message, ToolTipIcon.Warning);
                }
            }
            if (_mainWindow is not null)
                await Dispatcher.InvokeAsync(() => { _ = _mainWindow.RefreshAsync(); });
        }
        finally { _dailyCheckGate.Release(); }
    }

    private async Task ApplyAutomaticSourceAsync(ApodItem? downloadedToday)
    {
        if (_library is null) return;
        var items = await _library.GetItemsAsync();
        var item = _settings.FavoritesOnly
            ? FavoriteSelector.ChooseRandom(items, _settings.LastFavoritePath)
            : downloadedToday is { HasImage: true } ? downloadedToday : items.FirstOrDefault(candidate => candidate.HasImage);
        if (item is null) return;
        if (_settings.FavoritesOnly) _settings.LastFavoritePath = item.FilePath;

        if (_settings.ApplyWallpaper)
        {
            try
            {
                WallpaperService.SetDesktopWallpaper(item.FilePath);
                _settings.CurrentWallpaperPath = item.FilePath;
            }
            catch (Exception exception)
            {
                _trayIcon?.ShowBalloonTip(4000, "AstroDay", exception.Message, ToolTipIcon.Warning);
            }
        }
        if (_settings.ApplyLockScreen && (!_settings.FavoritesOnly || _settings.CycleFavoritesOnLockScreen))
        {
            try { await LockScreenService.SetAsync(item.FilePath); }
            catch (Exception exception)
            {
                _trayIcon?.ShowBalloonTip(4000, "Lock screen unavailable", exception.Message, ToolTipIcon.Warning);
            }
        }
        if (_settings.CurrentWallpaperPath is not null)
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDay");
            await SettingsService.SaveAsync(Path.Combine(appData, "settings.json"), _settings);
        }
    }

    public void UpdateFavoriteRotation()
    {
        _favoriteTimer?.Dispose();
        _favoriteTimer = null;
        if (_exiting || !_settings.FavoritesOnly || (!_settings.ApplyWallpaper && !(_settings.CycleFavoritesOnLockScreen && _settings.ApplyLockScreen)))
            return;

        var intervalMinutes = Math.Clamp(_settings.FavoriteCycleIntervalMinutes, 1, 10080);
        _favoriteTimer = new System.Threading.Timer(async _ => await RotateFavoriteAsync(), null,
            TimeSpan.FromMinutes(intervalMinutes), TimeSpan.FromMinutes(intervalMinutes));
    }

    private async Task RotateFavoriteAsync()
    {
        if (_library is null || _exiting || !await _rotationGate.WaitAsync(0)) return;
        try
        {
            var items = await _library.GetItemsAsync();
            var item = FavoriteSelector.ChooseRandom(items, _settings.LastFavoritePath);
            if (item is null) return;

            if (_settings.ApplyWallpaper)
            {
                WallpaperService.SetDesktopWallpaper(item.FilePath);
                _settings.CurrentWallpaperPath = item.FilePath;
            }
            if (_settings.ApplyLockScreen && _settings.CycleFavoritesOnLockScreen)
                await LockScreenService.SetAsync(item.FilePath);

            _settings.LastFavoritePath = item.FilePath;
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDay");
            await SettingsService.SaveAsync(Path.Combine(appData, "settings.json"), _settings);
            if (_mainWindow is not null)
                await Dispatcher.InvokeAsync(() => _mainWindow?.SetBackgroundStatus($"Favorite applied · {item.Date}"));
        }
        catch (Exception exception)
        {
            ErrorReporter.Log(exception, "Favorite rotation");
            _trayIcon?.ShowBalloonTip(4000, "Favorite rotation failed", exception.Message, ToolTipIcon.Warning);
        }
        finally { _rotationGate.Release(); }
    }

    private void ShowMainWindow()
    {
        if (_library is null || _apod is null) return;
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow(_library, _apod, _settings);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        _dailyTimer?.Dispose();
        _favoriteTimer?.Dispose();
        _trayIcon?.Dispose();
        if (_mainWindow is not null) _mainWindow.AllowClose = true;
        _mainWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dailyTimer?.Dispose();
        _favoriteTimer?.Dispose();
        _trayIcon?.Dispose();
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
