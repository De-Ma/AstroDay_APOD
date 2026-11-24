using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace AstroDay.Converters;

public sealed class FavoriteLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string mode && mode.Equals("tooltip", StringComparison.OrdinalIgnoreCase))
            return value is true ? "Remove favorite" : "Add favorite";
        return value is true ? "★  Favorite" : "☆  Add favorite";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class FileImageConverter : IValueConverter
{
    // Keyed by path and write time so a reloaded file produces a fresh thumbnail.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Path, long Ticks), System.Windows.Media.Imaging.BitmapImage> Cache = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;
        try
        {
            var key = (path, File.GetLastWriteTimeUtc(path).Ticks);
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
            bitmap.DecodePixelWidth = 420;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            foreach (var stale in Cache.Keys.Where(existing => existing.Path == path).ToArray())
                Cache.TryRemove(stale, out _);
            Cache[key] = bitmap;
            return bitmap;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
