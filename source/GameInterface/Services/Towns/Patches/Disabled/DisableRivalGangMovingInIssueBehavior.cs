using HarmonyLib;
using SandBox.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(RivalGangMovingInIssueBehavior))]
internal class DisableRivalGangMovingInIssueBehavior
{
    [HarmonyPatch(nameof(RivalGangMovingInIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
