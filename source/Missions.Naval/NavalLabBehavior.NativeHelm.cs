#if DEBUG
using System;
using Common;
using Missions.Battles;
using NavalDLC.View.MissionViews;
using NavalDLC.Missions.Objects;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private Guid nativeHelmOperation;
    private bool nativeHelmTake;
    private bool nativeHelmDispatched;
    private string nativeHelmPhase = "not_requested";
    private string nativeHelmReceipt;
    private string nativeHelmFailure;
    private object nativeHelmLastOutcome;
    private double nativeHelmDeadline;
    private MissionShip nativeHelmShip;
    private Agent nativeHelmAgent;
    private StandingPoint nativeHelmPoint;
    private long nativeHelmTicks;
    private long nativeHelmDispatchTick;
    private long nativeHelmObservedTick;
    private int nativeHelmUseCallbacks;
    private int nativeHelmStopCallbacks;
    private bool HasUncertainNativeHelmDispatch => IsTwoClientNative && nativeHelmDispatched
        && (nativeHelmPhase == "pending" || nativeHelmPhase == "failed");

    internal string RequestNativeHelm(Guid operationId, int slot, bool take)
    {
        if (!GameThread.Instance.IsGameThread) return "rejected:not_game_thread";
        if (!IsTwoClientNative) return "rejected:wrong_mode";
        if (operationId == Guid.Empty || slot != OwnSlot) return "rejected:operation_or_owner";
        if (nativeHelmOperation == operationId)
            return take == nativeHelmTake ? nativeHelmReceipt : "rejected:conflicting_operation";
        if (nativeHelmPhase == "pending") return "rejected:native_helm_pending";
        if (nativeHelmPhase == "failed" || factoryTerminal) return "rejected:terminal_hold";
        nativeHelmOperation = operationId;
        nativeHelmTake = take;
        nativeHelmPhase = "requested";
        nativeHelmDispatched = false;
        nativeHelmFailure = null;
        nativeHelmObservedTick = nativeHelmDispatchTick = 0;
        nativeHelmDeadline = 0;
        nativeHelmUseCallbacks = nativeHelmStopCallbacks = 0;
        nativeHelmShip = LocalShip;
        nativeHelmAgent = LocalCaptain;
        nativeHelmPoint = LocalShip?.ShipControllerMachine?.PilotStandingPoint;
        try
        {
            string blocker = NativeHelmPrecondition(take);
            if (blocker != null)
            {
                if (nativeHelmPhase == "failed") return nativeHelmReceipt = "failed:" + blocker;
                nativeHelmPhase = "rejected";
                nativeHelmFailure = blocker;
                return nativeHelmReceipt = "rejected:" + blocker;
            }
            // Dispatch is not completion. Only a later mission tick can observe both identities.
            nativeHelmPhase = "pending";
            nativeHelmDispatched = true;
            nativeHelmDispatchTick = nativeHelmTicks;
            nativeHelmDeadline = ControlNow + 2;
            nativeHelmReceipt = "dispatched:synthetic_native_helm_pending_observation";
            if (take) nativeHelmAgent.HandleStartUsingAction(nativeHelmPoint, -1);
            else nativeHelmAgent.HandleStopUsingAction();
            return nativeHelmReceipt;
        }
        catch (Exception exception)
        {
            FailNativeHelm("native_exception:" + exception.GetType().FullName);
            return nativeHelmReceipt = nativeHelmDispatched
                ? "failed:synthetic_native_helm_uncertain" : "failed:native_helm_preflight_exception";
        }
    }

    private string NativeHelmIdentityBlocker()
    {
        if (Mission == null || Mission != Mission.Current || !CanUseNativeControls || factoryTerminal)
            return "native_helm_lifetime_or_authority";
        var ship = LocalShip;
        var machine = ship?.ShipControllerMachine;
        if (nativeHelmAgent == null || nativeHelmAgent != LocalCaptain || nativeHelmAgent != Mission.MainAgent
            || nativeHelmAgent.Pointer == UIntPtr.Zero || !nativeHelmAgent.IsActive() || !nativeHelmAgent.IsPlayerControlled)
            return "native_helm_agent_identity";
        if (ship == null || ship != nativeHelmShip || ship.ShipOrigin is not NavalLabShipOrigin
            || ship.ShipOrigin.Hull != hull || !ship.IsDeployed || nativeDeploymentCallbacks != 1 || !ship.GameEntity.IsValid
            || machine == null || machine.AttachedShip != ship || nativeHelmPoint == null
            || machine.PilotStandingPoint != nativeHelmPoint || !nativeHelmPoint.GameEntity.IsValid
            || nativeHelmAgent.Formation != ship.Formation)
            return "native_helm_ship_or_point_identity";
        return null;
    }

    private string NativeHelmPrecondition(bool take)
    {
        var blocker = NativeHelmIdentityBlocker();
        if (blocker != null) return blocker;
        if (GameNetwork.IsClientOrReplay) return "native_network_client_or_replay";
        var point = nativeHelmPoint;
        var agent = nativeHelmAgent;
        if (point.UserAgent != null && point.UserAgent != agent) return "foreign_occupant";
        if (agent.CurrentlyUsedGameObject != null && agent.CurrentlyUsedGameObject != point) return "agent_using_other_object";
        if ((point.UserAgent == agent) != (agent.CurrentlyUsedGameObject == point))
        {
            FailNativeHelm("contradictory_use_identity");
            return "contradictory_use_identity";
        }
        if (point.MovingAgent != null || point.HasAIMovingTo) return "standing_point_reserved";
        if (take && point.UserAgent == agent) return "already_occupied_by_owner";
        if (!take && point.UserAgent == null) return "already_clear";
        var controller = Mission.GetMissionBehavior<MissionMainAgentController>();
        var screen = controller?.MissionScreen;
        if (screen == null || !controller._activated || controller.IsDisabled || !ScreenManager._isWindowFocused
            || ScreenManager.TopScreen != screen || screen.IsCheatGhostMode || screen.IsPhotoModeEnabled
            || screen.IsRadialMenuActive || Mission.IsOrderMenuOpen || !Mission.IsMainAgentItemInteractionEnabled
            || Mission.GetMissionBehavior<MissionShipControlView>()?.IsDisplayingADialog != false)
            return "native_interaction_view_unavailable";
        if (!take) return null;
        if (!Mission.IsMainAgentObjectInteractionEnabled || agent.HasMount || !agent.IsAbleToUseMachine()
            || !point.IsFocusable || !agent.ObjectHasVacantPosition(point) || !agent.CanUseObject(point)
            || LocalShip.ShipControllerMachine.IsAttachedShipVacant()) return "native_use_ineligible";
        if (LocalShip.ShipControllerMachine.GetValidVacantReachableStandingPointForAgent(agent) != point.GameEntity)
            return "native_helm_not_reachable";
        if (controller.InteractionComponent.CurrentFocusedObject != point
            || controller.InteractionComponent._currentInteractableObject != point) return "native_helm_not_focused";
        return null;
    }

    private void FailNativeHelm(string reason)
    {
        nativeHelmPhase = "failed";
        nativeHelmFailure = reason;
        CaptureNativeHelmOutcome();
        Reject("native.helm_" + reason);
    }

    private void CancelPendingNativeHelm()
    {
        if (IsTwoClientNative && nativeHelmPhase == "pending") FailNativeHelm("terminal_before_observation");
    }

    private void TickNativeHelm()
    {
        nativeHelmTicks++;
        if (!IsTwoClientNative || nativeHelmPhase != "pending" || nativeHelmTicks <= nativeHelmDispatchTick) return;
        string blocker = NativeHelmIdentityBlocker();
        if (blocker != null) { FailNativeHelm(blocker); return; }
        var user = nativeHelmPoint.UserAgent;
        var used = nativeHelmAgent.CurrentlyUsedGameObject;
        if ((user != null && user != nativeHelmAgent) || (used != null && used != nativeHelmPoint))
        {
            FailNativeHelm("foreign_identity_after_dispatch");
            return;
        }
        if (ControlNow >= nativeHelmDeadline) { FailNativeHelm("observation_timeout"); return; }
        bool complete = nativeHelmTake ? user == nativeHelmAgent && used == nativeHelmPoint : user == null && used == null;
        if (!complete) return;
        nativeHelmPhase = nativeHelmTake ? "observed_taken" : "observed_released";
        nativeHelmObservedTick = nativeHelmTicks;
        CaptureNativeHelmOutcome();
    }

    private void CaptureNativeHelmOutcome()
    {
        nativeHelmLastOutcome = new
        {
            operationId = nativeHelmOperation, incarnation = manifest.IncarnationId, epoch = 1,
            owner = ownControllerId, slot = OwnSlot, shipId = manifest.Ships[OwnSlot],
            combatantId = manifest.Combatants[OwnSlot * NavalLabManifest.CrewPerShip],
            pointId = nativeHelmPoint?.Id.Id, pointCreatedAtRuntime = nativeHelmPoint?.Id.CreatedAtRuntime,
            phase = nativeHelmPhase, failure = nativeHelmFailure, dispatched = nativeHelmDispatched,
            observedTick = nativeHelmObservedTick,
            pointUser = nativeHelmPoint?.UserAgent == null ? "none" : nativeHelmPoint.UserAgent == nativeHelmAgent ? "exact" : "other",
            agentUsedObject = nativeHelmAgent?.CurrentlyUsedGameObject == null ? "none"
                : nativeHelmAgent.CurrentlyUsedGameObject == nativeHelmPoint ? "exact" : "other"
        };
    }

    private object InspectNativeHelmPreconditions()
    {
        try
        {
            if (Mission == null || Mission != Mission.Current || factoryTerminal || !nativeDeploymentComplete)
                return new { unavailable = "mission_lifetime_or_deployment" };
            var agent = LocalCaptain;
            var point = LocalShip?.ShipControllerMachine?.PilotStandingPoint;
            var controller = Mission?.GetMissionBehavior<MissionMainAgentController>();
            if (agent == null || agent.Pointer == UIntPtr.Zero || !agent.IsActive() || point == null || !point.GameEntity.IsValid)
                return new { unavailable = "agent_or_point" };
            return new
            {
                controlsReady = CanUseNativeControls, deploymentCallbacks = nativeDeploymentCallbacks,
                point.IsDeactivated, disabledForAgent = point.IsDisabledForAgent(agent), usableByAgent = point.IsUsableByAgent(agent),
                agent.HasMount, ableToUseMachine = agent.IsAbleToUseMachine(), point.HasAIMovingTo,
                movingAgent = InspectHelmAgent(point.MovingAgent),
                distanceSquared = agent.Position.DistanceSquared(point.InteractionEntity.GlobalPosition),
                interactionDistance = agent.GetInteractionDistanceToUsable(point),
                nativeReachableOwnPoint = agent.CurrentlyUsedGameObject == null
                    && LocalShip.ShipControllerMachine.GetValidVacantReachableStandingPointForAgent(agent) == point.GameEntity,
                focusedPointId = (controller?.InteractionComponent.CurrentFocusedObject as UsableMissionObject)?.Id.Id,
                interactablePointId = (controller?.InteractionComponent._currentInteractableObject as UsableMissionObject)?.Id.Id,
                controllerActive = controller != null && controller._activated && !controller.IsDisabled,
                windowFocused = ScreenManager._isWindowFocused,
                missionScreenOnTop = controller?.MissionScreen != null && ScreenManager.TopScreen == controller.MissionScreen
            };
        }
        catch (Exception exception) { return new { unavailable = exception.GetType().FullName }; }
    }

    internal object InspectHelmStatus()
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Read helm status on the game thread.");
        var view = Mission?.GetMissionBehavior<MissionShipControlView>();
        return new
        {
            incarnation = manifest.IncarnationId, epoch = 1, owner = ownControllerId, slot = OwnSlot,
            operationId = nativeHelmOperation, requested = nativeHelmOperation != Guid.Empty,
            action = nativeHelmOperation == Guid.Empty ? null : nativeHelmTake ? "native-take-helm" : "native-release-helm",
            dispatched = nativeHelmDispatched, phase = nativeHelmPhase, pending = nativeHelmPhase == "pending",
            failure = nativeHelmFailure, receipt = nativeHelmReceipt, lastOutcome = nativeHelmLastOutcome,
            nativeHelmDispatchTick, nativeHelmObservedTick, nativeHelmUseCallbacks, nativeHelmStopCallbacks,
            remainingObservationSeconds = nativeHelmPhase == "pending" ? Math.Max(0, nativeHelmDeadline - ControlNow) : 0,
            identity = OwnSlot >= 0 && OwnSlot < Ships.Length ? InspectHelmIdentity(LocalShip, OwnSlot) : null,
            requestedAgent = InspectHelmAgent(nativeHelmAgent), requestedPointId = nativeHelmPoint?.Id.Id,
            shipId = OwnSlot >= 0 && OwnSlot < manifest.Ships.Length ? (Guid?)manifest.Ships[OwnSlot] : null,
            preconditions = InspectNativeHelmPreconditions(),
            requestedPointCreatedAtRuntime = nativeHelmPoint?.Id.CreatedAtRuntime,
            viewMatchesOwnHelm = view != null && LocalShip?.ShipControllerMachine != null
                && view.ControllerMachine == LocalShip.ShipControllerMachine,
            inputPermitted = view != null && HasNativeInputPermission(view),
            remotePilotReplication = "unimplemented", keyboardAcceptance = false, terminal = factoryTerminal,
            observation = "Operation completion is a past tick observation; identity is the current read. Receipt is dispatch only."
        };
    }
}
#endif
