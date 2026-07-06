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
    public bool IsMount { get; set; }
    public Agent RiderAgent { get; set; }
    public Vec3 Position { get; set; }
    public BasicCharacterObject Character { get; set; }
    public Team Team { get; set; }
    public Formation Formation { get; set; }
    public IAgentOriginBase Origin { get; set; }
    public Agent MountAgent { get; set; }
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
