using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AstroDay.Models;
using AstroDay.Services;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace AstroDay;

public partial class MainWindow : Window
{
    private readonly LibraryService _library;
    private readonly ApodService _apod;
    private readonly AppSettings _settings;
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDay", "settings.json");
    private ApodItem? _featuredItem;
    private bool _loadingSettings = true;
    private bool _showFavorites;
    private bool _changingDatePicker;

    public MainWindow(LibraryService library, ApodService apod, AppSettings settings)
    {
        InitializeComponent();
        WorkAreaFix.Attach(this);
        _library = library;
        _apod = apod;
        _settings = settings;
        _settings.ThemeName = ThemeService.NormalizeName(settings.ThemeName);
        ThemeService.Apply(_settings.ThemeName);
        Icon = AppIconService.CreateImageSource();
        ApodDatePicker.DisplayDateStart = new DateTime(1995, 6, 16);
        ApodDatePicker.DisplayDateEnd = ApodToday;
        ApodDatePicker.SelectedDate = DateTime.TryParse(settings.SelectedApodDate, out var selectedDate)
            ? selectedDate.Date
            : ApodToday;
        FavoriteCycleMinutesTextBox.Text = settings.FavoriteCycleIntervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        UpdateAccentSwatches();
        LibraryPathText.Text = settings.LibraryDirectory;
        ApiKeyText.Text = settings.ApiKey;
        WallpaperCheck.IsChecked = settings.ApplyWallpaper;
        _settings.WallpaperPlacement = WallpaperService.NormalizePlacement(settings.WallpaperPlacement);
        WallpaperPlacementCombo.SelectedIndex = _settings.WallpaperPlacement == "Fit" ? 1 : 0;
        LockScreenCheck.IsChecked = settings.ApplyLockScreen;
        FavoritesModeToggle.IsChecked = settings.FavoritesOnly;
        CycleFavoritesLockScreenCheck.IsChecked = settings.CycleFavoritesOnLockScreen;
        LoadSourceRows();
        StartupCheck.IsChecked = settings.RunAtStartup;
        CloseToTrayCheck.IsChecked = settings.CloseToTray;
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var creationDate = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "CreationDate")?.Value ?? "Unknown";
        var versionText = $"{version?.Major}.{version?.Minor}.{version?.Build}";
        VersionText.Text = $"v{versionText}";
        AboutText.Text = $"Version {versionText}  ·  Created {creationDate}\nNASA Astronomy Picture of the Day for Windows.";
        UpdateLastCheckedText();
        _loadingSettings = false;
        Closing += MainWindow_Closing;
        Loaded += async (_, _) => await RefreshAsync();
    }

    // NASA publishes by US Eastern time, which can be a day behind the local date.
    private static DateTime ApodToday => ApodParsing.ApodToday(DateTime.UtcNow).ToDateTime(TimeOnly.MinValue);

    private int _refreshVersion;

    private async void FeaturedPreview_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (_featuredItem is { HasImage: true } item)
        {
            try { Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true }); }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Could not open the image", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }
        await OpenApodPageAsync((ApodDatePicker.SelectedDate ?? ApodToday).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    public bool AllowClose { get; set; }

    public void SetImageLoading(bool isLoading) =>
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

    public void SetBackgroundStatus(string message) => StatusText.Text = message;

    public void SetUpdateStatus(string message) => StatusText.Text = message;

    private UpdateCheckResult? _latestUpdate;

    public void SetUpdateResult(UpdateCheckResult? result)
    {
        _latestUpdate = result;
        if (result is null)
        {
            UpdateText.Text = "No published release was found.";
            ViewReleaseButton.Visibility = Visibility.Collapsed;
            DownloadUpdateButton.Visibility = Visibility.Collapsed;
            return;
        }
        UpdateText.Text = result.IsUpdateAvailable
            ? $"{result.ReleaseName} is available. Installed: {result.CurrentVersion}."
            : $"AstroDay {result.CurrentVersion} is up to date.";
        ViewReleaseButton.Visibility = Visibility.Visible;
        DownloadUpdateButton.Visibility = result.IsUpdateAvailable && result.InstallerUrl is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public async Task RefreshAsync()
    {
        var version = ++_refreshVersion;
        UpdateLastCheckedText();
        var items = await _library.GetItemsAsync();
        if (version != _refreshVersion) return;
        var selectedGalleryDate = (GalleryList.SelectedItem as ApodItem)?.Date;
        var visibleItems = _showFavorites ? items.Where(item => item.IsFavorite).ToArray() : items.ToArray();
        GalleryList.ItemsSource = visibleItems;
        LibraryCountText.Text = _showFavorites
            ? $"{visibleItems.Length} favorite{(visibleItems.Length == 1 ? "" : "s")}"
            : $"{visibleItems.Length} image{(visibleItems.Length == 1 ? "" : "s")}";
        AllImagesButton.Background = _showFavorites ? (System.Windows.Media.Brush)FindResource("RaisedBrush") : (System.Windows.Media.Brush)FindResource("AccentBrush");
        AllImagesButton.Foreground = _showFavorites ? (System.Windows.Media.Brush)FindResource("TextBrush") : (System.Windows.Media.Brush)FindResource("CanvasBrush");
        FavoritesFilterButton.Background = _showFavorites ? (System.Windows.Media.Brush)FindResource("AccentBrush") : (System.Windows.Media.Brush)FindResource("RaisedBrush");
        FavoritesFilterButton.Foreground = _showFavorites ? (System.Windows.Media.Brush)FindResource("CanvasBrush") : (System.Windows.Media.Brush)FindResource("TextBrush");
        if (selectedGalleryDate is not null)
            GalleryList.SelectedItem = visibleItems.FirstOrDefault(item => item.Date == selectedGalleryDate);

        var selectedDate = ApodDatePicker.SelectedDate ?? ApodToday;
        var selectedDateText = selectedDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        _featuredItem = items.FirstOrDefault(item => item.Date == selectedDateText);
        if (_featuredItem is null && selectedDate.Date == ApodToday)
        {
            _featuredItem = items.FirstOrDefault();
            if (_featuredItem is not null)
                SetDatePickerSelection(DateTime.ParseExact(_featuredItem.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }
        if (_featuredItem is null)
        {
            FeaturedImage.Source = null;
            FeaturedImage.Visibility = Visibility.Collapsed;
            EmptyImageText.Visibility = Visibility.Visible;
            EmptyImageText.Text = "No still image is available for this date";
            FeaturedTitle.Text = "No still image available";
            FeaturedExplanation.Text = selectedDate.Date == ApodToday
                ? "NASA has not published a still image for today yet. AstroDay will check again automatically."
                : "NASA has no still image published for this date. Try another day.";
            ImageDate.Text = selectedDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            FavoriteButton.Content = "☆  Save to favorites";
            return;
        }

        if (!_featuredItem.HasImage)
        {
            FeaturedImage.Source = null;
            FeaturedImage.Visibility = Visibility.Collapsed;
            EmptyImageText.Visibility = Visibility.Visible;
            EmptyImageText.Text = "No still image for this day (probably a video). Use \"Go to APOD\" to view it on NASA's website.";
            FeaturedTitle.Text = _featuredItem.Title;
            FeaturedExplanation.Text = _featuredItem.Explanation;
            ImageDate.Text = $"NASA ASTRONOMY PICTURE OF THE DAY     /     {_featuredItem.Date}";
            FavoriteButton.Content = "☆  Save to favorites";
            return;
        }

        FeaturedImage.Visibility = Visibility.Visible;
        EmptyImageText.Visibility = Visibility.Collapsed;
        var featuredPath = _featuredItem.FilePath;
        var bitmap = await Task.Run(() =>
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(featuredPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        });
        if (version != _refreshVersion || _featuredItem?.FilePath != featuredPath) return;
        FeaturedImage.Source = bitmap;
        FeaturedTitle.Text = _featuredItem.Title;
        FeaturedExplanation.Text = _featuredItem.Explanation;
        ImageDate.Text = $"NASA ASTRONOMY PICTURE OF THE DAY     /     {_featuredItem.Date}";
        FavoriteButton.Content = _featuredItem.IsFavorite ? "★  Saved to favorites" : "☆  Save to favorites";
        FooterText.Text = $"Library · {items.Count} image{(items.Count == 1 ? "" : "s")} · {Path.GetFileName(_library.DirectoryPath)}";
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Checking NASA…";
        SetImageLoading(true);
        try
        {
            var item = await _apod.DownloadLatestStillImageAsync(CancellationToken.None);
            if (item is not null)
                SetDatePickerSelection(DateTime.ParseExact(item.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            await RefreshAsync();
            if (item is null)
                StatusText.Text = "No still image found in the latest 6 APOD days; keeping the current desktop.";
            else
            {
                StatusText.Text = $"Updated from recent APOD · {item.Date}";
                await ApplyAutomaticSourceAsync(item);
            }
        }
        catch (Exception exception)
        {
            StatusText.Text = "NASA check failed";
            MessageBox.Show(this, exception.Message, "Could not update today's image", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetImageLoading(false);
            _settings.LastCheckedUtc = DateTime.UtcNow;
            UpdateLastCheckedText();
            await SaveSettingsAsync();
        }
    }

    private async void ApodDatePicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || _changingDatePicker || ApodDatePicker.SelectedDate is not DateTime selectedDate) return;
        await LoadApodDateAsync(selectedDate);
    }

    private async Task LoadApodDateAsync(DateTime selectedDate)
    {
        var requestedDate = DateOnly.FromDateTime(selectedDate);
        var requestedDateText = requestedDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        _settings.SelectedApodDate = requestedDateText;
        await SaveSettingsAsync();
        StatusText.Text = $"Loading APOD · {requestedDateText}";
        SetImageLoading(true);
        // Show what we already have for this day right away instead of the previous day's image.
        await RefreshAsync();
        try
        {
            var item = await _library.GetByDateAsync(requestedDateText)
                ?? await _apod.DownloadDateAsync(requestedDate, CancellationToken.None);
            if (ApodDatePicker.SelectedDate?.Date != selectedDate.Date) return;
            if (item is not null && item.Date != requestedDateText)
            {
                var actualDate = DateTime.ParseExact(item.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                SetDatePickerSelection(actualDate);
            }
            await RefreshAsync();
            StatusText.Text = item is null ? "No still image is available for that date" : $"Loaded · {item.Date}";
        }
        catch (Exception exception)
        {
            StatusText.Text = "Could not load selected date";
            await RefreshAsync();
            MessageBox.Show(this, exception.Message, "Could not load this APOD date", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetImageLoading(false); }
    }

    private async void PreviousDay_Click(object sender, RoutedEventArgs e) =>
        await SelectAndLoadDateAsync((ApodDatePicker.SelectedDate ?? ApodToday).AddDays(-1));

    private async void NextDay_Click(object sender, RoutedEventArgs e)
    {
        var nextDate = (ApodDatePicker.SelectedDate ?? ApodToday).AddDays(1);
        if (nextDate.Date <= ApodToday) await SelectAndLoadDateAsync(nextDate);
    }

    private async void Today_Click(object sender, RoutedEventArgs e) => await SelectAndLoadDateAsync(ApodToday);

    private async Task SelectAndLoadDateAsync(DateTime date)
    {
        SetDatePickerSelection(date);
        await LoadApodDateAsync(date.Date);
    }

    private void SetDatePickerSelection(DateTime date)
    {
        _changingDatePicker = true;
        ApodDatePicker.SelectedDate = date.Date;
        _settings.SelectedApodDate = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        _changingDatePicker = false;
        _ = SaveSettingsAsync();
    }

    private void UpdateLastCheckedText()
    {
        LastCheckedText.Text = _settings.LastCheckedUtc is { } lastChecked
            ? $"Last checked · {lastChecked.ToLocalTime():yyyy-MM-dd, HH:mm}"
            : "Not checked yet";
    }

    private void SettingsDrawer_Click(object sender, RoutedEventArgs e) =>
        SettingsDrawer.Visibility = SettingsDrawer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_featuredItem is null) return;
        await _library.ToggleFavoriteAsync(_featuredItem.Date);
        await RefreshAsync();
        (Application.Current as App)?.UpdateFavoriteRotation();
        if (_settings.FavoritesOnly) await ApplyAutomaticSourceAsync();
    }

    private async void GalleryFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string date }) return;
        await _library.ToggleFavoriteAsync(date);
        await RefreshAsync();
        (Application.Current as App)?.UpdateFavoriteRotation();
        if (_settings.FavoritesOnly) await ApplyAutomaticSourceAsync();
    }

    private void GalleryMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static Button? GetMenuOwner(object sender) =>
        (sender as System.Windows.Controls.MenuItem)?.Parent is System.Windows.Controls.ContextMenu { PlacementTarget: Button target } ? target : sender as Button;

    private async void GalleryRemove_Click(object sender, RoutedEventArgs e)
    {
        if (GetMenuOwner(sender)?.Tag is not string dateText) return;
        var item = (await _library.GetItemsAsync()).FirstOrDefault(entry => entry.Date == dateText);
        if (item is null) return;
        var answer = MessageBox.Show(this, $"Remove \"{item.Title}\" ({item.Date}) from the library and delete its file?", "Remove image", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        await _library.RemoveAsync(dateText);
        await RefreshAsync();
        (Application.Current as App)?.UpdateFavoriteRotation();
        StatusText.Text = $"Removed · {dateText}";
        if (string.Equals(_settings.CurrentWallpaperPath, item.FilePath, StringComparison.OrdinalIgnoreCase))
            await ApplyAutomaticSourceAsync();
    }

    private async void GalleryItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: ApodItem item }) return;
        // Double-clicking the card's buttons must not open the image.
        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, sender);
             node = node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.Primitives.ButtonBase) return;

        if (!item.HasImage)
        {
            await OpenApodPageAsync(item.Date);
            return;
        }
        try { Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true }); }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not open the image", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void GalleryOpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (GetMenuOwner(sender)?.Tag is not string dateText) return;
        var item = (await _library.GetItemsAsync()).FirstOrDefault(entry => entry.Date == dateText);
        try
        {
            var arguments = item is { HasImage: true } ? $"/select,\"{item.FilePath}\"" : $"\"{_library.DirectoryPath}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not open the file location", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void GallerySetDesktop_Click(object sender, RoutedEventArgs e)
    {
        if (GetMenuOwner(sender)?.Tag is not string dateText) return;
        var item = (await _library.GetItemsAsync()).FirstOrDefault(entry => entry.Date == dateText);
        if (item is not { HasImage: true }) return;
        try
        {
            WallpaperService.SetDesktopWallpaper(item.FilePath, _settings.WallpaperPlacement);
            _settings.CurrentWallpaperPath = item.FilePath;
            await SaveSettingsAsync();
            StatusText.Text = $"Desktop set · {item.Date}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not set desktop wallpaper", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void GalleryOpenApod_Click(object sender, RoutedEventArgs e)
    {
        if (GetMenuOwner(sender)?.Tag is string dateText)
            await OpenApodPageAsync(dateText);
    }

    private async void TodayOpenApod_Click(object sender, RoutedEventArgs e) =>
        await OpenApodPageAsync((ApodDatePicker.SelectedDate ?? ApodToday).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    private async Task OpenApodPageAsync(string dateText)
    {
        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) return;
        StatusText.Text = $"Finding APOD page · {dateText}";
        try
        {
            var url = await _apod.FindApodPageUrlAsync(date, CancellationToken.None);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            StatusText.Text = $"Opened APOD · {dateText}";
        }
        catch (Exception exception)
        {
            StatusText.Text = "Could not open APOD";
            MessageBox.Show(this, exception.Message, "Could not open the APOD website", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void GalleryReload_Click(object sender, RoutedEventArgs e)
    {
        if (GetMenuOwner(sender)?.Tag is string dateText)
            await ReloadDateAsync(dateText);
    }

    private async void TodayReload_Click(object sender, RoutedEventArgs e) =>
        await ReloadDateAsync((ApodDatePicker.SelectedDate ?? ApodToday).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    private async Task ReloadDateAsync(string dateText)
    {
        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) return;
        StatusText.Text = $"Reloading APOD · {dateText}";
        SetImageLoading(true);
        try
        {
            var item = await _apod.DownloadDateAsync(date, CancellationToken.None, forceRefresh: true);
            await RefreshAsync();
            StatusText.Text = item is null ? "NASA has no still image for that date" : $"Reloaded · {item.Date}";
        }
        catch (Exception exception)
        {
            StatusText.Text = "Reload failed";
            MessageBox.Show(this, exception.Message, "Could not reload this image", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetImageLoading(false); }
    }

    private void GalleryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GalleryList.SelectedItem is ApodItem item)
            StatusText.Text = $"Selected · {item.Date} · {item.Title}";
    }

    private async void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        _showFavorites = false;
        await RefreshAsync();
    }

    private async void ShowFavorites_Click(object sender, RoutedEventArgs e)
    {
        _showFavorites = true;
        await RefreshAsync();
    }

    private async void SetDesktop_Click(object sender, RoutedEventArgs e)
    {
        var item = await GetImageToApplyAsync();
        if (item is null) return;
        try
        {
            WallpaperService.SetDesktopWallpaper(item.FilePath, _settings.WallpaperPlacement);
            _settings.CurrentWallpaperPath = item.FilePath;
            await SaveSettingsAsync();
            StatusText.Text = $"Desktop set · {item.Date}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not set desktop wallpaper", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SetLockScreen_Click(object sender, RoutedEventArgs e)
    {
        var item = await GetImageToApplyAsync();
        if (item is null) return;
        try
        {
            await LockScreenService.SetAsync(item.FilePath);
            StatusText.Text = $"Lock screen set · {item.Date}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Windows did not allow the lock-screen image to change. This can depend on package identity, account policy, or device management.\n\n{exception.Message}", "Lock screen unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose where AstroDay stores NASA images",
            SelectedPath = _library.DirectoryPath,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        try
        {
            await _library.SetLibraryDirectoryAsync(dialog.SelectedPath);
            SettingsService.RememberLibraryDirectory(_library.DirectoryPath);
            LibraryPathText.Text = _library.DirectoryPath;
            await RefreshAsync();
            StatusText.Text = "Library location updated";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not move library", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenLibraryFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_library.DirectoryPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = _library.DirectoryPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not open the library folder", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.ApplyWallpaper = WallpaperCheck.IsChecked == true;
        _settings.ApplyLockScreen = LockScreenCheck.IsChecked == true;
        _settings.RunAtStartup = StartupCheck.IsChecked == true;
        try
        {
            if (sender == StartupCheck)
                StartupService.SetEnabled(_settings.RunAtStartup);
            await SaveSettingsAsync();
            StatusText.Text = "Settings saved";
            (Application.Current as App)?.UpdateFavoriteRotation();
            if (sender == WallpaperCheck && WallpaperCheck.IsChecked == true || sender == LockScreenCheck && LockScreenCheck.IsChecked == true)
                await ApplyAutomaticSourceAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text = "Could not save settings";
            MessageBox.Show(this, exception.Message, "Settings could not be applied", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void WallpaperPlacement_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || WallpaperPlacementCombo.SelectedItem is not ComboBoxItem { Tag: string placement }) return;
        _settings.WallpaperPlacement = WallpaperService.NormalizePlacement(placement);
        await SaveSettingsAsync();
        StatusText.Text = _settings.WallpaperPlacement == "Fit"
            ? "Wallpaper placement: fit the whole image"
            : "Wallpaper placement: fill the screen";
        if (_settings.CurrentWallpaperPath is not null && File.Exists(_settings.CurrentWallpaperPath))
        {
            try { WallpaperService.SetDesktopWallpaper(_settings.CurrentWallpaperPath, _settings.WallpaperPlacement); }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Could not update wallpaper placement", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Checking for updates…";
        if (Application.Current is App app)
            await app.CheckForUpdatesAsync(userInitiated: true);
    }

    private void OpenRepository_Click(object sender, RoutedEventArgs e) =>
        OpenWebsite(UpdateCheckResult.RepositoryUrl, "Could not open the AstroDay repository");

    private void ViewRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_latestUpdate is not null)
            OpenWebsite(_latestUpdate.ReleaseUrl, "Could not open the release page");
    }

    private void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_latestUpdate?.InstallerUrl is not null)
            OpenWebsite(_latestUpdate.InstallerUrl, "Could not download the AstroDay installer");
    }

    private void OpenWebsite(string url, string title)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void FavoritesModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.FavoritesOnly = FavoritesModeToggle.IsChecked == true;
        await SaveSettingsAsync();
        (Application.Current as App)?.UpdateFavoriteRotation();
        StatusText.Text = _settings.FavoritesOnly ? "Favorites are now the automatic source" : "NASA's latest image is the automatic source";
        await ApplyAutomaticSourceAsync();
    }

    private readonly List<(string Source, bool Enabled)> _sourceRows = [];

    private void LoadSourceRows()
    {
        _sourceRows.Clear();
        foreach (var source in _settings.SourceOrder.Where(ApodSources.All.Contains).Distinct())
            _sourceRows.Add((source, true));
        foreach (var source in ApodSources.All.Where(source => !_sourceRows.Any(row => row.Source == source)))
            _sourceRows.Add((source, false));
        RenderSourceRows();
    }

    private void RenderSourceRows()
    {
        SourceRows.Children.Clear();
        for (var index = 0; index < _sourceRows.Count; index++)
        {
            var (source, enabled) = _sourceRows[index];
            var rowIndex = index;
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var down = new Button { Content = "\u25BC", Width = 28, MinWidth = 28, Padding = new Thickness(0), IsEnabled = index < _sourceRows.Count - 1, Margin = new Thickness(4, 0, 0, 0) };
            var up = new Button { Content = "\u25B2", Width = 28, MinWidth = 28, Padding = new Thickness(0), IsEnabled = index > 0 };
            down.Click += async (_, _) => await MoveSourceAsync(rowIndex, 1);
            up.Click += async (_, _) => await MoveSourceAsync(rowIndex, -1);
            DockPanel.SetDock(down, Dock.Right);
            DockPanel.SetDock(up, Dock.Right);
            var check = new System.Windows.Controls.Primitives.ToggleButton { IsChecked = enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Style = (Style)FindResource("FavoritesSwitchStyle") };
            DockPanel.SetDock(check, Dock.Right);
            var label = new TextBlock { Text = $"{index + 1}. {ApodSources.DisplayName(source)}", VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0) };
            check.Click += async (_, _) =>
            {
                _sourceRows[rowIndex] = (source, check.IsChecked == true);
                await SaveSourceRowsAsync();
            };
            row.Children.Add(down);
            row.Children.Add(up);
            row.Children.Add(check);
            row.Children.Add(label);
            SourceRows.Children.Add(row);
        }
    }

    private async Task MoveSourceAsync(int index, int offset)
    {
        var target = index + offset;
        if (target < 0 || target >= _sourceRows.Count) return;
        (_sourceRows[index], _sourceRows[target]) = (_sourceRows[target], _sourceRows[index]);
        RenderSourceRows();
        await SaveSourceRowsAsync();
    }

    private async Task SaveSourceRowsAsync()
    {
        _settings.SourceOrder = _sourceRows.Where(row => row.Enabled).Select(row => row.Source).ToList();
        await SaveSettingsAsync();
        StatusText.Text = _settings.SourceOrder.Count == 0
            ? "All image sources are disabled"
            : "Source order: " + string.Join(" → ", _settings.SourceOrder.Select(ApodSources.DisplayName));
    }

    private async void AccentSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings || sender is not Button { Tag: string themeName }) return;
        _settings.ThemeName = themeName;
        ThemeService.Apply(themeName);
        UpdateAccentSwatches();
        await SaveSettingsAsync();
        StatusText.Text = $"{themeName} accent applied";
    }

    private void UpdateAccentSwatches()
    {
        var selected = ThemeService.NormalizeName(_settings.ThemeName);
        _settings.ThemeName = selected;
        var buttons = new[] { MintAccentButton, SkyAccentButton, VioletAccentButton, AmberAccentButton, CoralAccentButton, LimeAccentButton };
        foreach (var button in buttons)
        {
            var isSelected = string.Equals(button.Tag as string, selected, StringComparison.OrdinalIgnoreCase);
            button.BorderBrush = (System.Windows.Media.Brush)FindResource(isSelected ? "TextBrush" : "MutedBrush");
            button.BorderThickness = isSelected ? new Thickness(3) : new Thickness(1);
        }
    }

    private async void SaveApiKey_Click(object sender, RoutedEventArgs e)
    {
        _settings.ApiKey = string.IsNullOrWhiteSpace(ApiKeyText.Text) ? "DEMO_KEY" : ApiKeyText.Text.Trim();
        _settings.ProtectedApiKey = null;
        _settings.ApiKeyUnprotectFailed = false;
        await SaveSettingsAsync();
        StatusText.Text = "NASA API key saved";
    }

    private async Task ApplyAutomaticSourceAsync(ApodItem? preferredItem = null)
    {
        var items = await _library.GetItemsAsync();
        var item = _settings.FavoritesOnly
            ? FavoriteSelector.ChooseRandom(items, _settings.LastFavoritePath)
            : preferredItem is { HasImage: true } ? preferredItem : items.FirstOrDefault(candidate => candidate.HasImage);
        if (item is null)
        {
            if (_settings.FavoritesOnly) StatusText.Text = "Add a favorite before using favorites as the automatic source";
            return;
        }
        if (_settings.FavoritesOnly) _settings.LastFavoritePath = item.FilePath;

        if (_settings.ApplyWallpaper)
        {
            try
            {
                WallpaperService.SetDesktopWallpaper(item.FilePath, _settings.WallpaperPlacement);
                _settings.CurrentWallpaperPath = item.FilePath;
            }
            catch (Exception exception) { StatusText.Text = $"Desktop update failed: {exception.Message}"; }
        }
        if (_settings.ApplyLockScreen && (!_settings.FavoritesOnly || _settings.CycleFavoritesOnLockScreen))
        {
            try { await LockScreenService.SetAsync(item.FilePath); }
            catch (Exception exception) { StatusText.Text = $"Lock screen update unavailable: {exception.Message}"; }
        }
        await SaveSettingsAsync();
    }

    private async Task<ApodItem?> GetImageToApplyAsync()
    {
        if (!_settings.FavoritesOnly) return GetSelectedImage();
        var item = FavoriteSelector.ChooseRandom(await _library.GetItemsAsync(), _settings.LastFavoritePath);
        if (item is null)
        {
            StatusText.Text = "Add a favorite before applying favorites";
            return null;
        }
        _settings.LastFavoritePath = item.FilePath;
        await SaveSettingsAsync();
        StatusText.Text = $"Random favorite selected · {item.Date}";
        return item;
    }

    private ApodItem? GetSelectedImage()
    {
        var item = GalleryList.SelectedItem as ApodItem ?? _featuredItem;
        if (item is { HasImage: false })
        {
            StatusText.Text = "This day has no still image to apply";
            return null;
        }
        return item;
    }

    private Task SaveSettingsAsync() => SettingsService.SaveAsync(_settingsPath, _settings);

    private void Hide_Click(object sender, RoutedEventArgs e) => Close();

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (AllowClose) return;
        e.Cancel = true;
        if (_settings.CloseToTray)
        {
            Hide();
            return;
        }

        AllowClose = true;
        Application.Current.Shutdown();
    }

    private async void FavoriteCycleMinutes_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        if (!int.TryParse(FavoriteCycleMinutesTextBox.Text, out var interval) || interval is < 1 or > 10080)
        {
            FavoriteCycleMinutesTextBox.Text = _settings.FavoriteCycleIntervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            StatusText.Text = "Enter an interval from 1 to 10080 minutes";
            return;
        }
        _settings.FavoriteCycleIntervalMinutes = interval;
        await SaveSettingsAsync();
        (Application.Current as App)?.UpdateFavoriteRotation();
        StatusText.Text = $"Favorite rotation · {_settings.FavoriteCycleIntervalMinutes} min";
    }

    private async void CycleFavoritesLockScreen_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.CycleFavoritesOnLockScreen = CycleFavoritesLockScreenCheck.IsChecked == true;
        await SaveSettingsAsync();
        (Application.Current as App)?.UpdateFavoriteRotation();
        StatusText.Text = _settings.CycleFavoritesOnLockScreen ? "Favorites will also rotate on lock screen" : "Lock screen rotation disabled";
    }

    private async void CloseToTray_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.CloseToTray = CloseToTrayCheck.IsChecked == true;
        await SaveSettingsAsync();
        StatusText.Text = _settings.CloseToTray ? "Closing will minimize to tray" : "Closing will exit AstroDay";
    }
}
