using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Common.Util;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// Headless mirror of a <see cref="Team"/> and its per-class <see cref="Formation"/>s. Backs the
/// <c>Team.GetFormation</c> / <c>Formation.SetControlledByAI</c> / <c>Formation.SetMovementOrder</c> shims so
/// the adoption path (<c>ConvertPuppetToHostAi</c> + the Charge order issued on host migration) can run and be
/// asserted. Created by tests; shells are skip-constructor engine objects bound to the mock via side-tables.
/// </summary>
public sealed class MockTeam
{
    private static readonly ConditionalWeakTable<Team, MockTeam> ByShell = new();
    private static readonly ConditionalWeakTable<TeamQuerySystem, MockTeam> ByQuerySystem = new();

    public Team Shell { get; }
    public BattleSideEnum Side { get; }
    /// <summary>How many times <c>Team.ResetTactic</c> ran, the NPC release resets the enemy team's tactic.</summary>
    public int ResetTacticCalls { get; set; }
    public TeamQuerySystem QuerySystem { get; } = ObjectHelper.SkipConstructor<TeamQuerySystem>();

    private readonly Dictionary<FormationClass, MockFormation> formations = new();

    public MockTeam(BattleSideEnum side)
    {
        Side = side;
        Shell = ObjectHelper.SkipConstructor<Team>();
        ByShell.AddOrUpdate(Shell, this);
        ByQuerySystem.AddOrUpdate(QuerySystem, this);
    }

    public static bool ForShell(Team shell, out MockTeam team) => ByShell.TryGetValue(shell, out team);

    public static bool OwnsQuerySystem(TeamQuerySystem querySystem) => ByQuerySystem.TryGetValue(querySystem, out _);

    public MBList<Formation> Formations => new(formations.Values.Select(f => f.Shell));

    public MockFormation GetFormation(FormationClass formationClass)
    {
        if (!formations.TryGetValue(formationClass, out var f))
        {
            f = new MockFormation(this, formationClass);
            formations[formationClass] = f;
        }
        return f;
    }
}

/// <summary>Headless mirror of a <see cref="Formation"/>: records the AI-control flag, the movement order it was
/// given, and its member agents — the state a migration/adoption test asserts.</summary>
public sealed class MockFormation
{
    private static readonly ConditionalWeakTable<Formation, MockFormation> ByShell = new();

    public Formation Shell { get; }
    public MockTeam Team { get; }
    public FormationClass FormationClass { get; }
    public bool IsAIControlled { get; set; }
    /// <summary>Whether <c>SetMovementOrder</c> was issued at all (the migration fix issues one; the live bug was
    /// that none was). Note <see cref="Order"/> stays Invalid headless — the real MovementOrder constants are
    /// engine-populated natives that can't be reproduced without the game.</summary>
    public bool MovementOrderSet { get; set; }
    public MovementOrder.MovementOrderEnum Order { get; set; } = MovementOrder.MovementOrderEnum.Invalid;

    private readonly List<Agent> units = new();
    public int CountOfUnits => units.Count;

    public MockFormation(MockTeam team, FormationClass formationClass)
    {
        Team = team;
        FormationClass = formationClass;
        Shell = ObjectHelper.SkipConstructor<Formation>();
        ByShell.AddOrUpdate(Shell, this);
    }

    public static bool ForShell(Formation shell, out MockFormation formation) => ByShell.TryGetValue(shell, out formation);

    public void AddUnit(Agent agent) => units.Add(agent);
    public IReadOnlyList<Agent> Units => units;
}
