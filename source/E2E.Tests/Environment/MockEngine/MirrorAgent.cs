using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// Mirrored state for a headless (skip-constructor) <see cref="Agent"/>. The engine shims in
/// <see cref="MissionEngineFixture"/> read/write this instead of the native agent, so the coop battle code can
/// spawn, damage, move and kill agents with no live <see cref="Mission"/>. State the harness asserts on.
/// </summary>
public sealed class MirrorAgent
{
    public int Index { get; set; }
    public AgentControllerType Controller { get; set; }
    public float Health { get; set; } = 100f;
    public bool IsActive { get; set; } = true;
    public bool IsHuman { get; set; } = true;
    public bool WasKilled { get; set; }
    public int DeathAction { get; set; } = -1;
    public Vec3 Position { get; set; }
    public BasicCharacterObject Character { get; set; }
    public Team Team { get; set; }
    public Formation Formation { get; set; }
    public IAgentOriginBase Origin { get; set; }
    public Agent MountAgent { get; set; }
    /// <summary>True for a horse agent (mirrors <c>Agent.IsMount</c>); horses keep <see cref="Character"/> null,
    /// like the engine's implicitly spawned cavalry mounts.</summary>
    public bool IsMount { get; set; }
    /// <summary>The rider currently on this (mount) agent; kept in step with the rider's
    /// <see cref="MountAgent"/> by the <c>set_MountAgent</c> shim.</summary>
    public Agent RiderAgent { get; set; }
    /// <summary>The mission this agent was spawned into (its mock's shell) — read by the movement apply path's
    /// <c>agent.Mission != Mission.Current</c> staleness guard.</summary>
    public Mission Mission { get; set; }
    // Networked movement and action state captured from the engine.
    public Vec3 LookDirection { get; set; }
    public Vec2 MovementDirection { get; set; }
    public Vec2 InputVector { get; set; }
    public Agent.MovementControlFlag MovementFlags { get; set; }
    public Agent.EventControlFlag EventControlFlags { get; set; }
    public bool CrouchMode { get; set; }
    public Agent.GuardMode GuardMode { get; set; } = Agent.GuardMode.None;
    public Agent.ActionCodeType Action0CodeType { get; set; } = Agent.ActionCodeType.Idle;
    public Agent.ActionCodeType Action1CodeType { get; set; } = Agent.ActionCodeType.Idle;
    public Agent.UsageDirection Action0Direction { get; set; } = Agent.UsageDirection.None;
    public Agent.UsageDirection Action1Direction { get; set; } = Agent.UsageDirection.None;
    public int Action0Index { get; set; } = -1;
    public int Action1Index { get; set; } = -1;
    public float Action0Progress { get; set; }
    public float Action1Progress { get; set; }
    public AnimFlags Action0Flags { get; set; }
    public AnimFlags Action1Flags { get; set; }
    public int SetActionChannelCalls { get; set; }
    public int LastSetActionChannel { get; set; } = -1;
    public Agent.MovementControlFlag DefendMovementFlag { get; set; }
    public int SetWeaponGuardCalls { get; set; }
    public int ResetGuardCalls { get; set; }
}

/// <summary>
/// Global side-table binding a real (skip-constructor) <see cref="Agent"/> to its <see cref="MirrorAgent"/>.
/// Keyed by the agent instance so it works across clients in one process (each client mints its own agents).
/// </summary>
public static class AgentMirror
{
    private static readonly ConditionalWeakTable<Agent, MirrorAgent> Table = new();

    public static void Bind(Agent agent, MirrorAgent mirror) => Table.AddOrUpdate(agent, mirror);

    public static bool TryGet(Agent agent, out MirrorAgent mirror) => Table.TryGetValue(agent, out mirror);
}
