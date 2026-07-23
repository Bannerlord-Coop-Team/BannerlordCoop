using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the GarrisonAutoRecruitmentIsEnabled changes in a town.
/// </summary>
public readonly struct TownGarrisonAutoRecruitmentIsEnabledChanged : ICommand
{
    public readonly Town Town;
    public readonly bool GarrisonAutoRecruitmentIsEnabled;

    public TownGarrisonAutoRecruitmentIsEnabledChanged(Town town, bool garrisonAutoRecruitmentIsEnabled)
    {
        Town = town;
        GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
    }
}