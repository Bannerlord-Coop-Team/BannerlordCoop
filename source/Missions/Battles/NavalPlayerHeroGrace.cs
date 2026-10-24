using System.Collections.Generic;
using TaleWorlds.Core;

namespace Missions.Battles;

/// <summary>
/// Vanilla's main-agent grace on a side whose hulls are all out of action, kept for each side's player heroes, so in PvP
/// neither side loses its hulls' fight while one of its players still fights or died less than 20 s ago.
/// </summary>
public sealed class NavalPlayerHeroGrace
{
    /// <summary>Vanilla's <c>NavalBattleEndLogic.MainAgentConsideredDeadDuration</c>.</summary>
    public const float HeroConsideredDeadSeconds = 20f;

    private readonly Dictionary<BattleSideEnum, float> lastLiveHeroAt = new Dictionary<BattleSideEnum, float>();

    /// <summary>Records that <paramref name="side"/> has a live player hero at mission time <paramref name="now"/>.</summary>
    public void ObserveLiveHero(BattleSideEnum side, float now) => lastLiveHeroAt[side] = now;

    /// <summary>
    /// Whether <paramref name="side"/>'s hulls out of action may defeat it at <paramref name="now"/>: no live player hero of it
    /// was seen here, as for an AI side, or none for more than <see cref="HeroConsideredDeadSeconds"/>.
    /// </summary>
    public bool HasExpired(BattleSideEnum side, float now) =>
        !lastLiveHeroAt.TryGetValue(side, out float seenAt) || now - seenAt > HeroConsideredDeadSeconds;
}
