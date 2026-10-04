namespace AstroDay.Models;

public sealed class ApodItem
{
    public string Date { get; set; } = "";
    public string Title { get; set; } = "";
    public string Explanation { get; set; } = "";
    // Empty for days without a still image (e.g. videos); only the text is kept.
    public string FilePath { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public bool IsFavorite { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasImage => FilePath.Length > 0;
}
