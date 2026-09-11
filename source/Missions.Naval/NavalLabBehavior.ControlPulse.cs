#if DEBUG
using System;
using System.Linq;
using System.Threading;
using Common;
using Missions.Messages;
using NavalDLC.GauntletUI.MissionViews;
using NavalDLC.View.MissionViews;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private Guid pulseOperation;
    private Vec2 pulseAxes;
    private bool pulsePending;
    private bool pulseCompleting;
    private double pulseDeadline;
    private long pulseDeadlineUtcTicks;
    private string pulsePhase = "not_requested";
    private MissionShipControlView pulseView;
    private NetworkNavalLabHelmInput lastSentNativeInput;
    private string pulseReceipt;
    private long pulseFirstInputSequence;
    private long pulseLastInputSequence;
    private long pulseNeutralInputSequence;
    private const string PulseReceipt = "requested:synthetic_axes_not_keyboard_or_propulsion_proof";

    internal string RequestAxesPulse(Guid operationId, int slot, float lateral, bool row, long deadlineUtcTicks)
    {
        if (!GameThread.Instance.IsGameThread) return "rejected:not_game_thread";
        if (!IsTwoClientNative) return "rejected:wrong_mode";
        if (operationId == Guid.Empty || slot != OwnSlot || float.IsNaN(lateral) || float.IsInfinity(lateral) || Math.Abs(lateral) > 1)
            return "rejected:operation_owner_or_axes";
        var axes = new Vec2(lateral, row ? 1 : 0);
        if (pulseOperation == operationId) return pulseAxes == axes ? pulseReceipt : "rejected:conflicting_operation";
        if (pulsePending) return "rejected:axes_pulse_pending";
        long now = DateTime.UtcNow.Ticks;
        if (deadlineUtcTicks <= now || deadlineUtcTicks > now + TimeSpan.TicksPerSecond) return "rejected:expired_control";
        var view = Mission?.GetMissionBehavior<MissionGauntletShipControlView>();
        if (Mission != Mission.Current || view == null || !HasNativeInputPermission(view))
            return "rejected:owner_helm_or_input_unavailable";
        pulseOperation = operationId;
        pulseAxes = axes;
        pulseView = view;
        pulseDeadlineUtcTicks = deadlineUtcTicks;
        pulseDeadline = ControlNow + TimeSpan.FromTicks(deadlineUtcTicks - now).TotalSeconds;
        pulsePending = true;
        pulsePhase = "pending_synthetic_axes";
        pulseFirstInputSequence = pulseLastInputSequence = pulseNeutralInputSequence = 0;
        nextNativeInput = 0;
        try
        {
            RouteNativeAxes(view);
            return pulseReceipt = PulseReceipt;
        }
        catch (Exception exception)
        {
            pulsePending = pulseCompleting = false;
            pulsePhase = "failed_dispatch_safety_hold";
            Reject("native.axes_pulse_dispatch_failed:" + exception.GetType().FullName);
            return pulseReceipt = "failed:synthetic_axes_dispatch_uncertain";
        }
    }

    private void TickAxesPulse()
    {
        if (!pulsePending) return;
        var view = Mission?.GetMissionBehavior<MissionGauntletShipControlView>();
        if (Mission != Mission.Current || view == null || view != pulseView || !HasNativeInputPermission(view))
        {
            CancelAxesPulse("permission_lost_safety_stop");
            return;
        }
        RouteNativeAxes(view);
    }

    private void CancelAxesPulse(string reason)
    {
        if (!pulsePending) return;
        pulsePending = pulseCompleting = false;
        pulsePhase = reason;
        nextNativeInput = 0;
        // Safety loss retains the existing complete Stop semantics, including raised sails.
        var stop = new NetworkNavalLabHelmInput(manifest.IncarnationId, 1, OwnSlot, ++nativeInputSequence,
            DateTime.UtcNow.AddSeconds(1).Ticks, false, 0, 0, 0, 0, 0);
        try
        {
            SendNativeInput?.Invoke(stop);
            lastSentNativeInput = stop;
            pulseNeutralInputSequence = stop.Sequence;
        }
        catch (Exception exception)
        {
            pulsePhase = "failed_safety_stop_dispatch";
            Reject("native.axes_pulse_stop_failed:" + exception.GetType().FullName);
        }
    }

    internal object InspectControlStatus()
    {
        if (!GameThread.Instance.IsGameThread) return new { unavailable = "not_game_thread" };
        if (!IsTwoClientNative) return new { unavailable = "wrong_mode" };
        bool ready = !factoryTerminal && CanUseNativeControls;
        return new
        {
            manifest.IncarnationId, epoch = 1, owner = ownControllerId, ship = OwnSlot,
            shipId = OwnSlot >= 0 ? (Guid?)manifest.Ships[OwnSlot] : null, electedSimulator = factoryHost,
            ready, terminal = factoryTerminal, blocked = Blocker != null,
            operationId = pulseOperation, phase = pulsePhase, pulseSynthetic = true,
            pulseFirstInputSequence, pulseLastInputSequence, pulseNeutralInputSequence,
            requestedLateralAxis = pulseAxes.x, requestedForwardAxis = pulseAxes.y,
            remainingSeconds = pulsePending ? Math.Max(0, pulseDeadline - ControlNow) : 0,
            pulseDeadlineUtcTicks, lastSent = lastSentNativeInput?.IsValid == true ? lastSentNativeInput : null,
            hostLastAppliedHelmInput = factoryHost ? lastReceivedNativeInput : null,
            currentHostApplication = ready && factoryHost ? Ships.Select(ship =>
            {
                var input = ship?.PlayerController?._inputRecord;
                return input.HasValue && !float.IsNaN(input.Value.RudderLateral) && !float.IsInfinity(input.Value.RudderLateral)
                    ? new { rudder = input.Value.RudderLateral, lateral = (int)input.Value.RowerLateral,
                        longitudinal = (int)input.Value.RowerLongitudinal, doubleTap = (int)input.Value.RowerLongitudinalDoubleTap,
                        sail = (int)input.Value.Sail } : null;
            }).ToArray() : null,
            localObservedShips = ready ? Ships.Select(InspectShip).ToArray() : null,
            forceApplications = Interlocked.Read(ref ForceApplications), fixedTicks = Interlocked.Read(ref FixedTicks),
            activeFixedTicks = Interlocked.Read(ref ActiveFixedTicks),
            activeParallelFixedEntries = Interlocked.Read(ref factoryActiveParallelEntries),
            preCompletionOrUnattributedFixedEntries = Interlocked.Read(ref factoryPreCompletionFixedEntries),
            timing = "local read; input sequences and source frame sequences are not local callback ordinals; no synchronized physics cut",
            unavailable = ready ? null : "not_ready_or_terminal"
        };
    }
}
#endif
