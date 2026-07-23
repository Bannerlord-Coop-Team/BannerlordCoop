using HarmonyLib;
using SandBox.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(RuralNotableInnAndOutIssueBehavior))]
internal class DisableRuralNotableInnAndOutIssueBehavior
{
    [HarmonyPatch(nameof(RuralNotableInnAndOutIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
