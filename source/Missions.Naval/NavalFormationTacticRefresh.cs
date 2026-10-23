using System.Collections.Generic;
using System.Linq;
using NavalDLC.Missions.AI.TeamAI;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Formations a mid-battle hull joined, checked from the naval mission tick, where the async team tick isn't
/// running: one with units whose behaviours a capture turned off gets its team's tactic re-applied on fresh ship lists.
/// </summary>
public interface INavalFormationTacticRefresh
{
    /// <summary>Notes that a hull spawned into <paramref name="formation"/>, from the spawn postfix.</summary>
    void Note(Formation formation);

    /// <summary>[Mission tick] Re-applies the tactic of every noted formation that needs it, then forgets them.</summary>
    void Apply();

    /// <summary>Forgets the noted formations, as the battle ends.</summary>
    void Clear();
}

/// <inheritdoc cref="INavalFormationTacticRefresh"/>
public class NavalFormationTacticRefresh : INavalFormationTacticRefresh
{
    private readonly List<Formation> joined = new List<Formation>();

    /// <summary>The running battle's refresh, which the static spawn postfix notes formations on; null outside a battle.</summary>
    internal static INavalFormationTacticRefresh Active { get; set; }

    /// <summary>How many formations wait for the next check (diagnostics, tests).</summary>
    internal int Noted => joined.Count;

    // Each formation once per tick.
    public void Note(Formation formation)
    {
        if (formation != null && !joined.Contains(formation)) joined.Add(formation);
    }

    // Checks every noted formation, then starts the next tick's list empty.
    public void Apply()
    {
        if (joined.Count == 0) return;

        foreach (var formation in joined) Refresh(formation);
        joined.Clear();
    }

    // Nothing noted outlives the battle.
    public void Clear() => joined.Clear();

    // The tactic re-weights formations only when their count or order changes, which swimmers already in the formation hide,
    // and walks a ship list cached 5 s; so the lists expire, as vanilla's ManageFormationCounts does, and the tactic re-applies.
    private static void Refresh(Formation formation)
    {
        if (formation.AI == null || formation.Team?.TeamAI is not TeamAINavalComponent teamAi) return;
        if (!NeedsTacticReapply(formation.CountOfUnits, formation.AI._behaviors.Any(behavior => behavior.WeightFactor > 1E-07f))) return;

        teamAi.TeamNavalQuerySystem.ForceExpireSameSideShipLists();
        if (teamAi.CurrentTactic != null) teamAi.CurrentTactic.IsTacticReapplyNeeded = true;
    }

    /// <summary>Whether a hull joined a formation with units whose behaviours all weigh nothing, as a capture leaves them.</summary>
    internal static bool NeedsTacticReapply(int units, bool anyBehaviorWeighted) => units > 0 && !anyBehaviorWeighted;
}
