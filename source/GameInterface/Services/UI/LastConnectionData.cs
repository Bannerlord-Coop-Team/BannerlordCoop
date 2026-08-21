using System.Text.Json.Serialization;

namespace GameInterface.Services.UI;

public class LastConnectionData
{
    public const string TabId = "Connection";
    public const string SectionId = "LastSession";

    [JsonPropertyName("directIp")] public string DirectIp { get; set; }
    [JsonPropertyName("directPort")] public string DirectPort { get; set; }
    [JsonPropertyName("directPassword")] public string DirectPassword { get; set; }
    [JsonPropertyName("steamLobbyId")] public ulong SteamLobbyId { get; set; }
    [JsonPropertyName("steamLobbyHostName")] public string SteamLobbyHostName { get; set; }
}
