using AstroDay.Models;
using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class RecentImageFinderTests
{
    [Fact]
    public async Task FindLatestAsync_ReturnsFirstRecentStillImage()
    {
        var requestedDates = new List<DateOnly>();
        var today = new DateOnly(2026, 11, 21);

        var result = await RecentImageFinder.FindLatestAsync(today, (date, _) =>
        {
            requestedDates.Add(date);
            return Task.FromResult<ApodItem?>(date == today.AddDays(-2)
                ? new ApodItem { Date = "2026-11-19", FilePath = "image.jpg" }
                : new ApodItem { Date = date.ToString("yyyy-MM-dd"), FilePath = "" });
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("2026-11-19", result.Date);
        Assert.Equal([today, today.AddDays(-1), today.AddDays(-2)], requestedDates);
    }

    [Fact]
    public async Task FindLatestAsync_StopsAfterFiveDaysOfLookback()
    {
        var requestedDates = new List<DateOnly>();
        var today = new DateOnly(2026, 11, 21);

        var result = await RecentImageFinder.FindLatestAsync(today, (date, _) =>
        {
            requestedDates.Add(date);
            return Task.FromResult<ApodItem?>(null);
        }, TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(6, requestedDates.Count);
        Assert.Equal(today.AddDays(-5), requestedDates[^1]);
    }
}
