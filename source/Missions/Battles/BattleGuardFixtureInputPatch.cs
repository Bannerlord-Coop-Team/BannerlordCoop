#if DEBUG
using GameInterface.Services.Battles.Messages;
using HarmonyLib;
using System;
using System.Runtime.InteropServices;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Missions.Battles;

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
internal static class BattleGuardFixtureControlPatch
{
    private static bool Prefix()
    {
        CoopBattleController controller = Mission.Current?
            .GetMissionBehavior<CoopBattleController>();
        if (controller?.IsGuardFixtureDrivingPlayerInput() != true)
            return true;

        if (!controller.ShouldRunGuardFixtureNativePlayerControlTick())
            return false;

        Agent agent = Mission.Current.MainAgent;
        if (controller.TryGetGuardFixtureNativePlayerDefendDirection(
                agent,
                out BattleGuardFixtureDirection direction))
            BattleGuardFixtureNativeDefendInput.Inject(agent, direction);

        return true;
    }
}

internal static class BattleGuardFixtureNativeDefendInput
{
    // v1.4.7 caches defend input in both the agent and mission input buffer.
    private const int AgentDefendDirectionOffset = 0x834;
    private const int AgentInputPointerOffset = 0xAB8;
    private const int DefendRightWeightOffset = 0x1184394;
    private const int DefendLeftWeightOffset = 0x1184398;
    private const int DefendDownWeightOffset = 0x118439C;
    private const int DefendUpWeightOffset = 0x11843A0;
    private const int ControllerDefendDirectionOffset = 0x1184A50;
    private const int ActiveWeightBits = 0x3F800000;

    internal static void Inject(
        Agent agent,
        BattleGuardFixtureDirection direction)
    {
        if (agent == null)
            throw new ArgumentNullException(nameof(agent));

        UIntPtr nativeAgent = agent.GetPtr();
        if (nativeAgent == UIntPtr.Zero)
            throw new InvalidOperationException(
                "Guard fixture agent has no native pointer.");

        IntPtr agentPointer = new IntPtr(
            unchecked((long)nativeAgent.ToUInt64()));
        IntPtr inputPointer = Marshal.ReadIntPtr(
            agentPointer,
            AgentInputPointerOffset);
        if (inputPointer == IntPtr.Zero)
            throw new InvalidOperationException(
                "Guard fixture agent has no native input pointer.");

        int cacheValue = GetCacheValue(direction);
        Marshal.WriteInt32(inputPointer, DefendUpWeightOffset, 0);
        Marshal.WriteInt32(inputPointer, DefendDownWeightOffset, 0);
        Marshal.WriteInt32(inputPointer, DefendLeftWeightOffset, 0);
        Marshal.WriteInt32(inputPointer, DefendRightWeightOffset, 0);
        Marshal.WriteInt32(
            inputPointer,
            GetActiveWeightOffset(direction),
            ActiveWeightBits);
        Marshal.WriteInt32(
            inputPointer,
            ControllerDefendDirectionOffset,
            cacheValue);
        Marshal.WriteInt32(
            agentPointer,
            AgentDefendDirectionOffset,
            cacheValue);
    }

    internal static int GetCacheValue(
        BattleGuardFixtureDirection direction)
    {
        switch (direction)
        {
            case BattleGuardFixtureDirection.Up:
                return 0;
            case BattleGuardFixtureDirection.Down:
                return 1;
            case BattleGuardFixtureDirection.Left:
                return 2;
            case BattleGuardFixtureDirection.Right:
                return 3;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    null);
        }
    }

    internal static int GetActiveWeightOffset(
        BattleGuardFixtureDirection direction)
    {
        switch (direction)
        {
            case BattleGuardFixtureDirection.Up:
                return DefendUpWeightOffset;
            case BattleGuardFixtureDirection.Down:
                return DefendDownWeightOffset;
            case BattleGuardFixtureDirection.Left:
                return DefendLeftWeightOffset;
            case BattleGuardFixtureDirection.Right:
                return DefendRightWeightOffset;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    null);
        }
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(
    typeof(InputContext),
    nameof(InputContext.IsGameKeyDown),
    new[] { typeof(int) })]
internal static class BattleGuardFixtureBlockInputPatch
{
    private const int DefendGameKey = 10;

    private static void Postfix(int gameKey, ref bool __result)
    {
        if (gameKey != DefendGameKey)
            return;

        if (Mission.Current?
            .GetMissionBehavior<CoopBattleController>()?
            .IsGuardFixtureHoldingNativePlayerBlock() == true)
        {
            __result = true;
        }
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(
    typeof(MissionMainAgentController),
    nameof(MissionMainAgentController.OnPreMissionTick),
    new[] { typeof(float) })]
internal static class BattleGuardFixtureInputPatch
{
    private static void Postfix()
    {
        // The fixture input must be installed before native consumes control flags.
        Mission.Current?
            .GetMissionBehavior<CoopBattleController>()?
            .ApplyGuardFixturePlayerInput();
    }
}

#endif
