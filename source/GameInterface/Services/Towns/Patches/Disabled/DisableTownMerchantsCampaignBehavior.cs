using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(TownMerchantsCampaignBehavior))]
internal class DisableTownMerchantsCampaignBehavior
{
    [HarmonyPatch(nameof(TownMerchantsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
