#if DEBUG
using Common;
using System;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace Missions.Diagnostics;

public interface IMissionInspection
{
    object Read(MissionInspectionSlice slice, int offset, int limit);
}

public sealed class MissionInspection : IMissionInspection
{
    public const int MaximumPageSize = 16;
    public const int MaximumOffset = 100000;

    public object Read(MissionInspectionSlice slice, int offset, int limit)
    {
        if (!Enum.IsDefined(typeof(MissionInspectionSlice), slice) || offset < 0 || offset > MaximumOffset
            || limit < 1 || limit > MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(offset));
        object result = null;
        GameThread.Run(() =>
        {
            try { result = ReadMission(Mission.Current, slice, offset, limit); }
            catch (Exception exception) { result = new { status = "error", error = Text(exception.GetType().Name) }; }
        }, blocking: true);
        return result;
    }

    internal object ReadMission(Mission mission, MissionInspectionSlice slice, int offset, int limit)
    {
        if (mission == null) return new { status = "unavailable:no_mission" };
        if (mission.IsFinalized) return new { status = "unavailable:finalized_mission" };
        if (mission.CurrentState != Mission.State.Continuing || mission.MissionEnded)
            return new { status = "unavailable:mission_not_continuing", state = mission.CurrentState.ToString() };
        switch (slice)
        {
            case MissionInspectionSlice.Summary:
                return new
                {
                    status = "observed", scene = Text(mission.SceneName), state = mission.CurrentState.ToString(),
                    mode = mission.Mode.ToString(), deploymentFinished = mission.IsDeploymentFinished,
                    agentCount = mission.AllAgents.Count, behaviorCount = mission.MissionBehaviors.Count,
                    mainAgent = ReadAgent(mission, mission.MainAgent),
                    note = "Sequential game-thread getters, not an atomic physics or rendering snapshot."
                };
            case MissionInspectionSlice.Agents:
                int total = mission.AllAgents.Count;
                var rows = mission.AllAgents.Skip(offset).Take(limit).Select(agent => ReadAgent(mission, agent)).ToArray();
                return new { status = "observed", offset, limit, total, nextOffset = offset + rows.Length < total ? (int?)(offset + rows.Length) : null, rows };
            case MissionInspectionSlice.Views:
                var behaviors = mission.MissionBehaviors;
                var views = behaviors.Skip(offset).Take(limit).Select(behavior => new
                {
                    type = Text(behavior.GetType().FullName), isView = behavior is MissionView,
                    cameraModeLogic = behavior is ICameraModeLogic,
                    finalized = (behavior as MissionView)?.IsFinalized,
                    screenMatches = behavior is MissionView view && view.MissionScreen != null
                        && ReferenceEquals(view.MissionScreen, ScreenManager.TopScreen),
                    readiness = "unavailable:virtual_IsReady_not_invoked"
                }).ToArray();
                return new { status = "observed", offset, limit, total = behaviors.Count,
                    nextOffset = offset + views.Length < behaviors.Count ? (int?)(offset + views.Length) : null, rows = views };
            default:
                return ReadCamera(mission);
        }
    }

    private object ReadCamera(Mission mission)
    {
        var screen = ScreenManager.TopScreen as MissionScreen;
        if (screen == null || !ReferenceEquals(screen.Mission, mission) || !screen.IsActive)
            return new { status = "unavailable:no_active_mission_screen" };
        var camera = screen.CombatCamera;
        if (mission.Scene == null || mission.Scene.Pointer == UIntPtr.Zero || screen.SceneLayer == null
            || camera == null || camera.Pointer == UIntPtr.Zero)
            return new { status = "unavailable:camera_or_scene" };
        var frame = camera.Frame;
        var controller = mission.GetMissionBehavior<MissionMainAgentController>();
        var focus = controller?.InteractionComponent?.CurrentFocusedObject;
        return new
        {
            status = "observed", source = "MissionScreen.CombatCamera", screenType = Text(screen.GetType().FullName),
            sceneBoundCamera = "unavailable:no_read_only_scene_view_camera_getter",
            observerMode = "unavailable:GetSpectatingData_performs_input_selection",
            origin = new MissionInspectionVector(frame.origin), rotationS = new MissionInspectionVector(frame.rotation.s),
            rotationF = new MissionInspectionVector(frame.rotation.f), rotationU = new MissionInspectionVector(frame.rotation.u),
            customCameraPresent = screen.CustomCamera != null, screen.IsDeploymentActive,
            screen.IsPhotoModeEnabled, screen.IsCheatGhostMode, screen.IsFocusLost, screen.LockCameraMovement,
            mainAgentControllerDisabled = controller?.IsDisabled,
            followedAgent = ReadAgent(mission, screen.LastFollowedAgent),
            mainAgentDistance = Distance(mission, mission.MainAgent, frame.origin),
            followedAgentDistance = Distance(mission, screen.LastFollowedAgent, frame.origin),
            focusType = Text(focus?.GetType().FullName), focusedAgentIndex = (focus as Agent)?.Index,
            focusedAgentDistance = Distance(mission, focus as Agent, frame.origin),
            focusIdentity = focus == null ? "none" : focus is Agent ? "agent_index" : "unavailable:non_agent_focus",
            note = "CombatCamera is not proof of the scene-bound camera or visible pixels; distances are world-space."
        };
    }

    private bool CanReadAgent(Mission mission, Agent agent) => agent != null && ReferenceEquals(agent.Mission, mission)
        && agent.HasBeenBuilt && agent.Pointer != UIntPtr.Zero && mission.AllAgents.Contains(agent);

    private object ReadAgent(Mission mission, Agent agent)
    {
        if (agent == null) return new { status = "unavailable:no_agent" };
        if (!CanReadAgent(mission, agent)) return new { index = agent.Index, status = "unavailable:agent_lifetime" };
        try
        {
            var state = agent.State;
            if (state != AgentState.Active) return new { index = agent.Index, status = "unavailable:inactive_agent", state = state.ToString() };
            var support = agent.GetSteppedEntity();
            bool? visible = null;
            // Do not use AgentVisuals: its getter hydrates the weak-reference cache.
            if (agent._visualsWeakRef.TryGetTarget(out var visuals) && visuals != null
                && visuals.Pointer != UIntPtr.Zero && visuals.IsValid()) visible = visuals.GetVisible();
            return new
            {
                status = "observed", index = agent.Index, state = state.ToString(), controller = agent.Controller.ToString(),
                movementMode = agent.MovementMode.ToString(), position = new MissionInspectionVector(agent.Position),
                health = Number(agent.Health), supportPresent = support.IsValid,
                supportName = support.IsValid ? Text(support.Name) : null,
                visualsVisible = visible, visibilityStatus = visible.HasValue ? "observed:visuals_flag_not_camera_visibility" : "unavailable:no_cached_valid_visuals",
                note = "Stepped entity and movement mode are raw observations, not a swimming or stable-contact inference."
            };
        }
        catch (Exception exception) { return new { index = agent.Index, status = "error", error = Text(exception.GetType().Name) }; }
    }

    private double? Distance(Mission mission, Agent agent, Vec3 origin)
    {
        if (!CanReadAgent(mission, agent) || agent.State != AgentState.Active) return null;
        var position = agent.Position;
        double x = (double)position.x - origin.x, y = (double)position.y - origin.y, z = (double)position.z - origin.z;
        double distance = Math.Sqrt((x * x) + (y * y) + (z * z));
        return double.IsNaN(distance) || double.IsInfinity(distance) ? (double?)null : distance;
    }

    private float? Number(float value) => float.IsNaN(value) || float.IsInfinity(value) ? (float?)null : value;
    private string Text(string value) => value?.Length > 160 ? value.Substring(0, 160) : value;
}

public enum MissionInspectionSlice { Summary, Camera, Agents, Views }

/// <summary>Only finite scalar components cross JSON; never serialize an engine vector or frame.</summary>
public sealed class MissionInspectionVector
{
    public float? X { get; }
    public float? Y { get; }
    public float? Z { get; }
    public string Status { get; }

    public MissionInspectionVector(Vec3 value)
    {
        X = float.IsNaN(value.x) || float.IsInfinity(value.x) ? (float?)null : value.x;
        Y = float.IsNaN(value.y) || float.IsInfinity(value.y) ? (float?)null : value.y;
        Z = float.IsNaN(value.z) || float.IsInfinity(value.z) ? (float?)null : value.z;
        Status = X.HasValue && Y.HasValue && Z.HasValue ? "observed" : "unavailable:nonfinite_component";
    }
}
#endif
