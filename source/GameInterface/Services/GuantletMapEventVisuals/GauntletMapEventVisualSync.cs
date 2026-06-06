using GameInterface.DynamicSync;
using HarmonyLib;
using SandBox.GauntletUI.Map;

namespace GameInterface.Services.GuantletMapEventVisuals;

internal class GauntletMapEventVisualSync : IDynamicSync
{
    public GauntletMapEventVisualSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.MapEvent)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.WorldPosition)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.IsVisible)));
    }
}
