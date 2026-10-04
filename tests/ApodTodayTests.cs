using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class ApodTodayTests
{
    [Fact]
    public void ApodToday_IsStillPreviousDay_WhenItIsEarlyMorningInEurope()
    {
        // 2026-10-04 01:30 in Germany (UTC+2) is 2026-10-03 19:30 in New York.
        var utc = new DateTime(2026, 10, 3, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 10, 3), ApodParsing.ApodToday(utc));
    }

    [Fact]
    public void ApodToday_RollsOverAtEasternMidnight()
    {
        // EDT is UTC-4 in October.
        Assert.Equal(new DateOnly(2026, 10, 3), ApodParsing.ApodToday(new DateTime(2026, 10, 4, 3, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 10, 4), ApodParsing.ApodToday(new DateTime(2026, 10, 4, 4, 0, 0, DateTimeKind.Utc)));
    }
}
