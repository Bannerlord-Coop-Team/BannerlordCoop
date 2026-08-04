using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages.Retreat;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Routes the break-in's troop losses through the server instead of letting the client charge itself.
/// </summary>
/// <remarks>
/// Vanilla's ApplyBreakIn picks victims with a local MBRandom draw and removes them straight out of
/// MobileParty.MainParty's roster (spreading across the army when the party leads one). Co-op has no route
/// for that at all - the client mutated its own roster and replicated nothing, so every other machine kept
/// the pre-break-in troops. The client now announces the intent and the server applies the authoritative
/// loss, which arrives back as ordinary roster deltas.
///
/// The out parameters are zeroed on the client, so the debrief menu reports no casualties locally even
/// though the server does remove them. That is a display gap, not a state gap - the rosters converge.
/// </remarks>
[HarmonyPatch(typeof(BreakInOutBesiegedSettlementAction))]
internal class BreakInCasualtiesPatch
{
    [HarmonyPatch(nameof(BreakInOutBesiegedSettlementAction.ApplyBreakIn))]
    [HarmonyPrefix]
    private static bool ApplyBreakInPrefix(ref TroopRoster casualties, ref int armyCasualtiesCount)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        casualties = TroopRoster.CreateDummyTroopRoster();
        armyCasualtiesCount = 0;

        var mainParty = MobileParty.MainParty;
        var settlement = mainParty?.CurrentSettlement ?? mainParty?.BesiegedSettlement;
        if (mainParty == null || settlement == null) return false;

        MessageBroker.Instance.Publish(mainParty, new BreakInCasualtiesAttempted(mainParty, settlement));
        return false;
    }
}
