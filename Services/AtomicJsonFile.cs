using System.IO;
using System.Text.Json;

namespace AstroDay.Services;

public static class AtomicJsonFile
{
    public static async Task WriteAsync<T>(string path, T value, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            const int attempts = 4;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporaryPath, path, true);
                    break;
                }
                catch (IOException) when (attempt < attempts)
                {
                    await Task.Delay(attempt * 100, cancellationToken);
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
