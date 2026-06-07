using HarmonyLib;
using SandBox.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(NotableWantsDaughterFoundIssueBehavior))]
internal class DisableNotableWantsDaughterFoundIssueBehavior
{
    [HarmonyPatch(nameof(NotableWantsDaughterFoundIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
