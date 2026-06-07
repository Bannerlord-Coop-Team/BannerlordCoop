using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(CommonTownsfolkCampaignBehavior))]
internal class DisableCommonTownsfolkCampaignBehavior
{
    [HarmonyPatch(nameof(CommonTownsfolkCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
