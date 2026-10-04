using System.Windows.Media;

namespace AstroDay.Services;

public static class ThemeService
{
    private static readonly IReadOnlyDictionary<string, string[]> Palettes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Mint"] = ["#101619", "#192226", "#26353A", "#F1F5F2", "#B5C3BF", "#9ADBC7", "#14231F"],
        ["Sky"] = ["#101619", "#192226", "#26353A", "#F1F5F2", "#B5C3BF", "#86BDEB", "#14202C"],
        ["Amber"] = ["#101619", "#192226", "#26353A", "#F1F5F2", "#B5C3BF", "#F2BE72", "#2A2115"],
        ["Coral"] = ["#101619", "#192226", "#26353A", "#F1F5F2", "#B5C3BF", "#F08D82", "#2A1919"],
        ["Violet"] = ["#101619", "#192226", "#26353A", "#F1F5F2", "#B5C3BF", "#B8A6F0", "#1D1A2B"],
        ["Lime"] = ["#101619", "#192226", "#26353A", "#F1F5F2", "#B5C3BF", "#B5E07A", "#1D2512"]
    };

    public static IReadOnlyCollection<string> Names => Palettes.Keys.ToArray();

    public static string NormalizeName(string? name) => name?.ToLowerInvariant() switch
    {
        "aurora" or "mint" => "Mint",
        "eclipse" or "sky" => "Sky",
        "solar" or "amber" => "Amber",
        "coral" => "Coral",
        "violet" => "Violet",
        "lime" => "Lime",
        _ => "Mint"
    };

    public static void Apply(string? name)
    {
        if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => Apply(name));
            return;
        }

        var selected = Palettes[NormalizeName(name)];
        var keys = new[] { "CanvasBrush", "PanelBrush", "RaisedBrush", "TextBrush", "MutedBrush", "AccentBrush", "AccentSoftBrush" };
        for (var index = 0; index < keys.Length; index++)
            System.Windows.Application.Current.Resources[keys[index]] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(selected[index]));
    }
}
