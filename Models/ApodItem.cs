using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AstroDay.Models;

public sealed class ApodItem : INotifyPropertyChanged
{
    private string _date = "";
    private string _title = "";
    private string _explanation = "";
    private string _filePath = "";
    private string _imageUrl = "";
    private bool _isFavorite;
    private bool _lastKnownImageAvailability;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Date
    {
        get => _date;
        set => SetField(ref _date, value);
    }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Explanation
    {
        get => _explanation;
        set => SetField(ref _explanation, value);
    }

    // Empty for days without a still image (e.g. videos); only the text is kept.
    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetField(ref _filePath, value))
            {
                OnPropertyChanged(nameof(HasImage));
                OnPropertyChanged(nameof(IsImageAvailable));
                OnPropertyChanged(nameof(IsImageSyncing));
            }
        }
    }

    public string ImageUrl
    {
        get => _imageUrl;
        set => SetField(ref _imageUrl, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public long? FileSizeBytes { get; set; }

    [JsonIgnore]
    public bool HasImage => FilePath.Length > 0;

    [JsonIgnore]
    public bool IsImageAvailable
    {
        get
        {
            if (!HasImage || !File.Exists(FilePath)) return false;
            if (FileSizeBytes is null or <= 0) return true;
            try { return new FileInfo(FilePath).Length == FileSizeBytes; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    [JsonIgnore]
    public bool IsImageSyncing => HasImage && !IsImageAvailable;

    public void RefreshFileState()
    {
        var isAvailable = IsImageAvailable;
        if (isAvailable == _lastKnownImageAvailability) return;
        _lastKnownImageAvailability = isAvailable;
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(IsImageAvailable));
        OnPropertyChanged(nameof(IsImageSyncing));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
