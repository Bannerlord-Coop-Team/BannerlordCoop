#if DEBUG
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Missions.Agents.Handlers;

internal static class MovementDebugCommands
{
    [CommandLineArgumentFunction("state", "coop.debug.movement")]
    public static string State(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.movement.state";
        if (!TryGetHandler(out IAgentMovementHandler handler) ||
            !TryGetDebugControl(handler, out IAgentMovementDebugControl debugControl))
            return "No active co-op mission movement handler.";

        MovementPerformanceState performance = GetPerformanceState(
            handler,
            debugControl);
        MovementRateSnapshot state = performance.Rate;
        AgentMovementHandler.MovementHotPathSnapshot hotPath =
            performance.HotPath;
        return string.Join("|", new[]
        {
            $"profile={state.Profile}",
            $"bulkHz={state.BulkHz}",
            $"priorityHz={state.PriorityHz}",
            $"frameLimitHz={state.FrameLimitHz}",
            $"performanceCeilingHz={state.PerformanceCeilingHz}",
            $"localAdaptiveHz={state.LocalAdaptiveHz}",
            $"receiverCapHz={state.AdvertisedReceiverCapHz}",
            $"peerCapHz={FormatNullable(state.PeerReceiverCapHz)}",
            $"peerCapSource={state.PeerReceiverCapSource ?? "none"}",
            $"forcedBulkHz={FormatNullable(state.ForcedBulkHz)}",
            $"forcedReceiverCapHz={FormatNullable(state.ForcedReceiverCapHz)}",
            $"activeAgents={state.ActiveAgents}",
            $"activeHumans={performance.ActiveHumans}",
            $"movingAgents={performance.MovingAgents}",
            $"localAgents={state.LocallyControlledAgents}",
            $"controllers={state.Controllers}",
            $"fps={state.FramesPerSecond.ToString("0.0", CultureInfo.InvariantCulture)}",
            $"senderMsPerSecond={state.SenderMillisecondsPerSecond.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"receiverApplyMsPerSecond={state.ReceiverApplyMillisecondsPerSecond.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"receiverQueueMs={state.MaximumReceiverQueueMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"wireBytesPerSecond={state.WireBytesPerSecond}",
            $"deferred={state.MaximumDeferredSnapshots}",
            $"deferredAge={state.MaximumDeferredAgeSeconds.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"bulkPolls={state.BulkPollsPerSecond}",
            $"priorityOnlyPolls={state.PriorityOnlyPollsPerSecond}",
            $"initialConfiguredBulkHz={debugControl.InitialConfiguredBulkHz}",
            $"syntheticReceivePressureActive={debugControl.SyntheticReceivePressureActive.ToString().ToLowerInvariant()}",
            $"syntheticReceivePressureRemainingSeconds={debugControl.SyntheticReceivePressureRemainingSeconds.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"hotPathWindowSeconds={hotPath.WindowSeconds.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"hotPathWindowSequence={hotPath.WindowSequence}",
            $"interpolatorTicks={hotPath.InterpolatorTicks}",
            $"interpolatorMilliseconds={hotPath.InterpolatorMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"mountedTargets={hotPath.MountedTargets}",
            $"mountedCorrections={hotPath.MountedCorrections}",
            $"continuousStateAttempts={hotPath.ContinuousStateAttempts}",
            $"continuousStateWrites={hotPath.ContinuousStateWrites}",
            $"lookAttempts={hotPath.LookAttempts}",
            $"lookWrites={hotPath.LookWrites}",
            $"lookReplayMilliseconds={hotPath.LookReplayMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"mountIdentityHits={hotPath.MountIdentityHits}",
            $"mountIdentityResolutions={hotPath.MountIdentityResolutions}",
            $"reusedSentStates={hotPath.ReusedSentStates}",
            $"createdSentStates={hotPath.CreatedSentStates}",
            $"reusedMovementBatches={hotPath.ReusedMovementBatches}",
            $"createdMovementBatches={hotPath.CreatedMovementBatches}",
            $"rotatedSnapshotCopiesAvoided={hotPath.RotatedSnapshotCopiesAvoided}",
            $"maximumMountedDrift={hotPath.MaximumMountedDrift.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"maximumMountedTargetAge={hotPath.MaximumMountedTargetAge.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"mountedContacts={hotPath.MountedContacts}",
            $"maximumMountedContactDrift={hotPath.MaximumMountedContactDrift.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"maximumMountedContactTargetAge={hotPath.MaximumMountedContactTargetAge.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"reason={state.Reason}",
        });
    }

    [CommandLineArgumentFunction("performance_state", "coop.debug.movement")]
    public static string PerformanceState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.movement.performance_state";
        if (!TryGetHandler(out IAgentMovementHandler handler) ||
            !TryGetDebugControl(handler, out IAgentMovementDebugControl debugControl))
        {
            return "No active co-op mission movement handler.";
        }

        MovementPerformanceState performance = GetPerformanceState(
            handler,
            debugControl);
        MovementRateSnapshot state = performance.Rate;
        AgentMovementHandler.MovementHotPathSnapshot hotPath =
            performance.HotPath;
        string structuredState = JsonConvert.SerializeObject(new
        {
            sampleSequence = hotPath.WindowSequence,
            windowSeconds = hotPath.WindowSeconds,
            profile = state.Profile.ToString(),
            bulkHz = state.BulkHz,
            priorityHz = state.PriorityHz,
            frameLimitHz = state.FrameLimitHz,
            performanceCeilingHz = state.PerformanceCeilingHz,
            localAdaptiveHz = state.LocalAdaptiveHz,
            receiverCapHz = state.AdvertisedReceiverCapHz,
            peerReceiverCapHz = state.PeerReceiverCapHz,
            peerReceiverCapSource = state.PeerReceiverCapSource,
            activeAgents = state.ActiveAgents,
            activeHumans = performance.ActiveHumans,
            movingAgents = performance.MovingAgents,
            localAgents = state.LocallyControlledAgents,
            controllers = state.Controllers,
            fps = state.FramesPerSecond,
            senderMillisecondsPerSecond = state.SenderMillisecondsPerSecond,
            receiverApplyMillisecondsPerSecond =
                state.ReceiverApplyMillisecondsPerSecond,
            receiverQueueMilliseconds = state.MaximumReceiverQueueMilliseconds,
            wireBytesPerSecond = state.WireBytesPerSecond,
            maximumDeferredSnapshots = state.MaximumDeferredSnapshots,
            maximumDeferredAgeSeconds = state.MaximumDeferredAgeSeconds,
            bulkPollsPerSecond = state.BulkPollsPerSecond,
            priorityOnlyPollsPerSecond = state.PriorityOnlyPollsPerSecond,
            reason = state.Reason,
            hotPath = new
            {
                interpolatorTicks = hotPath.InterpolatorTicks,
                interpolatorMilliseconds = hotPath.InterpolatorMilliseconds,
                mountedTargets = hotPath.MountedTargets,
                mountedCorrections = hotPath.MountedCorrections,
                continuousStateAttempts = hotPath.ContinuousStateAttempts,
                continuousStateWrites = hotPath.ContinuousStateWrites,
                lookAttempts = hotPath.LookAttempts,
                lookWrites = hotPath.LookWrites,
                lookReplayMilliseconds = hotPath.LookReplayMilliseconds,
                mountIdentityHits = hotPath.MountIdentityHits,
                mountIdentityResolutions = hotPath.MountIdentityResolutions,
                reusedSentStates = hotPath.ReusedSentStates,
                createdSentStates = hotPath.CreatedSentStates,
                reusedMovementBatches = hotPath.ReusedMovementBatches,
                createdMovementBatches = hotPath.CreatedMovementBatches,
                rotatedSnapshotCopiesAvoided =
                    hotPath.RotatedSnapshotCopiesAvoided,
                maximumMountedDrift = hotPath.MaximumMountedDrift,
                maximumMountedTargetAge = hotPath.MaximumMountedTargetAge,
                mountedContacts = hotPath.MountedContacts,
                maximumMountedContactDrift =
                    hotPath.MaximumMountedContactDrift,
                maximumMountedContactTargetAge =
                    hotPath.MaximumMountedContactTargetAge,
            },
        });

        return $"MOVEMENT_PERFORMANCE_STATE sequence={hotPath.WindowSequence} " +
            $"fps={state.FramesPerSecond.ToString("0.0", CultureInfo.InvariantCulture)} " +
            $"activeAgents={state.ActiveAgents} movingAgents={performance.MovingAgents}" +
            Environment.NewLine + $"LIVE_TEST_JSON={structuredState}";
    }

    [CommandLineArgumentFunction("force_rate", "coop.debug.movement")]
    public static string ForceRate(List<string> args)
    {
        if (!TryParseRate(
                args,
                "coop.debug.movement.force_rate",
                "rate",
                out int? rate,
                out string error))
            return error;
        if (!TryGetHandler(out IAgentMovementHandler handler))
            return "No active co-op mission movement handler.";
        if (!handler.TrySetForcedBulkHz(rate, out error))
            return error;

        return rate.HasValue
            ? $"MOVEMENT_RATE_FORCED hz={rate.Value}"
            : "MOVEMENT_RATE_AUTOMATIC";
    }

    [CommandLineArgumentFunction("force_receiver_cap", "coop.debug.movement")]
    public static string ForceReceiverCap(List<string> args)
    {
        if (!TryParseRate(
                args,
                "coop.debug.movement.force_receiver_cap",
                "receiver cap",
                out int? rate,
                out string error))
        {
            return error;
        }
        if (!TryGetHandler(out IAgentMovementHandler handler))
            return "No active co-op mission movement handler.";
        if (!handler.TrySetForcedReceiverCapHz(rate, out error))
            return error;

        return rate.HasValue
            ? $"MOVEMENT_RECEIVER_CAP_FORCED hz={rate.Value}"
            : "MOVEMENT_RECEIVER_CAP_AUTOMATIC";
    }

    [CommandLineArgumentFunction("simulate_receive_pressure", "coop.debug.movement")]
    public static string SimulateReceivePressure(List<string> args)
    {
        if (args.Count != 4 ||
            !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float durationSeconds) ||
            !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double queueMilliseconds) ||
            !double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double applyMilliseconds) ||
            !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int snapshots))
        {
            return "Usage: coop.debug.movement.simulate_receive_pressure <duration-seconds> <queue-ms> <apply-ms> <snapshots>";
        }
        if (!TryGetHandler(out IAgentMovementHandler handler) ||
            !TryGetDebugControl(handler, out IAgentMovementDebugControl debugControl))
            return "No active co-op mission movement handler.";
        if (!debugControl.TrySetSyntheticReceivePressure(
                durationSeconds,
                queueMilliseconds,
                applyMilliseconds,
                snapshots,
                out string error))
        {
            return error;
        }

        return string.Join(" ", new[]
        {
            "MOVEMENT_RECEIVE_PRESSURE_ACTIVE",
            $"durationSeconds={durationSeconds.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"queueMilliseconds={queueMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"applyMilliseconds={applyMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"snapshots={snapshots}",
        });
    }

    [CommandLineArgumentFunction("clear_receive_pressure", "coop.debug.movement")]
    public static string ClearReceivePressure(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.movement.clear_receive_pressure";
        if (!TryGetHandler(out IAgentMovementHandler handler) ||
            !TryGetDebugControl(handler, out IAgentMovementDebugControl debugControl))
            return "No active co-op mission movement handler.";

        debugControl.ClearSyntheticReceivePressure();
        return "MOVEMENT_RECEIVE_PRESSURE_CLEARED";
    }

    private static bool TryGetHandler(out IAgentMovementHandler handler)
    {
        handler = Mission.Current?
            .GetMissionBehavior<CoopMissionController>()?
            .AgentMovementHandler;
        return handler != null;
    }

    private static MovementPerformanceState GetPerformanceState(
        IAgentMovementHandler handler,
        IAgentMovementDebugControl debugControl)
    {
        int activeHumans = Mission.Current?.Agents.Count(agent =>
            agent != null && agent.IsActive() && agent.IsHuman) ?? 0;
        int movingAgents = Mission.Current?.Agents.Count(agent =>
            agent != null &&
            agent.IsActive() &&
            agent.GetRealGlobalVelocity().AsVec2.LengthSquared > 0.01f) ?? 0;
        return new MovementPerformanceState(
            handler.MovementRate,
            debugControl.HotPath,
            activeHumans,
            movingAgents);
    }

    private readonly struct MovementPerformanceState
    {
        public MovementRateSnapshot Rate { get; }
        public AgentMovementHandler.MovementHotPathSnapshot HotPath { get; }
        public int ActiveHumans { get; }
        public int MovingAgents { get; }

        public MovementPerformanceState(
            MovementRateSnapshot rate,
            AgentMovementHandler.MovementHotPathSnapshot hotPath,
            int activeHumans,
            int movingAgents)
        {
            Rate = rate;
            HotPath = hotPath;
            ActiveHumans = activeHumans;
            MovingAgents = movingAgents;
        }
    }

    private static bool TryGetDebugControl(
        IAgentMovementHandler handler,
        out IAgentMovementDebugControl debugControl)
    {
        debugControl = handler as IAgentMovementDebugControl;
        return debugControl != null;
    }

    private static bool TryParseRate(
        List<string> args,
        string commandName,
        string valueName,
        out int? rate,
        out string error)
    {
        rate = null;
        if (args.Count != 1)
        {
            error = $"Usage: {commandName} <auto|10|15|20|30|40|60>";
            return false;
        }
        if (args[0].ToLowerInvariant() == "auto")
        {
            error = null;
            return true;
        }
        if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            error = $"Invalid {valueName}. Use auto, 10, 15, 20, 30, 40, or 60.";
            return false;
        }

        rate = parsed;
        error = null;
        return true;
    }

    private static string FormatNullable(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "none";
}
#endif
