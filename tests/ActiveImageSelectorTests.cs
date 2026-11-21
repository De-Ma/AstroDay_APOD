using AstroDay.Models;
using AstroDay.Services;
using Xunit;

namespace AstroDay.Tests;

public sealed class ActiveImageSelectorTests
{
    [Fact]
    public void Select_UsesFeaturedItemOnTodayTabDespiteStaleLibrarySelection()
    {
        var staleLibraryItem = new ApodItem { Date = "2026-11-16" };
        var featuredItem = new ApodItem { Date = "2026-11-17" };

        var selected = ActiveImageSelector.Select(false, staleLibraryItem, featuredItem);

        Assert.Same(featuredItem, selected);
    }

    [Fact]
    public void Select_UsesLibraryItemOnLibraryTab()
    {
        var libraryItem = new ApodItem { Date = "2026-11-16" };
        var featuredItem = new ApodItem { Date = "2026-11-17" };

        var selected = ActiveImageSelector.Select(true, libraryItem, featuredItem);

        Assert.Same(libraryItem, selected);
    }
}
