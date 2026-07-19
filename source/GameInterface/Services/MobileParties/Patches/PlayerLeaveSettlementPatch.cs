using Common.Messaging;
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
        if (!ShouldRequestLeave(party))
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
