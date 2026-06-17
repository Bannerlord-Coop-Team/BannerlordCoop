using GameInterface.DynamicSync;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.Services.GuantletMapEventVisuals;

internal class GauntletMapEventVisualSync : IDynamicSync
{
    public GauntletMapEventVisualSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.MapEvent)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.WorldPosition)));
    }
}

[HarmonyPatch(typeof(GauntletMapEventVisual))]
class Debug
{
    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(GauntletMapEventVisual));

    [HarmonyPrefix]
    private static void Prefix(GauntletMapEventVisual __instance)
    {
        ;
    }
}
