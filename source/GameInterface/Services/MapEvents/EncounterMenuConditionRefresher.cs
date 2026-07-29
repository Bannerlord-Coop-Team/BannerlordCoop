using Common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents;

internal interface IEncounterMenuConditionRefresher
{
    void RefreshForMapEvent(MapEvent mapEvent);
    void RefreshForRoster(TroopRoster roster);
}

internal class EncounterMenuConditionRefresher : IEncounterMenuConditionRefresher
{
    public void RefreshForMapEvent(MapEvent mapEvent)
    {
        if (ModInformation.IsServer || mapEvent == null)
            return;

        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (menuContext?.GameMenu?.StringId != "encounter" || MobileParty.MainParty?.MapEvent != mapEvent)
            return;

        Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(menuContext);
    }

    public void RefreshForRoster(TroopRoster roster)
    {
        RefreshForMapEvent(roster?.OwnerParty?.MapEvent);
    }
}
