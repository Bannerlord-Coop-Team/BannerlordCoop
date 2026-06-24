using GameInterface.Services.GuantletMapEventVisuals.Patches;
using SandBox.GauntletUI.Map;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.GuantletMapEventVisuals;

/// <summary>
/// Client-side correction for a field-battle map event's ambient battle sound.
///
/// On a client a map event's visual can initialize before its sides and the parties within them have
/// finished syncing in (issue #1449). When that happens the vanilla <see cref="GauntletMapEventVisual"/>
/// ambient sound is set up with the smallest <c>battle_size</c> - the #1426 guard defaults it to 0 to
/// avoid dereferencing un-synced state - and because the vanilla init is one-shot the size would stay
/// stuck there. This re-applies the real <c>battle_size</c> to the already-created sound as the sides
/// and parties stream in, so a large battle ends up with the correct ambient size.
/// </summary>
internal static class MapEventBattleSizeCorrection
{
    // Pending field-battle map events, keyed by the registry StringId (a stable id - the MapEvent object's
    // hash is its mutable MBGUID, so it makes a poor key) and valued by the highest battle_size applied so
    // far. The value is seeded with the size Initialize applied and re-applied as the sides/parties stream
    // in, but only ever upward: a map event is computable (and its size readable) while only partly
    // populated, and the live headcount counts healthy members (which falls as troops die), so re-applying
    // without a ceiling would lock in a partial count or drift below vanilla mid-battle. Ratchet up to the
    // full roster's size and hold it, matching vanilla which sets it once at the peak. Cleared when the
    // visual is torn down (battle ends) or the session resets.
    private static readonly ConcurrentDictionary<string, int> pendingMaxSize = new ConcurrentDictionary<string, int>();

    public static void Register(MapEvent mapEvent)
    {
        if (mapEvent?.StringId == null) return;

        // Seed the ceiling with the size Initialize just applied: the real bucket if the battle was already
        // computable (possibly a partial roster), else the #1426 stopgap's 0. TryCorrect only raises it, so
        // a later message can't push the size below what Initialize already set.
        var initialSize = GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent)
            ? ComputeBattleSize(mapEvent.GetNumberOfInvolvedMen())
            : 0;
        pendingMaxSize.TryAdd(mapEvent.StringId, initialSize);
    }

    public static void Clear(MapEvent mapEvent)
    {
        if (mapEvent?.StringId != null) pendingMaxSize.TryRemove(mapEvent.StringId, out _);
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
        if (mapEvent?.StringId == null || !pendingMaxSize.TryGetValue(mapEvent.StringId, out var appliedSize)) return;
        if (!GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent)) return;

        var size = ComputeBattleSize(mapEvent.GetNumberOfInvolvedMen());
        if (size <= appliedSize) return;

        if (mapEvent.MapEventVisual is not GauntletMapEventVisual visual) return;

        // Initialize creates the sound; if the visual-init message has not been applied yet there is
        // nothing to correct (that first Initialize computes the real size directly once sides are ready).
        var soundEvent = visual._mapEventSoundEvent;
        if (soundEvent == null || !soundEvent.IsValid) return;

        soundEvent.SetParameter("battle_size", (float)size);
        pendingMaxSize[mapEvent.StringId] = size;
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
