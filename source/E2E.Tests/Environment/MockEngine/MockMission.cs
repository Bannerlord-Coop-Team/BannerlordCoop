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

    /// <summary>Per-side teams, returned by the <c>Mission.AttackerTeam</c>/<c>DefenderTeam</c> shims so the
    /// reinforcement spawn (which resolves the team by side) can field troops into them headless.</summary>
    public MockTeam AttackerTeam { get; } = new MockTeam(BattleSideEnum.Attacker);
    public MockTeam DefenderTeam { get; } = new MockTeam(BattleSideEnum.Defender);

    private readonly Dictionary<int, Agent> agentsByIndex = new();
    private int nextIndex;

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
        ByShell.AddOrUpdate(Shell, this);
    }

    /// <summary>Resolve the mock that owns a given Mission shell (used by the Mission member shims).</summary>
    public static bool ForShell(Mission shell, out MockMission mock) => ByShell.TryGetValue(shell, out mock);

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
        };
        AgentMirror.Bind(agent, mirror);
        agentsByIndex[mirror.Index] = agent;
        return agent;
    }

    public Agent FindAgentWithIndex(int index) => agentsByIndex.TryGetValue(index, out var a) ? a : null;
}
