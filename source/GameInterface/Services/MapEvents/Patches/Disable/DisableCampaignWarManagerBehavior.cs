using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Battles.Patches.Disable;

[HarmonyPatch(typeof(CampaignWarManagerBehavior))]
internal class DisableCampaignWarManagerBehavior
{
    [HarmonyPatch(nameof(CampaignWarManagerBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
