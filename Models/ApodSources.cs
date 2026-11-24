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

    public static string Description(string source) => source switch
    {
        Api => "Queries NASA's structured APOD API for the requested date. This is precise and fast, but NASA limits the shared DEMO_KEY; your own free key is recommended.",
        Article => "Searches NASA's dated article archive, then extracts a trusted NASA-hosted still image. This is the best fallback for historical dates.",
        Page => "Reads the current NASA APOD webpage. It can recover today's image when the API is unavailable, but it does not search older dates.",
        _ => $"Uses the {source} image source."
    };
}
