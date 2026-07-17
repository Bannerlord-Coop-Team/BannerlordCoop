using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.FormationMarker;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(MissionFormationMarkerVM), nameof(MissionFormationMarkerVM.IsEnabled), MethodType.Setter)]
internal class HideTacticalUnitSymbolsPatch
{
    [HarmonyPrefix]
    private static void IsEnabledPrefix(ref bool value)
    {
        if (!value || !TacticalUnitSymbolsSettings.HideTacticalUnitSymbols) return;

        value = false;
    }
}
