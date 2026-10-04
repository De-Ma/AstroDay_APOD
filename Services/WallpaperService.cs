using System.IO;
using System.Runtime.InteropServices;

namespace AstroDay.Services;

public static class WallpaperService
{
    private const uint SpiSetDesktopWallpaper = 0x0014;
    private const uint UpdateProfile = 0x0001;
    private const uint SendChange = 0x0002;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, string value, uint flags);

    public static void SetDesktopWallpaper(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("The selected image is missing.", filePath);
        if (!SystemParametersInfo(SpiSetDesktopWallpaper, 0, filePath, UpdateProfile | SendChange))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Windows could not set the desktop wallpaper.");
    }
}
