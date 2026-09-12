#if DEBUG
using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using TaleWorlds.Core;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace Missions.Battles;

public interface ISiegeInteractionDebugBehavior
{
    object Observe();
}

internal sealed class SiegeInteractionDebugBehavior : MissionBehavior, ISiegeInteractionDebugBehavior
{
    private const int UseGameKeyId = 13;
    private readonly IMessageBroker messageBroker;
    private readonly HashSet<string> requests = new HashSet<string>();
    private readonly List<object> inputSamples = new List<object>();
    private readonly Dictionary<int, object> receivedStates = new Dictionary<int, object>();
    private int stateSequence;
    private int inputGameKeyId = UseGameKeyId;
    private readonly Dictionary<int, int> localShots = new Dictionary<int, int>();
    private readonly Dictionary<int, int> receivedShots = new Dictionary<int, int>();
    private MissionMainAgentInteractionComponent interaction;
    private IFocusable eventFocus;
    private bool? eventInteractable;
    private string requestId;
    private string status = "unexercised";
    private bool pressInvoked;
    private bool edgeObserved;
    private bool edgeCleared;
    private int tick;
    private int pressTick;
    private bool removed;
    private Agent capturedAgent;
    private Vec3 capturedPosition;
    private Vec3 capturedLookDirection;
    private MissionScreen capturedScreen;
    private Camera capturedCamera;
    private Camera stagingCamera;
    private bool fixtureRestored;
    private string captureFailureReason;
    private Agent dismountAgent;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public SiegeInteractionDebugBehavior(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkSiegeInteractionDebugRequest>(Handle);
        messageBroker.Subscribe<NetworkSiegeMachineState>(ObserveReceivedState);
        messageBroker.Subscribe<SiegeWeaponFired>(ObserveLocalShot);
        messageBroker.Subscribe<NetworkSiegeWeaponFired>(ObserveReceivedShot);
    }

    public override void OnPreDisplayMissionTick(float dt)
    {
        tick++;
        UpdateDismount();
        BindObserver();
        if (!pressInvoked || edgeCleared || inputSamples.Count >= 300) return;
        var screen = ScreenManager.TopScreen as MissionScreen;
        if (screen?.SceneLayer?.Input == null) return;
        bool pressed = screen.SceneLayer.Input.IsGameKeyPressed(inputGameKeyId);
        bool down = screen.SceneLayer.Input.IsGameKeyDown(inputGameKeyId);
        bool released = screen.SceneLayer.Input.IsGameKeyReleased(inputGameKeyId);
        edgeObserved |= pressed || down;
        edgeCleared = edgeObserved && tick > pressTick && !pressed && !down;
        inputSamples.Add(new { tick, pressed, down, released });
        if (edgeCleared) status = "input_edge_cleared";
        else if (inputSamples.Count == 300) status = "unexercised_input_lifecycle";
    }

    public override void OnRemoveBehavior()
    {
        removed = true;
        messageBroker.Unsubscribe<NetworkSiegeInteractionDebugRequest>(Handle);
        messageBroker.Unsubscribe<NetworkSiegeMachineState>(ObserveReceivedState);
        messageBroker.Unsubscribe<SiegeWeaponFired>(ObserveLocalShot);
        messageBroker.Unsubscribe<NetworkSiegeWeaponFired>(ObserveReceivedShot);
        UnbindObserver();
        ReleaseCamera();
    }

    private void BindObserver()
    {
        var next = Mission?.GetMissionBehavior<MissionMainAgentController>()?.InteractionComponent;
        if (ReferenceEquals(next, interaction)) return;
        UnbindObserver();
        interaction = next;
        if (interaction == null) return;
        interaction.OnFocusGained += OnFocusGained;
        interaction.OnFocusLost += OnFocusLost;
    }

    private void UnbindObserver()
    {
        if (interaction != null)
        {
            interaction.OnFocusGained -= OnFocusGained;
            interaction.OnFocusLost -= OnFocusLost;
        }
        interaction = null;
        eventFocus = null;
        eventInteractable = null;
    }

    private void OnFocusGained(Agent agent, IFocusable focus, bool isInteractable)
    {
        if (agent != Mission.MainAgent) return;
        eventFocus = focus;
        eventInteractable = isInteractable;
    }

    private void OnFocusLost(Agent agent, IFocusable focus)
    {
        if (agent != Mission.MainAgent || !ReferenceEquals(eventFocus, focus)) return;
        eventFocus = null;
        eventInteractable = null;
    }

    private void ObserveLocalShot(MessagePayload<SiegeWeaponFired> payload)
    {
        int id = payload.What.Weapon.Id.Id;
        GameThread.RunSafe(() => CountShot(localShots, id));
    }

    private void ObserveReceivedShot(MessagePayload<NetworkSiegeWeaponFired> payload)
    {
        GameThread.RunSafe(() => CountShot(receivedShots, payload.What.MachineId));
    }

    private void CountShot(Dictionary<int, int> counts, int id)
    {
        if (removed || Mission != TaleWorlds.MountAndBlade.Mission.Current) return;
        counts.TryGetValue(id, out int previous);
        counts[id] = previous + 1;
    }

    private void ObserveReceivedState(MessagePayload<NetworkSiegeMachineState> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (removed || Mission != TaleWorlds.MountAndBlade.Mission.Current) return;
            var message = payload.What;
            receivedStates[message.MachineId] = new
            {
                sequence = ++stateSequence, receiveTick = tick,
                message.HostEpoch, message.AuthorityRevision, message.SenderControllerId,
                message.GateState, message.LadderState, message.StoneAmmo, message.WeaponState
            };
        });
    }

    private void Handle(MessagePayload<NetworkSiegeInteractionDebugRequest> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => ApplyRequest(payload.What));
    }

    private void ApplyRequest(NetworkSiegeInteractionDebugRequest request)
    {
        var session = Mission?.GetMissionBehavior<CoopBattleController>()?.Session;
        if (removed || Mission != TaleWorlds.MountAndBlade.Mission.Current || session == null ||
            session.InstanceId != request.MapEventId || session.OwnControllerId != request.ControllerId ||
            string.IsNullOrEmpty(request.RequestId) || requests.Contains(request.RequestId)) return;
        if (pressInvoked && !edgeCleared && request.Action != "restore") return;
        if (requests.Count >= 256) return;
        requests.Add(request.RequestId);
        requestId = request.RequestId;
        if (status == "fixture_dismount_pending") status = "unexercised";
        BindObserver();
        var screen = ScreenManager.TopScreen as MissionScreen;
        var agent = Mission.MainAgent;
        if (request.Action == "dismount")
        {
            Dismount(agent);
            return;
        }
        if (request.Action == "capture")
        {
            Capture(screen, agent);
            return;
        }
        if (request.Action == "stage" || request.Action == "watch")
        {
            Stage(screen, agent, request.MachineId, request.StandingPointIndex, request.Action == "watch");
            return;
        }
        if (request.Action == "restore")
        {
            Restore(screen, agent);
            return;
        }
        if (request.Action != "use" && request.Action != "stop" && request.Action != "fire" &&
            request.Action != "attack")
        {
            status = "unexercised_unknown_action";
            return;
        }
        inputGameKeyId = (request.Action == "fire" || request.Action == "attack") ? 9 : UseGameKeyId;
        pressInvoked = false;
        edgeObserved = false;
        edgeCleared = false;
        inputSamples.Clear();
        var focusedMachine = interaction?.CurrentFocusedMachine as MissionObject;
        var focusedObject = interaction?.CurrentFocusedObject as MissionObject;
        var machine = Mission.MissionObjects.OfType<UsableMachine>()
            .FirstOrDefault(candidate => candidate.Id.Id == request.MachineId);
        if (request.Action == "stop" && request.MachineId == 0 && agent?.IsUsingGameObject == true)
            machine = Mission.MissionObjects.OfType<UsableMachine>().FirstOrDefault(candidate =>
                candidate.StandingPoints.Any(point => ReferenceEquals(point, agent.CurrentlyUsedGameObject)));
        bool usingTarget = agent?.IsUsingGameObject == true && machine != null &&
            machine.StandingPoints.Any(point => ReferenceEquals(point, agent.CurrentlyUsedGameObject));
        bool focusedTarget = (focusedMachine?.Id.Id == request.MachineId ||
            focusedObject?.Id.Id == request.MachineId) && interaction?._currentInteractableObject != null;
        bool actionReady = request.Action == "fire"
            ? usingTarget && machine is RangedSiegeWeapon
            : request.Action == "attack"
                ? capturedAgent == agent && agent != null && !agent.IsUsingGameObject
                : request.Action == "stop" ? usingTarget : focusedTarget && agent != null && !agent.IsUsingGameObject;
        if (screen?.SceneLayer?.Input == null || agent == null || !agent.IsActive() || !actionReady)
        {
            status = "unexercised_no_focus";
            return;
        }
        if (screen.SceneLayer.Input.IsGameKeyDown(inputGameKeyId) ||
            screen.SceneLayer.Input.IsGameKeyPressed(inputGameKeyId))
        {
            status = "unexercised_input_busy";
            return;
        }
        var key = HotKeyManager.GetCategory(CombatHotKeyCategory.CategoryId)?.GetGameKey(inputGameKeyId);
        if (key?.KeyboardKey == null)
        {
            status = "unexercised_unbound_key";
            return;
        }
        pressTick = tick;
        status = "press_invoked_outcome_pending";
        pressInvoked = true;
        Input.PressKey(key.KeyboardKey.InputKey);
    }

    private void Dismount(Agent agent)
    {
        dismountAgent = null;
        if (agent == null || !agent.IsActive() || agent.IsUsingGameObject)
        {
            status = "fixture_dismount_rejected";
            return;
        }
        if (agent.MountAgent == null)
        {
            status = "fixture_dismounted";
            return;
        }
        var controller = Mission?.GetMissionBehavior<MissionMainAgentController>();
        if (controller == null)
        {
            status = "fixture_dismount_rejected";
            return;
        }
        dismountAgent = agent;
        status = "fixture_dismount_pending";
        // Let the player controller retain braking until it can dismount.
        controller._autoDismountModeActive = true;
    }

    private void UpdateDismount()
    {
        if (dismountAgent == null) return;
        if (!dismountAgent.IsActive() || dismountAgent.IsUsingGameObject)
        {
            if (status == "fixture_dismount_pending") status = "fixture_dismount_rejected";
            dismountAgent = null;
        }
        else if (dismountAgent.MountAgent == null)
        {
            if (status == "fixture_dismount_pending") status = "fixture_dismounted";
            dismountAgent = null;
        }
    }

    private void Capture(MissionScreen screen, Agent agent)
    {
        captureFailureReason = null;
        if (RejectCapture(capturedAgent != null, "already_captured") ||
            RejectCapture(screen?.CombatCamera == null, "combat_camera_missing") ||
            RejectCapture(agent == null, "agent_missing") ||
            RejectCapture(!agent.IsActive(), "agent_inactive") ||
            RejectCapture(agent.IsUsingGameObject, "agent_using_object") ||
            RejectCapture(agent.MountAgent != null, "agent_mounted")) return;
        capturedAgent = agent;
        capturedPosition = agent.Position;
        capturedLookDirection = agent.LookDirection;
        capturedScreen = screen;
        capturedCamera = screen.CustomCamera;
        fixtureRestored = false;
        status = "fixture_captured";
    }

    internal bool RejectCapture(bool rejected, string reason)
    {
        if (!rejected) return false;
        captureFailureReason = reason;
        status = "fixture_capture_rejected";
        return true;
    }

    private void Stage(MissionScreen screen, Agent agent, int machineId, int pointIndex, bool watchOnly)
    {
        var machine = Mission.MissionObjects.OfType<UsableMachine>()
            .FirstOrDefault(candidate => candidate.Id.Id == machineId);
        if (capturedAgent != agent || agent == null || !agent.IsActive() || agent.IsUsingGameObject ||
            screen != capturedScreen || machine == null || pointIndex < 0 ||
            pointIndex >= machine.StandingPoints.Count ||
            !ReferenceEquals(screen.CustomCamera, stagingCamera ?? capturedCamera))
        {
            status = "fixture_stage_rejected";
            return;
        }
        var point = machine.StandingPoints[pointIndex];
        var position = point.GetUserFrameForAgent(agent).Origin.GetGroundVec3();
        if (stagingCamera == null)
        {
            stagingCamera = Camera.CreateCamera();
            stagingCamera.FillParametersFrom(screen.CombatCamera);
        }
        if (!watchOnly) agent.TeleportToPosition(position);
        var target = GetStagingTarget(machine, point.GameEntity.GlobalPosition, watchOnly);
        var eye = watchOnly ? target + new Vec3(3f, 3f, 2f) : position + (Vec3.Up * 1.6f);
        var up = Math.Abs(Vec3.DotProduct((target - eye).NormalizedCopy(), Vec3.Up)) > 0.99f
            ? new Vec3(0f, 1f, 0f) : Vec3.Up;
        stagingCamera.LookAt(eye, target, up);
        screen.CustomCamera = stagingCamera;
        status = watchOnly ? "fixture_observer_camera_staged" : "fixture_staged_native_focus_pending";
    }

    internal Vec3 GetStagingTarget(UsableMachine machine, Vec3 standingPointPosition, bool watchOnly)
    {
        if (watchOnly || !(machine is CastleGate gate)) return standingPointPosition;
        // Gate standing-point origins can lie directly beneath the player's feet.
        var bounds = gate.ComputeGlobalPhysicsBoundingBoxMinMax();
        return (bounds.Item1 + bounds.Item2) * 0.5f;
    }

    private void Restore(MissionScreen screen, Agent agent)
    {
        if (capturedAgent == null || capturedAgent != agent || screen != capturedScreen ||
            !agent.IsActive() || agent.IsUsingGameObject ||
            !ReferenceEquals(screen.CustomCamera, stagingCamera ?? capturedCamera))
        {
            status = "fixture_restore_rejected";
            return;
        }
        agent.TeleportToPosition(capturedPosition);
        agent.LookDirection = capturedLookDirection;
        ReleaseCamera();
        fixtureRestored = (agent.Position - capturedPosition).LengthSquared < 0.01f &&
            ReferenceEquals(screen.CustomCamera, capturedCamera);
        status = fixtureRestored ? "fixture_restored" : "fixture_restore_mismatch";
        if (fixtureRestored) capturedAgent = null;
    }

    private void ReleaseCamera()
    {
        if (stagingCamera == null) return;
        if (capturedScreen != null && ReferenceEquals(capturedScreen.CustomCamera, stagingCamera))
            capturedScreen.CustomCamera = capturedCamera;
        stagingCamera.ReleaseCamera();
        stagingCamera = null;
    }

    public object Observe()
    {
        var screen = ScreenManager.TopScreen as MissionScreen;
        var agent = Mission?.MainAgent;
        var session = Mission?.GetMissionBehavior<CoopBattleController>()?.Session;
        return new
        {
            success = !removed && Mission == TaleWorlds.MountAndBlade.Mission.Current,
            sessionId = session?.InstanceId,
            controllerId = session?.OwnControllerId,
            screenPresent = screen != null,
            inputContextPresent = screen?.SceneLayer?.Input != null,
            mainAgentActive = agent?.IsActive() == true,
            wieldedSlot = agent == null ? -1 : (int)agent.GetPrimaryWieldedItemIndex(),
            focusedObject = Describe(interaction?.CurrentFocusedObject),
            focusedMachine = Describe(interaction?.CurrentFocusedMachine),
            nativeInteractable = interaction?._currentInteractableObject != null,
            focusEventObject = Describe(eventFocus),
            focusEventInteractable = eventInteractable,
            usingObject = agent?.IsUsingGameObject == true,
            usedObject = Describe(agent?.CurrentlyUsedGameObject),
            requestId, status, pressInvoked, edgeObserved, edgeCleared, inputGameKeyId, tick,
            fixtureActive = capturedAgent != null,
            fixtureRestored, captureFailureReason,
            stagingCameraActive = stagingCamera != null && ReferenceEquals(screen?.CustomCamera, stagingCamera),
            focusDiagnostic = ReadFocusDiagnostic(screen, agent),
            inputSamples = inputSamples.ToArray(),
            receivedStates, localShots, receivedShots,
            equipment = ReadEquipment(agent),
            machines = Mission?.MissionObjects.OfType<UsableMachine>().Select(machine => new
            {
                id = machine.Id.Id,
                type = machine.GetType().Name,
                machine.IsDeactivated,
                machine.IsDisabled,
                gateState = machine is CastleGate gate ? (int?)gate.State : null,
                stoneAmmo = machine is StonePile stones ? (int?)stones.AmmoCount : null,
                stoneItemId = machine is StonePile pile ? pile.GivenItemID : null,
                hitPoints = machine.DestructionComponent?.HitPoint,
                ladderState = machine is SiegeLadder ladder ? (int?)ladder.State : null,
                rangedState = machine is RangedSiegeWeapon weapon ? (int?)weapon.State : null,
                standingPoints = machine.StandingPoints.Select((point, index) =>
                {
                    try
                    {
                        float? distanceSquared = null;
                        float? heightDifference = null;
                        bool? reachable = null;
                        if (agent != null)
                        {
                            var userFrame = point.GetUserFrameForAgent(agent);
                            distanceSquared = userFrame.Origin.AsVec2.DistanceSquared(agent.Position.AsVec2);
                            float pointHeight = point.UseOwnPositionInsteadOfWorldPosition
                                ? point.GameEntity.GlobalPosition.z : userFrame.Origin.GetGroundVec3().z;
                            heightDifference = Math.Abs(pointHeight - agent.Position.z);
                            reachable = agent.CanReachAndUseObject(point, distanceSquared.Value);
                        }
                        return (object)new
                        {
                            index,
                            id = point.Id.Id,
                            point.IsDeactivated,
                            point.IsDisabledForPlayers,
                            disabledForMainAgent = agent == null ? (bool?)null : point.IsDisabledForAgent(agent),
                            occupied = point.UserAgent != null,
                            point.HasAIUser,
                            vacantForPlayer = !point.HasUser || point.HasAIUser,
                            ownedByMainAgent = agent != null && point.UserAgent == agent,
                            distanceSquared, heightDifference, reachable,
                            heightWithinReach = heightDifference.HasValue ? (bool?)(heightDifference.Value < 1.5f) : null,
                            x = point.GameEntity.GlobalPosition.X,
                            y = point.GameEntity.GlobalPosition.Y,
                            z = point.GameEntity.GlobalPosition.Z
                        };
                    }
                    catch (Exception exception)
                    {
                        return new { index, diagnosticError = exception.ToString() };
                    }
                }).ToArray()
            }).ToArray()
        };
    }

    internal object ReadFocusDiagnostic(MissionScreen screen, Agent agent)
    {
        if (removed) return null;
        if (screen == null || agent == null)
            return new { tick, requestId, unavailable = "camera_agent_or_scene_missing" };
        object gates = null;
        try
        {
            var controller = Mission?.GetMissionBehavior<MissionMainAgentController>();
            var focusAgent = Agent.Main;
            gates = new
            {
                controllerPresent = controller != null,
                controllerDisabled = controller?.IsDisabled,
                controllerActivated = controller?._activated,
                agentState = agent == null ? (int?)null : (int)agent.State,
                agentAIControlled = agent?.IsAIControlled,
                sameFocusAgent = ReferenceEquals(agent, focusAgent),
                focusAgentPresent = focusAgent != null,
                ghostMode = screen?.IsCheatGhostMode,
                photoMode = screen?.IsPhotoModeEnabled,
                missionMode = Mission == null ? (int?)null : (int)Mission.Mode,
                itemInteractionEnabled = Mission?.IsMainAgentItemInteractionEnabled,
                objectInteractionEnabled = Mission?.IsMainAgentObjectInteractionEnabled,
                focusMountable = interaction?._currentInteractableObject is Agent mount && mount.IsMount,
                orderMenuOpen = Mission?.IsOrderMenuOpen,
                key25Down = screen?.SceneLayer?.Input?.IsGameKeyDown(25),
                ableToUseMachine = focusAgent?.IsAbleToUseMachine()
            };
            if (screen?.CombatCamera == null || focusAgent == null || Mission?.Scene == null)
                return new { tick, requestId, gates, unavailable = "camera_agent_or_scene_missing" };
            var camera = screen.CombatCamera;
            var position = camera.Position;
            var direction = camera.Direction;
            var agentPosition = focusAgent.Position;
            float horizontalDistance = new Vec2(position.X, position.Y)
                .Distance(new Vec2(agentPosition.X, agentPosition.Y));
            var origin = position + (direction * horizontalDistance);
            float length = 10f;
            bool blockerHit = Mission.Scene.FocusRayCastForFixedPhysics(origin, origin + (direction * length),
                out float blockerDistance, out Vec3 blockerPoint, out WeakGameEntity blocker,
                0.01f, unchecked((BodyFlags)(-251707585)));
            if (blockerHit) length = blockerDistance;
            bool terrainHit = Mission.Scene.RayCastForClosestEntityOrTerrain(origin, origin + (direction * length),
                out float terrainDistance, out Vec3 terrainPoint, out WeakGameEntity terrain,
                0.01f, unchecked((BodyFlags)(-251707585)));
            if (terrainHit && terrainDistance < length) length = terrainDistance;
            bool focusHit = Mission.Scene.FocusRayCastForFixedPhysics(origin, origin + (direction * (length + 0.1f)),
                out float focusDistance, out Vec3 focusPoint, out WeakGameEntity hit,
                0.2f, (BodyFlags)79617);
            return new
            {
                tick, requestId, gates,
                agentPosition = new[] { agentPosition.X, agentPosition.Y, agentPosition.Z },
                cameraPosition = new[] { position.X, position.Y, position.Z },
                cameraDirection = new[] { direction.X, direction.Y, direction.Z },
                stagingPosition = ReferenceEquals(stagingCamera, null) ? null
                    : new[] { stagingCamera.Position.X, stagingCamera.Position.Y, stagingCamera.Position.Z },
                stagingDirection = ReferenceEquals(stagingCamera, null) ? null
                    : new[] { stagingCamera.Direction.X, stagingCamera.Direction.Y, stagingCamera.Direction.Z },
                rayOrigin = new[] { origin.X, origin.Y, origin.Z },
                // These read-only probes do not replace vanilla's agent and wider fallback ray selection.
                rayProbe = new
                {
                    length, blockerHit, blockerDistance, blockerAncestors = DescribeAncestors(blocker),
                    terrainHit, terrainDistance, terrainAncestors = DescribeAncestors(terrain),
                    focusHit, focusDistance, focusAncestors = DescribeAncestors(hit)
                }
            };
        }
        catch (Exception exception)
        {
            return new { tick, requestId, gates, diagnosticError = exception.ToString() };
        }
    }

    private static object[] DescribeAncestors(WeakGameEntity entity)
    {
        var ancestors = new List<object>();
        for (int depth = 0; entity.IsValid && depth < 16; depth++, entity = entity.Parent)
        {
            var focus = entity.GetFirstScriptWithInterfaceOfType<IFocusable>();
            var missionObject = entity.GetFirstScriptOfType<MissionObject>();
            ancestors.Add(new
            {
                depth, id = missionObject?.Id.Id, type = missionObject?.GetType().Name,
                focus = Describe(focus), isFocusable = focus?.IsFocusable
            });
        }
        return ancestors.ToArray();
    }

    private static object[] ReadEquipment(Agent agent)
    {
        if (agent == null) return Array.Empty<object>();
        var slots = new List<object>();
        for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot;
             index < EquipmentIndex.NumAllWeaponSlots; index++)
        {
            var weapon = agent.Equipment[index];
            slots.Add(new
            {
                slot = (int)index, itemId = weapon.Item?.StringId, amount = weapon.Amount,
                weaponClass = weapon.Item?.PrimaryWeapon?.WeaponClass.ToString()
            });
        }
        return slots.ToArray();
    }

    private static object Describe(IFocusable focus)
    {
        if (focus == null) return null;
        var missionObject = focus as MissionObject;
        return new { type = focus.GetType().Name, id = missionObject?.Id.Id };
    }
}
#endif
