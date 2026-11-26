using System;
using System.Runtime.InteropServices;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// Writes a mirrored agent's state into the skip-constructor <see cref="Agent"/>'s own fields and state pointers. A
/// patched vanilla method runs as Harmony's generated replacement, which the JIT fully optimizes, so it can inline
/// <see cref="Agent"/> getters past the <see cref="MissionEngineFixture"/> shims and read those directly.
/// </summary>
public static class NativeAgentFields
{
    // Process-lifetime cells the flag and state pointers read; a few bytes that are never freed.
    private static readonly UIntPtr HumanFlags = Cell((long)AgentFlag.IsHumanoid);
    private static readonly UIntPtr MountFlags = Cell((long)AgentFlag.Mountable);
    private static readonly UIntPtr ActiveState = Cell((long)AgentState.Active);
    private static readonly UIntPtr KilledState = Cell((long)AgentState.Killed);
    private static readonly UIntPtr UnconsciousState = Cell((long)AgentState.Unconscious);
    private static readonly UIntPtr ArmedHumanFlags = Cell((long)(AgentFlag.IsHumanoid | AgentFlag.CanAttack));
    private static readonly UIntPtr OriginPosition = VectorAtOrigin();
    private static readonly UIntPtr PuppetController = Cell((long)AgentControllerType.None);
    private static readonly UIntPtr AiController = Cell((long)AgentControllerType.AI);
    private static readonly UIntPtr PlayerController = Cell((long)AgentControllerType.Player);

    private static readonly AccessTools.FieldRef<Team, BattleSideEnum> TeamSide =
        AccessTools.FieldRefAccess<Team, BattleSideEnum>("<Side>k__BackingField");

    /// <summary>Backs an active agent's character, health, origin, team and flags with its mirrored values.</summary>
    public static void Back(Agent agent, MirrorAgent mirror)
    {
        agent._character = mirror.Character;
        agent._health = mirror.Health;
        agent.Origin = mirror.Origin;
        agent.Team = mirror.Team;
        if (mirror.Team != null && MockTeam.ForShell(mirror.Team, out var team))
            TeamSide(mirror.Team) = team.Side;
        agent._flagsPointer = mirror.IsMount ? MountFlags : HumanFlags;
        agent._statePointer = ActiveState;
    }

    /// <summary>Backs a headless agent standing at the origin for vanilla's proximity check.</summary>
    public static void BackForProximityCheck(Agent agent, MirrorAgent mirror)
    {
        Back(agent, mirror);
        agent.Mission = mirror.Mission;
        agent._flagsPointer = ArmedHumanFlags;
        agent._positionPointer = OriginPosition;
        agent._controllerTypePointer = mirror.Controller switch
        {
            AgentControllerType.AI => AiController,
            AgentControllerType.Player => PlayerController,
            _ => PuppetController,
        };
    }

    /// <summary>Copies the mirrored health into a backed agent.</summary>
    public static void SyncHealth(Agent agent, MirrorAgent mirror)
    {
        if (IsBacked(agent)) agent._health = mirror.Health;
    }

    /// <summary>Moves a backed agent's native state to the removal state.</summary>
    public static void MarkRemoved(Agent agent, AgentState agentState)
    {
        if (IsBacked(agent))
            agent._statePointer = agentState == AgentState.Killed ? KilledState : UnconsciousState;
    }

    private static bool IsBacked(Agent agent) => agent._statePointer != UIntPtr.Zero;

    private static UIntPtr Cell(long value)
    {
        IntPtr cell = Marshal.AllocHGlobal(sizeof(long));
        Marshal.WriteInt64(cell, value);
        return new UIntPtr((ulong)cell.ToInt64());
    }

    private static UIntPtr VectorAtOrigin()
    {
        IntPtr cell = Marshal.AllocHGlobal(4 * sizeof(float));
        Marshal.WriteInt64(cell, 0);
        Marshal.WriteInt64(cell, sizeof(long), 0);
        return new UIntPtr((ulong)cell.ToInt64());
    }
}
