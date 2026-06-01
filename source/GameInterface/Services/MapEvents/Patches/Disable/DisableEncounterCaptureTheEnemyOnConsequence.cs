using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MapEvents.Patches.Disable;

[HarmonyPatch(typeof(MenuHelper))]
internal class DisableEncounterCaptureTheEnemyOnConsequence
{
    [HarmonyPatch(nameof(MenuHelper.EncounterCaptureTheEnemyOnConsequence))]
    [HarmonyPrefix]
    private static bool PrefixEncounterCaptureTheEnemyOnConsequence() => false;
}
