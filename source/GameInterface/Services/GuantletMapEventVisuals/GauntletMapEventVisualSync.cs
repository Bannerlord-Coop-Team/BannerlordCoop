using GameInterface.AutoSync;
using HarmonyLib;
using SandBox.GauntletUI.Map;

namespace GameInterface.Services.GuantletMapEventVisuals;

internal class GauntletMapEventVisualSync : IAutoSync
{
    public GauntletMapEventVisualSync(AutoSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.WorldPosition)));
    }
}
