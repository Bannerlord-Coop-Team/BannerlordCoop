using HarmonyLib;
using Helpers;

namespace GameInterface.Services.MapEvents.Patches.Disable;

[HarmonyPatch(typeof(MenuHelper))]
internal class DisableEncounterCaptureTheEnemyOnConsequence
{
    [HarmonyPatch(nameof(MenuHelper.EncounterCaptureTheEnemyOnConsequence))]
    [HarmonyPrefix]
    private static bool PrefixEncounterCaptureTheEnemyOnConsequence() => false;
}
