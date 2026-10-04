namespace AstroDay.Models;

public static class ApodSources
{
    public const string Api = "Api";
    public const string Article = "Article";
    public const string Page = "Page";

    public static readonly string[] All = [Api, Article, Page];

    public static string DisplayName(string source) => source switch
    {
        Api => "NASA API (api key)",
        Article => "NASA article search",
        Page => "NASA webpage (today only)",
        _ => source
    };
}
