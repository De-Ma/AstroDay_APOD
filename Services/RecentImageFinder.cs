using AstroDay.Models;

namespace AstroDay.Services;

public static class RecentImageFinder
{
    public const int MaxLookbackDays = 5;

    public static async Task<ApodItem?> FindLatestAsync(
        DateOnly today,
        Func<DateOnly, CancellationToken, Task<ApodItem?>> load,
        CancellationToken cancellationToken,
        int maxLookbackDays = MaxLookbackDays)
    {
        var boundedLookback = Math.Clamp(maxLookbackDays, 0, MaxLookbackDays);
        for (var offset = 0; offset <= boundedLookback; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = await load(today.AddDays(-offset), cancellationToken);
            if (item is { HasImage: true })
                return item;
        }
        return null;
    }
}
