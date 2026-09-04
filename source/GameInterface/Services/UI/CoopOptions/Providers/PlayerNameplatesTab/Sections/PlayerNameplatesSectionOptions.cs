using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;

/// <summary>Stores the client's optional nameplate preference.</summary>
public class PlayerNameplatesSectionOptions
{
    public const PlayerNameplatesDisplayMode DefaultDisplayMode = PlayerNameplatesDisplayMode.Always;

    [JsonPropertyName("playerNameplatesDisplayMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlayerNameplatesDisplayMode? DisplayMode { get; set; }

    public PlayerNameplatesDisplayMode GetDisplayModeOrDefault()
    {
        return DisplayMode ?? DefaultDisplayMode;
    }
}
