using Common.Messaging;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab;

/// <summary>
/// Provides the Coop Options tab for the mission map time display
/// </summary>
public class MapTimeOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "MapTimeTab";
    public const string TabName = "Map Time";
    
    public string Id => TabId;
    public bool IsAvailable => true;

    public CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker messageBroker,
        Action<CoopOptionsTabVM> onSelect)
    {
        return new CoopOptionsTabVM(
            Id,
            TabName,
            new CoopOptionsSectionVM[]
            {
                new MapTimeSection(GetShowMapTimeInMissionsOrDefault(options))
            },
            onSelect);
    }

    public static bool GetShowMapTimeInMissionsOrDefault(CoopOptionsData options)
    {
        var sectionOptions =
            (options ?? new CoopOptionsData()).GetSectionOrDefault(
                TabId,
                MapTimeSection.SectionId,
                new MapTimeSectionOptions());
        return sectionOptions.GetShowMapTimeInMissionsOrDefault();
    }
}
