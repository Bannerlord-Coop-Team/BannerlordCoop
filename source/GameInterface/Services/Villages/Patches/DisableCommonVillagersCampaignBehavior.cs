using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(CommonVillagersCampaignBehavior))]
internal class DisableCommonVillagersCampaignBehavior
{
    [HarmonyPatch(nameof(CommonVillagersCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
