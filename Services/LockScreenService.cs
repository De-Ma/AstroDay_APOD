using System.IO;
using Windows.Storage;
using Windows.System.UserProfile;

namespace AstroDay.Services;

public static class LockScreenService
{
    public static async Task SetAsync(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("The selected image is missing.", filePath);
        var imageFile = await StorageFile.GetFileFromPathAsync(filePath);
        await LockScreen.SetImageFileAsync(imageFile);
    }
}
