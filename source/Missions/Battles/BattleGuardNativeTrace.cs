#if DEBUG
using HarmonyLib;
using GameInterface.Services.Battles.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

internal static class BattleGuardNativeTrace
{
    private const int Capacity = 256;
    private static readonly object Sync = new();
    private static readonly List<string> Records = new(Capacity);
    private static readonly int ProcessId = Process.GetCurrentProcess().Id;
    private static readonly string ProcessRole =
        ReadCommandLineArgument("/platformId")
        ?? (HasCommandLineArgument("/server") ? "server" : "unknown");
    private static readonly string RunToken =
        ReadCommandLineArgument("/cooptestrun") ?? "unscoped";
    private static Agent target;
    private static Guid targetId;
    private static Guid commandId;
    private static long ordinal;
    private static string phase = "unscoped";

    internal static bool IsTarget(Agent agent)
    {
        return ReferenceEquals(Volatile.Read(ref target), agent);
    }

    internal static void SetTarget(
        Agent agent,
        Guid agentId,
        Guid nextCommandId,
        BattleGuardFixtureMode mode,
        BattleGuardFixturePhase fixturePhase,
        BattleGuardFixtureDirection direction)
    {
        if (agent == null)
            return;

        lock (Sync)
        {
            bool commandChanged =
                !ReferenceEquals(target, agent)
                || commandId != nextCommandId;
            Volatile.Write(ref target, agent);
            targetId = agentId;
            commandId = nextCommandId;
            phase = "fixture-command";
            if (commandChanged)
            {
                Records.Clear();
                ordinal = 0;
            }
        }

        Record(
            agent,
            "target",
            $"command={nextCommandId},mode={mode},fixturePhase={fixturePhase},direction={direction}");
    }

    internal static void Stop()
    {
        Agent observedTarget = Volatile.Read(ref target);
        if (observedTarget != null)
            Record(observedTarget, "target-stop");

        lock (Sync)
        {
            Volatile.Write(ref target, null);
            phase = "stopped";
        }
    }

    internal static void Mark(string nextPhase)
    {
        if (Volatile.Read(ref target) == null)
            return;

        Agent observedTarget;
        lock (Sync)
        {
            phase = nextPhase;
            observedTarget = Volatile.Read(ref target);
        }
        if (observedTarget != null)
            Record(observedTarget, "state");
    }

    internal static void Record(
        Agent agent,
        string operation,
        string argument = null)
    {
        if (!IsTarget(agent))
            return;

        string observedPhase;
        lock (Sync)
        {
            if (!ReferenceEquals(Volatile.Read(ref target), agent))
                return;

            observedPhase = phase;
        }

        string state;
        try
        {
            ActionIndexCache action0 = agent.GetCurrentAction(0);
            ActionIndexCache action1 = agent.GetCurrentAction(1);
            state =
                $"a0={action0.Index},{agent.GetCurrentActionType(0)},{agent.GetCurrentActionDirection(0)}|" +
                $"a1={action1.Index},{agent.GetCurrentActionType(1)},{agent.GetCurrentActionDirection(1)}|" +
                $"guard={agent.CurrentGuardMode}|" +
                $"move={(uint)agent.MovementFlags}|" +
                $"defend={(uint)agent.GetDefendMovementFlag()}|" +
                $"mount={agent.MountAgent?.Index ?? -1}";
        }
        catch (Exception exception)
        {
            state = $"captureError={exception.GetType().Name}";
        }

        float missionTime = -1f;
        try
        {
            missionTime = Mission.Current?.CurrentTime ?? -1f;
        }
        catch
        {
        }

        string token;
        lock (Sync)
        {
            if (!ReferenceEquals(Volatile.Read(ref target), agent))
                return;

            long nextOrdinal = ++ordinal;
            token =
                $"{nextOrdinal}@{missionTime.ToString("0.000", CultureInfo.InvariantCulture)}" +
                $"#{Thread.CurrentThread.ManagedThreadId}|" +
                $"{Sanitize(observedPhase)}|{Sanitize(operation)}|{state}";
            if (!string.IsNullOrEmpty(argument))
                token += $"|arg={Sanitize(argument)}";
            if (Records.Count == Capacity)
                Records.RemoveAt(0);
            Records.Add(token);
        }
    }

    internal static string GetToken(int maximumRecords)
    {
        lock (Sync)
        {
            if (Records.Count == 0)
                return "none";

            int count = Math.Max(1, Math.Min(maximumRecords, Records.Count));
            int start = Records.Count - count;
            var builder = new StringBuilder();
            for (int index = start; index < Records.Count; index++)
            {
                if (builder.Length > 0)
                    builder.Append(';');
                builder.Append(Records[index]);
            }
            return builder.ToString();
        }
    }

    internal static string GetReport()
    {
        lock (Sync)
        {
            string header =
                $"processId={ProcessId} role={Sanitize(ProcessRole)} runToken={Sanitize(RunToken)} " +
                $"agentId={targetId} commandId={commandId} records={Records.Count}";
            if (Records.Count == 0)
                return header;

            return header + Environment.NewLine +
                string.Join(Environment.NewLine, Records);
        }
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "none";

        return value
            .Replace(' ', '_')
            .Replace('\t', '_')
            .Replace('\r', '_')
            .Replace('\n', '_')
            .Replace(';', ',')
            .Replace('|', '/');
    }

    private static bool HasCommandLineArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length; index++)
        {
            if (string.Equals(
                    arguments[index],
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string ReadCommandLineArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index + 1 < arguments.Length; index++)
        {
            if (string.Equals(
                    arguments[index],
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }
        return null;
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(Agent), nameof(Agent.MovementFlags), MethodType.Setter)]
internal static class BattleGuardMovementFlagsTracePatch
{
    private static void Prefix(
        Agent __instance,
        Agent.MovementControlFlag value)
    {
        if (BattleGuardNativeTrace.IsTarget(__instance))
        {
            BattleGuardNativeTrace.Record(
                __instance,
                "movement-prefix",
                $"value={(uint)value}");
        }
    }

    private static void Postfix(
        Agent __instance,
        Agent.MovementControlFlag value)
    {
        if (BattleGuardNativeTrace.IsTarget(__instance))
        {
            BattleGuardNativeTrace.Record(
                __instance,
                "movement-postfix",
                $"value={(uint)value}");
        }
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(Agent), nameof(Agent.SetWeaponGuard))]
internal static class BattleGuardSetWeaponGuardTracePatch
{
    private static void Prefix(
        Agent __instance,
        Agent.UsageDirection direction)
    {
        if (BattleGuardNativeTrace.IsTarget(__instance))
        {
            BattleGuardNativeTrace.Record(
                __instance,
                "guard-prefix",
                $"direction={direction}");
        }
    }

    private static void Postfix(
        Agent __instance,
        Agent.UsageDirection direction)
    {
        if (BattleGuardNativeTrace.IsTarget(__instance))
        {
            BattleGuardNativeTrace.Record(
                __instance,
                "guard-postfix",
                $"direction={direction}");
        }
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(Agent), nameof(Agent.ResetGuard))]
internal static class BattleGuardResetGuardTracePatch
{
    private static void Prefix(Agent __instance)
    {
        if (BattleGuardNativeTrace.IsTarget(__instance))
            BattleGuardNativeTrace.Record(__instance, "reset-prefix");
    }

    private static void Postfix(Agent __instance)
    {
        if (BattleGuardNativeTrace.IsTarget(__instance))
            BattleGuardNativeTrace.Record(__instance, "reset-postfix");
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(Agent), nameof(Agent.SetActionChannel))]
internal static class BattleGuardSetActionChannelTracePatch
{
    private static void Prefix(
        Agent __instance,
        int channelNo,
        ref ActionIndexCache actionIndexCache,
        bool ignorePriority,
        AnimFlags additionalFlags,
        float startProgress)
    {
        if (BattleGuardNativeTrace.IsTarget(__instance))
        {
            BattleGuardNativeTrace.Record(
                __instance,
                "action-prefix",
                $"channel={channelNo},action={actionIndexCache.Index},ignore={ignorePriority}," +
                $"flags={(ulong)additionalFlags},progress={startProgress.ToString("0.000", CultureInfo.InvariantCulture)}");
        }
    }

    private static void Postfix(
        Agent __instance,
        int channelNo,
        ref ActionIndexCache actionIndexCache,
        bool __result)
    {
        if (BattleGuardNativeTrace.IsTarget(__instance))
        {
            BattleGuardNativeTrace.Record(
                __instance,
                "action-postfix",
                $"channel={channelNo},action={actionIndexCache.Index},result={__result}");
        }
    }
}
#endif
