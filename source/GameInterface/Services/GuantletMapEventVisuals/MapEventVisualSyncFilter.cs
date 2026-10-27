using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.GuantletMapEventVisuals;

/// <summary>
/// Picks the battle visuals AutoSync can skip without logging when they have no network handle.
/// </summary>
public static class MapEventVisualSyncFilter
{
    // GauntletMapEventVisual is the only registered visual. The others (the dedicated server's creator,
    // MapEventBattleFactory's fallback) don't render anything, and they are matched by type rather than by
    // name because the released dedicated server is obfuscated.
    public static bool IsHeadless(IMapEventVisual visual) => visual is not GauntletMapEventVisual;
}
