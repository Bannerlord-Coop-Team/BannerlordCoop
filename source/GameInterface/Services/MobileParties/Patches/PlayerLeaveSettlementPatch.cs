using Common.Messaging;
#if DEBUG
using GameInterface.Services.GameMenus.Patches;
#endif
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for player pressing the leave settlement button.
/// Allows calling the functionality separately from the button press.
/// </summary>
[HarmonyPatch]
internal class PlayerLeaveSettlementPatch
{
    static IEnumerable<MethodBase> TargetMethods() => new MethodInfo[]
    {
        typeof(PlayerTownVisitCampaignBehavior).GetMethod("game_menu_settlement_leave_on_consequence", BindingFlags.NonPublic | BindingFlags.Static),
        typeof(EncounterGameMenuBehavior).GetMethod("game_menu_castle_outside_leave_on_consequence", BindingFlags.NonPublic | BindingFlags.Instance),
        typeof(EncounterGameMenuBehavior).GetMethod("army_encounter_leave_on_consequence", BindingFlags.NonPublic | BindingFlags.Instance),
        typeof(HideoutCampaignBehavior).GetMethod("game_menu_hideout_leave_on_consequence", BindingFlags.NonPublic | BindingFlags.Instance),
        // A village entered while it is looted opens the "village_looted" menu, whose single option is
        // its own leave route. Without it here the press ran vanilla only: the menu closed and the
        // player regained map control locally, but no request ever reached the server, so the server
        // kept the party inside the settlement and never replicated a leave back — the map party stayed
        // invisible and frozen until coop.unstuck released the settlement server-side by hand.
        AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), "village_looted_leave_on_consequence"),
    };

    private static bool Prefix() => RequestLeave();

    internal static bool RequestLeave()
    {
        var party = MobileParty.MainParty;

        var message = new EndSettlementEncounterAttempted(party);

        MessageBroker.Instance.Publish(party, message);

        return false;
    }
}

[HarmonyPatch(
    typeof(EncounterGameMenuBehavior),
    nameof(EncounterGameMenuBehavior.break_in_leave_consequence))]
internal class PlayerLeaveSiegeEncounterPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        var party = MobileParty.MainParty;
        bool shouldRequestLeave = ShouldRequestLeave(party);
#if DEBUG
        SiegeEncounterMenuTrace.LogLeaveRoute(party, shouldRequestLeave);
#endif
        if (!shouldRequestLeave)
        {
            // Vanilla clears siege and army state after Finish returns. Hold first so ExitToLast
            // cannot recreate the encounter while that state is still active.
            party?.SetMoveModeHold();
            return true;
        }

        return PlayerLeaveSettlementPatch.RequestLeave();
    }

    internal static bool ShouldRequestLeave(MobileParty party) =>
        party != null &&
        party.SiegeEvent == null &&
        (party.Army == null || party.Army.LeaderParty == party);
}
