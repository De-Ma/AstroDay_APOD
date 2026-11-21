using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AstroDay.Services;

public static class WallpaperService
{
    private const uint SpiSetDesktopWallpaper = 0x0014;
    private const uint UpdateProfile = 0x0001;
    private const uint SendChange = 0x0002;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, string value, uint flags);

    public static string NormalizePlacement(string? placement) =>
        string.Equals(placement, "Fit", StringComparison.OrdinalIgnoreCase) ? "Fit" : "Fill";

    public static void SetDesktopWallpaper(string filePath, string? placement = "Fill")
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("The selected image is missing.", filePath);
        var style = NormalizePlacement(placement) == "Fit" ? "6" : "10";
        using (var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true))
        {
            if (desktop is null)
                throw new InvalidOperationException("The Windows desktop personalization settings could not be opened.");
            desktop.SetValue("WallpaperStyle", style, RegistryValueKind.String);
            desktop.SetValue("TileWallpaper", "0", RegistryValueKind.String);
        }
        if (!SystemParametersInfo(SpiSetDesktopWallpaper, 0, filePath, UpdateProfile | SendChange))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Windows could not set the desktop wallpaper.");
    }
}
