using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Common.Util;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// Per-client headless mirror of <see cref="Mission"/>. Holds a skip-constructor Mission "shell" (what
/// <c>Mission.Current</c> returns while this client is active, under the <see cref="MissionEngineFixture"/>
/// shims) plus the mirrored agent collection. The coop battle controllers call the real engine
/// (<c>Mission.Current.SpawnAgent</c>, <c>agent.Health</c>, …); the shims route those onto this object.
/// </summary>
public sealed class MockMission
{
    private static readonly ConditionalWeakTable<Mission, MockMission> ByShell = new();

    /// <summary>The skip-constructor <see cref="Mission"/> that <c>Mission.Current</c> returns for this client.</summary>
    public Mission Shell { get; }

    public Agent MainAgent { get; set; }
    public bool EndMissionCalled { get; set; }

    /// <summary>Per-side teams, returned by the <c>Mission.AttackerTeam</c>/<c>DefenderTeam</c> shims so the
    /// reinforcement spawn (which resolves the team by side) can field troops into them headless.</summary>
    public MockTeam AttackerTeam { get; } = new MockTeam(BattleSideEnum.Attacker);
    public MockTeam DefenderTeam { get; } = new MockTeam(BattleSideEnum.Defender);
    public MockTeam AttackerAllyTeam => attackerAllyTeam;
    public MockTeam DefenderAllyTeam => defenderAllyTeam;
    public Mission.TeamCollection Teams { get; }

    /// <summary>The local player's team, returned by the <c>Mission.PlayerTeam</c> shim (the non-host retreat
    /// despawn filters by its side). Null until a test assigns one of the side teams.</summary>
    public MockTeam PlayerTeam { get; set; }

    private readonly Dictionary<int, Agent> agentsByIndex = new();
    private int nextIndex;
    private MockTeam attackerAllyTeam;
    private MockTeam defenderAllyTeam;

    // Mirror of Mission._missilesDictionary keys. Mission.OnAgentHit indexes that dictionary for missile blows
    // (the lookup that threw KeyNotFound when an unsynced projectile's index was applied on the owner). The
    // RegisterBlow shim models that lookup against this set so the harness reproduces / guards that bug class.
    private readonly HashSet<int> missiles = new();

    public IReadOnlyCollection<Agent> Agents => agentsByIndex.Values;

    public void RegisterMissile(int index) => missiles.Add(index);
    public bool HasMissile(int index) => missiles.Contains(index);

    public MockMission()
    {
        Shell = ObjectHelper.SkipConstructor<Mission>();
        Teams = new Mission.TeamCollection(Shell);
        ByShell.AddOrUpdate(Shell, this);
    }

    public Team AddTeam(BattleSideEnum side)
    {
        if (side == BattleSideEnum.Attacker)
            return AddTeam(AttackerTeam, ref attackerAllyTeam);
        if (side == BattleSideEnum.Defender)
            return AddTeam(DefenderTeam, ref defenderAllyTeam);
        return null;
    }

    private Team AddTeam(MockTeam mainTeam, ref MockTeam allyTeam)
    {
        var team = mainTeam;
        if (Teams.Contains(mainTeam.Shell))
        {
            allyTeam ??= new MockTeam(mainTeam.Side);
            team = allyTeam;
        }
        if (!Teams.Contains(team.Shell))
            ((List<Team>)Teams).Add(team.Shell);
        return team.Shell;
    }

    /// <summary>Resolve the mock that owns a given Mission shell (used by the Mission member shims).</summary>
    public static bool ForShell(Mission shell, out MockMission mock) => ByShell.TryGetValue(shell, out mock);

    /// <summary>While true, each <see cref="SpawnAgent"/> also mints a linked horse — models the engine
    /// spawning a cavalry rider's mount implicitly (from its equipment) inside the same SpawnAgent call.</summary>
    public bool SpawnMounted { get; set; }

    /// <summary>Headless replacement for <see cref="Mission.SpawnAgent"/>: mints a skip-ctor agent, mirrors the
    /// build data, assigns a mission-local index, and tracks it.</summary>
    public Agent SpawnAgent(AgentBuildData buildData)
    {
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var mirror = new MirrorAgent
        {
            Index = nextIndex++,
            Controller = buildData.AgentController,
            Character = buildData.AgentCharacter,
            Team = buildData.AgentTeam,
            Position = buildData.AgentInitialPosition ?? default,
            Origin = buildData.AgentOrigin,
            Mission = Shell,
        };
        AgentMirror.Bind(agent, mirror);
        agentsByIndex[mirror.Index] = agent;
        if (SpawnMounted) SpawnMount(agent);
        return agent;
    }

    /// <summary>Mints a horse agent, optionally seated under <paramref name="rider"/>. Like the engine's
    /// implicit cavalry mounts it has NO character (<c>Agent.Character</c> null) and Controller None.</summary>
    public Agent SpawnMount(Agent rider = null)
    {
        var horse = ObjectHelper.SkipConstructor<Agent>();
        var mirror = new MirrorAgent
        {
            Index = nextIndex++,
            Controller = AgentControllerType.None,
            IsMount = true,
            Mission = Shell,
        };
        AgentMirror.Bind(horse, mirror);
        agentsByIndex[mirror.Index] = horse;

        if (rider != null && AgentMirror.TryGet(rider, out var riderMirror))
        {
            riderMirror.MountAgent = horse;
            mirror.RiderAgent = rider;
            mirror.Position = riderMirror.Position;
        }
        return horse;
    }

    /// <summary>Remove an agent from the active mission view without changing its mirrored state. Used to model
    /// a rider that exists and is linked to its built mount but has not reached Mission.BuildAgent yet.</summary>
    public bool UntrackAgent(Agent agent)
    {
        if (!AgentMirror.TryGet(agent, out var mirror)) return false;
        return agentsByIndex.Remove(mirror.Index);
    }

    public Agent FindAgentWithIndex(int index) => agentsByIndex.TryGetValue(index, out var a) ? a : null;
}
