using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(PlayerVariablesBehavior))]
internal class DisablePlayerVariablesBehavior
{
    [HarmonyPatch(nameof(PlayerVariablesBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
