using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(AiVisitSettlementBehavior))]
internal class DisableAiVisitSettlementBehavior
{
    [HarmonyPatch(nameof(AiVisitSettlementBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
