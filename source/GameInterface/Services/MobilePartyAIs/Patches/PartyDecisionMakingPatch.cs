using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(AiEngagePartyBehavior))]
internal class AiEngagePartyBehaviorPatches
{
    [HarmonyPatch(nameof(AiEngagePartyBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(AiMilitaryBehavior))]
internal class DisableAiMilitaryBehavior
{
    [HarmonyPatch(nameof(AiMilitaryBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(AiPartyThinkBehavior))]
internal class DisableAiPartyThinkBehavior
{
    [HarmonyPatch(nameof(AiPartyThinkBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(AiPatrollingBehavior))]
internal class DisableAiPatrollingBehavior
{
    [HarmonyPatch(nameof(AiPatrollingBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(AiVisitSettlementBehavior))]
internal class DisableAiVisitSettlementBehavior
{
    [HarmonyPatch(nameof(AiVisitSettlementBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}