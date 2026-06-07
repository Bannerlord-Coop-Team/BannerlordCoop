using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(ExtortionByDesertersIssueBehavior))]
internal class DisableExtortionByDesertersIssueBehavior
{
    [HarmonyPatch(nameof(ExtortionByDesertersIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
