using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobilePartyAIs.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Adapts <see cref="AiPartyThinkBehavior"/> to co-op player parties. Its listeners are only registered on the
/// server (<see cref="AiEngagePartyBehaviorPatches"/>).
/// </summary>
[HarmonyPatch(typeof(AiPartyThinkBehavior))]
internal class AiPartyThinkBehaviorPatches
{
    // Skips the hourly AI decision for client-driven player parties, and for AI parties in a player's map event
    // so a siege or raid attacker leader can't pick a new target and finalize the battle under the player.
    [HarmonyPatch(nameof(AiPartyThinkBehavior.PartyHourlyAiTick))]
    [HarmonyPrefix]
    private static bool PartyHourlyAiTickPrefix(MobileParty mobileParty) =>
        mobileParty == null || (!mobileParty.IsPlayerParty() && !mobileParty.MapEvent.ContainsPlayerParty());

    // Vanilla only holds the lords chasing the main party, which the server doesn't have on the map.
    [HarmonyPatch(nameof(AiPartyThinkBehavior.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void OnClanChangedKingdomPostfix(Clan clan)
    {
        if (!ContainerProvider.TryResolve<IAiPartyThinkBehaviorInterface>(out var aiPartyThinkBehaviorInterface)) return;

        aiPartyThinkBehaviorInterface.HoldPartiesChasingPlayerParties(clan);
    }
}
