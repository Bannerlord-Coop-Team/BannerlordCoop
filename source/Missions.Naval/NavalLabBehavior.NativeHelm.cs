#if DEBUG
using System;
using Common;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.View.MissionViews;
using NavalDLC.Missions.Objects;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private bool nativeAutoHelmAttempted;
    private bool nativeAutoHelmObserved;
    internal bool CanUseNativeInput => CanUseNativeControls && (!IsTwoClientNative || (nativeAutoHelmObserved && HelmReplicasReady));

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
        if (!nativeAutoHelmObserved) return "rejected:auto_helm_setup_incomplete";
        if (!HelmReplicasReady) return "rejected:helm_replicas_pending";
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

    private void TrySeatNativeHelmAfterDeployment()
    {
        if (!IsTwoClientNative || nativeAutoHelmAttempted || !nativeDeploymentComplete
            || factoryTerminal || nativeTerminalHold || Blocker != null || NativeAuthority?.Invoke() != true) return;
        nativeAutoHelmAttempted = true;
        nativeHelmTake = true;
        nativeHelmShip = LocalShip;
        nativeHelmAgent = LocalCaptain;
        nativeHelmPoint = LocalShip?.ShipControllerMachine?.PilotStandingPoint;
        try
        {
            var blocker = NativeHelmIdentityBlocker();
            if (blocker != null) { FailNativeHelm(blocker); return; }
            var machine = nativeHelmShip.ShipControllerMachine;
            var agent = nativeHelmAgent;
            var point = nativeHelmPoint;
            if (!GameThread.Instance.IsGameThread || GameNetwork.IsClientOrReplay
                || nativeAfterDeploymentCallbacks != 1 || !Mission.IsDeploymentFinished || Mission.Mode != MissionMode.Battle
                || !nativeHelmShip.IsInitialized || !machine.GameEntity.IsValid
                || machine._navalShipsLogic == null || machine._navalShipsLogic != Mission.GetMissionBehavior<NavalShipsLogic>()
                || machine._navalAgentsLogic == null || machine._navalAgentsLogic != Mission.GetMissionBehavior<NavalAgentsLogic>()
                || point.GetComponent<ResetAnimationOnStopUsageComponent>() == null)
            {
                FailNativeHelm("auto_spawn_not_initialized");
                return;
            }
            if (point.UserAgent != null || machine.PilotAgent != null || agent.CurrentlyUsedGameObject != null
                || point.MovingAgent != null || point.HasAIMovingTo || point.IsDeactivated
                || !agent.IsAbleToUseMachine() || !agent.CanUseObject(point) || machine.IsAttachedShipVacant())
            {
                FailNativeHelm("auto_spawn_occupied_or_ineligible");
                return;
            }
            // Only lab setup uses native spawn placement; normal take still requires focus and reachability.
            nativeHelmPhase = "pending";
            nativeHelmDispatched = true;
            nativeHelmDispatchTick = nativeHelmTicks;
            nativeHelmDeadline = ControlNow + 2;
            nativeHelmReceipt = "dispatched:automatic_native_spawn_pending_observation";
            agent.UseGameObject(point);
            if (point.UserAgent != agent || agent.CurrentlyUsedGameObject != point || machine.PilotAgent != agent)
            {
                FailNativeHelm("auto_spawn_use_identity_missing");
                return;
            }
            machine.OnPilotAssignedDuringSpawn();
        }
        catch (Exception exception)
        {
            FailNativeHelm("auto_spawn_exception:" + exception.GetType().FullName);
        }
    }

    private string NativeHelmIdentityBlocker()
    {
        if (Mission == null || Mission != Mission.Current || !CanUseNativeControls || factoryTerminal)
            return "native_helm_lifetime_or_authority";
        var ship = LocalShip;
        var machine = ship?.ShipControllerMachine;
        if (nativeHelmAgent == null || nativeHelmAgent != LocalCaptain || nativeHelmAgent != Mission.MainAgent
            || nativeHelmAgent != Mission.InitialPlayerAgent || nativeHelmAgent.Mission != Mission
            || nativeHelmAgent.Pointer == UIntPtr.Zero || !nativeHelmAgent.IsActive() || !nativeHelmAgent.IsPlayerControlled
            || !nativeHelmAgent.IsHuman || nativeHelmAgent.HasMount || nativeHelmAgent.IsMount)
            return "native_helm_agent_identity";
        if (ship == null || ship != nativeHelmShip || ship.ShipOrigin is not NavalLabShipOrigin
            || ship.ShipOrigin.Hull != hull || !ship.IsDeployed || nativeDeploymentCallbacks != 1 || !ship.GameEntity.IsValid
            || machine == null || machine.AttachedShip != ship || nativeHelmPoint == null
            || machine.PilotStandingPoint != nativeHelmPoint || !nativeHelmPoint.GameEntity.IsValid
            || nativeHelmAgent.Formation != ship.Formation)
            return "native_helm_ship_or_point_identity";
        return null;
    }

    private bool HasOccupiedLocalHelm()
    {
        if (!IsTwoClientNative || !nativeAutoHelmObserved || nativeHelmPhase == "pending" || nativeHelmPhase == "failed"
            || NativeHelmIdentityBlocker() != null) return false;
        var machine = nativeHelmShip.ShipControllerMachine;
        return machine.GameEntity.IsValid && !nativeHelmPoint.IsDeactivated
            && nativeHelmPoint.UserAgent == nativeHelmAgent && machine.PilotAgent == nativeHelmAgent
            && nativeHelmAgent.CurrentlyUsedGameObject == nativeHelmPoint;
    }

    internal bool IsOccupiedHelmMovement(Guid incarnationId, Guid combatantId, Agent agent) =>
        incarnationId == manifest.IncarnationId && HasOccupiedLocalHelm()
        && manifest.Combatants[OwnSlot * NavalLabManifest.CrewPerShip] == combatantId && agent == nativeHelmAgent;

    private void RefreshFollowerHelmTarget()
    {
        if (factoryHost || !HasOccupiedLocalHelm() || !nativeHelmPoint.LockUserFrames) return;
        // Refresh the native target after follower hull writes, without moving the actor directly.
        var frame = nativeHelmPoint.GetUserFrameForAgent(nativeHelmAgent);
        nativeHelmAgent.SetTargetPositionAndDirection(frame.Origin.AsVec2, in frame.Rotation.f);
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
        if (NativeInteractionViewBlocker() != null) return "native_interaction_view_unavailable";
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

    private string NativeInteractionViewBlocker()
    {
        var controller = Mission?.GetMissionBehavior<MissionMainAgentController>();
        if (controller == null) return "main_agent_controller_missing";
        if (controller.InteractionComponent == null) return "interaction_component_missing";
        var screen = controller.MissionScreen;
        if (screen == null) return "mission_screen_missing";
        if (!controller._activated || controller.IsDisabled) return "main_agent_controller_inactive";
        if (!ScreenManager._isWindowFocused) return "window_not_focused";
        if (ScreenManager.TopScreen != screen) return "mission_screen_not_on_top";
        if (screen.IsCheatGhostMode) return "cheat_ghost_mode";
        if (screen.IsPhotoModeEnabled) return "photo_mode";
        if (screen.IsRadialMenuActive) return "radial_menu_active";
        if (Mission.IsOrderMenuOpen) return "order_menu_open";
        if (!Mission.IsMainAgentItemInteractionEnabled) return "item_interaction_disabled";
        var view = Mission.GetMissionBehavior<MissionShipControlView>();
        if (view == null) return "ship_control_view_missing";
        return view.IsDisplayingADialog ? "ship_control_dialog" : null;
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
        TrySeatNativeHelmAfterDeployment();
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
        bool complete = nativeHelmTake ? user == nativeHelmAgent && used == nativeHelmPoint
            && nativeHelmShip.ShipControllerMachine.PilotAgent == nativeHelmAgent
            : user == null && used == null && nativeHelmShip.ShipControllerMachine.PilotAgent == null;
        if (!complete) return;
        if (nativeAutoHelmAttempted && nativeHelmOperation == Guid.Empty) nativeAutoHelmObserved = true;
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
                focusedPointId = (controller?.InteractionComponent?.CurrentFocusedObject as UsableMissionObject)?.Id.Id,
                interactablePointId = (controller?.InteractionComponent?._currentInteractableObject as UsableMissionObject)?.Id.Id,
                interactionComponentPresent = controller?.InteractionComponent != null,
                interactionViewBlocker = NativeInteractionViewBlocker(),
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
            autoSpawnAttempted = nativeAutoHelmAttempted, autoSpawnObserved = nativeAutoHelmObserved,
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
            remotePilotReplication = InspectHelmOccupancy(), keyboardAcceptance = false, terminal = factoryTerminal,
            observation = "Operation completion is a past tick observation; identity is the current read. Receipt is dispatch only."
        };
    }
}
#endif
