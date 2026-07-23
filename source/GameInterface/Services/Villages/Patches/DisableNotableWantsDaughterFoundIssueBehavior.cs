using HarmonyLib;
using SandBox.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(NotableWantsDaughterFoundIssueBehavior))]
internal class DisableNotableWantsDaughterFoundIssueBehavior
{
    [HarmonyPatch(nameof(NotableWantsDaughterFoundIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
