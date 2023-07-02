using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(AiPatrollingBehavior))]
internal class DisableAiPatrollingBehavior
{
    [HarmonyPatch(nameof(AiPatrollingBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
