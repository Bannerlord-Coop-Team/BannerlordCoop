#if DEBUG
using GameInterface.Services.Battles.Messages;
using HarmonyLib;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Missions.Battles;

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(
    typeof(MissionMainAgentController),
    nameof(MissionMainAgentController.OnPreMissionTick),
    new[] { typeof(float) })]
internal static class BattleGuardFixtureInputPatch
{
    private static void Prefix()
    {
        if (TryGetNativeFixtureInput(
                out _,
                out Agent agent,
                out BattleGuardFixtureDirection direction))
        {
            BattleGuardFixtureNativeDefendInput.Inject(agent, direction);
        }
    }

    private static void Postfix()
    {
        if (!TryGetNativeFixtureInput(
                out CoopBattleController controller,
                out Agent agent,
                out BattleGuardFixtureDirection direction))
        {
            controller?.ApplyGuardFixturePlayerInput();
            return;
        }

        BattleGuardFixtureNativeDefendInput.Inject(agent, direction);
        controller.ApplyGuardFixturePlayerInput();
        BattleGuardFixtureNativeDefendInput.Inject(agent, direction);
    }

    internal static bool TryGetNativeFixtureInput(
        out CoopBattleController controller,
        out Agent agent,
        out BattleGuardFixtureDirection direction)
    {
        controller = Mission.Current?
            .GetMissionBehavior<CoopBattleController>();
        agent = Mission.Current?.MainAgent;
        direction = BattleGuardFixtureDirection.Up;
        return controller != null &&
            controller.TryGetGuardFixtureNativePlayerDefendDirection(
                agent,
                out direction);
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(
    typeof(InputContext),
    nameof(InputContext.IsGameKeyDown),
    new[] { typeof(int) })]
internal static class BattleGuardFixtureDefendKeyPatch
{
    // Vanilla ControlTick reads game key 10 before GetDefendMovementFlag.
    internal const int DefendGameKey = 10;

    private static void Postfix(int gameKey, ref bool __result)
    {
        if (__result || gameKey != DefendGameKey)
            return;

        bool hasFixtureInput =
            BattleGuardFixtureInputPatch.TryGetNativeFixtureInput(
                out _,
                out _,
                out _);
        __result = ShouldReportDefendGameKeyDown(
            gameKey,
            __result,
            hasFixtureInput);
    }

    internal static bool ShouldReportDefendGameKeyDown(
        int gameKey,
        bool isDown,
        bool hasFixtureInput)
    {
        return isDown ||
            (gameKey == DefendGameKey && hasFixtureInput);
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(Agent), nameof(Agent.GetDefendMovementFlag))]
internal static class BattleGuardFixtureDefendDirectionPatch
{
    private static void Postfix(
        Agent __instance,
        ref Agent.MovementControlFlag __result)
    {
        bool hasFixtureInput =
            BattleGuardFixtureInputPatch.TryGetNativeFixtureInput(
                out _,
                out Agent fixtureAgent,
                out BattleGuardFixtureDirection direction) &&
            ReferenceEquals(__instance, fixtureAgent);
        __result = ResolveDefendMovementFlag(
            __result,
            hasFixtureInput,
            direction);
    }

    internal static Agent.MovementControlFlag ResolveDefendMovementFlag(
        Agent.MovementControlFlag nativeFlag,
        bool hasFixtureInput,
        BattleGuardFixtureDirection direction)
    {
        return hasFixtureInput
            ? BattleGuardFixture.GetDefendDirectionFlag(direction)
            : nativeFlag;
    }
}

internal static class BattleGuardFixtureNativeDefendInput
{
    // The SHA gate keeps these v1.4.7 offsets from writing another layout.
    internal const int AgentDefendDirectionOffset = 0x834;
    internal const int AgentInputPointerOffset = 0xAB8;
    internal const int DefendRightWeightOffset = 0x1184394;
    internal const int DefendLeftWeightOffset = 0x1184398;
    internal const int DefendDownWeightOffset = 0x118439C;
    internal const int DefendUpWeightOffset = 0x11843A0;
    internal const int ControllerDefendDirectionOffset = 0x1184A50;
    private const int ActiveWeightBits = 0x3F800000;
    private const string ExpectedNativeSha256 =
        "f46493715a0d92558da9dc922cb17824d3f9afde3b5359b2498fa4935c669751";
    private static bool nativeBinaryValidated;

    internal static void Inject(
        Agent agent,
        BattleGuardFixtureDirection direction)
    {
        ValidateNativeBinary();
        ResolvePointers(agent, out IntPtr agentPointer, out IntPtr inputPointer);

        Marshal.WriteInt32(inputPointer, DefendUpWeightOffset, 0);
        Marshal.WriteInt32(inputPointer, DefendDownWeightOffset, 0);
        Marshal.WriteInt32(inputPointer, DefendLeftWeightOffset, 0);
        Marshal.WriteInt32(inputPointer, DefendRightWeightOffset, 0);
        Marshal.WriteInt32(
            inputPointer,
            GetActiveWeightOffset(direction),
            ActiveWeightBits);

        int cacheValue = GetCacheValue(direction);
        Marshal.WriteInt32(
            inputPointer,
            ControllerDefendDirectionOffset,
            cacheValue);
        Marshal.WriteInt32(
            agentPointer,
            AgentDefendDirectionOffset,
            cacheValue);
    }

    private static void ValidateNativeBinary()
    {
        if (nativeBinaryValidated)
            return;

        string managedDirectory =
            Path.GetDirectoryName(typeof(Agent).Assembly.Location);
        string nativePath =
            Path.Combine(managedDirectory, "TaleWorlds.Native.dll");
        string actualSha256;
        using (FileStream stream = File.OpenRead(nativePath))
        using (SHA256 sha256 = SHA256.Create())
        {
            actualSha256 = BitConverter
                .ToString(sha256.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        if (!string.Equals(
                actualSha256,
                ExpectedNativeSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Guard fixture native layout requires TaleWorlds.Native.dll " +
                $"{ExpectedNativeSha256}, but found {actualSha256}.");
        }

        nativeBinaryValidated = true;
    }

    private static void ResolvePointers(
        Agent agent,
        out IntPtr agentPointer,
        out IntPtr inputPointer)
    {
        if (agent == null)
            throw new ArgumentNullException(nameof(agent));

        UIntPtr nativeAgent = agent.GetPtr();
        if (nativeAgent == UIntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Guard fixture agent has no native pointer.");
        }

        agentPointer = new IntPtr(
            unchecked((long)nativeAgent.ToUInt64()));
        inputPointer = Marshal.ReadIntPtr(
            agentPointer,
            AgentInputPointerOffset);
        if (inputPointer == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Guard fixture agent has no native input pointer.");
        }
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

#endif
