using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(AiLandBanditPatrollingBehavior))]
internal class DisableAiBanditPatrollingBehavior
{
    [HarmonyPatch(nameof(AiLandBanditPatrollingBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
