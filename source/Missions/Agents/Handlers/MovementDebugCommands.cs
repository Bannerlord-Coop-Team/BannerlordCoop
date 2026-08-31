#if DEBUG
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

internal static class MovementDebugCommands
{
    public static string State(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.movement.state";
        if (!TryGetHandler(out IAgentMovementHandler handler) ||
            !TryGetDebugControl(handler, out IAgentMovementDebugControl debugControl))
            return "No active co-op mission movement handler.";

        MovementRateSnapshot state = handler.MovementRate;
        int activeHumans = Mission.Current?.Agents.Count(agent =>
            agent != null && agent.IsActive() && agent.IsHuman) ?? 0;
        int movingAgents = Mission.Current?.Agents.Count(agent =>
            agent != null &&
            agent.IsActive() &&
            agent.GetRealGlobalVelocity().AsVec2.LengthSquared > 0.01f) ?? 0;
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
            $"activeHumans={activeHumans}",
            $"movingAgents={movingAgents}",
            $"localAgents={state.LocallyControlledAgents}",
            $"controllers={state.Controllers}",
            $"fps={state.FramesPerSecond.ToString("0.0", CultureInfo.InvariantCulture)}",
            $"senderMsPerSecond={state.SenderMillisecondsPerSecond.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"receiverApplyMsPerSecond={state.ReceiverApplyMillisecondsPerSecond.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"receiverQueueMs={state.MaximumReceiverQueueMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"wireBytesPerSecond={state.WireBytesPerSecond}",
            $"configuredOutgoingBytesPerSecond={state.ConfiguredOutgoingBytesPerSecond}",
            $"availableOutgoingBytes={handler.AvailableOutgoingMovementBytes}",
            $"configuredIncomingBytesPerSecond={state.ConfiguredIncomingBytesPerSecond}",
            $"incomingBytesPerSender={state.AdvertisedIncomingBytesPerSender}",
            $"focusAgentId={state.FocusAgentId}",
            $"deferred={state.MaximumDeferredSnapshots}",
            $"deferredAge={state.MaximumDeferredAgeSeconds.ToString("0.000", CultureInfo.InvariantCulture)}",
            $"bulkPolls={state.BulkPollsPerSecond}",
            $"priorityOnlyPolls={state.PriorityOnlyPollsPerSecond}",
            $"initialConfiguredBulkHz={debugControl.InitialConfiguredBulkHz}",
            $"syntheticReceivePressureActive={debugControl.SyntheticReceivePressureActive.ToString().ToLowerInvariant()}",
            $"syntheticReceivePressureRemainingSeconds={debugControl.SyntheticReceivePressureRemainingSeconds.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"reason={state.Reason}",
        });
    }

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
