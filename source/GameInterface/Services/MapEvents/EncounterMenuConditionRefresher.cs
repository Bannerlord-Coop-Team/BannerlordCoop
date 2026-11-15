using Common;
using GameInterface.Services.MapEvents.Extensions;
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

        var encounter = PlayerEncounter.Current;

        // The authoritative attach can land while the joiner is still in join_encounter, so bind before the menu check.
        AdoptJoinedFieldBattleIfNeeded(mapEvent, encounter);

        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (menuContext?.GameMenu?.StringId != "encounter")
            return;

        // The encounter can hold the event before the main party's own attach lands (and the other way round).
        if (MobileParty.MainParty?.MapEvent != mapEvent && encounter?._mapEvent != mapEvent)
            return;

        if (mapEvent.IsRaidHostileAction() && !mapEvent.IsFinalized && !mapEvent.HasWinner &&
            encounter?._mapEvent == mapEvent && MobileParty.MainParty?.Party.MapEventSide == mapEvent.AttackerSide &&
            mapEvent.DefenderSide.TroopCount == 0 && mapEvent.MapEventSettlement?.Militia == 0f &&
            Game.Current?.GameStateManager.ActiveState is MapState { MapConversationActive: false })
        {
            // Surrender can arrive after encounter init; wait for both defender and militia replay.
            if (PlayerEncounter.InsideSettlement)
                PlayerEncounter.LeaveSettlement();
            GameMenu.ActivateGameMenu("raiding_village");
            return;
        }

        if (NeedsEncounterMenuReinit(mapEvent, encounter))
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

    private static void AdoptJoinedFieldBattleIfNeeded(MapEvent mapEvent, PlayerEncounter encounter)
    {
        if (encounter?._mapEvent != null)
            return; // Preserve an encounter that is already bound.

        if (encounter?.IsJoinedBattle != true)
            return; // Only a server-authoritative joined battle needs adoption.

        if (MobileParty.MainParty?.MapEvent != mapEvent)
            return; // The event must be the main party's current encounter.

        if (mapEvent.IsSiegeStyleJoin())
            return; // Siege and sally joins use BattleJoinLeaveHandler's re-entry flow.

        // The encounter menu's on_init runs PlayerEncounter.Update(), which nulls _mapEvent while the join is pending.
        encounter._mapEvent = mapEvent;
    }

    internal static bool NeedsEncounterMenuReinit(MapEvent mapEvent, PlayerEncounter encounter)
        => encounter != null &&
           encounter._mapEvent == null &&
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
