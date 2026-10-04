using Microsoft.Win32;

namespace AstroDay.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AstroDay";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Could not determine the AstroDay executable path.");
            key.SetValue(ValueName, $"\"{executable}\" --background");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
