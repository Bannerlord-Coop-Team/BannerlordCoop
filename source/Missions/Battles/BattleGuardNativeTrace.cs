#if DEBUG
using Common.Util;
using GameInterface.Services.Battles.Messages;
using HarmonyLib;
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
    private const int CollisionCapacity = 512;
    private static readonly object Sync = new();
    private static readonly List<string> Records = new(Capacity);
    // Keep one contact window separate so per-frame writer records cannot evict callback order.
    private static readonly List<string> CollisionRecords =
        new(CollisionCapacity);
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
    private static bool collisionCaptureActive;
    private static bool collisionCaptureCompletionPending;
    private static bool meleeCallbackActive;

    internal static bool IsTarget(Agent agent)
    {
        return agent != null &&
            ReferenceEquals(Volatile.Read(ref target), agent);
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
                CollisionRecords.Clear();
                ordinal = 0;
                collisionCaptureActive = false;
                collisionCaptureCompletionPending = false;
                meleeCallbackActive = false;
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
            collisionCaptureActive = false;
            collisionCaptureCompletionPending = false;
            meleeCallbackActive = false;
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
                $"a0={GetActionState(agent, 0, action0)}|" +
                $"a1={GetActionState(agent, 1, action1)}|" +
                $"guard={agent.CurrentGuardMode}|" +
                $"move={(uint)agent.MovementFlags}|" +
                $"defend={(uint)agent.GetDefendMovementFlag()}|" +
                $"mount={agent.MountAgent?.Index ?? -1}|" +
                $"allowed={AllowedThread.IsThisThreadAllowed()}";
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
            if (collisionCaptureActive)
            {
                if (CollisionRecords.Count == CollisionCapacity)
                    CollisionRecords.RemoveAt(0);
                CollisionRecords.Add(token);
            }
        }
    }

    internal static void RecordCollision(
        Agent agent,
        string operation,
        in AttackCollisionData collisionData,
        string argument = null)
    {
        BeginCollisionCapture(agent, operation);
        string collision =
            $"result={collisionData.CollisionResult}," +
            $"attack={collisionData.AttackDirection}," +
            $"progress={collisionData.AttackProgress.ToString("0.000", CultureInfo.InvariantCulture)}," +
            $"distance={collisionData.CollisionDistanceOnWeapon.ToString("0.000", CultureInfo.InvariantCulture)}," +
            $"bone={collisionData.CollisionBoneIndex}," +
            $"body={collisionData.VictimHitBodyPart}," +
            $"shield={collisionData.AttackBlockedWithShield}," +
            $"correctShield={collisionData.CorrectSideShieldBlock}," +
            $"flags={(uint)collisionData.CollisionHitResultFlags}";
        if (!string.IsNullOrEmpty(argument))
            collision += $",{argument}";
        Record(agent, operation, collision);
        CompleteMeleeCallback(operation);
    }

    internal static void CompleteCollisionCapture(
        Agent agent,
        string outcome)
    {
        if (!IsTarget(agent))
            return;

        Record(agent, "collision-capture-complete", outcome);
        lock (Sync)
        {
            if (ReferenceEquals(Volatile.Read(ref target), agent))
            {
                if (meleeCallbackActive)
                    collisionCaptureCompletionPending = true;
                else
                    collisionCaptureActive = false;
            }
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
                $"agentId={targetId} commandId={commandId} records={Records.Count} " +
                $"collisionRecords={CollisionRecords.Count}";
            if (Records.Count == 0 && CollisionRecords.Count == 0)
                return header;

            var builder = new StringBuilder(header);
            if (Records.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("TRACE=rolling");
                builder.Append(
                    string.Join(Environment.NewLine, Records));
            }
            if (CollisionRecords.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("TRACE=collision");
                builder.Append(
                    string.Join(
                        Environment.NewLine,
                        CollisionRecords));
            }
            return builder.ToString();
        }
    }

    private static void BeginCollisionCapture(
        Agent agent,
        string operation)
    {
        if (!IsTarget(agent))
            return;

        lock (Sync)
        {
            if (!ReferenceEquals(Volatile.Read(ref target), agent))
            {
                return;
            }

            if (string.Equals(
                    operation,
                    "melee-prefix",
                    StringComparison.Ordinal))
            {
                CollisionRecords.Clear();
                collisionCaptureActive = true;
                collisionCaptureCompletionPending = false;
                meleeCallbackActive = true;
            }
            else if (!collisionCaptureActive &&
                !collisionCaptureCompletionPending)
            {
                CollisionRecords.Clear();
                collisionCaptureActive = true;
            }
        }
    }

    private static void CompleteMeleeCallback(string operation)
    {
        if (!string.Equals(
                operation,
                "melee-postfix",
                StringComparison.Ordinal))
        {
            return;
        }

        lock (Sync)
        {
            meleeCallbackActive = false;
            if (collisionCaptureCompletionPending)
            {
                collisionCaptureActive = false;
                collisionCaptureCompletionPending = false;
            }
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

    private static string GetActionState(
        Agent agent,
        int channel,
        ActionIndexCache action)
    {
        return
            $"{action.Index}," +
            $"{agent.GetCurrentActionType(channel)}," +
            $"{agent.GetCurrentActionStage(channel)}," +
            $"{agent.GetCurrentActionDirection(channel)}," +
            $"{agent.GetCurrentActionProgress(channel).ToString("0.000", CultureInfo.InvariantCulture)}";
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
[HarmonyPatch(typeof(Mission), "GetDefendCollisionResults")]
internal static class BattleGuardDefendCollisionTracePatch
{
    private static void Prefix(
        Agent attackerAgent,
        Agent defenderAgent,
        CombatCollisionResult collisionResult,
        int attackerWeaponSlotIndex,
        bool isAlternativeAttack,
        StrikeType strikeType,
        Agent.UsageDirection attackDirection,
        float collisionDistanceOnWeapon,
        float attackProgress,
        bool attackIsParried,
        bool isPassiveUsageHit,
        bool isHeavyAttack,
        ref bool crushedThrough)
    {
        if (!BattleGuardNativeTrace.IsTarget(defenderAgent))
            return;

        BattleGuardNativeTrace.Record(
            defenderAgent,
            "defend-prefix",
            $"attacker={attackerAgent?.Index ?? -1}," +
            $"result={collisionResult},slot={attackerWeaponSlotIndex}," +
            $"alternative={isAlternativeAttack},strike={strikeType}," +
            $"attack={attackDirection}," +
            $"distance={collisionDistanceOnWeapon.ToString("0.000", CultureInfo.InvariantCulture)}," +
            $"progress={attackProgress.ToString("0.000", CultureInfo.InvariantCulture)}," +
            $"parried={attackIsParried},passive={isPassiveUsageHit}," +
            $"heavy={isHeavyAttack},crushed={crushedThrough}");
    }

    private static void Postfix(
        Agent defenderAgent,
        ref bool crushedThrough)
    {
        if (BattleGuardNativeTrace.IsTarget(defenderAgent))
        {
            BattleGuardNativeTrace.Record(
                defenderAgent,
                "defend-postfix",
                $"crushed={crushedThrough}");
        }
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
internal static class BattleGuardMeleeHitTracePatch
{
    private static void Prefix(
        ref AttackCollisionData collisionData,
        Agent attacker,
        Agent victim,
        ref MeleeCollisionReaction colReaction)
    {
        if (BattleGuardNativeTrace.IsTarget(victim))
        {
            BattleGuardNativeTrace.RecordCollision(
                victim,
                "melee-prefix",
                in collisionData,
                $"attacker={attacker?.Index ?? -1},reaction={colReaction}");
        }
    }

    private static void Postfix(
        ref AttackCollisionData collisionData,
        Agent attacker,
        Agent victim,
        ref MeleeCollisionReaction colReaction)
    {
        if (BattleGuardNativeTrace.IsTarget(victim))
        {
            BattleGuardNativeTrace.RecordCollision(
                victim,
                "melee-postfix",
                in collisionData,
                $"attacker={attacker?.Index ?? -1},reaction={colReaction}");
        }
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(Mission), "OnAgentHitBlocked")]
internal static class BattleGuardBlockedHitTracePatch
{
    private static void Prefix(
        Agent affectedAgent,
        Agent affectorAgent,
        ref AttackCollisionData collisionData,
        bool isMissile)
    {
        if (BattleGuardNativeTrace.IsTarget(affectedAgent))
        {
            BattleGuardNativeTrace.RecordCollision(
                affectedAgent,
                "blocked-prefix",
                in collisionData,
                $"attacker={affectorAgent?.Index ?? -1},missile={isMissile}");
        }
    }

    private static void Postfix(
        Agent affectedAgent,
        Agent affectorAgent,
        ref AttackCollisionData collisionData,
        bool isMissile)
    {
        if (BattleGuardNativeTrace.IsTarget(affectedAgent))
        {
            BattleGuardNativeTrace.RecordCollision(
                affectedAgent,
                "blocked-postfix",
                in collisionData,
                $"attacker={affectorAgent?.Index ?? -1},missile={isMissile}");
        }
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
