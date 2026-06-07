using HarmonyLib;
using SandBox.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(ProdigalSonIssueBehavior))]
internal class DisableProdigalSonIssueBehavior
{
    [HarmonyPatch(nameof(ProdigalSonIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
