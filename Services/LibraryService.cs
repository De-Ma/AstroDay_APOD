using System.Globalization;
using System.IO;
using System.Text.Json;
using AstroDay.Models;

namespace AstroDay.Services;

public sealed class LibraryService : IDisposable
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly AppSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<ApodItem> _items = [];
    private bool _hasLoadedIndex;
    private string? _indexStamp;
    private FileSystemWatcher? _watcher;
    private DateTimeOffset _ignoreLibraryChangeUntil;
    private const long MaximumImageBytes = 100L * 1024 * 1024;

    public LibraryService(string directory, string settingsPath, AppSettings settings)
    {
        DirectoryPath = directory;
        _settingsPath = settingsPath;
        _settings = settings;
        ResetWatcher();
    }

    public string DirectoryPath { get; private set; }
    public string? LastLoadWarning { get; private set; }
    public event EventHandler? FilesChangedExternally;
    private string IndexPath => Path.Combine(DirectoryPath, "library.json");

    public async Task<IReadOnlyList<ApodItem>> GetItemsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await LoadIndexAsync();
            return _items.OrderByDescending(item => item.Date).ToArray();
        }
        finally { _gate.Release(); }
    }

    public async Task<ApodItem?> GetByDateAsync(string date)
    {
        var items = await GetItemsAsync();
        return items.FirstOrDefault(item => item.Date == date);
    }

    public async Task<ApodItem> SaveDownloadAsync(ApodItem item, Stream image, CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParseExact(item.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidDataException("The APOD record contains an invalid date.");
        if (!Uri.TryCreate(item.ImageUrl, UriKind.Absolute, out var imageUri) || imageUri.Scheme != Uri.UriSchemeHttps || !IsTrustedImageHost(imageUri.Host))
            throw new InvalidDataException("The APOD image URL must use HTTPS on a NASA host.");

        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            await LoadIndexAsync();
            var extension = Path.GetExtension(imageUri.AbsolutePath);
            if (extension.Length is < 2 or > 6 || extension.Any(character => !char.IsLetterOrDigit(character) && character != '.'))
                extension = ".jpg";
            var filePath = Path.Combine(DirectoryPath, $"{item.Date}{extension.ToLowerInvariant()}");
            var temporaryPath = Path.Combine(DirectoryPath, $".{item.Date}.{Guid.NewGuid():N}.download");
            try
            {
                await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    var buffer = new byte[65536];
                    long totalBytes = 0;
                    int bytesRead;
                    while ((bytesRead = await image.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                    {
                        totalBytes += bytesRead;
                        if (totalBytes > MaximumImageBytes)
                            throw new InvalidDataException("The APOD image exceeds AstroDay's 100 MB download limit.");
                        await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    }
                    if (totalBytes == 0)
                        throw new InvalidDataException("NASA returned an empty image.");
                    await output.FlushAsync(cancellationToken);
                }

                File.Move(temporaryPath, filePath, true);
                item.FilePath = filePath;
                item.FileSizeBytes = new FileInfo(filePath).Length;
                item.UpdatedAtUtc = DateTimeOffset.UtcNow;
                var old = _items.FirstOrDefault(existing => existing.Date == item.Date);
                if (old is not null) item.IsFavorite = old.IsFavorite;
                var staleFile = old is not null && !string.Equals(old.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ? old.FilePath : null;
                var previousItems = _items;
                _items = [.. _items.Where(existing => existing.Date != item.Date), item];
                try { await SaveIndexAsync(); }
                catch
                {
                    _items = previousItems;
                    throw;
                }
                if (staleFile is not null && IsInsideLibrary(staleFile))
                {
                    try { File.Delete(staleFile); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                return item;
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<ApodItem> SaveTextOnlyAsync(ApodItem item)
    {
        if (!DateOnly.TryParseExact(item.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidDataException("The APOD record contains an invalid date.");
        item.FilePath = "";
        item.ImageUrl = "";
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            await LoadIndexAsync();
            var old = _items.FirstOrDefault(existing => existing.Date == item.Date);
            if (old is not null) item.IsFavorite = old.IsFavorite;
            var previousItems = _items;
            _items = [.. _items.Where(existing => existing.Date != item.Date), item];
            try { await SaveIndexAsync(); }
            catch
            {
                _items = previousItems;
                throw;
            }
            if (old is { HasImage: true } && IsInsideLibrary(old.FilePath))
            {
                try { File.Delete(old.FilePath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return item;
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(string date)
    {
        await _gate.WaitAsync();
        try
        {
            await LoadIndexAsync();
            var item = _items.FirstOrDefault(entry => entry.Date == date);
            if (item is null) return;
            _items = _items.Where(entry => entry.Date != date).ToList();
            await SaveIndexAsync();
            if (item.HasImage && IsInsideLibrary(item.FilePath))
            {
                try { File.Delete(item.FilePath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<bool?> ToggleFavoriteAsync(string date)
    {
        await _gate.WaitAsync();
        try
        {
            await LoadIndexAsync();
            var item = _items.FirstOrDefault(entry => entry.Date == date);
            if (item is null) return null;
            item.IsFavorite = !item.IsFavorite;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await SaveIndexAsync();
            return item.IsFavorite;
        }
        finally { _gate.Release(); }
    }

    public async Task SetLibraryDirectoryAsync(string newDirectory)
    {
        var target = Path.GetFullPath(newDirectory);
        var source = Path.GetFullPath(DirectoryPath);
        if (string.Equals(target, source, StringComparison.OrdinalIgnoreCase)) return;

        await _gate.WaitAsync();
        try
        {
            await LoadIndexAsync();
            Directory.CreateDirectory(target);
            foreach (var item in _items)
            {
                if (string.IsNullOrWhiteSpace(item.FilePath)) continue;
                var destination = Path.Combine(target, Path.GetFileName(item.FilePath));
                if (!string.Equals(Path.GetFullPath(item.FilePath), destination, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(item.FilePath))
                    {
                        if (File.Exists(destination))
                            File.Move(destination, CreatePreservedPath(destination), false);
                        File.Copy(item.FilePath, destination, false);
                    }
                }
                item.FilePath = destination;
            }
            DirectoryPath = target;
            ResetWatcher();
            _settings.LibraryDirectory = target;
            var targetIndexPath = Path.Combine(target, "library.json");
            if (File.Exists(targetIndexPath))
                File.Move(targetIndexPath, CreatePreservedPath(targetIndexPath), false);
            await SaveIndexAsync();
            await SettingsService.SaveAsync(_settingsPath, _settings);
        }
        finally { _gate.Release(); }
    }

    private static string CreatePreservedPath(string path) =>
        path + ".preserved-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture);

    private async Task LoadIndexAsync()
    {
        Directory.CreateDirectory(DirectoryPath);
        foreach (var item in _items)
            item.RefreshFileState();

        var currentStamp = GetIndexStamp();
        if (_hasLoadedIndex && currentStamp == _indexStamp)
            return;

        LastLoadWarning = null;
        if (!File.Exists(IndexPath))
        {
            if (_hasLoadedIndex && _items.Count > 0)
            {
                LastLoadWarning = "The shared image catalogue is temporarily unavailable. AstroDay kept the last loaded items.";
                return;
            }
            _items = [];
            _hasLoadedIndex = true;
            _indexStamp = null;
            return;
        }

        try
        {
            await using var stream = new FileStream(IndexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            var loadedItems = await JsonSerializer.DeserializeAsync<List<ApodItem>>(stream) ?? [];
            loadedItems = loadedItems.Where(item => item is not null).ToList();
            foreach (var item in loadedItems)
                item.FilePath = ResolveLibraryPath(item.FilePath);
            ReconcileItems(loadedItems);
            _hasLoadedIndex = true;
            _indexStamp = GetIndexStamp();
        }
        catch (JsonException exception)
        {
            if (_hasLoadedIndex && _items.Count > 0)
                LastLoadWarning = $"The shared image catalogue is not readable yet. AstroDay kept the last loaded items. {exception.Message}";
            else
            {
                LastLoadWarning = $"The image catalogue is damaged. AstroDay saved a recovery copy. {exception.Message}";
                PreserveDamagedIndex();
                _items = [];
                _hasLoadedIndex = true;
            }
        }
        catch (IOException exception)
        {
            LastLoadWarning = _hasLoadedIndex
                ? $"The image catalogue is temporarily locked by a sync operation. AstroDay kept the last loaded items. {exception.Message}"
                : $"The image catalogue could not be read yet. AstroDay will retry after the sync operation. {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            LastLoadWarning = _hasLoadedIndex
                ? $"The image catalogue is temporarily inaccessible. AstroDay kept the last loaded items. {exception.Message}"
                : $"The image catalogue could not be accessed. AstroDay will retry when it becomes available. {exception.Message}";
        }
    }

    private async Task SaveIndexAsync()
    {
        var serializableItems = _items.Select(item => new ApodItem
        {
            Date = item.Date,
            Title = item.Title,
            Explanation = item.Explanation,
            FilePath = MakePortablePath(item.FilePath),
            ImageUrl = item.ImageUrl,
            IsFavorite = item.IsFavorite,
            FileSizeBytes = item.FileSizeBytes,
            UpdatedAtUtc = item.UpdatedAtUtc
        }).ToList();
        await AtomicJsonFile.WriteAsync(IndexPath, serializableItems, Options);
        _ignoreLibraryChangeUntil = DateTimeOffset.UtcNow.AddSeconds(1);
        _hasLoadedIndex = true;
        _indexStamp = GetIndexStamp();
    }

    private void ReconcileItems(IReadOnlyList<ApodItem> loadedItems)
    {
        var currentByDate = _items.ToDictionary(item => item.Date, StringComparer.Ordinal);
        var loadedDates = loadedItems.Select(item => item.Date).ToHashSet(StringComparer.Ordinal);
        foreach (var item in currentByDate.Values.Where(item => !loadedDates.Contains(item.Date)).ToArray())
            _items.Remove(item);

        foreach (var loaded in loadedItems)
        {
            if (currentByDate.TryGetValue(loaded.Date, out var current))
            {
                current.Title = loaded.Title;
                current.Explanation = loaded.Explanation;
                current.FilePath = loaded.FilePath;
                current.ImageUrl = loaded.ImageUrl;
                current.IsFavorite = loaded.IsFavorite;
                current.FileSizeBytes = loaded.FileSizeBytes;
                current.UpdatedAtUtc = loaded.UpdatedAtUtc;
                current.RefreshFileState();
            }
            else
            {
                _items.Add(loaded);
            }
        }
        _items.Sort((left, right) => string.CompareOrdinal(right.Date, left.Date));
    }

    private string? GetIndexStamp()
    {
        try
        {
            var info = new FileInfo(IndexPath);
            return info.Exists ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}" : null;
        }
        catch (IOException) { return _indexStamp; }
        catch (UnauthorizedAccessException) { return _indexStamp; }
    }

    private void ResetWatcher()
    {
        _watcher?.Dispose();
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var watcher = new FileSystemWatcher(DirectoryPath)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Changed += LibraryFileChanged;
            watcher.Created += LibraryFileChanged;
            watcher.Deleted += LibraryFileChanged;
            watcher.Renamed += LibraryFileChanged;
            watcher.Error += (_, _) =>
            {
                LastLoadWarning = "AstroDay could not watch this shared folder for changes. Use the normal refresh actions to reload it.";
            };
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (IOException exception)
        {
            LastLoadWarning = $"The shared library folder is temporarily unavailable. {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            LastLoadWarning = $"The shared library folder cannot be watched right now. {exception.Message}";
        }
    }

    private void LibraryFileChanged(object sender, FileSystemEventArgs args)
    {
        if (args.Name is null || args.Name.StartsWith(".library.json.", StringComparison.OrdinalIgnoreCase))
            return;
        if (args.Name.Equals("library.json", StringComparison.OrdinalIgnoreCase) && DateTimeOffset.UtcNow < _ignoreLibraryChangeUntil)
            return;
        FilesChangedExternally?.Invoke(this, EventArgs.Empty);
    }

    private string ResolveLibraryPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return "";
        try
        {
            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(fileName)) return "";
            return Path.GetFullPath(Path.Combine(DirectoryPath, fileName));
        }
        catch (ArgumentException) { return ""; }
        catch (NotSupportedException) { return ""; }
        catch (PathTooLongException) { return ""; }
    }

    private string MakePortablePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return "";
        try
        {
            return IsInsideLibrary(filePath) ? Path.GetRelativePath(DirectoryPath, filePath) : Path.GetFileName(filePath);
        }
        catch (ArgumentException) { return ""; }
        catch (NotSupportedException) { return ""; }
        catch (PathTooLongException) { return ""; }
    }

    private bool IsInsideLibrary(string filePath)
    {
        try
        {
            var root = Path.GetFullPath(DirectoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(filePath);
            return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (PathTooLongException) { return false; }
    }

    private static bool IsTrustedImageHost(string host) =>
        host.Equals("nasa.gov", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".nasa.gov", StringComparison.OrdinalIgnoreCase);

    private void PreserveDamagedIndex()
    {
        if (!File.Exists(IndexPath)) return;
        var backupPath = $"{IndexPath}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        try { File.Move(IndexPath, backupPath, false); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
        _gate.Dispose();
    }
}
