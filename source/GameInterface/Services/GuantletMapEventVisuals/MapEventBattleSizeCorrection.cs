using GameInterface.Services.GuantletMapEventVisuals.Patches;
using SandBox.GauntletUI.Map;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.GuantletMapEventVisuals;

/// <summary>
/// Client-side: re-applies a field battle's ambient <c>battle_size</c> when its visual initialized before
/// the sides/parties synced. Vanilla <see cref="GauntletMapEventVisual"/> bakes the size into the sound once
/// (the guard defaults it to 0 while un-synced and never updates it), so without this a large battle
/// stays too quiet for its whole life. We re-apply the real size to the existing sound as the parties arrive.
/// </summary>
internal static class MapEventBattleSizeCorrection
{
    // Field-battle visuals needing a size correction, keyed by the visual itself. Value = the highest
    // battle_size applied so far; TryCorrect only ever raises it, since a battle is computable while still
    // partly populated and the live headcount drops as troops die, so without a ceiling the size would lock
    // in a partial count or drift below vanilla mid-battle. Cleared when the visual is torn down or on reset.
    private static readonly ConcurrentDictionary<GauntletMapEventVisual, int> pendingMaxSize = new ConcurrentDictionary<GauntletMapEventVisual, int>();

    public static void Register(GauntletMapEventVisual visual)
    {
        var mapEvent = visual?.MapEvent;
        if (mapEvent == null) return;

        // Seed the ceiling with the size Initialize just applied: the real bucket if the battle was already
        // computable (possibly a partial roster), else the stopgap's 0. TryCorrect only raises it, so
        // a later message can't push the size below what Initialize already set.
        var initialSize = GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent)
            ? ComputeBattleSize(mapEvent.GetNumberOfInvolvedMen())
            : 0;
        pendingMaxSize.TryAdd(visual, initialSize);
    }

    public static void Clear(GauntletMapEventVisual visual)
    {
        if (visual != null) pendingMaxSize.TryRemove(visual, out _);
    }

    // Drops all pending corrections - called when the client session ends so the static map does not
    // accumulate stale ids across reconnects.
    public static void Reset() => pendingMaxSize.Clear();

    /// <summary>
    /// Re-applies the real ambient battle size to a pending map event's sound once its sides/parties are
    /// populated enough to compute it, but only when the size has grown. A cheap no-op for any map event
    /// that is not pending.
    /// </summary>
    public static void TryCorrect(MapEvent mapEvent)
    {
        if (mapEvent?.MapEventVisual is not GauntletMapEventVisual visual) return;
        if (!pendingMaxSize.TryGetValue(visual, out var appliedSize)) return;
        if (!GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent)) return;

        var size = ComputeBattleSize(mapEvent.GetNumberOfInvolvedMen());
        if (size <= appliedSize) return;

        // Initialize creates the sound; if the visual-init message has not been applied yet there is
        // nothing to correct (that first Initialize computes the real size directly once sides are ready).
        var soundEvent = visual._mapEventSoundEvent;
        if (soundEvent == null || !soundEvent.IsValid) return;

        soundEvent.SetParameter("battle_size", (float)size);
        pendingMaxSize[visual] = size;
    }

    // Mirrors the field-battle / sally-out buckets of vanilla GauntletMapEventVisual.GetBattleSizeValue.
    // The siege-assault constant does not apply here - siege events never set up the size-driven sound.
    public static int ComputeBattleSize(int numberOfInvolvedMen)
    {
        if (numberOfInvolvedMen < 30) return 0;
        if (numberOfInvolvedMen < 80) return 1;
        if (numberOfInvolvedMen >= 120) return 3;
        return 2;
    }
}
