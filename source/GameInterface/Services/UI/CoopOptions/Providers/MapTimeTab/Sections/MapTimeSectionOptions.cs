using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab.Sections;

/// <summary>
/// Stores the persisted options for the mission map time overlay
/// </summary>
public class MapTimeSectionOptions
{
    public const bool DefaultShowMapTimeInMissions = true;
    
    [JsonPropertyName("showMapTimeInMissions")]
    public bool? ShowMapTimeInMissions { get; set; }

    public bool GetShowMapTimeInMissionsOrDefault()
    {
        return ShowMapTimeInMissions ?? DefaultShowMapTimeInMissions;
    }
}