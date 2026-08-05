using Common;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Agents.Packets;
using Missions.Services.Network;
#if DEBUG
using Missions.Diagnostics;
#endif
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Missions.Battles;

/// <summary>Reports state needed to verify co-op battle synchronization.</summary>
internal static class BattleDebugCommands
{
    private static readonly Dictionary<int, Vec3> EnemyPositions = new Dictionary<int, Vec3>();
    private static int ownDamageEvents;
    private static readonly Dictionary<Agent, AgentControllerType> CavalryControllers =
        new Dictionary<Agent, AgentControllerType>();
    private const int MaximumMountPoseSamples = 120;
    private static readonly List<MountPoseSample> MountPoseSamples =
        new List<MountPoseSample>();
    private static Mission observedMission;
    private static Camera ladderCamera;
    private static Camera mountCamera;
    private static MatrixFrame mountCameraLocalFrame;
    private static Agent focusedMount;
    private static Guid focusedMountId;
    private static Agent capturedMount;
    private static Guid capturedMountId;
    private static float mountPoseCaptureStartTime;
    private static BattleDebugTickBehavior battleDebugTickBehavior;

    private sealed class BattleDebugTickBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnPreDisplayMissionTick(float dt)
        {
            CaptureMountPoseFrame();
        }

        public override void OnAgentHit(
            Agent affectedAgent,
            Agent affectorAgent,
            in MissionWeapon affectorWeapon,
            in Blow blow,
            in AttackCollisionData attackCollisionData)
        {
            if (blow.InflictedDamage <= 0
                || affectedAgent?.Team == null
                || affectorAgent?.Team == null
                || !affectedAgent.IsHuman
                || !affectorAgent.IsHuman
                || affectedAgent.Team.Side == affectorAgent.Team.Side)
                return;

            var playerSide = Mission?.PlayerTeam?.Side ?? BattleSideEnum.None;
            if (affectedAgent.Team.Side == playerSide)
                ownDamageEvents++;
        }

        public override void OnRemoveBehavior()
        {
#if DEBUG
            if (ReferenceEquals(focusedPlayerPairMission, Mission))
                ReleasePlayerPairCamera();
#endif
            if (ReferenceEquals(battleDebugTickBehavior, this))
                battleDebugTickBehavior = null;
        }
    }

    private sealed class MountPoseSample
    {
        public float Time { get; set; }
        public float Speed { get; set; }
        public int ActionIndex { get; set; }
        public string ActionName { get; set; }
        public string AnimationName { get; set; }
        public float AnimationProgress { get; set; }
        public float AnimationSpeed { get; set; }
        public int TurnDirection { get; set; }
        public float ChannelWeight { get; set; }
        public float CurrentActionWeight { get; set; }
        public Vec3 HeadPosition { get; set; }
        public Vec3 HeadForward { get; set; }
    }

#if DEBUG
    private static Agent wieldTestAgent;
    private static Guid wieldTestAgentId;
    private static EquipmentIndex wieldTestOriginalMainHand;
    private static bool wieldTestActive;
    private static Camera playerPairCamera;
    private static Agent focusedLocalPlayer;
    private static Agent focusedRemotePlayer;
    private static Guid focusedLocalPlayerId;
    private static Guid focusedRemotePlayerId;
    private static string focusedLocalControllerId;
    private static string focusedRemoteControllerId;
    private static Mission focusedPlayerPairMission;
    private static Vec3 playerPairCameraPosition;
    private static Vec3 playerPairCameraDirection;

    [CommandLineArgumentFunction("relay_state", "coop.debug.battle")]
    public static string RelayState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.battle.relay_state";
        if (!ContainerProvider.TryResolve<IMissionContext>(out var missionContext))
            return "Mission relay context is unavailable.";

        string[] controllerIds = missionContext.ControllersInMission
            .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
            .ToArray();
        string controllers = controllerIds.Length == 0
            ? "none"
            : string.Join(",", controllerIds);
        return $"Mission relay state: remoteControllers={controllerIds.Length} controllers={controllers}.";
    }

    [CommandLineArgumentFunction("action_performance", "coop.debug.battle")]
    public static string ActionPerformance(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.battle.action_performance " +
                   "<start|snapshot|stop|status>";
        }

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                MissionActionDiagnostics.StartPerformance();
                return "Action performance instrumentation is ON.";
            case "snapshot":
                return "ACTION_PERFORMANCE " +
                       MissionActionDiagnostics.SnapshotPerformance(
                           stop: false);
            case "stop":
                return "ACTION_PERFORMANCE " +
                       MissionActionDiagnostics.SnapshotPerformance(
                           stop: true);
            case "status":
                return "Action performance instrumentation is " +
                       (MissionActionDiagnostics.PerformanceEnabled
                           ? "ON."
                           : "OFF.");
            default:
                return "Usage: coop.debug.battle.action_performance " +
                       "<start|snapshot|stop|status>";
        }
    }

    [CommandLineArgumentFunction("animation_trace", "coop.debug.battle")]
    public static string AnimationTrace(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.battle.animation_trace " +
                   "<start|snapshot|stop|status>";
        }

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                MissionActionDiagnostics.StartAnimationTrace();
                return "Battle animation trace is ON.";
            case "snapshot":
                return "BATTLE_ANIMATION_TRACE " +
                       MissionActionDiagnostics.SnapshotAnimationTrace(
                           stop: false);
            case "stop":
                return "BATTLE_ANIMATION_TRACE " +
                       MissionActionDiagnostics.SnapshotAnimationTrace(
                           stop: true);
            case "status":
                return "Battle animation trace is " +
                       (MissionActionDiagnostics.AnimationTraceEnabled
                           ? "ON."
                           : "OFF.");
            default:
                return "Usage: coop.debug.battle.animation_trace " +
                       "<start|snapshot|stop|status>";
        }
    }

    [CommandLineArgumentFunction("wield_test", "coop.debug.battle")]
    public static string WieldTest(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.battle.wield_test <start|restore|status>";
        }

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                return StartWieldTest();
            case "restore":
                return RestoreWieldTest();
            case "status":
                return wieldTestActive
                    ? $"WIELD_TEST active agent={wieldTestAgentId:D}"
                    : "WIELD_TEST inactive";
            default:
                return "Usage: coop.debug.battle.wield_test <start|restore|status>";
        }
    }

    private static string StartWieldTest()
    {
        if (wieldTestActive) return "WIELD_TEST already active";
        Agent agent = Agent.Main;
        if (agent == null || !agent.IsActive() || agent.Mission != Mission.Current)
            return "WIELD_TEST no active main agent";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry)
            || !registry.TryGetAgentInfo(agent, out var info)
            || !registry.IsLocallyControlled(info.AgentId))
        {
            return "WIELD_TEST main agent is not locally controlled";
        }

        EquipmentIndex original = agent.GetPrimaryWieldedItemIndex();
        EquipmentIndex target = EquipmentIndex.None;
        if (original == EquipmentIndex.None)
        {
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                 slot < EquipmentIndex.NumAllWeaponSlots;
                 slot++)
            {
                if (agent.Equipment[slot].Item == null) continue;
                target = slot;
                break;
            }
            if (target == EquipmentIndex.None)
                return "WIELD_TEST main agent has no weapon";
        }

        wieldTestAgent = agent;
        wieldTestAgentId = info.AgentId;
        wieldTestOriginalMainHand = original;
        wieldTestActive = true;
        if (original == EquipmentIndex.None)
        {
            agent.TryToWieldWeaponInSlot(
                target,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible,
                isWieldedOnSpawn: false);
        }
        else
        {
            agent.TryToSheathWeaponInHand(
                Agent.HandIndex.MainHand,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible);
        }
        return $"WIELD_TEST_STARTED agent={wieldTestAgentId:D} " +
            $"original={(int)original} target={(int)target}";
    }

    private static string RestoreWieldTest()
    {
        if (!wieldTestActive) return "WIELD_TEST inactive";
        if (wieldTestAgent == null
            || !wieldTestAgent.IsActive()
            || wieldTestAgent.Mission != Mission.Current)
        {
            return "WIELD_TEST agent unavailable";
        }

        if (wieldTestOriginalMainHand == EquipmentIndex.None)
        {
            wieldTestAgent.TryToSheathWeaponInHand(
                Agent.HandIndex.MainHand,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible);
        }
        else
        {
            wieldTestAgent.TryToWieldWeaponInSlot(
                wieldTestOriginalMainHand,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible,
                isWieldedOnSpawn: false);
        }
        Guid restoredAgentId = wieldTestAgentId;
        wieldTestAgent = null;
        wieldTestAgentId = Guid.Empty;
        wieldTestOriginalMainHand = EquipmentIndex.None;
        wieldTestActive = false;
        return $"WIELD_TEST_RESTORED agent={restoredAgentId:D}";
    }

    [CommandLineArgumentFunction("player_agent_state", "coop.debug.battle")]
    public static string PlayerAgentState(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.battle.player_agent_state <remoteControllerId>";

        Mission mission = Mission.Current;
        CoopBattleController controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!TryResolvePlayerServices(
                out INetworkAgentRegistry registry,
                out IPlayerManager playerManager,
                out IObjectManager objectManager,
                out string serviceError))
        {
            return serviceError;
        }

        string ownControllerId = controller.Session.OwnControllerId;
        string remoteControllerId = args[0];
        if (remoteControllerId == ownControllerId)
            return "The remote controller must differ from the local controller";
        if (!TryResolvePlayerAgent(
                ownControllerId,
                mission,
                registry,
                playerManager,
                objectManager,
                out string ownHeroId,
                out CoopAgentInfo ownInfo,
                out string ownError))
        {
            return ownError;
        }
        if (!TryResolvePlayerAgent(
                remoteControllerId,
                mission,
                registry,
                playerManager,
                objectManager,
                out string remoteHeroId,
                out CoopAgentInfo remoteInfo,
                out string remoteError))
        {
            return remoteError;
        }

        bool targetKnown = controller.DebugAgentInterpolator.TryGetTargetFrame(
            remoteInfo.Agent,
            out Vec3 targetPosition,
            out Vec3 targetLookDirection,
            out long updateSequence);
        if (!targetKnown)
        {
            targetPosition = Vec3.Invalid;
            targetLookDirection = Vec3.Invalid;
            updateSequence = 0;
        }

        var output = new StringBuilder("kind=PLAYER_AGENT_STATE");
        output.AppendFormat(
            CultureInfo.InvariantCulture,
            "|instance={0}|ownController={1}|ownHero={2}|ownAgent={3:N}" +
            "|ownAuthority={4}|ownOriginalOwner={5}|ownMain={6}|ownActive={7}|ownVisual={8}" +
            "|ownX={9:F3}|ownY={10:F3}|ownZ={11:F3}" +
            "|remoteController={12}|remoteHero={13}|remoteAgent={14:N}" +
            "|remoteAuthority={15}|remoteOriginalOwner={16}|remoteMain={17}|remoteActive={18}|remoteVisual={19}" +
            "|remoteX={20:F3}|remoteY={21:F3}|remoteZ={22:F3}" +
            "|remoteTargetKnown={23}|remoteTargetX={24:F3}|remoteTargetY={25:F3}|remoteTargetZ={26:F3}" +
            "|remoteTargetLookX={27:F3}|remoteTargetLookY={28:F3}|remoteTargetLookZ={29:F3}" +
            "|remoteUpdateSequence={30}|distance={31:F3}",
            controller.Session.InstanceId,
            ownControllerId,
            ownHeroId,
            ownInfo.AgentId,
            ownInfo.CurrentAuthority,
            ownInfo.OriginalOwner,
            ReferenceEquals(ownInfo.Agent, Agent.Main),
            ownInfo.Agent.IsActive(),
            HasActiveVisual(ownInfo.Agent),
            ownInfo.Agent.Position.X,
            ownInfo.Agent.Position.Y,
            ownInfo.Agent.Position.Z,
            remoteControllerId,
            remoteHeroId,
            remoteInfo.AgentId,
            remoteInfo.CurrentAuthority,
            remoteInfo.OriginalOwner,
            ReferenceEquals(remoteInfo.Agent, Agent.Main),
            remoteInfo.Agent.IsActive(),
            HasActiveVisual(remoteInfo.Agent),
            remoteInfo.Agent.Position.X,
            remoteInfo.Agent.Position.Y,
            remoteInfo.Agent.Position.Z,
            targetKnown,
            targetPosition.X,
            targetPosition.Y,
            targetPosition.Z,
            targetLookDirection.X,
            targetLookDirection.Y,
            targetLookDirection.Z,
            updateSequence,
            ownInfo.Agent.Position.Distance(remoteInfo.Agent.Position));
        return output.ToString();
    }

    [CommandLineArgumentFunction("stage_main_agent", "coop.debug.battle")]
    public static string StageMainAgent(List<string> args)
    {
        if (args.Count != 4
            || !TryParseInvariantFloat(args[0], out float x)
            || !TryParseInvariantFloat(args[1], out float y)
            || !TryParseInvariantFloat(args[2], out float lookX)
            || !TryParseInvariantFloat(args[3], out float lookY))
        {
            return "Usage: coop.debug.battle.stage_main_agent <x> <y> <lookX> <lookY>";
        }

        Mission mission = Mission.Current;
        CoopBattleController controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!TryResolvePlayerServices(
                out INetworkAgentRegistry registry,
                out IPlayerManager playerManager,
                out IObjectManager objectManager,
                out string serviceError))
        {
            return serviceError;
        }
        if (!TryResolvePlayerAgent(
                controller.Session.OwnControllerId,
                mission,
                registry,
                playerManager,
                objectManager,
                out _,
                out CoopAgentInfo ownInfo,
                out string ownError))
        {
            return ownError;
        }

        Agent agent = ownInfo.Agent;
        if (!ReferenceEquals(agent, Agent.Main)
            || !registry.IsLocallyControlled(ownInfo.AgentId))
        {
            return "The resolved local player agent is not the locally controlled main agent";
        }
        if (agent.MountAgent != null)
            return "The live-test player staging command requires an unmounted main agent";

        var lookDirection = new Vec2(lookX, lookY);
        if (lookDirection.LengthSquared <= 0.0001f)
            return "The look direction must be non-zero";
        lookDirection.Normalize();

        var groundProbe = new Vec3(x, y, agent.Position.Z);
        float groundZ = mission.Scene.GetGroundHeightAtPosition(groundProbe);
        if (float.IsNaN(groundZ) || float.IsInfinity(groundZ))
            return "The target position has no finite ground height";

        var targetPosition = new Vec3(x, y, groundZ);
        agent.TeleportToPosition(targetPosition);
        agent.SetMovementDirection(lookDirection);
        agent.LookDirection = new Vec3(lookDirection.X, lookDirection.Y, 0f);
        agent.MovementInputVector = new Vec2(0f, 0f);

        return string.Format(
            CultureInfo.InvariantCulture,
            "STAGED_MAIN_AGENT controller={0} agent={1:N} x={2:F3} y={3:F3} z={4:F3} lookX={5:F3} lookY={6:F3}",
            controller.Session.OwnControllerId,
            ownInfo.AgentId,
            targetPosition.X,
            targetPosition.Y,
            targetPosition.Z,
            lookDirection.X,
            lookDirection.Y);
    }

    [CommandLineArgumentFunction("focus_player_pair", "coop.debug.battle")]
    public static string FocusPlayerPair(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.battle.focus_player_pair <remoteControllerId>";

        Mission mission = Mission.Current;
        CoopBattleController controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!(ScreenManager.TopScreen is MissionScreen missionScreen)
            || ReferenceEquals(missionScreen.CombatCamera, null))
        {
            return "The mission screen is not active";
        }
        if (!TryResolvePlayerServices(
                out INetworkAgentRegistry registry,
                out IPlayerManager playerManager,
                out IObjectManager objectManager,
                out string serviceError))
        {
            return serviceError;
        }

        string ownControllerId = controller.Session.OwnControllerId;
        string remoteControllerId = args[0];
        if (!TryResolvePlayerAgent(
                ownControllerId,
                mission,
                registry,
                playerManager,
                objectManager,
                out _,
                out CoopAgentInfo ownInfo,
                out string ownError))
        {
            return ownError;
        }
        if (!TryResolvePlayerAgent(
                remoteControllerId,
                mission,
                registry,
                playerManager,
                objectManager,
                out _,
                out CoopAgentInfo remoteInfo,
                out string remoteError))
        {
            return remoteError;
        }
        if (ReferenceEquals(ownInfo.Agent, remoteInfo.Agent))
            return "The local and remote player resolved to the same agent";
        if (!HasActiveVisual(ownInfo.Agent) || !HasActiveVisual(remoteInfo.Agent))
            return "Both player agents need active visual entities before camera focus";

        Vec3 pairDirection = remoteInfo.Agent.Position - ownInfo.Agent.Position;
        pairDirection = new Vec3(pairDirection.X, pairDirection.Y, 0f);
        if (pairDirection.LengthSquared <= 0.25f)
            return "The player agents are too close to frame independently";
        pairDirection.Normalize();
        var sideOffset = new Vec3(-pairDirection.Y, pairDirection.X, 0f);
        Vec3 target = ((ownInfo.Agent.Position + remoteInfo.Agent.Position) * 0.5f)
            + (Vec3.Up * 1.1f);
        Vec3 position = ownInfo.Agent.Position
            - (pairDirection * 4.5f)
            + (sideOffset * 1.25f)
            + (Vec3.Up * 2.3f);
        Vec3 direction = target - position;
        direction.Normalize();

        ObserveMission(mission);
        EnsureBattleDebugTickBehavior(mission);
        ReleaseLadderCamera();
        ReleaseMountCamera();
        ReleasePlayerPairCamera();
        playerPairCamera = Camera.CreateCamera();
        playerPairCamera.FillParametersFrom(missionScreen.CombatCamera);
        playerPairCamera.LookAt(position, target, Vec3.Up);
        focusedLocalPlayer = ownInfo.Agent;
        focusedRemotePlayer = remoteInfo.Agent;
        focusedLocalPlayerId = ownInfo.AgentId;
        focusedRemotePlayerId = remoteInfo.AgentId;
        focusedLocalControllerId = ownControllerId;
        focusedRemoteControllerId = remoteControllerId;
        focusedPlayerPairMission = mission;
        playerPairCameraPosition = position;
        playerPairCameraDirection = direction;
        missionScreen.CustomCamera = playerPairCamera;

        return string.Format(
            CultureInfo.InvariantCulture,
            "FOCUSED_PLAYER_PAIR instance={0} localController={1} localAgent={2:N} remoteController={3} remoteAgent={4:N} distance={5:F3}",
            controller.Session.InstanceId,
            ownControllerId,
            ownInfo.AgentId,
            remoteControllerId,
            remoteInfo.AgentId,
            ownInfo.Agent.Position.Distance(remoteInfo.Agent.Position));
    }

    [CommandLineArgumentFunction("player_pair_camera_state", "coop.debug.battle")]
    public static string PlayerPairCameraState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.battle.player_pair_camera_state";
        Mission mission = Mission.Current;
        if (ReferenceEquals(playerPairCamera, null)
            || ReferenceEquals(focusedLocalPlayer, null)
            || ReferenceEquals(focusedRemotePlayer, null)
            || !ReferenceEquals(focusedPlayerPairMission, mission)
            || !(ScreenManager.TopScreen is MissionScreen missionScreen)
            || ReferenceEquals(missionScreen.CombatCamera, null)
            || !focusedLocalPlayer.IsActive()
            || !focusedRemotePlayer.IsActive())
        {
            return "active=False";
        }

        bool active = ReferenceEquals(missionScreen.CustomCamera, playerPairCamera);
        Vec3 renderedPosition = missionScreen.CombatCamera.Position;
        Vec3 renderedDirection = missionScreen.CombatCamera.Direction;
        renderedDirection.Normalize();
        float localDirectionDot = DirectionDot(
            renderedPosition,
            renderedDirection,
            focusedLocalPlayer.Position + Vec3.Up);
        float remoteDirectionDot = DirectionDot(
            renderedPosition,
            renderedDirection,
            focusedRemotePlayer.Position + Vec3.Up);
        float expectedDirectionDot = (renderedDirection.X * playerPairCameraDirection.X)
            + (renderedDirection.Y * playerPairCameraDirection.Y)
            + (renderedDirection.Z * playerPairCameraDirection.Z);

        return string.Format(
            CultureInfo.InvariantCulture,
            "active={0} localController={1} localAgent={2:N} remoteController={3} remoteAgent={4:N} " +
            "localVisual={5} remoteVisual={6} pairDistance={7:F3} positionDelta={8:F3} " +
            "directionDot={9:F3} localDirectionDot={10:F3} remoteDirectionDot={11:F3}",
            active,
            focusedLocalControllerId,
            focusedLocalPlayerId,
            focusedRemoteControllerId,
            focusedRemotePlayerId,
            HasActiveVisual(focusedLocalPlayer),
            HasActiveVisual(focusedRemotePlayer),
            focusedLocalPlayer.Position.Distance(focusedRemotePlayer.Position),
            renderedPosition.Distance(playerPairCameraPosition),
            expectedDirectionDot,
            localDirectionDot,
            remoteDirectionDot);
    }

    [CommandLineArgumentFunction("release_player_pair_camera", "coop.debug.battle")]
    public static string ReleasePlayerPairCameraCommand(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.battle.release_player_pair_camera";

        bool released = ReleasePlayerPairCamera();
        return released ? "Released the player-pair camera" : "No player-pair camera was active";
    }

    private static bool TryResolvePlayerServices(
        out INetworkAgentRegistry registry,
        out IPlayerManager playerManager,
        out IObjectManager objectManager,
        out string error)
    {
        registry = null;
        playerManager = null;
        objectManager = null;
        error = null;
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out registry))
        {
            error = "Network agent registry is unavailable";
            return false;
        }
        if (!ContainerProvider.TryResolve<IPlayerManager>(out playerManager))
        {
            error = "Player manager is unavailable";
            return false;
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out objectManager))
        {
            error = "Object manager is unavailable";
            return false;
        }
        return true;
    }

    private static bool TryResolvePlayerAgent(
        string controllerId,
        Mission mission,
        INetworkAgentRegistry registry,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        out string heroId,
        out CoopAgentInfo agentInfo,
        out string error)
    {
        heroId = null;
        agentInfo = null;
        error = null;
        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            error = $"Player {controllerId} is not registered";
            return false;
        }
        heroId = player.HeroId;
        if (!objectManager.TryGetObject<Hero>(heroId, out Hero hero))
        {
            error = $"Hero {heroId} for player {controllerId} is not registered";
            return false;
        }

        CoopAgentInfo[] matches = registry.GetAgents(controllerId)
            .Where(info => info.OriginalOwner == controllerId
                && info.Agent != null
                && !info.Agent.IsMount
                && info.Agent.IsHuman
                && info.Agent.IsActive()
                && info.Agent.Mission == mission
                && info.Agent.Character is CharacterObject character
                && character.IsHero
                && character.HeroObject == hero)
            .ToArray();
        if (matches.Length != 1)
        {
            error = $"Player {controllerId} resolved {matches.Length} active hero agents";
            return false;
        }

        agentInfo = matches[0];
        return true;
    }

    private static bool HasActiveVisual(Agent agent)
    {
        return agent?.AgentVisuals?.GetEntity() != null;
    }

    private static bool TryParseInvariantFloat(string value, out float result)
    {
        return float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static float DirectionDot(Vec3 origin, Vec3 direction, Vec3 target)
    {
        Vec3 toTarget = target - origin;
        if (toTarget.LengthSquared <= 0.0001f)
            return 1f;
        toTarget.Normalize();
        return (direction.X * toTarget.X)
            + (direction.Y * toTarget.Y)
            + (direction.Z * toTarget.Z);
    }

    private static bool ReleasePlayerPairCamera()
    {
        if (ReferenceEquals(playerPairCamera, null)) return false;

        if (ScreenManager.TopScreen is MissionScreen missionScreen
            && ReferenceEquals(missionScreen.CustomCamera, playerPairCamera))
        {
            missionScreen.CustomCamera = null;
        }

        playerPairCamera.ReleaseCamera();
        playerPairCamera = null;
        focusedLocalPlayer = null;
        focusedRemotePlayer = null;
        focusedLocalPlayerId = Guid.Empty;
        focusedRemotePlayerId = Guid.Empty;
        focusedLocalControllerId = null;
        focusedRemoteControllerId = null;
        focusedPlayerPairMission = null;
        playerPairCameraPosition = Vec3.Invalid;
        playerPairCameraDirection = Vec3.Invalid;
        return true;
    }
#endif

    [CommandLineArgumentFunction("state", "coop.debug.battle")]
    public static string State(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.battle.state";
        }

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var playerTeam = mission?.PlayerTeam;
        if (mission == null || controller == null)
        {
            return "No active coop battle mission";
        }

        ObserveMission(mission);
        EnsureBattleDebugTickBehavior(mission);

        var enemies = new List<Agent>();
        int enemyParties = 0;
        if (playerTeam != null)
        {
            var enemySide = playerTeam.Side == BattleSideEnum.Attacker
                ? BattleSideEnum.Defender
                : BattleSideEnum.Attacker;
            enemies.AddRange(mission.Agents
                .Where(agent => agent.IsActive() && agent.IsHuman && agent.Team?.Side == enemySide));
            enemyParties = playerTeam.Side == BattleSideEnum.Attacker
                ? MobileParty.MainParty?.MapEvent?.DefenderSide?.Parties?.Count ?? 0
                : MobileParty.MainParty?.MapEvent?.AttackerSide?.Parties?.Count ?? 0;
        }

        int moved = 0;
        foreach (var enemy in enemies)
        {
            if (EnemyPositions.TryGetValue(enemy.Index, out var previous)
                && previous.DistanceSquared(enemy.Position) > 0.25f)
            {
                moved++;
            }
            EnemyPositions[enemy.Index] = enemy.Position;
        }

        bool deploymentReady = mission.GetMissionBehavior<DeploymentMissionController>()?.TeamSetupOver == true;
        int activeAgents = mission.Agents.Count(agent => agent.IsActive());
        int enemyFleeing = enemies.Count(agent => agent.IsRunningAway);
        var result = mission.MissionResult;
        var suppliers = CoopTroopSupplierRegistry.GetSuppliers(controller.Session.InstanceId);
        var receiverReserves = suppliers
            .Where(supplier => !string.IsNullOrEmpty(supplier.PlayerPartyId))
            .Select(supplier => $"{supplier.Side}:{supplier.PlayerPartyId}")
            .ToArray();

        return $"instance={controller.Session.InstanceId} host={controller.Session.IsLocalHost} " +
            $"activated={controller.Deployment.IsActivated} committed={controller.Deployment.IsCommitted} " +
            $"deploymentReady={deploymentReady} mainAgent={Agent.Main != null} activeAgents={activeAgents} " +
            $"reserveSuppliers={suppliers.Count} populatedReserves={suppliers.Count(supplier => supplier.IsPopulated)} " +
            $"receiverOwnedReserves={receiverReserves.Length} receiverReserve={string.Join(",", receiverReserves)} " +
            $"playerSide={playerTeam?.Side.ToString() ?? "None"} enemyParties={enemyParties} enemyActive={enemies.Count} " +
            $"enemyAi={enemies.Count(agent => agent.IsAIControlled)} enemyFleeing={enemyFleeing} " +
            $"enemyMovedSinceLast={moved} damageReceivedEvents={ownDamageEvents} " +
            $"resultState={result?.BattleState.ToString() ?? "None"} " +
            $"battleResolved={result?.BattleResolved ?? false} playerVictory={result?.PlayerVictory ?? false}";
    }

    [CommandLineArgumentFunction("charge_owned_formations", "coop.debug.battle")]
    public static string ChargeOwnedFormations(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.battle.charge_owned_formations";

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        CoopAgentInfo[] ownedAgents = registry.GetAgents(controller.Session.OwnControllerId)
            .Where(info => info.OriginalOwner == controller.Session.OwnControllerId)
            .Where(info => info.Agent != null
                && info.Agent.IsActive()
                && info.Agent.IsHuman
                && info.Agent.Team == mission.PlayerTeam
                && info.Agent.Formation != null)
            .ToArray();
        Formation[] formations = ownedAgents
            .Select(info => info.Agent.Formation)
            .Distinct()
            .ToArray();
        if (formations.Length == 0)
            return "The local player has no active owned formations";

        foreach (Formation formation in formations)
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);

        return $"Charged {formations.Length} locally owned formation(s) with {ownedAgents.Length} active agent(s)";
    }

    [CommandLineArgumentFunction("mount_state", "coop.debug.battle")]
    public static string MountState(List<string> args)
    {
        if (args.Count > 1)
            return "Usage: coop.debug.battle.mount_state [host|host-player-team|local|controllerId]";

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        string filter = args.Count == 1 ? args[0] : null;
        var mounts = registry.GetControllerIds()
            .SelectMany(registry.GetAgents)
            .Where(info => MatchesAuthority(
                controller.Session,
                info,
                filter,
                mission.PlayerTeam))
            .Where(info => info.Agent != null && info.Agent.IsMount && info.Agent.IsActive())
            .OrderBy(info => info.AgentId)
            .ToArray();

        int stationaryCount = 0;
        int stationaryAnimatedCount = 0;
        int stationaryTurningCount = 0;
        var output = new StringBuilder();
        foreach (var info in mounts)
        {
            Agent mount = info.Agent;
            float speed = mount.GetRealGlobalVelocity().AsVec2.Length;
            bool stationary = speed <= AgentMountData.StationarySpeedThreshold;
            AgentMountData.GetRenderedAction0State(
                mount,
                out string animationName,
                out float animationSpeed,
                out float animationProgress);
            int actionIndex = mount.GetCurrentAction(0).Index;
            string actionName = AgentActionData.GetActionNameWithCode(actionIndex);
            int turnDirection = AgentMountData.GetTurnDirection(actionName, animationName);
            bool locomotionAction = AgentMountData.IsLocomotionAction(actionIndex, animationName);
            bool stationaryAnimated = stationary
                && locomotionAction
                && animationSpeed > 0.001f;
            bool stationaryTurning = stationary
                && turnDirection != AgentMountData.NoTurn
                && animationSpeed > 0.001f;
            if (stationary) stationaryCount++;
            if (stationaryAnimated) stationaryAnimatedCount++;
            if (stationaryTurning) stationaryTurningCount++;

            string riderId = "none";
            if (mount.RiderAgent != null
                && registry.TryGetAgentInfo(mount.RiderAgent, out var riderInfo))
            {
                riderId = riderInfo.AgentId.ToString("N");
            }

            output.Append("id=").Append(info.AgentId.ToString("N"))
                .Append(" authority=").Append(info.CurrentAuthority)
                .Append(" local=").Append(controller.Session.IsOwn(info.CurrentAuthority))
                .Append(" rider=").Append(riderId)
                .Append(" position=").Append(mount.Position.X.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(',').Append(mount.Position.Y.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" speed=").Append(speed.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" input=").Append(mount.MovementInputVector.X.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(',').Append(mount.MovementInputVector.Y.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" direction=").Append(mount.GetMovementDirection().X.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(',').Append(mount.GetMovementDirection().Y.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" action0=").Append(actionIndex)
                .Append(" actionName=").Append(actionName ?? "none")
                .Append(" actionProgress=").Append(animationProgress.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" animation=").Append(animationName ?? "none")
                .Append(" animationSpeed=").Append(animationSpeed.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" locomotion=").Append(locomotionAction)
                .Append(" turnDirection=").Append(turnDirection)
                .Append(" stationaryTurning=").Append(stationaryTurning)
                .Append(" stationaryAnimated=").Append(stationaryAnimated)
                .AppendLine();
        }

        output.Insert(
            0,
            $"mounts={mounts.Length} stationary={stationaryCount} stationaryAnimated={stationaryAnimatedCount} " +
            $"stationaryTurning={stationaryTurningCount} " +
            $"own={controller.Session.OwnControllerId} host={controller.Session.IsLocalHost}{Environment.NewLine}");
        return output.ToString().TrimEnd();
    }

    [CommandLineArgumentFunction("capture_mount_pose", "coop.debug.battle")]
    public static string CaptureMountPose(List<string> args)
    {
        if (args.Count != 1 || !Guid.TryParseExact(args[0], "N", out Guid mountId))
            return "Usage: coop.debug.battle.capture_mount_pose <mountAgentId>";

        var mission = Mission.Current;
        if (mission == null)
            return "No active mission";
        if (!TryGetActiveMount(mountId, out Agent mount))
            return $"Active mount {mountId:N} was not found";

        MBAgentVisuals visuals = mount.AgentVisuals;
        Skeleton skeleton = visuals?.GetSkeleton();
        if (ReferenceEquals(visuals, null)
            || !visuals.IsValid()
            || ReferenceEquals(skeleton, null)
            || !skeleton.IsValid)
        {
            return $"Mount {mountId:N} has no active skeleton";
        }

        ObserveMission(mission);
        EnsureBattleDebugTickBehavior(mission);
        capturedMount = mount;
        capturedMountId = mountId;
        mountPoseCaptureStartTime = mission.CurrentTime;
        MountPoseSamples.Clear();
        CaptureMountPoseFrame();
        return $"Capturing rendered horse-head pose for mount {mountId:N}";
    }

    [CommandLineArgumentFunction("mount_pose_samples", "coop.debug.battle")]
    public static string MountPoseSamplesState(List<string> args)
    {
        if (args.Count != 1 || !Guid.TryParseExact(args[0], "N", out Guid mountId))
            return "Usage: coop.debug.battle.mount_pose_samples <mountAgentId>";
        if (capturedMount == null || capturedMountId != mountId)
            return $"No pose capture is active for mount {mountId:N}";

        var output = new StringBuilder();
        output.Append("mount=").Append(capturedMountId.ToString("N"))
            .Append(" samples=").Append(MountPoseSamples.Count)
            .AppendLine();
        for (int index = 0; index < MountPoseSamples.Count; index++)
        {
            MountPoseSample sample = MountPoseSamples[index];
            output.Append("sample=").Append(index)
                .Append(" time=").Append(sample.Time.ToString("0.0000", CultureInfo.InvariantCulture))
                .Append(" speed=").Append(sample.Speed.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" action0=").Append(sample.ActionIndex)
                .Append(" actionName=").Append(sample.ActionName ?? "none")
                .Append(" animation=").Append(sample.AnimationName ?? "none")
                .Append(" animationProgress=").Append(sample.AnimationProgress.ToString("0.0000", CultureInfo.InvariantCulture))
                .Append(" animationSpeed=").Append(sample.AnimationSpeed.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(" turnDirection=").Append(sample.TurnDirection)
                .Append(" channelWeight=").Append(sample.ChannelWeight.ToString("0.0000", CultureInfo.InvariantCulture))
                .Append(" currentActionWeight=").Append(sample.CurrentActionWeight.ToString("0.0000", CultureInfo.InvariantCulture))
                .Append(" headPosition=").Append(sample.HeadPosition.X.ToString("0.00000", CultureInfo.InvariantCulture))
                .Append(',').Append(sample.HeadPosition.Y.ToString("0.00000", CultureInfo.InvariantCulture))
                .Append(',').Append(sample.HeadPosition.Z.ToString("0.00000", CultureInfo.InvariantCulture))
                .Append(" headForward=").Append(sample.HeadForward.X.ToString("0.00000", CultureInfo.InvariantCulture))
                .Append(',').Append(sample.HeadForward.Y.ToString("0.00000", CultureInfo.InvariantCulture))
                .Append(',').Append(sample.HeadForward.Z.ToString("0.00000", CultureInfo.InvariantCulture))
                .AppendLine();
        }
        return output.ToString().TrimEnd();
    }

    private static void CaptureMountPoseFrame()
    {
        UpdateMountCameraFrame();

        Agent mount = capturedMount;
        Mission mission = Mission.Current;
        if (mount == null || MountPoseSamples.Count >= MaximumMountPoseSamples)
            return;
        if (mission == null
            || !mount.IsActive()
            || mount.Mission != mission
            || observedMission != mission)
        {
            capturedMount = null;
            capturedMountId = Guid.Empty;
            MountPoseSamples.Clear();
            return;
        }

        MBAgentVisuals visuals = mount.AgentVisuals;
        Skeleton skeleton = visuals?.GetSkeleton();
        if (ReferenceEquals(visuals, null)
            || !visuals.IsValid()
            || ReferenceEquals(skeleton, null)
            || !skeleton.IsValid)
        {
            return;
        }

        AgentMountData.GetRenderedAction0State(
            mount,
            out string animationName,
            out float animationSpeed,
            out float animationProgress);
        int actionIndex = mount.GetCurrentAction(0).Index;
        string actionName = AgentActionData.GetActionNameWithCode(actionIndex);
        MatrixFrame headFrame = skeleton.GetBoneEntitialFrameWithName("horse_head");
        MountPoseSamples.Add(new MountPoseSample
        {
            Time = mission.CurrentTime - mountPoseCaptureStartTime,
            Speed = mount.GetRealGlobalVelocity().AsVec2.Length,
            ActionIndex = actionIndex,
            ActionName = actionName,
            AnimationName = animationName,
            AnimationProgress = animationProgress,
            AnimationSpeed = animationSpeed,
            TurnDirection = AgentMountData.GetTurnDirection(actionName, animationName),
            ChannelWeight = mount.GetActionChannelWeight(0),
            CurrentActionWeight = mount.GetActionChannelCurrentActionWeight(0),
            HeadPosition = headFrame.origin,
            HeadForward = headFrame.rotation.f
        });
    }

    [CommandLineArgumentFunction("move_cavalry", "coop.debug.battle")]
    public static string MoveCavalry(List<string> args)
    {
        if (args.Count != 1
            || !float.TryParse(
                args[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float distance)
            || distance < 5f
            || distance > 100f)
        {
            return "Usage: coop.debug.battle.move_cavalry <distance: 5-100>";
        }

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!controller.Session.IsLocalHost)
            return "Run this command on the battle-host client";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        Agent[] riders = GetBattleHostCavalryRiders(
            mission,
            controller,
            registry);
        Formation[] formations = riders
            .Select(agent => agent.Formation)
            .Distinct()
            .ToArray();
        if (formations.Length == 0)
            return "The battle host has no active cavalry formations";

        foreach (Agent rider in riders)
        {
            RestoreCavalryController(rider);
            if (rider.MountAgent != null)
                RestoreCavalryController(rider.MountAgent);
            rider.SetScriptedFlags(Agent.AIScriptedFrameFlags.None);
            rider.SetMaximumSpeedLimit(-1f, isMultiplier: false);
            rider.MountAgent?.SetMaximumSpeedLimit(-1f, isMultiplier: false);
            rider.SetIsAIPaused(false);
            rider.MountAgent?.SetIsAIPaused(false);
        }

        foreach (Formation formation in formations)
        {
            Vec2 direction = formation.Direction;
            if (direction.LengthSquared <= 0.0001f)
                direction = Vec2.Forward;
            else
                direction.Normalize();

            WorldPosition destination = formation.CachedMedianPosition;
            destination.SetVec2(formation.CurrentPosition + (direction * distance));
            formation.SetMovementOrder(MovementOrder.MovementOrderMove(destination));
        }

        return $"Moved {formations.Length} battle-host cavalry formations {distance:0.0} meters";
    }

    [CommandLineArgumentFunction("hold_cavalry", "coop.debug.battle")]
    public static string HoldCavalry(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.battle.hold_cavalry";

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!controller.Session.IsLocalHost)
            return "Run this command on the battle-host client";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        Agent[] riders = GetBattleHostCavalryRiders(
            mission,
            controller,
            registry);
        Formation[] formations = riders
            .Select(agent => agent.Formation)
            .Distinct()
            .ToArray();
        if (formations.Length == 0)
            return "The battle host has no active cavalry formations";

        foreach (Formation formation in formations)
            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
        foreach (Agent rider in riders)
        {
            FreezeCavalryController(rider);
            rider.SetMaximumSpeedLimit(0f, isMultiplier: false);
            rider.MovementInputVector = Vec2.Zero;
            rider.SetIsAIPaused(true);
            if (rider.MountAgent != null)
            {
                FreezeCavalryController(rider.MountAgent);
                rider.MountAgent.SetMaximumSpeedLimit(0f, isMultiplier: false);
                rider.MountAgent.MovementInputVector = Vec2.Zero;
                rider.MountAgent.SetIsAIPaused(true);
            }
        }

        return $"Stopped {formations.Length} battle-host cavalry formations";
    }

    [CommandLineArgumentFunction("turn_cavalry", "coop.debug.battle")]
    public static string TurnCavalry(List<string> args)
    {
        if (args.Count != 1
            || !float.TryParse(
                args[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float degrees)
            || float.IsNaN(degrees)
            || float.IsInfinity(degrees)
            || Math.Abs(degrees) < 1f
            || Math.Abs(degrees) > 180f)
        {
            return "Usage: coop.debug.battle.turn_cavalry <degrees: -180 to -1 or 1 to 180>";
        }

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "No active coop battle mission";
        if (!controller.Session.IsLocalHost)
            return "Run this command on the battle-host client";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";

        Agent[] riders = GetBattleHostCavalryRiders(
            mission,
            controller,
            registry);
        Formation[] formations = riders
            .Select(agent => agent.Formation)
            .Distinct()
            .ToArray();
        if (formations.Length == 0)
            return "The battle host has no active cavalry formations";

        float radians = degrees * ((float)Math.PI / 180f);
        float cosine = (float)Math.Cos(radians);
        float sine = (float)Math.Sin(radians);
        foreach (Formation formation in formations)
            formation.SetMovementOrder(MovementOrder.MovementOrderStop);

        foreach (Agent rider in riders)
        {
            Vec2 riderDirection = rider.GetMovementDirection();
            var turnedRiderDirection = new Vec2(
                (riderDirection.X * cosine) - (riderDirection.Y * sine),
                (riderDirection.X * sine) + (riderDirection.Y * cosine));
            rider.SetMovementDirection(in turnedRiderDirection);

            Agent mount = rider.MountAgent;
            if (mount != null)
            {
                Vec2 mountDirection = mount.GetMovementDirection();
                var turnedMountDirection = new Vec2(
                    (mountDirection.X * cosine) - (mountDirection.Y * sine),
                    (mountDirection.X * sine) + (mountDirection.Y * cosine));
                mount.SetMovementDirection(in turnedMountDirection);
            }
        }

        return $"Turned {formations.Length} battle-host cavalry formations {degrees:0.0} degrees in place";
    }

    private static void FreezeCavalryController(Agent rider)
    {
        if (!CavalryControllers.ContainsKey(rider))
            CavalryControllers.Add(rider, rider.Controller);
        rider.Controller = AgentControllerType.None;
    }

    private static void RestoreCavalryController(Agent rider)
    {
        if (!CavalryControllers.TryGetValue(rider, out AgentControllerType controller))
            return;

        rider.Controller = controller;
        CavalryControllers.Remove(rider);
    }

    private static Agent[] GetBattleHostCavalryRiders(
        Mission mission,
        CoopBattleController controller,
        INetworkAgentRegistry registry)
    {
        ObserveMission(mission);
        return registry.GetAgents(controller.Session.OwnControllerId)
            .Where(info => info.OriginalOwner == controller.Session.OwnControllerId)
            .Select(info => info.Agent)
            .Where(agent => agent != null
                && agent.IsActive()
                && !agent.IsMount
                && agent.HasMount
                && agent.Team == mission.PlayerTeam
                && agent.Formation != null)
            .ToArray();
    }

    private static void ObserveMission(Mission mission)
    {
        if (observedMission == mission)
            return;

        EnemyPositions.Clear();
        ownDamageEvents = 0;
        CavalryControllers.Clear();
        capturedMount = null;
        capturedMountId = Guid.Empty;
        MountPoseSamples.Clear();
        ReleaseLadderCamera();
        ReleaseMountCamera();
#if DEBUG
        ReleasePlayerPairCamera();
#endif
        observedMission = mission;
    }

    private static void EnsureBattleDebugTickBehavior(Mission mission)
    {
        if (!ReferenceEquals(battleDebugTickBehavior, null)
            && ReferenceEquals(battleDebugTickBehavior.Mission, mission))
        {
            return;
        }

        battleDebugTickBehavior = new BattleDebugTickBehavior();
        mission.AddMissionBehavior(battleDebugTickBehavior);
    }

    private static bool TryGetActiveMount(Guid mountId, out Agent mount)
    {
        mount = null;
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry)
            || !registry.TryGetAgentInfo(mountId, out var info)
            || info.Agent == null
            || !info.Agent.IsMount
            || !info.Agent.IsActive())
        {
            return false;
        }

        mount = info.Agent;
        return true;
    }

    private static bool MatchesAuthority(
        IBattleSession session,
        CoopAgentInfo info,
        string filter,
        Team playerTeam)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        if (filter == "host") return session.IsHostController(info.CurrentAuthority);
        if (filter == "host-player-team")
            return session.IsHostController(info.CurrentAuthority)
                && info.OriginalOwner == info.CurrentAuthority
                && info.Agent?.RiderAgent?.IsActive() == true
                && info.Agent.RiderAgent.Team?.Side == playerTeam?.Side;
        if (filter == "local") return session.IsOwn(info.CurrentAuthority);
        return info.CurrentAuthority == filter;
    }

    [CommandLineArgumentFunction("focus_mount", "coop.debug.battle")]
    public static string FocusMount(List<string> args)
    {
        if (args.Count != 1 || !Guid.TryParseExact(args[0], "N", out Guid mountId))
            return "Usage: coop.debug.battle.focus_mount <mountAgentId>";

        var mission = Mission.Current;
        if (mission == null)
            return "No active mission";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "Network agent registry is unavailable";
        if (!registry.TryGetAgentInfo(mountId, out var info)
            || info.Agent == null
            || !info.Agent.IsMount
            || !info.Agent.IsActive())
        {
            return $"Active mount {mountId:N} was not found";
        }

        if (!(ScreenManager.TopScreen is MissionScreen missionScreen)
            || ReferenceEquals(missionScreen.CombatCamera, null))
            return "The mission screen is not active";

#if DEBUG
        ReleasePlayerPairCamera();
#endif
        ReleaseLadderCamera();
        ObserveMission(mission);
        Agent mount = info.Agent;
        MBAgentVisuals visuals = mount.AgentVisuals;
        GameEntity visualEntity = visuals?.GetEntity();
        if (ReferenceEquals(visualEntity, null))
            return $"Mount {mountId:N} has no active visual entity";

        EnsureBattleDebugTickBehavior(mission);

        if (ReferenceEquals(mountCamera, null)
            || ReferenceEquals(mountCamera.Entity, null))
        {
            ReleaseMountCamera();
            mountCamera = Camera.CreateCamera();
            mountCamera.FillParametersFrom(missionScreen.CombatCamera);
            var localTarget = new Vec3(0f, 0f, 1.4f);
            var localPosition = new Vec3(-4f, -11f, 5.4f);
            mountCamera.LookAt(localPosition, localTarget, Vec3.Up);
            mountCameraLocalFrame = mountCamera.Frame;
            mountCamera.Entity = GameEntity.CreateEmpty(
                mission.Scene,
                isModifiableFromEditor: false,
                createPhysics: false,
                callScriptCallbacks: false);
        }

        focusedMount = mount;
        focusedMountId = mountId;
        UpdateMountCameraFrame();
        missionScreen.CustomCamera = mountCamera;

        return $"Focused the mission camera on mount {mountId:N}";
    }

    [CommandLineArgumentFunction("mount_camera_state", "coop.debug.battle")]
    public static string MountCameraState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.battle.mount_camera_state";

        if (ReferenceEquals(mountCamera, null)
            || ReferenceEquals(focusedMount, null)
            || !focusedMount.IsActive()
            || !(ScreenManager.TopScreen is MissionScreen missionScreen)
            || ReferenceEquals(missionScreen.CombatCamera, null)
            || ReferenceEquals(mountCamera.Entity, null))
        {
            return "active=False";
        }

        MatrixFrame cameraEntityFrame = mountCamera.Entity.GetGlobalFrame();
        Vec3 renderedPosition = missionScreen.CombatCamera.Position;
        Vec3 entityDirection = -cameraEntityFrame.rotation.u;
        entityDirection.Normalize();
        Vec3 renderedDirection = missionScreen.CombatCamera.Direction;
        float directionDot =
            (renderedDirection.X * entityDirection.X)
            + (renderedDirection.Y * entityDirection.Y)
            + (renderedDirection.Z * entityDirection.Z);

        float positionDelta =
            (renderedPosition - cameraEntityFrame.origin).Length;
        bool active = ReferenceEquals(missionScreen.CustomCamera, mountCamera);
        return string.Format(
            CultureInfo.InvariantCulture,
            "active={0} mount={1:N} positionDelta={2:F3} directionDot={3:F3}",
            active,
            focusedMountId,
            positionDelta,
            directionDot);
    }

    private static bool UpdateMountCameraFrame()
    {
        if (ReferenceEquals(mountCamera, null)
            || ReferenceEquals(mountCamera.Entity, null)
            || ReferenceEquals(focusedMount, null)
            || !focusedMount.IsActive())
        {
            return false;
        }

        GameEntity visualEntity = focusedMount.AgentVisuals?.GetEntity();
        if (ReferenceEquals(visualEntity, null))
            return false;

        MatrixFrame visualFrame = visualEntity.GetGlobalFrame();
        MatrixFrame localFrame = mountCameraLocalFrame;
        MatrixFrame globalFrame = visualFrame.TransformToParent(in localFrame);
        mountCamera.Entity.SetGlobalFrame(globalFrame);
        return true;
    }

    [CommandLineArgumentFunction("release_mount_camera", "coop.debug.battle")]
    public static string ReleaseMountCameraCommand(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.battle.release_mount_camera";

        bool released = ReleaseMountCamera();
        return released ? "Released the mount camera" : "No mount camera was active";
    }

    private static bool ReleaseMountCamera()
    {
        if (ReferenceEquals(mountCamera, null)) return false;

        if (ScreenManager.TopScreen is MissionScreen missionScreen
            && ReferenceEquals(missionScreen.CustomCamera, mountCamera))
        {
            missionScreen.CustomCamera = null;
        }

        if (ReferenceEquals(mountCamera.Entity, null))
            mountCamera.ReleaseCamera();
        else
            mountCamera.ReleaseCameraEntity();
        mountCamera = null;
        focusedMount = null;
        focusedMountId = Guid.Empty;
        return true;
    }

    [CommandLineArgumentFunction("ladder_state", "coop.debug.battle")]
    public static string LadderState(List<string> args)
    {
        if (args.Count > 1 || (args.Count == 1 && !int.TryParse(args[0], out _)))
        {
            return "Usage: coop.debug.battle.ladder_state [machineId]";
        }

        var mission = Mission.Current;
        if (mission == null || !mission.IsSiegeBattle)
        {
            return "No active siege mission";
        }

        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry))
        {
            return "Unable to resolve NetworkAgentRegistry";
        }

        int? selectedId = args.Count == 1 ? int.Parse(args[0]) : null;
        var ladders = mission.MissionObjects
            .OfType<SiegeLadder>()
            .Where(ladder => selectedId == null || ladder.Id.Id == selectedId.Value)
            .OrderBy(ladder => ladder.Id.Id)
            .ToArray();
        if (ladders.Length == 0)
        {
            return selectedId == null
                ? "No siege ladders are registered"
                : $"Siege ladder {selectedId.Value} was not found";
        }

        var output = new StringBuilder();
        output.AppendLine($"ladders={ladders.Length} authority={SiegeMissionAuthorityGate.IsLocalAuthority} " +
            $"known={SiegeMissionAuthorityGate.IsAuthorityKnown}");
        foreach (var ladder in ladders)
        {
            int animationIndex = ladder._ladderSkeleton.GetAnimationIndexAtChannel(0);
            float animationProgress = animationIndex >= 0
                ? ladder._ladderSkeleton.GetAnimationParameterAtChannel(0)
                : 0f;

            var users = new List<string>();
            int deactivatedPoints = 0;
            foreach (var standingPoint in ladder.StandingPoints)
            {
                if (standingPoint.IsDeactivated) deactivatedPoints++;

                var agent = standingPoint.UserAgent ?? standingPoint.MovingAgent;
                if (agent == null) continue;

                string role = standingPoint.GameEntity.HasTag(ladder.AttackerTag)
                    ? "attacker"
                    : standingPoint.GameEntity.HasTag(ladder.DefenderTag) ? "defender" : "other";
                string controller = agentRegistry.TryGetAgentInfo(agent, out var info)
                    ? info.CurrentAuthority
                    : "unregistered";
                users.Add($"{role}:{controller}:{agent.Index}");
            }

            output.AppendLine($"ladder={ladder.Id.Id:D5} state={ladder.State} animation={ladder._animationState} " +
                $"animationIndex={animationIndex} progress={animationProgress:0.000} " +
                $"simLocal={SiegeMissionAuthorityGate.IsMachineSimulatedLocally(ladder.Id.Id)} " +
                $"points={ladder.StandingPoints.Count} pointsOff={deactivatedPoints} " +
                $"users={(users.Count > 0 ? string.Join(",", users) : "none")}");
        }

        return output.ToString();
    }

    [CommandLineArgumentFunction("focus_ladder", "coop.debug.battle")]
    public static string FocusLadder(List<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out int machineId))
        {
            return "Usage: coop.debug.battle.focus_ladder <machineId>";
        }

        var mission = Mission.Current;
        var ladder = mission?.MissionObjects
            .OfType<SiegeLadder>()
            .FirstOrDefault(candidate => candidate.Id.Id == machineId);
        if (ladder == null)
        {
            return $"Siege ladder {machineId} was not found";
        }

        if (!(ScreenManager.TopScreen is MissionScreen missionScreen) || missionScreen.CombatCamera == null)
        {
            return "The mission screen is not active";
        }

#if DEBUG
        ReleasePlayerPairCamera();
#endif
        ReleaseMountCamera();
        ReleaseLadderCamera();
        ladderCamera = Camera.CreateCamera();
        ladderCamera.FillParametersFrom(missionScreen.CombatCamera);

        var frame = ladder.GameEntity.GetGlobalFrame();
        var target = frame.origin + (Vec3.Up * 2.5f);
        var position = target - (frame.rotation.f * 12f) + (Vec3.Up * 4f);
        ladderCamera.LookAt(position, target, Vec3.Up);
        missionScreen.CustomCamera = ladderCamera;

        return $"Focused the mission camera on siege ladder {machineId}";
    }

    [CommandLineArgumentFunction("release_ladder_camera", "coop.debug.battle")]
    public static string ReleaseLadderCameraCommand(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.battle.release_ladder_camera";
        }

        bool released = ReleaseLadderCamera();
        return released ? "Released the ladder camera" : "No ladder camera was active";
    }

    private static bool ReleaseLadderCamera()
    {
        if (ladderCamera == null) return false;

        if (ScreenManager.TopScreen is MissionScreen missionScreen
            && missionScreen.CustomCamera == ladderCamera)
        {
            missionScreen.CustomCamera = null;
        }

        ladderCamera.ReleaseCamera();
        ladderCamera = null;
        return true;
    }
}
