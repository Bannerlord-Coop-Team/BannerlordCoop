using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(AiLandBanditPatrollingBehavior))]
internal class DisableAiBanditPatrollingBehavior
{
    [HarmonyPatch(nameof(AiLandBanditPatrollingBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
