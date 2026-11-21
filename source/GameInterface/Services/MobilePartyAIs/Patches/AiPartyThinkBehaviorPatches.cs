using Common;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobilePartyAIs.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Runs <see cref="AiPartyThinkBehavior"/> on the server only and adapts it to co-op player parties.
/// </summary>
[HarmonyPatch(typeof(AiPartyThinkBehavior))]
internal class AiPartyThinkBehaviorPatches
{
    // Registers the listeners on the server only, which decides for every AI party.
    [HarmonyPatch(nameof(AiPartyThinkBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static bool RegisterEventsPrefix() => ModInformation.IsServer;

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
