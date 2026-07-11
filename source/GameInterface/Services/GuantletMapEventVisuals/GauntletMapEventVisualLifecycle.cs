using SandBox.GauntletUI.Map;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.GuantletMapEventVisuals;

/// <summary>
/// Makes local Gauntlet visual teardown idempotent across its own lifetime destroy and the owning
/// MapEvent root fallback. Either packet may arrive first, but UI handlers must observe one end only.
/// </summary>
internal static class GauntletMapEventVisualLifecycle
{
    private static readonly object Gate = new object();
    private static readonly ConditionalWeakTable<GauntletMapEventVisual, object> EndedVisuals =
        new ConditionalWeakTable<GauntletMapEventVisual, object>();

    public static bool TryEnd(IMapEventVisual visual)
    {
        if (visual == null) return false;
        if (visual is not GauntletMapEventVisual gauntletVisual)
        {
            visual.OnMapEventEnd();
            return true;
        }

        lock (Gate)
        {
            if (EndedVisuals.TryGetValue(gauntletVisual, out _)) return false;
            EndedVisuals.Add(gauntletVisual, new object());
        }

        try
        {
            gauntletVisual.OnMapEventEnd();
            return true;
        }
        catch
        {
            lock (Gate)
            {
                EndedVisuals.Remove(gauntletVisual);
            }

            throw;
        }
    }
}
