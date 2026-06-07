using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(PlayerArmyWaitBehavior))]
internal class DisablePlayerArmyWaitBehavior
{
    [HarmonyPatch(nameof(PlayerArmyWaitBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
