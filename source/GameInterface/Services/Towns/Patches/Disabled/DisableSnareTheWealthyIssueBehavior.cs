using HarmonyLib;
using SandBox.Issues;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(SnareTheWealthyIssueBehavior))]
internal class DisableSnareTheWealthyIssueBehavior
{
    [HarmonyPatch(nameof(SnareTheWealthyIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
