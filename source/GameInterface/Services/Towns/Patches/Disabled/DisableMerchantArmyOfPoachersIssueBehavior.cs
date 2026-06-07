using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior))]
internal class DisableMerchantArmyOfPoachersIssueBehavior
{
    [HarmonyPatch(nameof(MerchantArmyOfPoachersIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
