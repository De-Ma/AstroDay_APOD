using System.Globalization;
using System.IO;
using System.Text.Json;
using AstroDay.Models;

namespace AstroDay.Services;

public sealed class LibraryService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly AppSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<ApodItem> _items = [];
    private const long MaximumImageBytes = 100L * 1024 * 1024;

    public LibraryService(string directory, string settingsPath, AppSettings settings)
    {
        DirectoryPath = directory;
        _settingsPath = settingsPath;
        _settings = settings;
    }

    public string DirectoryPath { get; private set; }
    public string? LastLoadWarning { get; private set; }
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

    public async Task ToggleFavoriteAsync(string date)
    {
        await _gate.WaitAsync();
        try
        {
            await LoadIndexAsync();
            var item = _items.FirstOrDefault(entry => entry.Date == date);
            if (item is null) return;
            item.IsFavorite = !item.IsFavorite;
            await SaveIndexAsync();
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
                if (!File.Exists(item.FilePath)) continue;
                var destination = Path.Combine(target, Path.GetFileName(item.FilePath));
                if (!string.Equals(Path.GetFullPath(item.FilePath), destination, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(destination))
                        File.Move(destination, CreatePreservedPath(destination), false);
                    File.Copy(item.FilePath, destination, false);
                }
                item.FilePath = destination;
            }
            DirectoryPath = target;
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
        LastLoadWarning = null;
        if (!File.Exists(IndexPath)) { _items = []; return; }
        try
        {
            await using var stream = File.OpenRead(IndexPath);
            _items = await JsonSerializer.DeserializeAsync<List<ApodItem>>(stream) ?? [];
            _items = _items.Where(item => item is not null && (!item.HasImage || (IsInsideLibrary(item.FilePath) && File.Exists(item.FilePath)))).ToList();
        }
        catch (JsonException exception)
        {
            LastLoadWarning = $"The image catalogue is damaged. AstroDay saved a recovery copy. {exception.Message}";
            PreserveDamagedIndex();
            _items = [];
        }
    }

    private Task SaveIndexAsync() => AtomicJsonFile.WriteAsync(IndexPath, _items, Options);

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
}
