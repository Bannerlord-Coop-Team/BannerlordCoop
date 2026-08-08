using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab.Sections;

/// <summary>
/// Provides the Coop Options binding for the mission map time display
/// </summary>
public class MapTimeSection : CoopOptionsSectionVM
{
    public const string SectionId = "MapTimeSection";
    private bool showMapTimeInMissions;

    public MapTimeSection(bool showMapTimeInMissions)
    {
        this.showMapTimeInMissions = showMapTimeInMissions;
    }
    
    public override string Id => SectionId;
    public string TitleText => "Map Time";
    public string DescriptionText => "Configure the map time display shown while inside missions.";
    public string ShowMapTimeInMissionsText => "Show Map Time in Missions";

    [DataSourceProperty]
    public bool ShowMapTimeInMissions
    {
        get => showMapTimeInMissions;
        set
        {
            if (showMapTimeInMissions == value) return;
            showMapTimeInMissions = value;
            OnPropertyChanged(nameof(ShowMapTimeInMissions));
        }
    }

    public override void Apply(string tabId, CoopOptionsData options)
    {
        options.SetSection(
            tabId,
            Id,
            new MapTimeSectionOptions
            {
                ShowMapTimeInMissions = showMapTimeInMissions
            });
    }
}