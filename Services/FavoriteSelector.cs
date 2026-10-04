using AstroDay.Models;

namespace AstroDay.Services;

public static class FavoriteSelector
{
    public static ApodItem? ChooseRandom(IReadOnlyCollection<ApodItem> items, string? lastFavoritePath, Random? random = null)
    {
        var favorites = items.Where(item => item.IsFavorite && item.HasImage).ToArray();
        if (favorites.Length == 0) return null;

        var candidates = favorites.Length > 1
            ? favorites.Where(item => !string.Equals(item.FilePath, lastFavoritePath, StringComparison.OrdinalIgnoreCase)).ToArray()
            : favorites;
        var chooser = random ?? Random.Shared;
        return candidates[chooser.Next(candidates.Length)];
    }
}
