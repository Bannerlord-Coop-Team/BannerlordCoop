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
    public bool IsAiPaused { get; set; }
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
    // Movement state carried by AgentData (capture reads these, apply writes them back on the puppet).
    public Vec3 LookDirection { get; set; }
    public Vec2 MovementDirection { get; set; }
    public Vec2 InputVector { get; set; }
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
