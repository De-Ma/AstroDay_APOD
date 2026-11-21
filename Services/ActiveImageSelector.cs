using AstroDay.Models;

namespace AstroDay.Services;

public static class ActiveImageSelector
{
    public static ApodItem? Select(bool libraryTabActive, ApodItem? libraryItem, ApodItem? featuredItem) =>
        libraryTabActive ? libraryItem ?? featuredItem : featuredItem;
}
