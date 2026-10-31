using Common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents;

public interface IEncounterMenuConditionRefresher
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

        if (mapEvent.IsRaidHostileAction() && !mapEvent.IsFinalized && !mapEvent.HasWinner &&
            PlayerEncounter.Battle == mapEvent && MobileParty.MainParty?.Party.MapEventSide == mapEvent.AttackerSide &&
            mapEvent.DefenderSide.TroopCount == 0 && mapEvent.MapEventSettlement?.Militia == 0f &&
            Game.Current?.GameStateManager.ActiveState is MapState { MapConversationActive: false })
        {
            // Surrender can arrive after encounter init; wait for both defender and militia replay.
            if (PlayerEncounter.InsideSettlement)
                PlayerEncounter.LeaveSettlement();
            GameMenu.ActivateGameMenu("raiding_village");
            return;
        }

        if (NeedsEncounterMenuReinit(mapEvent))
        {
            // The menu opened after the client's battle request timed out, so it was built without a battle and
            // its option conditions dereference PlayerEncounter.Battle. Re-entering it runs vanilla's
            // game_menu_encounter_on_init, which adopts MainParty.MapEvent through PlayerEncounter.Init().
            GameMenu.SwitchToMenu("encounter");
            return;
        }

        RebuildMenuOptions(menuContext);
    }

    // Rebuilds the visible option list, because a condition refresh alone never shows an option hidden at build time.
    private static void RebuildMenuOptions(MenuContext menuContext)
    {
        // An unbound menu builds its options fresh when its view binds, and RefreshMenuOptions needs a handler.
        if (menuContext.Handler == null)
        {
            Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(menuContext);
            return;
        }

        Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
    }

    internal static bool NeedsEncounterMenuReinit(MapEvent mapEvent)
        => PlayerEncounter.Current != null &&
           PlayerEncounter.Battle == null &&
           MobileParty.MainParty?.MapEvent == mapEvent;

    public void RefreshForRoster(TroopRoster roster)
    {
        var party = roster?.OwnerParty;
        var mapEvent = party?.MapEvent;
        if (mapEvent == null && party?.MobileParty?.PartyComponent is MilitiaPartyComponent militia)
            mapEvent = militia.Settlement?.Party.MapEvent;
        RefreshForMapEvent(mapEvent);
    }
}
