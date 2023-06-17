using HarmonyLib;
using SandBox.Issues;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(RivalGangMovingInIssueBehavior))]
internal class DisableRivalGangMovingInIssueBehavior
{
    [HarmonyPatch(nameof(RivalGangMovingInIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
