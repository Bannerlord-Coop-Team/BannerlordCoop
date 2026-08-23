using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace GameInterface.Services.UI;

/// <summary>Persisted data for the last direct-connection and Steam lobby the player joined.</summary>
public class LastConnectionData
{
    public const string TabId = "Connection";
    public const string SectionId = "LastSession";

    [JsonPropertyName("directIp")] public string DirectIp { get; set; }
    [JsonPropertyName("directPort")] public string DirectPort { get; set; }
    [JsonPropertyName("directPasswordProtected")] public string DirectPasswordProtected { get; set; }
    [JsonPropertyName("steamLobbyId")] public ulong SteamLobbyId { get; set; }
    [JsonPropertyName("steamLobbyHostName")] public string SteamLobbyHostName { get; set; }
    [JsonPropertyName("steamLobbyPasswordProtected")] public string SteamLobbyPasswordProtected { get; set; }

    internal static string ProtectPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return null;
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    internal static string UnprotectPassword(string blob)
    {
        if (string.IsNullOrEmpty(blob)) return string.Empty;
        try
        {
            var decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(blob), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch { return string.Empty; }
    }
}
