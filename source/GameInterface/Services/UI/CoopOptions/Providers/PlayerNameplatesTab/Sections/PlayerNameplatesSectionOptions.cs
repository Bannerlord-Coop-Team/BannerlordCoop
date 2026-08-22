using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;

/// <summary>Stores the client's optional nameplate preference.</summary>
public class PlayerNameplatesSectionOptions
{
    public const bool DefaultShowPlayerNameplates = true;

    [JsonPropertyName("showPlayerNameplates")]
    public bool? ShowPlayerNameplates { get; set; }

    public bool GetShowPlayerNameplatesOrDefault()
    {
        return ShowPlayerNameplates ?? DefaultShowPlayerNameplates;
    }
}
