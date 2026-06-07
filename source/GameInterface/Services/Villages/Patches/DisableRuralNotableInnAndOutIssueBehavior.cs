using HarmonyLib;
using SandBox.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(RuralNotableInnAndOutIssueBehavior))]
internal class DisableRuralNotableInnAndOutIssueBehavior
{
    [HarmonyPatch(nameof(RuralNotableInnAndOutIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
