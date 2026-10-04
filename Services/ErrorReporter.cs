using System.IO;
using System.Text.RegularExpressions;

namespace AstroDay.Services;

public static class ErrorReporter
{
    private const long MaximumLogBytes = 1024 * 1024;
    private static readonly Regex ApiKeyPattern = new(@"(api_key=)[^&\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly object Gate = new();

    public static void Log(Exception exception, string context)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDay", "logs");
            Directory.CreateDirectory(directory);
            var logPath = Path.Combine(directory, "astroday.log");
            lock (Gate)
            {
                var file = new FileInfo(logPath);
                if (file.Exists && file.Length >= MaximumLogBytes)
                {
                    var previousPath = Path.Combine(directory, "astroday.previous.log");
                    File.Move(logPath, previousPath, true);
                }

                var details = ApiKeyPattern.Replace(exception.ToString(), "$1[redacted]");
                File.AppendAllText(logPath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{context}] {details}{Environment.NewLine}");
            }
        }
        catch (Exception logException) when (logException is IOException or UnauthorizedAccessException or ArgumentException)
        {
            System.Diagnostics.Debug.WriteLine($"AstroDay error logging failed: {logException.Message}");
        }
    }
}
