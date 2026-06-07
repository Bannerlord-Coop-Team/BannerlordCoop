using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(FiefBarterBehavior))]
internal class DisableFiefBarterBehavior
{
    [HarmonyPatch(nameof(FiefBarterBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
