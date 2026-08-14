using Common;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Agents.Packets;
using Newtonsoft.Json;
#if DEBUG
using Missions.Diagnostics;
#endif
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
    private static JoustDriverBehavior joustDriver;
    private static JoustDriverBehavior incomingAiJoustDriver;

    private sealed class JoustDriverBehavior : MissionBehavior
    {
        private const string RequiredWeaponId = "western_spear_4_t4";

        private readonly Agent rider;
        private readonly Agent target;
        private readonly Guid riderId;
        private readonly Guid targetId;
        private readonly EquipmentIndex originalMainHand;
        private readonly AgentControllerType expectedController;
        private readonly Agent originalTarget;
        private readonly bool originalAiPaused;
        private readonly Agent.MovementControlFlag originalMovementFlags;
        private readonly Vec2 originalMovementInput;
        private bool attackHeld;
        private bool releasedAttack;
        private int drivenFrames;
        private int inputBoundaryWrites;
        private int skippedInputBoundaryWrites;
        private string lastInputBoundarySkipReason = "not-applied";
        private Vec2 lastInputVector;
        private Agent.MovementControlFlag lastMovementFlags;
        private Vec2 lastMovementDirection;
        private Vec3 lastLookDirection;
        private Vec3 lastMountDirection;
        private int lastInputBoundaryThreadId;
        private bool lastInputBoundaryWasGameThread;

        public JoustDriverBehavior(
            Agent rider,
            Agent target,
            Guid riderId,
            Guid targetId,
            EquipmentIndex originalMainHand,
            AgentControllerType expectedController)
        {
            this.rider = rider;
            this.target = target;
            this.riderId = riderId;
            this.targetId = targetId;
            this.originalMainHand = originalMainHand;
            this.expectedController = expectedController;
            originalMovementFlags = rider.MovementFlags;
            originalMovementInput = rider.MovementInputVector;
            if (expectedController == AgentControllerType.AI)
            {
                originalTarget = rider.GetTargetAgent();
                originalAiPaused = rider.IsPaused;
            }
            attackHeld = true;
        }

        public override MissionBehaviorType BehaviorType =>
            MissionBehaviorType.Other;

        public Guid RiderId => riderId;
        public Guid TargetId => targetId;
        public int DrivenFrames => drivenFrames;
        public bool AttackHeld => attackHeld;
        public bool ReleasedAttack => releasedAttack;
        public int InputBoundaryWrites => inputBoundaryWrites;
        public int SkippedInputBoundaryWrites => skippedInputBoundaryWrites;
        public string LastInputBoundarySkipReason => lastInputBoundarySkipReason;
        public Vec2 LastInputVector => lastInputVector;
        public Agent.MovementControlFlag LastMovementFlags => lastMovementFlags;
        public Vec2 LastMovementDirection => lastMovementDirection;
        public Vec3 LastLookDirection => lastLookDirection;
        public Vec3 LastMountDirection => lastMountDirection;
        public int LastInputBoundaryThreadId => lastInputBoundaryThreadId;
        public bool LastInputBoundaryWasGameThread =>
            lastInputBoundaryWasGameThread;
        public Agent.ActionStage ActionStage =>
            rider?.GetCurrentActionStage(1) ?? Agent.ActionStage.None;
        public float TargetDistance => rider == null || target == null
            ? -1f
            : rider.Position.Distance(target.Position);
        public float MountedSpeed => rider?.MountAgent?
            .GetRealGlobalVelocity().AsVec2.Length ?? 0f;
        public AgentControllerType Controller =>
            rider?.Controller ?? AgentControllerType.None;
        public bool Active =>
            rider != null &&
            rider.IsActive() &&
            rider.Mission == Mission &&
            target != null &&
            target.IsActive() &&
            target.Mission == Mission;

        public override void OnMissionTick(float dt)
        {
            if (expectedController != AgentControllerType.AI ||
                !Active ||
                rider.Controller != expectedController)
            {
                return;
            }

            rider.SetIsAIPaused(false);
            rider.SetTargetAgent(target);
            ApplyInput();
        }

        public void ApplyInputAtNativeTickBoundary(Mission mission)
        {
            if (expectedController != AgentControllerType.Player)
                return;

            lastInputBoundaryThreadId =
                System.Threading.Thread.CurrentThread.ManagedThreadId;
            lastInputBoundaryWasGameThread = GameThread.Instance.IsGameThread;
            if (!lastInputBoundaryWasGameThread)
            {
                SkipInputBoundary("not-game-thread");
                return;
            }
            if (!ReferenceEquals(Mission, mission))
            {
                SkipInputBoundary("mission-mismatch");
                return;
            }
            if (!Active)
            {
                SkipInputBoundary("inactive");
                return;
            }
            if (rider.Controller != expectedController)
            {
                SkipInputBoundary("controller-mismatch");
                return;
            }

            ApplyInput();
            inputBoundaryWrites++;
            lastInputBoundarySkipReason = null;
        }

        private void SkipInputBoundary(string reason)
        {
            skippedInputBoundaryWrites++;
            lastInputBoundarySkipReason = reason;
        }

        private void ApplyInput()
        {
            drivenFrames++;
            Vec3 offset = target.Position - rider.Position;
            Vec2 heading = offset.AsVec2;
            if (heading.LengthSquared <= 0.0001f)
                heading = rider.GetMovementDirection();
            if (heading.LengthSquared <= 0.0001f)
                heading = Vec2.Forward;
            heading.Normalize();

            Vec3 lookDirection = offset;
            if (lookDirection.LengthSquared <= 0.0001f)
                lookDirection = new Vec3(heading.X, heading.Y, 0f);
            lookDirection.Normalize();

            rider.SetMovementDirection(in heading);
            rider.MovementInputVector = new Vec2(0f, 1f);
            rider.LookDirection = lookDirection;

            Agent.ActionStage stage = rider.GetCurrentActionStage(1);
            if (!releasedAttack &&
                (stage == Agent.ActionStage.AttackReady ||
                 stage == Agent.ActionStage.AttackQuickReady))
            {
                attackHeld = false;
                releasedAttack = true;
            }

            Agent.MovementControlFlag preservedFlags =
                rider.MovementFlags &
                ~(Agent.MovementControlFlag.MoveMask |
                  Agent.MovementControlFlag.AttackMask);
            rider.MovementFlags = preservedFlags |
                Agent.MovementControlFlag.Forward |
                (attackHeld
                    ? Agent.MovementControlFlag.AttackUp
                    : Agent.MovementControlFlag.None);

            lastInputVector = rider.MovementInputVector;
            lastMovementFlags = rider.MovementFlags;
            lastMovementDirection = rider.GetMovementDirection();
            lastLookDirection = rider.LookDirection;
            Agent mount = rider.MountAgent;
            if (mount != null)
                lastMountDirection = mount.LookDirection;
        }

        public void Restore()
        {
            if (rider == null || !rider.IsActive() || rider.Mission != Mission)
                return;

            rider.MovementFlags = originalMovementFlags;
            rider.MovementInputVector = originalMovementInput;
            if (expectedController == AgentControllerType.AI)
            {
                rider.SetTargetAgent(originalTarget);
                rider.SetIsAIPaused(originalAiPaused);
            }
            if (originalMainHand == EquipmentIndex.None)
            {
                rider.TryToSheathWeaponInHand(
                    Agent.HandIndex.MainHand,
                    Agent.WeaponWieldActionType.WithAnimationUninterruptible);
            }
            else
            {
                rider.TryToWieldWeaponInSlot(
                    originalMainHand,
                    Agent.WeaponWieldActionType.WithAnimationUninterruptible,
                    isWieldedOnSpawn: false);
            }
        }

        public override void OnRemoveBehavior()
        {
            Restore();
            if (ReferenceEquals(joustDriver, this))
                joustDriver = null;
            if (ReferenceEquals(incomingAiJoustDriver, this))
                incomingAiJoustDriver = null;
        }

        public static bool TryFindWeaponSlot(
            Agent agent,
            out EquipmentIndex slot)
        {
            slot = EquipmentIndex.None;
            for (EquipmentIndex candidate = EquipmentIndex.WeaponItemBeginSlot;
                 candidate < EquipmentIndex.NumAllWeaponSlots;
                 candidate++)
            {
                ItemObject item = agent.Equipment[candidate].Item;
                if (item?.StringId != RequiredWeaponId)
                    continue;

                slot = candidate;
                return true;
            }

            return false;
        }
    }

    internal static void ApplyJoustInputAtNativeTickBoundary(Mission mission)
    {
        joustDriver?.ApplyInputAtNativeTickBoundary(mission);
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

    [CommandLineArgumentFunction("joust", "coop.debug.battle")]
    public static string Joust(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.battle.joust <start|state|stop>";

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                return StartJoust();
            case "state":
                return FormatJoustState();
            case "stop":
                return StopJoust();
            default:
                return "Usage: coop.debug.battle.joust <start|state|stop>";
        }
    }

    [CommandLineArgumentFunction("joust_routed_state", "coop.debug.battle")]
    public static string JoustRoutedState(List<string> args)
    {
        if (args.Count > 1 ||
            (args.Count == 1 &&
             !Guid.TryParse(args[0], out Guid routedHitId)))
        {
            return "Usage: coop.debug.battle.joust_routed_state " +
                   "[attackerAgentId|routedHitId]";
        }
        if (args.Count == 0)
            routedHitId = Guid.Empty;

        CoopBattleController controller = Mission.Current?
            .GetMissionBehavior<CoopBattleController>();
        BattleDamageRouter.RoutedDamageDebugSnapshot snapshot = controller?
            .DebugDamageRouter
            .GetRoutedDamageDebugSnapshot(routedHitId);
        if (snapshot == null)
        {
            return routedHitId == Guid.Empty
                ? "JOUST_ROUTED_STATE no routed damage"
                : "JOUST_ROUTED_STATE no routed damage for hit " +
                  routedHitId.ToString("D");
        }

        return "JOUST_ROUTED_STATE hit=" + routedHitId.ToString("D") +
               Environment.NewLine +
               "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(snapshot);
    }

    [CommandLineArgumentFunction("incoming_ai_hit_source_state", "coop.debug.battle")]
    public static string IncomingAiHitSourceState(List<string> args)
    {
        if (args.Count != 4 ||
            !Guid.TryParse(args[0], out Guid victimRiderAgentId) ||
            victimRiderAgentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(args[1]) ||
            string.IsNullOrWhiteSpace(args[2]) ||
            !long.TryParse(args[3], out long afterSequence) ||
            afterSequence < 0)
        {
            return "Usage: coop.debug.battle.incoming_ai_hit_source_state " +
                   "<victimRiderAgentId> <attackerControllerId> " +
                   "<victimControllerId> <afterSequence>";
        }

        CoopBattleController controller = Mission.Current?
            .GetMissionBehavior<CoopBattleController>();
        BattleDamageRouter.RoutedDamageDebugSnapshot snapshot = controller?
            .DebugDamageRouter
            .GetIncomingAiDamageSourceDebugSnapshot(
                victimRiderAgentId,
                args[1],
                args[2],
                afterSequence);
        if (snapshot == null)
        {
            return "INCOMING_AI_HIT_SOURCE_STATE no matching source hit on " +
                   victimRiderAgentId.ToString("D") + " after " +
                   afterSequence;
        }

        return "INCOMING_AI_HIT_SOURCE_STATE hit=" +
               snapshot.RoutedHitId.ToString("D") +
               Environment.NewLine +
               "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(snapshot);
    }

    [CommandLineArgumentFunction("incoming_ai_joust", "coop.debug.battle")]
    public static string IncomingAiJoust(List<string> args)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            return "Usage: coop.debug.battle.incoming_ai_joust " +
                   "<start victimRiderAgentId|state|stop>";
        }

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                if (args.Count != 2 ||
                    !Guid.TryParse(args[1], out Guid victimRiderAgentId) ||
                    victimRiderAgentId == Guid.Empty)
                {
                    return "Usage: coop.debug.battle.incoming_ai_joust " +
                           "start <victimRiderAgentId>";
                }
                return StartIncomingAiJoust(victimRiderAgentId);
            case "state":
                return args.Count == 1
                    ? FormatIncomingAiJoustState()
                    : "Usage: coop.debug.battle.incoming_ai_joust state";
            case "stop":
                return args.Count == 1
                    ? StopIncomingAiJoust()
                    : "Usage: coop.debug.battle.incoming_ai_joust stop";
            default:
                return "Usage: coop.debug.battle.incoming_ai_joust " +
                       "<start victimRiderAgentId|state|stop>";
        }
    }

    private static string StartIncomingAiJoust(Guid victimRiderAgentId)
    {
        if (incomingAiJoustDriver != null)
            return FormatIncomingAiJoustState();

        Mission mission = Mission.Current;
        CoopBattleController controller = mission?
            .GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
            return "INCOMING_AI_JOUST no active coop battle";
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return "INCOMING_AI_JOUST network agent registry is unavailable";
        if (!registry.TryGetAgentInfo(
                victimRiderAgentId,
                out CoopAgentInfo targetInfo) ||
            targetInfo.Agent == null ||
            !targetInfo.Agent.IsActive() ||
            !targetInfo.Agent.IsHuman ||
            !targetInfo.Agent.HasMount ||
            targetInfo.Agent.MountAgent?.IsActive() != true ||
            registry.IsLocallyControlled(targetInfo.AgentId))
        {
            return "INCOMING_AI_JOUST victim must be an active remote mounted rider";
        }

        CoopAgentInfo riderInfo = SelectIncomingAiJoustRider(
            registry.GetAgents(controller.Session.OwnControllerId),
            targetInfo.Agent);
        if (riderInfo?.Agent == null)
            return "INCOMING_AI_JOUST no locally authoritative mounted AI attacker";
        if (!JoustDriverBehavior.TryFindWeaponSlot(
                riderInfo.Agent,
                out EquipmentIndex joustWeaponSlot))
        {
            return "INCOMING_AI_JOUST attacker does not carry western_spear_4_t4";
        }

        EquipmentIndex originalMainHand =
            riderInfo.Agent.GetPrimaryWieldedItemIndex();
        if (originalMainHand != joustWeaponSlot)
        {
            riderInfo.Agent.TryToWieldWeaponInSlot(
                joustWeaponSlot,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible,
                isWieldedOnSpawn: false);
        }

        incomingAiJoustDriver = new JoustDriverBehavior(
            riderInfo.Agent,
            targetInfo.Agent,
            riderInfo.AgentId,
            targetInfo.AgentId,
            originalMainHand,
            AgentControllerType.AI);
        mission.AddMissionBehavior(incomingAiJoustDriver);
        return FormatIncomingAiJoustState();
    }

    internal static CoopAgentInfo SelectIncomingAiJoustRider(
        IEnumerable<CoopAgentInfo> candidates,
        Agent target)
    {
        if (candidates == null || target == null)
            return null;

        CoopAgentInfo selected = candidates
            .Where(info =>
                info != null &&
                info.Agent != null &&
                info.Agent.IsActive() &&
                info.Agent.IsHuman &&
                info.Agent.Controller == AgentControllerType.AI &&
                info.Agent.HasMount &&
                info.Agent.MountAgent?.IsActive() == true &&
                info.Agent.Team?.Side != target.Team?.Side)
            .OrderBy(info =>
                info.Agent.Position.DistanceSquared(target.Position))
            .FirstOrDefault();
        return selected;
    }

    private static string StopIncomingAiJoust()
    {
        JoustDriverBehavior driver = incomingAiJoustDriver;
        if (driver == null)
            return "INCOMING_AI_JOUST inactive";

        Mission mission = driver.Mission;
        if (mission != null)
            mission.RemoveMissionBehavior(driver);
        else
            driver.Restore();
        incomingAiJoustDriver = null;
        return "INCOMING_AI_JOUST stopped";
    }

    private static string FormatIncomingAiJoustState()
    {
        JoustDriverBehavior driver = incomingAiJoustDriver;
        var state = new
        {
            active = driver?.Active == true,
            riderAgentId = driver?.RiderId ?? Guid.Empty,
            targetAgentId = driver?.TargetId ?? Guid.Empty,
            drivenFrames = driver?.DrivenFrames ?? 0,
            attackHeld = driver?.AttackHeld ?? false,
            attackReleased = driver?.ReleasedAttack ?? false,
            actionStage = driver?.ActionStage.ToString() ??
                Agent.ActionStage.None.ToString(),
            targetDistance = driver?.TargetDistance ?? -1f,
            mountedSpeed = driver?.MountedSpeed ?? 0f,
            controller = driver?.Controller.ToString() ??
                AgentControllerType.None.ToString(),
        };
        return "INCOMING_AI_JOUST active=" + state.active +
               " rider=" + state.riderAgentId.ToString("D") +
               " target=" + state.targetAgentId.ToString("D") +
               " controller=" + state.controller +
               Environment.NewLine +
               "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state);
    }

    private static string StartJoust()
    {
        if (joustDriver != null)
            return FormatJoustState();

        Mission mission = Mission.Current;
        CoopBattleController controller = mission?
            .GetMissionBehavior<CoopBattleController>();
        Agent rider = Agent.Main;
        if (mission == null || controller == null || rider == null ||
            !rider.IsActive() || rider.Mission != mission)
        {
            return "JOUST no active local battle rider";
        }
        if (rider.Controller != AgentControllerType.Player ||
            !rider.HasMount || rider.MountAgent?.IsActive() != true)
        {
            return "JOUST main agent must remain player-controlled and mounted";
        }
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry) ||
            !registry.TryGetAgentInfo(rider, out var riderInfo) ||
            !registry.IsLocallyControlled(riderInfo.AgentId))
        {
            return "JOUST main agent is not locally controlled";
        }
        if (!JoustDriverBehavior.TryFindWeaponSlot(
                rider,
                out EquipmentIndex joustWeaponSlot))
        {
            return "JOUST main agent does not carry western_spear_4_t4";
        }

        CoopAgentInfo targetInfo = registry.GetControllerIds()
            .Where(id => id != controller.Session.OwnControllerId)
            .SelectMany(registry.GetAgents)
            .Where(info => info.Agent != null &&
                           info.Agent.IsActive() &&
                           info.Agent.IsHuman &&
                           info.Agent.HasMount &&
                           info.Agent.MountAgent?.IsActive() == true &&
                           info.Agent.Team?.Side != rider.Team?.Side)
            .OrderBy(info =>
                info.Agent.Position.DistanceSquared(rider.Position))
            .FirstOrDefault();
        if (targetInfo?.Agent == null)
            return "JOUST no active remote mounted enemy puppet";

        EquipmentIndex originalMainHand = rider.GetPrimaryWieldedItemIndex();
        if (originalMainHand != joustWeaponSlot)
        {
            rider.TryToWieldWeaponInSlot(
                joustWeaponSlot,
                Agent.WeaponWieldActionType.WithAnimationUninterruptible,
                isWieldedOnSpawn: false);
        }

        joustDriver = new JoustDriverBehavior(
            rider,
            targetInfo.Agent,
            riderInfo.AgentId,
            targetInfo.AgentId,
            originalMainHand,
            AgentControllerType.Player);
        mission.AddMissionBehavior(joustDriver);
        return FormatJoustState();
    }

    private static string StopJoust()
    {
        JoustDriverBehavior driver = joustDriver;
        if (driver == null)
            return "JOUST inactive";

        Mission mission = driver.Mission;
        if (mission != null)
            mission.RemoveMissionBehavior(driver);
        else
            driver.Restore();
        joustDriver = null;
        return "JOUST stopped";
    }

    private static string FormatJoustState()
    {
        JoustDriverBehavior driver = joustDriver;
        var state = new
        {
            active = driver?.Active == true,
            riderAgentId = driver?.RiderId ?? Guid.Empty,
            targetAgentId = driver?.TargetId ?? Guid.Empty,
            drivenFrames = driver?.DrivenFrames ?? 0,
            inputBoundaryWrites = driver?.InputBoundaryWrites ?? 0,
            skippedInputBoundaryWrites =
                driver?.SkippedInputBoundaryWrites ?? 0,
            inputBoundarySkipReason =
                driver?.LastInputBoundarySkipReason ?? "inactive",
            inputBoundaryThreadId =
                driver?.LastInputBoundaryThreadId ?? 0,
            inputBoundaryWasGameThread =
                driver?.LastInputBoundaryWasGameThread ?? false,
            missionHash = driver?.Mission?.GetHashCode() ?? 0,
            currentMissionHash = Mission.Current?.GetHashCode() ?? 0,
            missionMatchesCurrent =
                driver != null &&
                ReferenceEquals(driver.Mission, Mission.Current),
            controller = driver?.Controller.ToString() ??
                AgentControllerType.None.ToString(),
            movementInputX = driver?.LastInputVector.X ?? 0f,
            movementInputY = driver?.LastInputVector.Y ?? 0f,
            movementFlags =
                (uint)(driver?.LastMovementFlags ??
                       Agent.MovementControlFlag.None),
            movementDirectionX =
                driver?.LastMovementDirection.X ?? 0f,
            movementDirectionY =
                driver?.LastMovementDirection.Y ?? 0f,
            lookDirectionX = driver?.LastLookDirection.X ?? 0f,
            lookDirectionY = driver?.LastLookDirection.Y ?? 0f,
            lookDirectionZ = driver?.LastLookDirection.Z ?? 0f,
            mountDirectionX = driver?.LastMountDirection.X ?? 0f,
            mountDirectionY = driver?.LastMountDirection.Y ?? 0f,
            mountDirectionZ = driver?.LastMountDirection.Z ?? 0f,
            attackHeld = driver?.AttackHeld ?? false,
            attackReleased = driver?.ReleasedAttack ?? false,
            actionStage = driver?.ActionStage.ToString() ??
                Agent.ActionStage.None.ToString(),
            targetDistance = driver?.TargetDistance ?? -1f,
            mountedSpeed = driver?.MountedSpeed ?? 0f,
        };
        return "JOUST active=" + state.active +
               " rider=" + state.riderAgentId.ToString("D") +
               " target=" + state.targetAgentId.ToString("D") +
               Environment.NewLine +
               "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state);
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

        string output = $"instance={controller.Session.InstanceId} host={controller.Session.IsLocalHost} " +
            $"activated={controller.Deployment.IsActivated} committed={controller.Deployment.IsCommitted} " +
            $"deploymentReady={deploymentReady} mainAgent={Agent.Main != null} activeAgents={activeAgents} " +
            $"reserveSuppliers={suppliers.Count} populatedReserves={suppliers.Count(supplier => supplier.IsPopulated)} " +
            $"receiverOwnedReserves={receiverReserves.Length} receiverReserve={string.Join(",", receiverReserves)} " +
            $"playerSide={playerTeam?.Side.ToString() ?? "None"} enemyParties={enemyParties} enemyActive={enemies.Count} " +
            $"enemyAi={enemies.Count(agent => agent.IsAIControlled)} enemyFleeing={enemyFleeing} " +
            $"enemyMovedSinceLast={moved} damageReceivedEvents={ownDamageEvents} " +
            $"resultState={result?.BattleState.ToString() ?? "None"} " +
            $"battleResolved={result?.BattleResolved ?? false} playerVictory={result?.PlayerVictory ?? false}";
        string structuredState = JsonConvert.SerializeObject(new
        {
            instanceId = controller.Session.InstanceId,
            isLocalHost = controller.Session.IsLocalHost,
            activated = controller.Deployment.IsActivated,
            committed = controller.Deployment.IsCommitted,
            deploymentReady,
            hasMainAgent = Agent.Main != null,
            activeAgents,
            reserveSuppliers = suppliers.Count,
            populatedReserves = suppliers.Count(supplier => supplier.IsPopulated),
            receiverOwnedReserves = receiverReserves.Length,
            receiverReserve = receiverReserves,
            playerSide = playerTeam?.Side.ToString() ?? "None",
            enemyParties,
            enemyActive = enemies.Count,
            enemyAi = enemies.Count(agent => agent.IsAIControlled),
            enemyFleeing,
            enemyMovedSinceLast = moved,
            damageReceivedEvents = ownDamageEvents,
            resultState = result?.BattleState.ToString() ?? "None",
            battleResolved = result?.BattleResolved ?? false,
            playerVictory = result?.PlayerVictory ?? false,
        });
        return output + Environment.NewLine +
            $"LIVE_TEST_JSON={structuredState}";
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
        if (joustDriver != null)
            StopJoust();
        if (incomingAiJoustDriver != null)
            StopIncomingAiJoust();
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
