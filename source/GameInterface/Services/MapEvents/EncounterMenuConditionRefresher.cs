using Common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
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
        if (menuContext?.GameMenu?.StringId != "encounter")
            return;

        // The encounter can hold the event before the main party's own attach lands (and the other way round).
        if (MobileParty.MainParty?.MapEvent != mapEvent && PlayerEncounter.Battle != mapEvent)
            return;

        if (NeedsEncounterMenuReinit(mapEvent))
        {
            // The menu opened after the client's battle request timed out, so it was built without a battle and
            // its option conditions dereference PlayerEncounter.Battle. Re-entering it runs vanilla's
            // game_menu_encounter_on_init, which adopts MainParty.MapEvent through PlayerEncounter.Init().
            GameMenu.SwitchToMenu("encounter");
            return;
        }

        Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(menuContext);
    }

    internal static bool NeedsEncounterMenuReinit(MapEvent mapEvent)
        => PlayerEncounter.Current != null &&
           PlayerEncounter.Battle == null &&
           MobileParty.MainParty?.MapEvent == mapEvent;

    public void RefreshForRoster(TroopRoster roster)
    {
        RefreshForMapEvent(roster?.OwnerParty?.MapEvent);
    }
}
