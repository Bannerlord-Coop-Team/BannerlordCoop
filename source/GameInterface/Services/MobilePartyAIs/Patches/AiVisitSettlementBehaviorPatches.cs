using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Runs <see cref="AiVisitSettlementBehavior"/> on the server only.
/// </summary>
[HarmonyPatch(typeof(AiVisitSettlementBehavior))]
internal class AiVisitSettlementBehaviorPatches
{
    // Registers the listeners on the server only, which decides for every AI party.
    [HarmonyPatch(nameof(AiVisitSettlementBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static bool RegisterEventsPrefix() => ModInformation.IsServer;
}
