using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Battles.Patches.Disable;

[HarmonyPatch(typeof(CampaignBattleRecoveryBehavior))]
internal class DisableCampaignBattleRecoveryBehavior
{
    [HarmonyPatch(nameof(CampaignBattleRecoveryBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
