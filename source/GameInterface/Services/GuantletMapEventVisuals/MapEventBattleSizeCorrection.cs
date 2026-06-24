using GameInterface.Services.GuantletMapEventVisuals.Patches;
using SandBox.GauntletUI.Map;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.GuantletMapEventVisuals;

/// <summary>
/// Client-side correction for a field-battle map event's ambient battle sound.
///
/// On a client a map event's visual can initialize before its sides and the parties within them have
/// finished syncing in. When that happens the vanilla <see cref="GauntletMapEventVisual"/>
/// ambient sound is set up with the smallest <c>battle_size</c> - the #1426 guard defaults it to 0 to
/// avoid dereferencing un-synced state - and because the vanilla init is one-shot the size would stay
/// stuck there. This re-applies the real <c>battle_size</c> to the already-created sound as the sides
/// and parties stream in, so a large battle ends up with the correct ambient size.
/// </summary>
internal static class MapEventBattleSizeCorrection
{
    // Pending field-battle visuals whose Initialize ran before the battle size was computable, keyed by the
    // visual itself - a plain reference-identity key, so no StringId / network id (that's the object
    // manager's job), and stable unlike the MapEvent whose hash is a mutable MBGUID. The value is the
    // highest battle_size applied so far: seeded with the size Initialize applied and re-applied as the
    // sides/parties stream in but only ever upward, because a map event is computable while only partly
    // populated and the live headcount counts healthy members (which falls as troops die), so re-applying
    // without a ceiling would lock in a partial count or drift below vanilla mid-battle. Ratchet up to the
    // full roster's size and hold it, matching vanilla which sets it once at the peak. Cleared when the
    // visual is torn down (battle ends) or the session resets.
    private static readonly ConcurrentDictionary<GauntletMapEventVisual, int> pendingMaxSize = new ConcurrentDictionary<GauntletMapEventVisual, int>();

    public static void Register(GauntletMapEventVisual visual)
    {
        var mapEvent = visual?.MapEvent;
        if (mapEvent == null) return;

        // Seed the ceiling with the size Initialize just applied: the real bucket if the battle was already
        // computable (possibly a partial roster), else the #1426 stopgap's 0. TryCorrect only raises it, so
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
