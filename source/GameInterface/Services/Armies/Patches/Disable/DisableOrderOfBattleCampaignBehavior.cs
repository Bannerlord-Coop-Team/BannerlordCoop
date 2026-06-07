using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(OrderOfBattleCampaignBehavior))]
internal class DisableOrderOfBattleCampaignBehavior
{
    [HarmonyPatch(nameof(OrderOfBattleCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
