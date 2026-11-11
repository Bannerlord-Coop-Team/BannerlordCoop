using GameInterface.Services.MobilePartyAIs.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Runs the player clan reactions of <see cref="AiPartyThinkBehavior"/> for co-op player clans. Its listeners
/// are only registered on the server (<see cref="AiEngagePartyBehaviorPatches"/>).
/// </summary>
[HarmonyPatch(typeof(AiPartyThinkBehavior))]
internal class AiPartyThinkBehaviorPatches
{
    // Vanilla only holds the lords chasing the main party, which the server doesn't have on the map.
    [HarmonyPatch(nameof(AiPartyThinkBehavior.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void OnClanChangedKingdomPostfix(Clan clan)
    {
        if (!ContainerProvider.TryResolve<IAiPartyThinkBehaviorInterface>(out var aiPartyThinkBehaviorInterface)) return;

        aiPartyThinkBehaviorInterface.HoldPartiesChasingPlayerParties(clan);
    }
}
