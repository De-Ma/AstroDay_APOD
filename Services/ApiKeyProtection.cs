using System.Security.Cryptography;
using System.Text;

namespace AstroDay.Services;

public static class ApiKeyProtection
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AstroDay.NASA.APOD.ApiKey.v1");

    public static string Protect(string apiKey)
    {
        var clearText = Encoding.UTF8.GetBytes(apiKey);
        var protectedBytes = ProtectedData.Protect(clearText, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string protectedApiKey)
    {
        var protectedBytes = Convert.FromBase64String(protectedApiKey);
        var clearText = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(clearText);
    }
}
