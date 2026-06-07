using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Battles.Patches.Disable;

[HarmonyPatch(typeof(BattleCampaignBehavior))]
internal class DisableBattleCampaignBehavior
{
    [HarmonyPatch(nameof(BattleCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
