using HarmonyLib;
using SandBox.Issues;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(ProdigalSonIssueBehavior))]
internal class DisableProdigalSonIssueBehavior
{
    [HarmonyPatch(nameof(ProdigalSonIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
