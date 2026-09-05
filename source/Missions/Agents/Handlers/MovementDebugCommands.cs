#if DEBUG
using System;
using Common.Commands;
using GameInterface;
using Missions.Services.Network;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

internal static class MovementDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class PeerStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.movement";

        public string Name => "peer_state";

        public string Description => "Reports peer route state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IBattleNetwork>(out var network))
                return Failed("No active co-op mission peer network.");
            var client = network as LiteNetP2PClient;
            if (client == null)
                return Failed("No active co-op mission peer network.");

            bool known = client.TryGetPeerRouteState(
                args[0],
                out bool credentialAnnounced,
                out bool routeExists,
                out bool credentialMatched,
                out bool steamIdentityMatched,
                out bool mapped);
            string structuredState = JsonConvert.SerializeObject(new
            {
                success = known && mapped && credentialMatched && steamIdentityMatched,
                controllerId = args[0],
                credentialAnnounced,
                routeExists,
                credentialMatched,
                steamIdentityMatched,
                mapped,
            });

            return Succeeded($"MISSION_PEER_STATE controller={args[0]}|mapped={mapped}|" +
                $"credentialAnnounced={credentialAnnounced}|credentialMatched={credentialMatched}|" +
                $"steamIdentityMatched={steamIdentityMatched}\nLIVE_TEST_JSON={structuredState}");
        }
    }

    public sealed class ControllerAgentsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.movement";

        public string Name => "controller_agents";

        public string Description => "Reports agents owned by a controller.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
                return Failed("Network agent registry is unavailable.");

            var agents = registry.GetAgents(args[0])
                .OrderBy(info => info.AgentId)
                .Select(info => new
                {
                    agentId = info.AgentId.ToString("D"),
                    info.OriginalOwner,
                    info.CurrentAuthority,
                    active = info.Agent != null && info.Agent.IsActive(),
                    position = info.Agent == null ? null : new
                    {
                        x = info.Agent.Position.x,
                        y = info.Agent.Position.y,
                        z = info.Agent.Position.z,
                    },
                    velocity = info.Agent == null ? null : new
                    {
                        x = info.Agent.GetRealGlobalVelocity().x,
                        y = info.Agent.GetRealGlobalVelocity().y,
                        z = info.Agent.GetRealGlobalVelocity().z,
                    },
                })
                .ToArray();
            string structuredState = JsonConvert.SerializeObject(new
            {
                success = agents.Length > 0,
                controllerId = args[0],
                agentCount = agents.Length,
                agents,
            });

            return Succeeded($"CONTROLLER_AGENTS controller={args[0]}|count={agents.Length}\n" +
                $"LIVE_TEST_JSON={structuredState}");
        }
    }

    public sealed class StateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.movement";

        public string Name => "state";

        public string Description => "Reports state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetHandler(out IAgentMovementHandler handler) ||
                !TryGetDebugControl(handler, out IAgentMovementDebugControl debugControl))
                return Failed("No active co-op mission movement handler.");

            MovementRateSnapshot state = handler.MovementRate;
            int activeHumans = Mission.Current?.Agents.Count(agent =>
                agent != null && agent.IsActive() && agent.IsHuman) ?? 0;
            int movingAgents = Mission.Current?.Agents.Count(agent =>
                agent != null &&
                agent.IsActive() &&
                agent.GetRealGlobalVelocity().AsVec2.LengthSquared > 0.01f) ?? 0;
            return Succeeded(string.Join("|", new[]
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
            }));
        }
    }

    public sealed class ForceRateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.movement";

        public string Name => "force_rate";

        public string Description => "Runs the force rate debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("rate", "The rate.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryParseRate(
                    args,
                    "rate",
                    out int? rate,
                    out string error))
                return Failed(error);
            if (!TryGetHandler(out IAgentMovementHandler handler))
                return Failed("No active co-op mission movement handler.");
            if (!handler.TrySetForcedBulkHz(rate, out error))
                return Failed(error);

            return Succeeded(rate.HasValue
                ? $"MOVEMENT_RATE_FORCED hz={rate.Value}"
                : "MOVEMENT_RATE_AUTOMATIC");
        }
    }

    public sealed class ForceReceiverCapCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.movement";

        public string Name => "force_receiver_cap";

        public string Description => "Runs the force receiver cap debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("receiver_cap", "The receiver cap.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryParseRate(
                    args,
                    "receiver cap",
                    out int? rate,
                    out string error))
            {
                return Failed(error);
            }
            if (!TryGetHandler(out IAgentMovementHandler handler))
                return Failed("No active co-op mission movement handler.");
            if (!handler.TrySetForcedReceiverCapHz(rate, out error))
                return Failed(error);

            return Succeeded(rate.HasValue
                ? $"MOVEMENT_RECEIVER_CAP_FORCED hz={rate.Value}"
                : "MOVEMENT_RECEIVER_CAP_AUTOMATIC");
        }
    }

    public sealed class SimulateReceivePressureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.movement";

        public string Name => "simulate_receive_pressure";

        public string Description => "Runs the simulate receive pressure debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("duration_seconds", "The duration seconds.", true),
            new ExpectedArgs("queue_ms", "The queue ms.", true),
            new ExpectedArgs("apply_ms", "The apply ms.", true),
            new ExpectedArgs("snapshots", "The snapshots.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float durationSeconds) ||
                !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double queueMilliseconds) ||
                !double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double applyMilliseconds) ||
                !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int snapshots))
            {
                return Failed("Invalid command argument value.");
            }
            if (!TryGetHandler(out IAgentMovementHandler handler) ||
                !TryGetDebugControl(handler, out IAgentMovementDebugControl debugControl))
                return Failed("No active co-op mission movement handler.");
            if (!debugControl.TrySetSyntheticReceivePressure(
                    durationSeconds,
                    queueMilliseconds,
                    applyMilliseconds,
                    snapshots,
                    out string error))
            {
                return Failed(error);
            }

            return Succeeded(string.Join(" ", new[]
            {
                "MOVEMENT_RECEIVE_PRESSURE_ACTIVE",
                $"durationSeconds={durationSeconds.ToString("0.00", CultureInfo.InvariantCulture)}",
                $"queueMilliseconds={queueMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)}",
                $"applyMilliseconds={applyMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)}",
                $"snapshots={snapshots}",
            }));
        }
    }

    public sealed class ClearReceivePressureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.movement";

        public string Name => "clear_receive_pressure";

        public string Description => "Restores or clears clear receive pressure.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetHandler(out IAgentMovementHandler handler) ||
                !TryGetDebugControl(handler, out IAgentMovementDebugControl debugControl))
                return Failed("No active co-op mission movement handler.");

            debugControl.ClearSyntheticReceivePressure();
            return Succeeded("MOVEMENT_RECEIVE_PRESSURE_CLEARED");
        }
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
        IReadOnlyList<string> args,
        string valueName,
        out int? rate,
        out string error)
    {
        rate = null;
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
