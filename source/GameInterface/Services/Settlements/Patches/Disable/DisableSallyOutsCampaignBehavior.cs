using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(SallyOutsCampaignBehavior))]
internal class DisableSallyOutsCampaignBehavior
{
    [HarmonyPatch(nameof(SallyOutsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
