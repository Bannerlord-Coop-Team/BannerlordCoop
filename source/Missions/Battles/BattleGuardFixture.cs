#if DEBUG
using Common;
using GameInterface.Services.Battles.Messages;
using GameInterface.Services.Entity;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace Missions.Battles;

public interface IBattleGuardFixture
{
    void Apply(
        NetworkBattleGuardFixtureCommand command,
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator interpolator);
    void ApplyMountedRoute(NetworkBattleGuardFixtureRoute route);
    void ApplyMountedStrike(NetworkBattleGuardFixtureStrike strike);
    bool IsDrivingPlayerInput(INetworkAgentRegistry agentRegistry);
    bool PrepareNativePlayerInput(INetworkAgentRegistry agentRegistry);
    bool IsHoldingNativePlayerBlock(INetworkAgentRegistry agentRegistry);
    void ApplyPlayerInput(INetworkAgentRegistry agentRegistry);
    void ReapplyPlayerGuardInput(INetworkAgentRegistry agentRegistry);
    void RefreshOwnedMountedStrikeLook(INetworkAgentRegistry agentRegistry);
    void ApplyPostAgentTickGuardInput(INetworkAgentRegistry agentRegistry);
    void Tick(float dt, INetworkAgentRegistry agentRegistry);
    void SamplePreReplayDisplayedState(
        float dt,
        INetworkAgentRegistry agentRegistry);
    void SamplePostReplayDisplayedState(INetworkAgentRegistry agentRegistry);
    void SampleFinalDisplayedState(float dt, INetworkAgentRegistry agentRegistry);
    void ObserveScoreHit(
        Agent affectedAgent,
        Agent affectorAgent,
        bool isBlocked,
        in AttackCollisionData collisionData,
        float damagedHp);
    string GetState(
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator interpolator);
    string GetCandidates(INetworkAgentRegistry agentRegistry, List<string> args);
    void Reset(INetworkAgentRegistry agentRegistry);
}

public class BattleGuardFixture : IBattleGuardFixture
{
    private const string GuardWeaponId = "empire_lance_1_t3_blunt";
    private const string FootStrikerWeaponId = "empire_sword_1_t2_blunt";
    private const string MountedStrikerWeaponId = "empire_menavlion_1_t3_blunt";
    private const string MountedLeftGuardAction =
        "act_defend_left_1h_passive";
    private const string MountedRightGuardAction =
        "act_defend_right_1h_passive";
    private const string WaitingForMountedGuardRouteError =
        "waiting for mounted guard route";
    private const float SampleIntervalSeconds = 0.05f;
    private const float ProgressEpsilon = 0.001f;
    private const float FixtureAttackPressSeconds = 0.35f;
    private const float FixtureLaneOffset = 25f;
    private const float MountedRouteLength = 120f;
    private const float MountedRouteRadius = 5f;
    private const float MountedRouteSampleLength = 5f;
    private const float MountedRouteMaximumRise = 1.5f;
    private const float MountedStrikeGuardedArcOffset = 1.5f;
    private const float MountedStrikeForwardBias = 1f;
    private const float MountedStrikeMaximumStageLateral = 3f;
    private const float MountedStrikeMinimumTravelGuardAlignment = 0.99f;
    private const float MountedStrikeMinimumContactAlignment = 0.95f;
    private const float MountedStrikeMinimumReplicatedLookAlignment = 0.999f;
    private const float MountedStrikeMinimumLeadDistance = 6f;
    private const float MountedStrikeMaximumLeadDistance = 10f;
    private const float MountedStrikeMinimumReleaseLeadSeconds = 0.25f;
    private const float MountedStrikeReleaseLeadStepSeconds = 0.1f;
    private const int MountedStrikeReleaseLeadProfiles = 5;
    private const float MountedStrikeMaximumChargeSeconds = 2.5f;
    private const float MountedStrikeMinimumCalibratedSpeedRatio = 0.95f;
    private const float FixtureStrikerSwingSpeedMultiplier = 1f;
    private const float MountedStrikeMinimumRunway =
        BattleGuardMountedRoute.StrikeClearanceDistance +
        MountedStrikeMaximumLeadDistance +
        MountedStrikeGuardedArcOffset;
    private const Agent.MovementControlFlag AttackFlags =
        Agent.MovementControlFlag.AttackDown |
        Agent.MovementControlFlag.AttackUp |
        Agent.MovementControlFlag.AttackLeft |
        Agent.MovementControlFlag.AttackRight;
    private const Agent.MovementControlFlag TranslationFlags =
        Agent.MovementControlFlag.Forward |
        Agent.MovementControlFlag.Backward |
        Agent.MovementControlFlag.StrafeLeft |
        Agent.MovementControlFlag.StrafeRight;
    private const Agent.MovementControlFlag TurnFlags =
        Agent.MovementControlFlag.TurnLeft |
        Agent.MovementControlFlag.TurnRight;
    private const Agent.MovementControlFlag DefendStateFlags =
        Agent.MovementControlFlag.DefendMask |
        Agent.MovementControlFlag.DefendBlock;
    private const Agent.MovementControlFlag DriveFlags =
        TranslationFlags |
        TurnFlags |
        AttackFlags |
        DefendStateFlags;

    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleNetwork network;
    private readonly List<AiPauseState> aiPauseStates = new();
    private GuardDriver guardDriver;
    private StrikerDriver strikerDriver;
    private PendingGuardRestore pendingGuardRestore;
    private FixtureRoles roles;
    private float sampleElapsed;
    private SampleState sample = new();
    private string lastError;
    private MissionScreen evidenceCameraScreen;
    private Camera evidenceCamera;
    private Camera previousCustomCamera;
    private MatrixFrame previousCombatCameraFrame;
    private bool previousAllowInputWithCustomCamera;
    private Guid evidenceCameraTargetId;
    private NetworkBattleGuardFixtureRoute pendingMountedRoute;
    private NetworkBattleGuardFixtureStrike pendingMountedStrike;
    private string currentBattleInstanceId;
    private Guid currentCommandId;

    public BattleGuardFixture(
        IControllerIdProvider controllerIdProvider,
        IBattleNetwork network)
    {
        if (controllerIdProvider == null)
            throw new ArgumentNullException(nameof(controllerIdProvider));
        if (network == null)
            throw new ArgumentNullException(nameof(network));

        this.controllerIdProvider = controllerIdProvider;
        this.network = network;
    }

    public void Apply(
        NetworkBattleGuardFixtureCommand command,
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator interpolator)
    {
        if (command == null ||
            agentRegistry == null ||
            interpolator == null)
            return;
        if (command.Reset)
        {
            Reset(agentRegistry);
            return;
        }
        if (pendingGuardRestore != null)
        {
            lastError = "guard remount is still pending";
            return;
        }
        if (!TryCreateRoles(command, out FixtureRoles commandRoles))
        {
            lastError = "invalid guard fixture role command";
            return;
        }

        bool fixtureChanged =
            (roles != null &&
             (roles.GuardAgentId != commandRoles.GuardAgentId ||
              roles.GuardAuthority != commandRoles.GuardAuthority ||
              roles.StrikerAgentId != commandRoles.StrikerAgentId ||
              roles.StrikerAuthority != commandRoles.StrikerAuthority)) ||
            (guardDriver != null &&
             (guardDriver.Mode != command.Mode ||
              guardDriver.UseMovementFlagGuardInput !=
                command.UseMovementFlagGuardInput));
        if (fixtureChanged)
        {
            NetworkBattleGuardFixtureRoute nextRoute = pendingMountedRoute;
            NetworkBattleGuardFixtureStrike nextStrike =
                pendingMountedStrike;
            Reset(agentRegistry);
            if (command.Mode == BattleGuardFixtureMode.Mounted &&
                MatchesMountedRoute(
                    nextRoute,
                    commandRoles,
                    command.Phase,
                    command.CommandId))
            {
                pendingMountedRoute = nextRoute;
            }
            if (command.Mode == BattleGuardFixtureMode.Mounted &&
                command.Phase == BattleGuardFixturePhase.Attack &&
                MatchesMountedStrike(
                    nextStrike,
                    commandRoles,
                    command.CommandId,
                    command.BattleInstanceId))
            {
                pendingMountedStrike = nextStrike;
            }
        }
        if (pendingGuardRestore != null)
        {
            lastError = "guard remount is still pending";
            return;
        }
        if (command.Phase == BattleGuardFixturePhase.Calibration)
            guardDriver?.ResetGuardEvidence();
        if (command.Phase == BattleGuardFixturePhase.Attack &&
            (guardDriver == null || !guardDriver.HasGuardBaselineHealth))
        {
            lastError = "guard phase must complete before attack phase";
            return;
        }

        roles = commandRoles;
        currentBattleInstanceId = command.BattleInstanceId;
        currentCommandId = command.CommandId;
        if (command.Phase != BattleGuardFixturePhase.Attack &&
            strikerDriver != null)
        {
            RestoreStriker(agentRegistry);
            strikerDriver = null;
        }
        if (aiPauseStates.Count == 0)
            CaptureAiPauseStates(agentRegistry);
        sampleElapsed = 0f;
        sample = new SampleState();
        if (command.Phase == BattleGuardFixturePhase.Attack)
            guardDriver.CopyGuardPresentationTo(sample);
        lastError = null;

        if (TryGetExactAgent(
                agentRegistry,
                roles.GuardAgentId,
                roles.GuardAuthority,
                out CoopAgentInfo guardInfo))
        {
            bool drivesGuard =
                controllerIdProvider.ControllerId == roles.GuardAuthority &&
                agentRegistry.IsLocallyControlled(roles.GuardAgentId);
            ApplyGuard(command, guardInfo.Agent, drivesGuard);
            if (command.Phase == BattleGuardFixturePhase.Guard)
            {
                guardDriver.GuardBaselineHealth = guardInfo.Agent.Health;
                guardDriver.HasGuardBaselineHealth = true;
            }

            sample.AgentId = guardInfo.AgentId;
            sample.BaselineHealth =
                command.Phase == BattleGuardFixturePhase.Attack
                    ? guardDriver.GuardBaselineHealth
                    : guardInfo.Agent.Health;
            sample.Health = guardInfo.Agent.Health;
            sample.HasBaselineHealth = true;
        }

        if (command.Phase == BattleGuardFixturePhase.Attack)
            TryApplyPendingMountedStrike(command);

        if (command.Phase == BattleGuardFixturePhase.Attack &&
            TryGetExactAgent(
                agentRegistry,
                roles.StrikerAgentId,
                roles.StrikerAuthority,
                out CoopAgentInfo strikerInfo) &&
            TryGetExactAgent(
                agentRegistry,
                roles.GuardAgentId,
                roles.GuardAuthority,
                out CoopAgentInfo attackGuardInfo))
        {
            bool drivesStriker =
                controllerIdProvider.ControllerId == roles.StrikerAuthority &&
                agentRegistry.IsLocallyControlled(roles.StrikerAgentId);
            ApplyStriker(
                strikerInfo.Agent,
                attackGuardInfo.Agent,
                drivesStriker,
                interpolator);
        }
    }

    public void ApplyMountedRoute(NetworkBattleGuardFixtureRoute route)
    {
        if (!IsValidMountedRoute(route))
            return;
        if (roles == null ||
            guardDriver == null ||
            currentCommandId != route.CommandId ||
            roles.GuardAgentId != route.GuardAgentId ||
            roles.GuardAuthority != route.GuardAuthority ||
            guardDriver.Phase != route.Phase)
        {
            pendingMountedRoute = route;
            return;
        }

        SetReceivedMountedRoute(guardDriver, route);
        pendingMountedRoute = null;
        lastError = ClearMountedRouteWaitError(lastError);
    }

    public void ApplyMountedStrike(NetworkBattleGuardFixtureStrike strike)
    {
        if (!IsValidMountedStrike(strike))
            return;
        if (!MatchesMountedStrike(
                strike,
                roles,
                currentCommandId,
                currentBattleInstanceId) ||
            guardDriver == null ||
            guardDriver.Phase != BattleGuardFixturePhase.Attack)
        {
            pendingMountedStrike = strike;
            return;
        }

        SetReceivedMountedStrike(guardDriver, strike);
        pendingMountedStrike = null;
    }

    public void ApplyPlayerInput(INetworkAgentRegistry agentRegistry)
    {
        if (TryGetDrivenGuardAgent(agentRegistry, out Agent agent))
            DriveGuardInput(agent, guardDriver);
    }

    public bool IsDrivingPlayerInput(INetworkAgentRegistry agentRegistry)
    {
        return TryGetDrivenGuardAgent(agentRegistry, out Agent agent) &&
            ReferenceEquals(agent, Mission.Current?.MainAgent);
    }

    public bool PrepareNativePlayerInput(
        INetworkAgentRegistry agentRegistry)
    {
        if (!TryGetDrivenGuardAgent(agentRegistry, out Agent agent) ||
            !ReferenceEquals(agent, Mission.Current?.MainAgent) ||
            !ShouldUseNativePlayerGuardInput(
                guardDriver.Mode,
                guardDriver.UseMovementFlagGuardInput))
        {
            return false;
        }

        if (IsGuarding(agent, guardDriver))
        {
            Input.PressKey(
                GetNativePlayerGuardDirectionKey(
                    guardDriver.Direction));
        }

        return true;
    }

    public bool IsHoldingNativePlayerBlock(
        INetworkAgentRegistry agentRegistry)
    {
        return TryGetDrivenGuardAgent(agentRegistry, out Agent agent) &&
            ReferenceEquals(agent, Mission.Current?.MainAgent) &&
            ShouldUseNativePlayerGuardInput(
                guardDriver.Mode,
                guardDriver.UseMovementFlagGuardInput) &&
            IsGuarding(agent, guardDriver);
    }

    public void ReapplyPlayerGuardInput(
        INetworkAgentRegistry agentRegistry)
    {
        if (!TryGetDrivenGuardAgent(agentRegistry, out Agent agent))
            return;

        bool guarding = IsGuarding(agent, guardDriver);
        Agent.MovementControlFlag defendFlags = guarding
            ? GetDefendFlags(guardDriver.Direction)
            : Agent.MovementControlFlag.None;
        if (!guardDriver.UseMovementFlagGuardInput &&
            guardDriver.MountedPostNativeGuardCommandPending)
        {
            Agent.GuardMode guardMode = guarding
                ? GetGuardMode(guardDriver.Direction)
                : Agent.GuardMode.None;
            if (guardDriver.MountedPostNativeDirectionChanged)
            {
                AgentActionData.ApplyGuardDirectionTransition(
                    agent,
                    guardMode);
            }
            else
            {
                AgentActionData.ApplyGuardState(
                    agent,
                    guardMode,
                    force: guarding);
            }
            guardDriver.MountedPresentationActionPending =
                ShouldQueueMountedGuardPresentation(
                    guarding,
                    guardDriver.Direction);
            guardDriver.MountedPostNativeGuardCommandPending = false;
            guardDriver.MountedPostNativeDirectionChanged = false;
        }

        AgentActionData.ApplyDefendMovementFlags(agent, defendFlags);
        guardDriver.ObserveAppliedInput(agent);
        ApplyOwnedMountedStrikeLook(agent, guardDriver);

        // Both production action polls must observe the same held fixture presentation.
        ApplyMountedGuardPresentationAction(agentRegistry);
    }

    public void RefreshOwnedMountedStrikeLook(
        INetworkAgentRegistry agentRegistry)
    {
        if (TryGetDrivenGuardAgent(agentRegistry, out Agent agent))
            ApplyOwnedMountedStrikeLook(agent, guardDriver);
    }

    public void Tick(float dt, INetworkAgentRegistry agentRegistry)
    {
        PauseOtherAi(agentRegistry);
        bool remountWasPending = pendingGuardRestore != null;
        TickPendingGuardRestore(agentRegistry);
        if (!remountWasPending &&
            pendingGuardRestore == null &&
            roles == null &&
            aiPauseStates.Count > 0)
        {
            RestoreAiPauseStates(agentRegistry);
            aiPauseStates.Clear();
        }
        TickGuard(agentRegistry);
        TickStriker(agentRegistry);
        TickEvidenceCamera(agentRegistry);
    }

    public void ApplyPostAgentTickGuardInput(
        INetworkAgentRegistry agentRegistry)
    {
        ApplyMountedGuardPresentationAction(agentRegistry);
    }

    public void SamplePreReplayDisplayedState(
        float dt,
        INetworkAgentRegistry agentRegistry)
    {
        if (roles == null ||
            !TryGetExactAgent(
                agentRegistry,
                roles.GuardAgentId,
                roles.GuardAuthority,
                out CoopAgentInfo info))
        {
            sample.ReplayEvidence.ClearPre();
            return;
        }

        ObservePreReplayDisplayedState(
            info.Agent,
            dt);
    }

    public void SamplePostReplayDisplayedState(INetworkAgentRegistry agentRegistry)
    {
        if (roles == null ||
            !TryGetExactAgent(
                agentRegistry,
                roles.GuardAgentId,
                roles.GuardAuthority,
                out CoopAgentInfo info))
        {
            sample.ReplayEvidence.ClearPre();
            return;
        }

        ObservePostReplayDisplayedState(info.Agent);
    }

    public void SampleFinalDisplayedState(float dt, INetworkAgentRegistry agentRegistry)
    {
        sampleElapsed += dt;
        if (roles == null)
            return;

        bool hasAgent = TryGetExactAgent(
            agentRegistry,
            roles.GuardAgentId,
            roles.GuardAuthority,
            out CoopAgentInfo info);
        if (hasAgent)
            ObserveFinalDisplayedState(info.Agent, dt);
        if (sampleElapsed < SampleIntervalSeconds)
            return;

        float elapsed = sampleElapsed;
        sampleElapsed = 0f;
        if (!hasAgent)
        {
            sample.MarkMissing(elapsed);
            return;
        }

        Sample(info.Agent, info.AgentId, elapsed);
    }

    public void ObserveScoreHit(
        Agent affectedAgent,
        Agent affectorAgent,
        bool isBlocked,
        in AttackCollisionData collisionData,
        float damagedHp)
    {
        if (strikerDriver == null ||
            !ReferenceEquals(affectorAgent, strikerDriver.Agent))
        {
            return;
        }

        strikerDriver.ObserveHit(
            affectedAgent,
            isBlocked,
            collisionData.CollisionResult,
            damagedHp);
        bool isExactGuard =
            guardDriver != null &&
            ReferenceEquals(affectedAgent, guardDriver.Agent);
        if (!ShouldCompleteStrikeFromScoreHit(
                isBlocked,
                isExactGuard))
        {
            return;
        }

        sample.PairBlockedHitCount++;
        sample.PairCollisionResult =
            collisionData.CollisionResult.ToString();
        sample.PairBlockedDamagedHp += damagedHp;
        strikerDriver.StopAfterBlockedHit();
    }

    public string GetState(
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator interpolator)
    {
        if (strikerDriver != null &&
            roles != null &&
            TryGetExactAgent(
                agentRegistry,
                strikerDriver.AgentId,
                roles.StrikerAuthority,
                out CoopAgentInfo strikerInfo))
        {
            strikerDriver.ObserveFixtureWeaponState(strikerInfo.Agent);
        }

        string guard = guardDriver == null
            ? "none"
            : $"{guardDriver.AgentId}:{guardDriver.Mode}:{guardDriver.Phase}:armed={guardDriver.GuardArmed}";
        string striker = strikerDriver == null
            ? "none"
            : $"{strikerDriver.AgentId}:state={strikerDriver.AttackState}:attempts={strikerDriver.AttackAttempts}";
        string restore = pendingGuardRestore == null
            ? "none"
            : $"{pendingGuardRestore.AgentId}:remount";
        float visiblePercent = sample.Samples == 0
            ? 0f
            : 100f * sample.VisibleSamples / sample.Samples;
        float plateauSpeed =
            guardDriver?.CalibratedPlateauSpeed ?? -1f;
        BattleGuardMountedRoute route =
            guardDriver?.MountedRoute;
        string strikeState =
            strikerDriver?.AttackState ?? "none";
        int strikeAttempts =
            strikerDriver?.AttackAttempts ?? 0;
        bool strikeWeaponReady =
            strikerDriver?.FixtureWeaponReady ?? false;
        bool strikeWeaponAvailable =
            strikerDriver?.FixtureWeaponAvailable ?? false;
        int strikeWieldRequests =
            strikerDriver?.FixtureWieldRequests ?? 0;
        int strikeOffHandSheathRequests =
            strikerDriver?.FixtureOffHandSheathRequests ?? 0;
        int strikeMainHand =
            (int)(strikerDriver?.FixtureMainHandIndex ??
                EquipmentIndex.None);
        int strikeOffHand =
            (int)(strikerDriver?.FixtureOffHandIndex ??
                EquipmentIndex.None);
        int strikeMainUsage =
            strikerDriver?.FixtureMainHandUsageIndex ?? -1;
        string strikeWeaponItem =
            strikerDriver?.FixtureMainHandItemId;
        string strikeExpectedWeapon =
            strikerDriver?.ExpectedWeaponId;
        int strikeExpectedUsage =
            strikerDriver?.ExpectedWeaponUsageIndex ?? -1;
        bool strikeSpeedReady =
            strikerDriver?.StrikeSpeedReady ?? false;
        bool strikeRunwayReady =
            strikerDriver?.StrikeRunwayReady ?? false;
        bool strikeTravelAligned =
            strikerDriver?.StrikeTravelAligned ?? false;
        float strikeTravelLookAlignment =
            strikerDriver?.StrikeTravelLookAlignment ?? -1f;
        float strikeReadySeconds =
            strikerDriver?.StrikeReadySeconds ?? 0f;
        string strikeStageRoute =
            strikerDriver?.StrikeStageRoute ?? "none";
        float strikeStageProgress =
            strikerDriver?.StrikeStageProgress ?? 0f;
        float strikeStageLateral =
            strikerDriver?.StrikeStageLateral ?? 0f;
        float strikeGuardLookAlignment =
            strikerDriver?.StrikeGuardLookAlignment ?? -1f;
        float strikeRouteAlignment =
            strikerDriver?.StrikeRouteAlignment ?? -1f;
        float strikeStandoff =
            strikerDriver?.StrikeStandoff ?? -1f;
        float strikeProfileReleaseLead =
            strikerDriver?.StrikeProfileReleaseLead ?? -1f;
        float strikeReleaseDistance =
            strikerDriver?.StrikeReleaseDistance ?? -1f;
        float strikeReleaseSpeed =
            strikerDriver?.StrikeReleaseSpeed ?? -1f;
        float strikeReleaseLead =
            strikerDriver?.StrikeReleaseLead ?? -1f;
        string strikeReleaseActionStage =
            strikerDriver?.StrikeReleaseActionStage ?? "none";
        string strikeReleaseActionDirection =
            strikerDriver?.StrikeReleaseActionDirection ?? "none";
        int strikeReleaseActionChannel =
            strikerDriver?.StrikeReleaseActionChannel ?? -1;
        float strikeReleaseActionProgress =
            strikerDriver?.StrikeReleaseActionProgress ?? -1f;
        bool strikeReleasedFromReady =
            strikerDriver?.StrikeReleasedFromReady ?? false;
        bool strikeReleaseObserved =
            strikerDriver?.StrikeReleaseObserved ?? false;
        int strikeGeometrySamples =
            strikerDriver?.StrikeGeometrySamples ?? 0;
        float strikeCurrentGuardLookAlignment =
            strikerDriver?.StrikeCurrentGuardLookAlignment ?? -1f;
        bool strikeReplicatedLookObserved =
            strikerDriver?.StrikeReplicatedLookObserved ?? false;
        float strikeReplicatedLookAlignment =
            strikerDriver?.StrikeReplicatedLookAlignment ?? -1f;
        long strikeStagedLookUpdateSequence =
            strikerDriver?.StrikeStagedLookUpdateSequence ?? 0;
        long strikeCurrentLookUpdateSequence =
            strikerDriver?.StrikeCurrentLookUpdateSequence ?? 0;
        float strikeCurrentStandoff =
            strikerDriver?.StrikeCurrentStandoff ?? -1f;
        float strikeClosestStandoff =
            strikerDriver?.StrikeClosestStandoff ?? -1f;
        int strikeAttemptHitCount =
            strikerDriver?.StrikeAttemptHitCount ?? 0;
        int strikeLastHitAttempt =
            strikerDriver?.StrikeLastHitAttempt ?? 0;
        float strikeLastHitGuardLookAlignment =
            strikerDriver?.StrikeLastHitGuardLookAlignment ?? -1f;
        float strikeLastHitStandoff =
            strikerDriver?.StrikeLastHitStandoff ?? -1f;
        int strikeFirstGuardHitAttempt =
            strikerDriver?.StrikeFirstGuardHitAttempt ?? 0;
        float strikeFirstGuardHitAlignment =
            strikerDriver?.StrikeFirstGuardHitAlignment ?? -1f;
        float strikeFirstGuardHitStandoff =
            strikerDriver?.StrikeFirstGuardHitStandoff ?? -1f;
        bool strikeFirstGuardHitBlocked =
            strikerDriver?.StrikeFirstGuardHitBlocked ?? false;
        float strikeFirstGuardHitDamagedHp =
            strikerDriver?.StrikeFirstGuardHitDamagedHp ?? 0f;
        int strikeHitCount =
            strikerDriver?.HitCount ?? 0;
        string strikeLastTarget =
            strikerDriver?.LastHitTarget ?? "none";
        string strikeLastCollision =
            strikerDriver?.LastHitCollision ?? "none";
        bool strikeLastBlocked =
            strikerDriver?.LastHitBlocked ?? false;
        float strikeLastDamagedHp =
            strikerDriver?.LastHitDamagedHp ?? 0f;
        float strikeOriginalSwingSpeed =
            strikerDriver?.OriginalSwingSpeedMultiplier ?? -1f;
        float strikeSwingSpeed =
            strikerDriver?.CurrentSwingSpeedMultiplier ?? -1f;
        int targetRiderMovementFlags = -1;
        int targetMountMovementFlags = -1;
        if (roles != null &&
            TryGetExactAgent(
                agentRegistry,
                roles.GuardAgentId,
                roles.GuardAuthority,
                out CoopAgentInfo targetInfo) &&
            interpolator != null &&
            interpolator.TryGetTargetMovementFlags(
                targetInfo.Agent,
                out uint targetRiderFlags,
                out uint targetMountFlags))
        {
            targetRiderMovementFlags = (int)targetRiderFlags;
            targetMountMovementFlags = (int)targetMountFlags;
        }
        return $"fixtureGuard={guard} fixtureStriker={striker} fixtureRestore={restore} " +
            $"trackedAgent={sample.AgentId} " +
            $"samples={sample.Samples} missing={sample.MissingSamples} visiblePct={visiblePercent:0.#} " +
            $"maxMissingGap={sample.MaxMissingGapSeconds:0.###} mounted={sample.Mounted} " +
            $"speed={sample.HorizontalSpeed:0.###} peakSpeed={sample.PeakHorizontalSpeed:0.###} " +
            $"medianSpeed={sample.GetMedianSpeed():0.###} health={sample.Health:0.###} " +
            $"riderMoveFlags={sample.RiderMovementFlags} " +
            $"mountMoveFlags={sample.MountMovementFlags} " +
            $"nativeDefendFlags={sample.NativeDefendMovementFlags} " +
            $"targetRiderMoveFlags={targetRiderMovementFlags} " +
            $"targetMountMoveFlags={targetMountMovementFlags} " +
            $"appliedRiderMoveFlags={guardDriver?.AppliedRiderMovementFlags ?? 0} " +
            $"appliedNativeDefendFlags={guardDriver?.AppliedNativeDefendMovementFlags ?? 0} " +
            $"riderBodyYaw={sample.RiderBodyYaw:0.###} " +
            $"riderLookYaw={sample.RiderLookYaw:0.###} " +
            $"riderMoveYaw={sample.RiderMovementYaw:0.###} " +
            $"mountBodyYaw={sample.MountBodyYaw:0.###} " +
            $"mountLookYaw={sample.MountLookYaw:0.###} " +
            $"plateauReady={plateauSpeed >= 0f} plateauSpeed={plateauSpeed:0.###} " +
            $"recentSpeed={sample.SpeedEvidence.RecentMedian:0.###} " +
            $"recentSpeedSamples={sample.SpeedEvidence.RecentSamples} " +
            $"recentSpeedSpread={sample.SpeedEvidence.RecentSpread:0.###} " +
            $"recentSpeedSlope={sample.SpeedEvidence.RecentSlope:0.###} " +
            $"guardMainHand={sample.MainHandIndex} " +
            $"guardOffHand={sample.OffHandIndex} " +
            $"guardMainUsage={sample.MainHandUsageIndex} " +
            $"guardMainItem={GetTokenValue(sample.MainHandItemId)} " +
            $"guardEquipmentReady={sample.GuardEquipmentReady} " +
            $"routeState={route?.State ?? "none"} " +
            $"routeProgress={route?.Progress ?? 0f:0.###} " +
            $"routeLateral={route?.LateralOffset ?? 0f:0.###} " +
            $"routeRemaining={route?.RemainingDistance ?? 0f:0.###} " +
            $"routeTurns={route?.CompletedTurns ?? 0} " +
            $"routeStrikeReady={route?.CanStageStrike == true} " +
            $"healthDelta={sample.HealthDelta:0.###} rawAction={sample.RawActionIndex} " +
            $"rawActionName={GetTokenValue(sample.RawActionName)} " +
            $"rawActionType={sample.RawActionType} " +
            $"rawActionDirection={sample.RawActionDirection} " +
            $"action0Direction={sample.Action0Direction} " +
            $"action1Direction={sample.Action1Direction} " +
            $"appliedAction0Direction={guardDriver?.AppliedAction0Direction ?? "None"} " +
            $"appliedAction1Direction={guardDriver?.AppliedAction1Direction ?? "None"} " +
            $"rawProgress={sample.RawProgress:0.###} guardChannel={sample.LatchedChannel} " +
            $"guardDirection={guardDriver?.Direction.ToString() ?? "none"} " +
            $"guardMode={sample.GuardMode} " +
            $"guardStateChanges={guardDriver?.MountedGuardStateChanges ?? 0} " +
            $"guardPresentationPending={guardDriver?.MountedPresentationActionPending == true} " +
            $"guardPresentationAttempts={guardDriver?.MountedPresentationAttempts ?? 0} " +
            $"guardPresentationApplied={guardDriver?.MountedPresentationApplied == true} " +
            $"guardPresentationRequestedAction={guardDriver?.MountedPresentationRequestedActionIndex ?? -1} " +
            $"guardPresentationImmediateAction={guardDriver?.MountedPresentationImmediateActionIndex ?? -1} " +
            $"guardPresentationStartProgress={guardDriver?.MountedPresentationStartProgress ?? -1f:0.###} " +
            $"guardPresentationImmediateMode={guardDriver?.MountedPresentationImmediateGuardMode ?? "None"} " +
            $"guardAction={sample.LatchedActionIndex} " +
            $"guardAnimation={sample.LatchedAnimationIndex} " +
            $"visualAction={sample.VisualActionIndex} visualAnimation={sample.VisualAnimationIndex} " +
            $"visualProgress={sample.VisualProgress:0.###} visible={sample.GuardVisible} " +
            $"guardExactPct={sample.GuardContinuityEvidence.ExactPercent:0.#} " +
            $"guardInterruptions={sample.GuardContinuityEvidence.Interruptions} " +
            $"guardMaxExactRun={sample.GuardContinuityEvidence.MaxExactRunSeconds:0.###} " +
            $"reaction={sample.Reaction} reactionSamples={sample.ReactionSamples} " +
            $"reactionAction={sample.ReactionActionIndex} " +
            $"reactionAnimation={sample.ReactionAnimationIndex} " +
            $"reactionReceivedActive={sample.ReceivedReactionActive} " +
            $"reactionReceivedProgress={sample.ReceivedReactionProgress:0.###} " +
            $"reactionReceivedCyclic={sample.ReceivedReactionCyclic} " +
            $"reactionActive={sample.ReactionEvidence.Active} " +
            $"reactionCompleted={sample.ReactionEvidence.Completed} " +
            $"reactionInterrupted={sample.ReactionEvidence.Interrupted} " +
            $"reactionChannel={sample.ReactionEvidence.Channel} " +
            $"reactionOnsetSpeed={sample.ReactionEvidence.OnsetSpeed:0.###} " +
            $"reactionVisualDuration={sample.ReactionEvidence.VisualDurationSeconds:0.###} " +
            $"reactionMaxProgress={sample.ReactionEvidence.MaxVisualProgress:0.###} " +
            $"pairBlockedHitCount={sample.PairBlockedHitCount} " +
            $"pairCollisionResult={sample.PairCollisionResult} " +
            $"pairBlockedDamagedHp={sample.PairBlockedDamagedHp:0.###} " +
            $"pairBlockedZeroDamage={sample.PairBlockedHitCount > 0 && Math.Abs(sample.PairBlockedDamagedHp) <= ProgressEpsilon} " +
            $"visualAnimations={sample.GetVisualAnimations()} visualRuns={sample.GetVisualRuns()} " +
            $"visualProgressAdvances={sample.VisualProgressAdvances} " +
            $"visualProgressStalls={sample.VisualProgressStalls} " +
            $"visualProgressResets={sample.VisualProgressResets} " +
            $"positionVisualDelta={sample.VisualRootEvidence.PositionVisualDelta:0.###} " +
            $"maxPositionVisualDelta={sample.VisualRootEvidence.MaxPositionVisualDelta:0.###} " +
            $"maxVisualRootStep={sample.VisualRootEvidence.MaxVisualRootStep:0.###} " +
            $"maxVisualRootStepRate={sample.VisualRootEvidence.MaxVisualRootStepRate:0.###} " +
            $"replayPairedFrames={sample.ReplayEvidence.PairedFrames} " +
            $"replayAnimationChanges={sample.ReplayEvidence.AnimationChanges} " +
            $"replayProgressRewinds={sample.ReplayEvidence.ProgressRewinds} " +
            $"replayMaxProgressDelta={sample.ReplayEvidence.MaxProgressDelta:0.###} " +
            $"replayMaxSpeedDelta={sample.ReplayEvidence.MaxSpeedDelta:0.###} " +
            $"strikeState={strikeState} strikeAttempts={strikeAttempts} " +
            $"strikeWeaponReady={strikeWeaponReady} " +
            $"strikeWeaponAvailable={strikeWeaponAvailable} " +
            $"strikeWieldRequests={strikeWieldRequests} " +
            $"strikeOffHandSheathRequests={strikeOffHandSheathRequests} " +
            $"strikeMainHand={strikeMainHand} " +
            $"strikeOffHand={strikeOffHand} " +
            $"strikeMainUsage={strikeMainUsage} " +
            $"strikeWeaponItem={GetTokenValue(strikeWeaponItem)} " +
            $"strikeExpectedWeapon={GetTokenValue(strikeExpectedWeapon)} " +
            $"strikeExpectedUsage={strikeExpectedUsage} " +
            $"strikeSpeedReady={strikeSpeedReady} " +
            $"strikeRunwayReady={strikeRunwayReady} " +
            $"strikeTravelAligned={strikeTravelAligned} " +
            $"strikeTravelLookAlignment={strikeTravelLookAlignment:0.###} " +
            $"strikeReadySeconds={strikeReadySeconds:0.###} " +
            $"strikeStageRoute={strikeStageRoute} " +
            $"strikeStageProgress={strikeStageProgress:0.###} " +
            $"strikeStageLateral={strikeStageLateral:0.###} " +
            $"strikeGuardLookAlignment={strikeGuardLookAlignment:0.###} " +
            $"strikeRouteAlignment={strikeRouteAlignment:0.###} " +
            $"strikeStandoff={strikeStandoff:0.###} " +
            $"strikeProfileReleaseLead={strikeProfileReleaseLead:0.###} " +
            $"strikeReleaseDistance={strikeReleaseDistance:0.###} " +
            $"strikeReleaseSpeed={strikeReleaseSpeed:0.###} " +
            $"strikeReleaseLead={strikeReleaseLead:0.###} " +
            $"strikeReleaseActionStage={strikeReleaseActionStage} " +
            $"strikeReleaseActionDirection={strikeReleaseActionDirection} " +
            $"strikeReleaseActionChannel={strikeReleaseActionChannel} " +
            $"strikeReleaseActionProgress={strikeReleaseActionProgress:0.###} " +
            $"strikeReleasedFromReady={strikeReleasedFromReady} " +
              $"strikeReleaseObserved={strikeReleaseObserved} " +
              $"strikeGeometrySamples={strikeGeometrySamples} " +
              $"strikeCurrentGuardLookAlignment={strikeCurrentGuardLookAlignment:0.###} " +
              $"strikeReplicatedLookObserved={strikeReplicatedLookObserved} " +
              $"strikeReplicatedLookAlignment={strikeReplicatedLookAlignment:0.###} " +
              $"strikeStagedLookSequence={strikeStagedLookUpdateSequence} " +
              $"strikeCurrentLookSequence={strikeCurrentLookUpdateSequence} " +
              $"strikeCurrentStandoff={strikeCurrentStandoff:0.###} " +
            $"strikeClosestStandoff={strikeClosestStandoff:0.###} " +
            $"strikeAttemptHitCount={strikeAttemptHitCount} " +
            $"strikeLastHitAttempt={strikeLastHitAttempt} " +
            $"strikeLastHitGuardLookAlignment={strikeLastHitGuardLookAlignment:0.###} " +
            $"strikeLastHitStandoff={strikeLastHitStandoff:0.###} " +
            $"strikeFirstGuardHitAttempt={strikeFirstGuardHitAttempt} " +
            $"strikeFirstGuardHitAlignment={strikeFirstGuardHitAlignment:0.###} " +
            $"strikeFirstGuardHitStandoff={strikeFirstGuardHitStandoff:0.###} " +
            $"strikeFirstGuardHitBlocked={strikeFirstGuardHitBlocked} " +
            $"strikeFirstGuardHitDamagedHp={strikeFirstGuardHitDamagedHp:0.###} " +
            $"strikeHitCount={strikeHitCount} " +
            $"strikeLastTarget={GetTokenValue(strikeLastTarget)} " +
            $"strikeLastCollision={strikeLastCollision} " +
            $"strikeLastBlocked={strikeLastBlocked} " +
            $"strikeLastDamagedHp={strikeLastDamagedHp:0.###} " +
            $"strikeOriginalSwingSpeed={strikeOriginalSwingSpeed:0.###} " +
            $"strikeSwingSpeed={strikeSwingSpeed:0.###} " +
            $"evidenceCamera={GetEvidenceCameraToken()} " +
            "visualTraceSchema=c:a:n:r:mr:d:md:pmin:pmax:span:adv:stall:reset:maxStep:sCur:sMin:sMax:sMean " +
            $"preVisualTraces={sample.PreReplayAnimationEvidence.GetToken()} " +
            $"visualTraces={sample.AnimationEvidence.GetToken()} " +
            $"error={GetTokenValue(lastError)}";
    }

    public string GetCandidates(INetworkAgentRegistry agentRegistry, List<string> args)
    {
        if (agentRegistry == null)
            return "No agent registry";

        var candidates = new List<string>();
        if (args == null || args.Count == 0 || string.Equals(args[0], "main", StringComparison.OrdinalIgnoreCase))
        {
            if (args != null && args.Count > 1)
                return "Usage: coop.debug.battle.guard_fixture_candidates main|enemy guard-agent-id";

            foreach (string authority in agentRegistry.GetControllerIds())
            {
                foreach (CoopAgentInfo info in agentRegistry.GetAgents(authority))
                {
                    if (ReferenceEquals(info?.Agent, Agent.Main))
                        candidates.Add(DescribeCandidate(info));
                }
            }
        }
        else if (string.Equals(args[0], "enemy", StringComparison.OrdinalIgnoreCase) &&
                 args.Count == 2 &&
                 Guid.TryParse(args[1], out Guid guardAgentId) &&
                 agentRegistry.TryGetAgentInfo(guardAgentId, out CoopAgentInfo guardInfo) &&
                 guardInfo?.Agent != null)
        {
            foreach (string authority in agentRegistry.GetControllerIds())
            {
                foreach (CoopAgentInfo info in agentRegistry.GetAgents(authority))
                {
                    Agent agent = info?.Agent;
                    if (agent != null &&
                        agent.IsActive() &&
                        agent.IsHuman &&
                        !agent.HasMount &&
                        agent.Team?.IsEnemyOf(guardInfo.Agent.Team) == true)
                    {
                        candidates.Add(DescribeCandidate(info));
                    }
                }
            }
        }
        else
        {
            return "Usage: coop.debug.battle.guard_fixture_candidates main|enemy guard-agent-id";
        }

        candidates.Sort(StringComparer.Ordinal);
        return candidates.Count == 0
            ? "No matching registered battle agents"
            : string.Join(Environment.NewLine, candidates);
    }

    private static string DescribeCandidate(CoopAgentInfo info)
    {
        Agent agent = info.Agent;
        return
            $"id={info.AgentId} authority={info.CurrentAuthority} originalOwner={info.OriginalOwner} " +
            $"controller={agent.Controller} main={ReferenceEquals(agent, Agent.Main)} " +
            $"mounted={agent.HasMount} team={agent.Team?.TeamIndex.ToString() ?? "none"} " +
            $"side={agent.Team?.Side.ToString() ?? "none"} human={agent.IsHuman} " +
            $"ai={agent.IsAIControlled} active={agent.IsActive()}";
    }

    private static string GetTokenValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "none";

        return value
            .Replace(' ', '_')
            .Replace('\t', '_')
            .Replace('\r', '_')
            .Replace('\n', '_');
    }

    public void Reset(INetworkAgentRegistry agentRegistry)
    {
        ReleaseEvidenceCamera();
        RestoreGuard(agentRegistry);
        RestoreStriker(agentRegistry);
        if (pendingGuardRestore == null)
        {
            RestoreAiPauseStates(agentRegistry);
            aiPauseStates.Clear();
        }
        guardDriver = null;
        strikerDriver = null;
        roles = null;
        pendingMountedRoute = null;
        pendingMountedStrike = null;
        currentBattleInstanceId = null;
        currentCommandId = Guid.Empty;
        sampleElapsed = 0f;
        sample = new SampleState();
        lastError = pendingGuardRestore == null
            ? null
            : "guard remount is still pending";
    }

    private string GetEvidenceCameraToken()
    {
        if (roles == null)
            return "none";
        if (controllerIdProvider.ControllerId == roles.GuardAuthority)
            return "owner";
        if (evidenceCamera != null &&
            ReferenceEquals(evidenceCameraScreen?.CustomCamera, evidenceCamera) &&
            evidenceCameraTargetId == roles.GuardAgentId)
        {
            return evidenceCameraTargetId.ToString();
        }
        return "pending";
    }

    private void TickEvidenceCamera(INetworkAgentRegistry agentRegistry)
    {
        if (!ModInformation.IsClient ||
            roles == null ||
            controllerIdProvider.ControllerId == roles.GuardAuthority)
        {
            ReleaseEvidenceCamera();
            return;
        }
        if (!TryGetExactAgent(
                agentRegistry,
                roles.GuardAgentId,
            roles.GuardAuthority,
            out CoopAgentInfo guardInfo) ||
            !IsActiveMissionAgent(guardInfo.Agent) ||
            guardInfo.Agent.AgentVisuals == null ||
            !guardInfo.Agent.AgentVisuals.IsValid())
        {
            ReleaseEvidenceCamera();
            return;
        }
        if (!(ScreenManager.TopScreen is MissionScreen screen) ||
            screen.Mission != Mission.Current ||
            screen.CombatCamera == null)
        {
            ReleaseEvidenceCamera();
            return;
        }
        if (evidenceCamera != null &&
            (!ReferenceEquals(evidenceCameraScreen, screen) ||
             !ReferenceEquals(screen.CustomCamera, evidenceCamera)))
        {
            ReleaseEvidenceCamera();
            lastError = "observer evidence camera was replaced";
            return;
        }

        try
        {
            if (evidenceCamera == null)
                CreateEvidenceCamera(screen);

            MatrixFrame frame =
                CreateEvidenceCameraFrame(guardInfo.Agent);
            evidenceCamera.Entity.SetGlobalFrame(in frame, true);
            evidenceCameraTargetId = roles.GuardAgentId;
        }
        catch (Exception exception)
        {
            ReleaseEvidenceCamera();
            lastError =
                $"observer evidence camera failed: {exception.GetType().Name}";
        }
    }

    private void CreateEvidenceCamera(MissionScreen screen)
    {
        evidenceCameraScreen = screen;
        previousCustomCamera = screen.CustomCamera;
        previousCombatCameraFrame = screen.CombatCamera.Frame;
        previousAllowInputWithCustomCamera =
            screen.AllowInputWithCustomCamera;

        Camera camera = Camera.CreateCamera();
        evidenceCamera = camera;
        camera.SetFovVertical(
            MathF.PI / 3f,
            TaleWorlds.Engine.Screen.AspectRatio,
            0.1f,
            12500f);
        camera.Entity = GameEntity.CreateEmpty(
            Mission.Current.Scene,
            false,
            false,
            false);
        screen.CustomCamera = camera;
        screen.AllowInputWithCustomCamera = false;
    }

    private static MatrixFrame CreateEvidenceCameraFrame(Agent guard)
    {
        MatrixFrame guardFrame =
            guard.AgentVisuals.GetGlobalFrame();
        Vec3 lower = guard.HasMount &&
                     guard.MountAgent?.AgentVisuals != null &&
                     guard.MountAgent.AgentVisuals.IsValid()
            ? guard.MountAgent.AgentVisuals.GetGlobalFrame().origin
            : guardFrame.origin;
        Vec3 upper =
            guard.AgentVisuals.GetGlobalStableEyePoint(true);
        Vec3 target = (lower + upper) * 0.5f;

        Vec3 forward = guard.LookDirection;
        forward.z = 0f;
        if (forward.LengthSquared < ProgressEpsilon)
            forward = new Vec3(1f, 0f, 0f);
        else
            forward.Normalize();
        Vec3 side = Vec3.CrossProduct(forward, Vec3.Up);
        side.Normalize();

        // Keep the fixed observer above battlefield vegetation without hiding the pose.
        float distance = guard.HasMount ? 7f : 4.5f;
        float height = guard.HasMount ? 4f : 3f;
        Vec3 origin = target +
            (side * distance) -
            (forward * (distance * 0.25f)) +
            (Vec3.Up * height);
        Vec3 direction = target - origin;
        direction.Normalize();

        MatrixFrame frame = MatrixFrame.Identity;
        frame.rotation.s =
            Vec3.CrossProduct(direction, Vec3.Up);
        frame.rotation.s.Normalize();
        frame.rotation.f =
            Vec3.CrossProduct(frame.rotation.s, direction);
        frame.rotation.f.Normalize();
        frame.rotation.u = -direction;
        frame.origin = origin;
        return frame;
    }

    private void ReleaseEvidenceCamera()
    {
        Camera camera = evidenceCamera;
        MissionScreen screen = evidenceCameraScreen;
        if (screen != null &&
            ReferenceEquals(screen.CustomCamera, camera))
        {
            if (previousCustomCamera == null &&
                screen.CombatCamera != null)
            {
                screen.UpdateFreeCamera(
                    previousCombatCameraFrame);
            }
            screen.CustomCamera = previousCustomCamera;
            screen.AllowInputWithCustomCamera =
                previousAllowInputWithCustomCamera;
        }

        camera?.ReleaseCameraEntity();
        evidenceCameraScreen = null;
        evidenceCamera = null;
        previousCustomCamera = null;
        previousCombatCameraFrame = MatrixFrame.Identity;
        previousAllowInputWithCustomCamera = false;
        evidenceCameraTargetId = Guid.Empty;
    }

    private void ApplyGuard(
        NetworkBattleGuardFixtureCommand command,
        Agent agent,
        bool drivesGuard)
    {
        if (guardDriver == null)
            guardDriver = new GuardDriver(
                command.GuardAgentId,
                command.Mode,
                command.Phase,
                command.Direction,
                command.UseMovementFlagGuardInput,
                agent);
        else
        {
            guardDriver.Mode = command.Mode;
            guardDriver.Phase = command.Phase;
            guardDriver.Direction = command.Direction;
            guardDriver.UseMovementFlagGuardInput =
                command.UseMovementFlagGuardInput;
        }
        if (command.Phase != BattleGuardFixturePhase.Attack)
            guardDriver.EndMountedStrike();

        if (!guardDriver.EquipmentReplaced &&
            !EquipFixtureWeapon(agent, GuardWeaponId, out string error))
        {
            lastError = error;
            return;
        }
        guardDriver.EquipmentReplaced = true;

        if (command.Mode == BattleGuardFixtureMode.Mounted &&
            !drivesGuard)
        {
            TryApplyPendingMountedRoute(command);
        }
        if (command.Mode == BattleGuardFixtureMode.Mounted &&
            guardDriver.MountedRoute == null)
        {
            if (drivesGuard)
            {
                if (!TryPrepareMountedRoute(agent, guardDriver))
                {
                    lastError =
                        "no clear mounted guard fixture lane was found";
                    return;
                }
            }
            else
            {
                lastError = WaitingForMountedGuardRouteError;
                return;
            }
        }
        if (command.Mode == BattleGuardFixtureMode.Mounted && drivesGuard)
            SendMountedRoute(command, guardDriver.MountedRoute);
        if (!drivesGuard)
            return;

        guardDriver.BeginDriving();
        if (command.Mode == BattleGuardFixtureMode.Mounted)
            guardDriver.MountedSpeedLimiter.Apply(agent.MountAgent);
        if (command.Phase == BattleGuardFixturePhase.Calibration)
        {
            guardDriver.GuardArmed = false;
        }
        if ((command.Phase == BattleGuardFixturePhase.Guard ||
             command.Phase == BattleGuardFixturePhase.Attack) &&
            !guardDriver.GuardArmed)
        {
            guardDriver.GuardArmed = true;
        }
    }

    private void ApplyStriker(
        Agent agent,
        Agent guard,
        bool drivesStriker,
        IAgentPositionInterpolator interpolator)
    {
        string weaponId =
            GetFixtureStrikerWeaponId(guardDriver.Mode);
        if (strikerDriver == null)
        {
            strikerDriver = new StrikerDriver(
                roles.StrikerAgentId,
                agent,
                guardDriver.Mode);
        }

        if (!strikerDriver.EquipmentReplaced &&
            !EquipFixtureWeapon(agent, weaponId, out string error))
        {
            lastError = error;
            return;
        }
        strikerDriver.EquipmentReplaced = true;
        if (!drivesStriker)
            return;

        SetControllerDirect(agent, AgentControllerType.AI);
        agent.SetTargetAgent(guard);
        agent.SetWatchState(Agent.WatchState.Alarmed);
        agent.SetIsAIPaused(false);
        strikerDriver.ApplyFixtureSwingSpeed(
            agent,
            FixtureStrikerSwingSpeedMultiplier);
        strikerDriver.AttachAttackDriver(
            agent,
            guard,
            guardDriver,
            interpolator,
            SendMountedStrike,
            SendMountedStrikeEnd);
        ClearDefendFlags(agent, strikerDriver.OriginalMovementFlags);
        strikerDriver.EnsureFixtureWeaponReady(
            agent,
            strikerDriver.AttackAttempts,
            retryDue: false);
        AgentAiWaker.Wake(agent);
    }

    private void TickGuard(INetworkAgentRegistry agentRegistry)
    {
        if (!TryGetDrivenGuardAgent(agentRegistry, out Agent agent))
            return;

        if (guardDriver.Mode == BattleGuardFixtureMode.Foot && agent.HasMount)
            return;
        if (guardDriver.Mode == BattleGuardFixtureMode.Mounted && !agent.HasMount)
        {
            if (guardDriver.OriginalMount != null &&
                guardDriver.OriginalMount.IsActive() &&
                guardDriver.OriginalMount.RiderAgent == null)
            {
                agent.Mount(guardDriver.OriginalMount);
            }
            return;
        }
        if (guardDriver.Mode == BattleGuardFixtureMode.Mounted)
            guardDriver.MountedSpeedLimiter.Apply(agent.MountAgent);
        if (!guardDriver.Positioned)
        {
            if (!TryPositionGuard(agent, guardDriver))
            {
                lastError =
                    "no clear mounted guard fixture lane was found";
                return;
            }
            guardDriver.Positioned = true;
        }
    }

    private bool TryGetDrivenGuardAgent(
        INetworkAgentRegistry agentRegistry,
        out Agent agent)
    {
        agent = null;
        if (guardDriver?.DrivesAgent != true)
            return false;

        if (controllerIdProvider.ControllerId != roles?.GuardAuthority ||
            !TryGetExactAgent(
                agentRegistry,
                guardDriver.AgentId,
                roles.GuardAuthority,
                out CoopAgentInfo info) ||
            !agentRegistry.IsLocallyControlled(guardDriver.AgentId))
        {
            DetachMigratedGuardDriver(agentRegistry);
            return false;
        }

        agent = info.Agent;
        return true;
    }

    private static void DriveGuardInput(
        Agent agent,
        GuardDriver driver)
    {
        bool dismounting =
            driver.Mode == BattleGuardFixtureMode.Foot &&
            agent.HasMount;
        bool moving =
            !dismounting &&
            driver.Mode == BattleGuardFixtureMode.Mounted;
        bool guarding = IsGuarding(agent, driver);
        Agent.MovementControlFlag defendFlags =
            GetDefendFlags(driver.Direction);

        agent.EventControlFlags &= ~Agent.EventControlFlag.Dismount;
        Agent.MovementControlFlag flags =
            agent.MovementFlags & ~DriveFlags;
        Vec2 movementInput = Vec2.Zero;
        Vec3 targetLookDirection = Vec3.Zero;
        if (moving)
        {
            if (driver.MountedRoute != null)
            {
                Agent mount = agent.MountAgent;
                Agent movementSource = mount ?? agent;
                BattleGuardMountedRouteInput routeInput;
                if (driver.TryGetMountedStrikeDirections(
                        agent,
                        out Vec3 strikeTravelDirection,
                        out Vec3 strikeLookDirection))
                {
                    routeInput =
                        BattleGuardMountedRoute.CreateStraightInput(
                            movementSource.GetMovementDirection(),
                            movementSource.LookDirection,
                            movementSource
                                .GetRealGlobalVelocity()
                                .AsVec2
                                .Length,
                            strikeTravelDirection,
                            strikeLookDirection);
                }
                else
                {
                    routeInput = driver.MountedRoute.Update(
                        movementSource.Position,
                        movementSource.GetMovementDirection(),
                        movementSource.LookDirection,
                        movementSource.Frame.rotation.f,
                        movementSource.GetRealGlobalVelocity().AsVec2.Length);
                }
                flags |=
                    routeInput.TranslationFlag |
                    routeInput.TurnFlag;
                movementInput = routeInput.Movement;
                if (routeInput.LookDirection.LengthSquared >= 0.0001f)
                    targetLookDirection = routeInput.LookDirection;
            }
            else
            {
                flags |= Agent.MovementControlFlag.Forward;
                movementInput = new Vec2(0f, 1f);
            }
        }
        if (guarding)
            flags |= defendFlags;
        bool mountedGuardDirectionChanged =
            driver.Mode == BattleGuardFixtureMode.Mounted &&
            ShouldResetMountedGuardDirection(
                guarding,
                driver.MountedGuardCommandActive,
                driver.Direction,
                driver.MountedGuardCommandDirection);
        if (ShouldApplyExplicitMountedGuardInput(
                driver.Mode,
                driver.UseMovementFlagGuardInput) &&
            ShouldCommandMountedGuardState(
                guarding,
                driver.MountedGuardCommandActive,
                driver.Direction,
                driver.MountedGuardCommandDirection))
        {
            Agent.GuardMode guardMode = guarding
                ? GetGuardMode(driver.Direction)
                : Agent.GuardMode.None;
            if (mountedGuardDirectionChanged)
            {
                AgentActionData.ApplyGuardDirectionTransition(
                    agent,
                    guardMode);
            }
            else
            {
                AgentActionData.ApplyGuardState(
                    agent,
                    guardMode,
                    force: guarding);
            }
            driver.MountedGuardCommandActive = guarding;
            driver.MountedGuardCommandDirection = driver.Direction;
            driver.MountedPostNativeGuardCommandPending = true;
            driver.MountedPostNativeDirectionChanged =
                mountedGuardDirectionChanged;
            driver.MountedGuardStateChanges++;
        }
        agent.MovementFlags = flags;
        agent.MovementInputVector = movementInput;
        AgentActionData.ApplyDefendMovementFlags(
            agent,
            guarding
                ? defendFlags
                : Agent.MovementControlFlag.None);
        if (targetLookDirection.LengthSquared >= 0.0001f)
            agent.LookDirection = targetLookDirection;
        if (dismounting)
            agent.EventControlFlags |= Agent.EventControlFlag.Dismount;
        driver.ObserveAppliedInput(agent);
    }

    private static bool IsGuarding(Agent agent, GuardDriver driver)
    {
        return !(driver.Mode == BattleGuardFixtureMode.Foot &&
                 agent.HasMount) &&
            driver.Phase != BattleGuardFixturePhase.Calibration;
    }

    private static bool ApplyOwnedMountedStrikeLook(
        Agent agent,
        GuardDriver driver)
    {
        if (!ShouldApplyOwnedMountedStrikeLook(
                driver.DrivesAgent,
                driver.Mode,
                driver.Phase) ||
            !driver.TryGetMountedStrikeDirections(
                agent,
                out _,
                out Vec3 lookDirection))
        {
            return false;
        }

        agent.LookDirection = lookDirection;
        return true;
    }

    internal static Agent.MovementControlFlag GetDefendFlags(
        BattleGuardFixtureDirection direction)
    {
        Agent.MovementControlFlag directionFlag = direction switch
        {
            BattleGuardFixtureDirection.Down =>
                Agent.MovementControlFlag.DefendDown,
            BattleGuardFixtureDirection.Left =>
                Agent.MovementControlFlag.DefendLeft,
            BattleGuardFixtureDirection.Right =>
                Agent.MovementControlFlag.DefendRight,
            _ => Agent.MovementControlFlag.DefendUp
        };
        return Agent.MovementControlFlag.DefendBlock | directionFlag;
    }

    internal static Agent.MovementControlFlag GetAttackFlagForGuard(
        BattleGuardFixtureDirection direction) =>
        direction switch
        {
            BattleGuardFixtureDirection.Down =>
                Agent.MovementControlFlag.AttackDown,
            BattleGuardFixtureDirection.Left =>
                Agent.MovementControlFlag.AttackRight,
            BattleGuardFixtureDirection.Right =>
                Agent.MovementControlFlag.AttackLeft,
            _ => Agent.MovementControlFlag.AttackUp
        };

    internal static Agent.GuardMode GetGuardMode(
        BattleGuardFixtureDirection direction) =>
        direction switch
        {
            BattleGuardFixtureDirection.Down => Agent.GuardMode.Down,
            BattleGuardFixtureDirection.Left => Agent.GuardMode.Left,
            BattleGuardFixtureDirection.Right => Agent.GuardMode.Right,
            _ => Agent.GuardMode.Up
        };

    internal static bool ShouldLatchGuardPresentation(
        BattleGuardFixturePhase phase,
        BattleGuardFixtureDirection expectedDirection,
        Agent.GuardMode observedGuardMode)
    {
        return phase != BattleGuardFixturePhase.Guard ||
            observedGuardMode == GetGuardMode(expectedDirection);
    }

    internal static string GetMountedGuardPresentationActionName(
        BattleGuardFixtureDirection direction) =>
        direction switch
        {
            BattleGuardFixtureDirection.Left => MountedLeftGuardAction,
            BattleGuardFixtureDirection.Right => MountedRightGuardAction,
            _ => null
        };

    internal static bool ShouldQueueMountedGuardPresentation(
        bool guarding,
        BattleGuardFixtureDirection direction)
    {
        return guarding &&
            GetMountedGuardPresentationActionName(direction) != null;
    }

    internal static bool ShouldApplyExplicitMountedGuardInput(
        BattleGuardFixtureMode mode,
        bool useMovementFlagGuardInput)
    {
        return mode == BattleGuardFixtureMode.Mounted &&
            !useMovementFlagGuardInput;
    }

    internal static bool ShouldUseNativePlayerGuardInput(
        BattleGuardFixtureMode mode,
        bool useMovementFlagGuardInput)
    {
        return mode == BattleGuardFixtureMode.Mounted &&
            useMovementFlagGuardInput;
    }

    private static InputKey GetNativePlayerGuardDirectionKey(
        BattleGuardFixtureDirection direction) =>
        direction switch
        {
            BattleGuardFixtureDirection.Down =>
                InputKey.ControllerRStickDown,
            BattleGuardFixtureDirection.Left =>
                InputKey.ControllerRStickLeft,
            BattleGuardFixtureDirection.Right =>
                InputKey.ControllerRStickRight,
            _ => InputKey.ControllerRStickUp
        };

    internal static bool ShouldMaintainMountedGuardPresentation(
        BattleGuardFixtureMode mode,
        BattleGuardFixturePhase phase,
        BattleGuardFixtureDirection direction,
        bool reactionActive)
    {
        return mode == BattleGuardFixtureMode.Mounted &&
            (phase == BattleGuardFixturePhase.Guard ||
             (phase == BattleGuardFixturePhase.Attack &&
              !reactionActive)) &&
            GetMountedGuardPresentationActionName(direction) != null;
    }

    internal static float GetMountedGuardPresentationStartProgress(
        bool transitionPending,
        float currentProgress)
    {
        return transitionPending ||
            float.IsNaN(currentProgress) ||
            float.IsInfinity(currentProgress) ||
            currentProgress < 0f ||
            currentProgress > 1f
                ? 0f
                : currentProgress;
    }

    private void ApplyMountedGuardPresentationAction(
        INetworkAgentRegistry agentRegistry)
    {
        GuardDriver driver = guardDriver;
        if (driver == null ||
            !TryGetDrivenGuardAgent(agentRegistry, out Agent agent))
        {
            return;
        }

        bool reactionActive =
            driver.Phase == BattleGuardFixturePhase.Attack &&
            IsReaction(agent, driver.GuardActionIndex);
        if (!ShouldApplyExplicitMountedGuardInput(
                driver.Mode,
                driver.UseMovementFlagGuardInput) ||
            !ShouldMaintainMountedGuardPresentation(
                driver.Mode,
                driver.Phase,
                driver.Direction,
                reactionActive) ||
            !agent.HasMount)
        {
            driver.MountedPresentationActionPending = false;
            return;
        }

        string actionName =
            GetMountedGuardPresentationActionName(driver.Direction);
        ActionIndexCache action = ActionIndexCache.Create(actionName);
        bool transitionPending =
            driver.MountedPresentationActionPending;
        bool actionChanged =
            agent.GetCurrentAction(1).Index != action.Index;

        AgentActionData.ApplyDefendMovementFlags(
            agent,
            GetDefendFlags(driver.Direction));
        if (!transitionPending && !actionChanged)
            return;

        float startProgress = GetMountedGuardPresentationStartProgress(
            transitionPending,
            agent.GetCurrentActionProgress(1));
        AnimFlags additionalFlags = transitionPending
            ? AnimFlags.anf_restart
            : (AnimFlags)0uL;
        driver.MountedPresentationActionPending = false;
        AgentActionData.ApplyGuardDirectionTransition(
            agent,
            GetGuardMode(driver.Direction));
        driver.MountedPresentationAttempts++;
        driver.MountedPresentationRequestedActionIndex = action.Index;
        driver.MountedPresentationStartProgress = startProgress;
        driver.MountedPresentationApplied = agent.SetActionChannel(
            1,
            in action,
            ignorePriority: true,
            additionalFlags: additionalFlags,
            startProgress: startProgress);
        driver.MountedPresentationImmediateActionIndex =
            agent.GetCurrentAction(1).Index;
        driver.MountedPresentationImmediateGuardMode =
            agent.CurrentGuardMode.ToString();
    }

    private static bool TryPositionGuard(Agent agent, GuardDriver driver)
    {
        Vec3 forward = driver.OriginalLookDirection;
        forward.z = 0f;
        if (forward.LengthSquared < 0.0001f)
            forward = new Vec3(0f, 1f, 0f);
        forward.Normalize();
        Vec3 origin = driver.OriginalMount != null
            ? driver.OriginalMountPosition
            : driver.OriginalPosition;
        Scene scene = Mission.Current?.Scene;
        Vec3 lane;
        Vec3 position;
        if (driver.Mode == BattleGuardFixtureMode.Mounted)
        {
            if (!TryGetMountedFixtureLane(
                    scene,
                    origin,
                    forward,
                    driver,
                    out lane,
                    out position))
            {
                return false;
            }
        }
        else
        {
            lane = new Vec3(forward.y, -forward.x, 0f);
            position = origin + (lane * FixtureLaneOffset);
            if (scene != null)
                position.z = scene.GetGroundHeightAtPosition(position);
        }

        if (agent.MountAgent != null)
        {
            Vec2 movementDirection = lane.AsVec2;
            agent.MountAgent.TeleportToPosition(position);
            agent.MountAgent.LookDirection = lane;
            agent.MountAgent.SetMovementDirection(movementDirection);
            agent.LookDirection = lane;
            agent.SetMovementDirection(movementDirection);
        }
        else
        {
            agent.TeleportToPosition(position);
            agent.LookDirection = lane;
            agent.SetMovementDirection(lane.AsVec2);
        }
        return true;
    }

    private static bool TryPrepareMountedRoute(
        Agent agent,
        GuardDriver driver)
    {
        Vec3 forward = driver.OriginalLookDirection;
        forward.z = 0f;
        if (forward.LengthSquared < 0.0001f)
            forward = new Vec3(0f, 1f, 0f);
        forward.Normalize();
        Vec3 origin = driver.OriginalMount != null
            ? driver.OriginalMountPosition
            : driver.OriginalPosition;
        return TryGetMountedFixtureLane(
            Mission.Current?.Scene,
            origin,
            forward,
            driver,
            out _,
            out _);
    }

    private bool TryApplyPendingMountedRoute(
        NetworkBattleGuardFixtureCommand command)
    {
        NetworkBattleGuardFixtureRoute route = pendingMountedRoute;
        if (!IsValidMountedRoute(route) ||
            roles == null ||
            route.CommandId != command.CommandId ||
            roles.GuardAgentId != route.GuardAgentId ||
            roles.GuardAuthority != route.GuardAuthority ||
            route.Phase != command.Phase)
        {
            return false;
        }

        SetReceivedMountedRoute(guardDriver, route);
        pendingMountedRoute = null;
        return true;
    }

    internal static string ClearMountedRouteWaitError(string error)
    {
        return error == WaitingForMountedGuardRouteError ? null : error;
    }

    private void SendMountedRoute(
        NetworkBattleGuardFixtureCommand command,
        BattleGuardMountedRoute route)
    {
        Vec3 start = route.Start;
        Vec3 direction = route.Direction;
        network.SendAllBut(
            controllerIdProvider.ControllerId,
            new NetworkBattleGuardFixtureRoute(
                command.BattleInstanceId,
                command.CommandId,
                command.GuardAgentId,
                command.GuardAuthority,
                start.x,
                start.y,
                start.z,
                direction.x,
                direction.y,
                route.Length,
                command.Phase));
    }

    private void SendMountedStrike(
        Vec3 travelDirection,
        Vec3 guardLookDirection,
        Vec3 target)
    {
        SendMountedStrikeLifecycle(
            true,
            travelDirection,
            guardLookDirection,
            target);
    }

    private void SendMountedStrikeEnd()
    {
        SendMountedStrikeLifecycle(
            false,
            Vec3.Zero,
            Vec3.Zero,
            Vec3.Zero);
    }

    private void SendMountedStrikeLifecycle(
        bool active,
        Vec3 travelDirection,
        Vec3 guardLookDirection,
        Vec3 target)
    {
        FixtureRoles currentRoles = roles;
        if (currentRoles == null ||
            string.IsNullOrEmpty(currentBattleInstanceId) ||
            currentCommandId == Guid.Empty)
        {
            return;
        }

        network.SendAllBut(
            controllerIdProvider.ControllerId,
            new NetworkBattleGuardFixtureStrike(
                currentBattleInstanceId,
                currentCommandId,
                currentRoles.GuardAgentId,
                currentRoles.GuardAuthority,
                currentRoles.StrikerAgentId,
                currentRoles.StrikerAuthority,
                active,
                travelDirection.x,
                travelDirection.y,
                guardLookDirection.x,
                guardLookDirection.y,
                target.x,
                target.y));
    }

    private static void SetReceivedMountedRoute(
        GuardDriver driver,
        NetworkBattleGuardFixtureRoute route)
    {
        // The guard authority owns positioning; observers use its route only as evidence.
        driver.MountedRoute = new BattleGuardMountedRoute(
            new Vec3(route.StartX, route.StartY, route.StartZ),
            new Vec3(route.DirectionX, route.DirectionY, 0f),
            route.Length);
    }

    private bool TryApplyPendingMountedStrike(
        NetworkBattleGuardFixtureCommand command)
    {
        NetworkBattleGuardFixtureStrike strike = pendingMountedStrike;
        if (!MatchesMountedStrike(
                strike,
                roles,
                command.CommandId,
                command.BattleInstanceId) ||
            guardDriver == null)
        {
            return false;
        }

        SetReceivedMountedStrike(guardDriver, strike);
        pendingMountedStrike = null;
        return true;
    }

    private static void SetReceivedMountedStrike(
        GuardDriver driver,
        NetworkBattleGuardFixtureStrike strike)
    {
        if (!strike.Active)
        {
            driver.EndMountedStrike();
            return;
        }

        driver.BeginMountedStrike(
            new Vec3(
                strike.TravelDirectionX,
                strike.TravelDirectionY,
                0f),
            new Vec3(
                strike.GuardLookDirectionX,
                strike.GuardLookDirectionY,
                0f),
            new Vec3(strike.TargetX, strike.TargetY, 0f));
    }

    internal static bool IsValidMountedStrike(
        NetworkBattleGuardFixtureStrike strike)
    {
        if (strike == null ||
            string.IsNullOrEmpty(strike.BattleInstanceId) ||
            strike.CommandId == Guid.Empty ||
            strike.GuardAgentId == Guid.Empty ||
            string.IsNullOrEmpty(strike.GuardAuthority) ||
            strike.StrikerAgentId == Guid.Empty ||
            string.IsNullOrEmpty(strike.StrikerAuthority))
        {
            return false;
        }
        if (!strike.Active)
            return true;
        if (!IsFinite(strike.TravelDirectionX) ||
            !IsFinite(strike.TravelDirectionY) ||
            !IsFinite(strike.GuardLookDirectionX) ||
            !IsFinite(strike.GuardLookDirectionY) ||
            !IsFinite(strike.TargetX) ||
            !IsFinite(strike.TargetY))
        {
            return false;
        }

        float travelLengthSquared =
            (strike.TravelDirectionX * strike.TravelDirectionX) +
            (strike.TravelDirectionY * strike.TravelDirectionY);
        float lookLengthSquared =
            (strike.GuardLookDirectionX * strike.GuardLookDirectionX) +
            (strike.GuardLookDirectionY * strike.GuardLookDirectionY);
        return travelLengthSquared > 0.0001f &&
            lookLengthSquared > 0.0001f;
    }

    private static bool MatchesMountedStrike(
        NetworkBattleGuardFixtureStrike strike,
        FixtureRoles expectedRoles,
        Guid commandId,
        string battleInstanceId)
    {
        return IsValidMountedStrike(strike) &&
            expectedRoles != null &&
            strike.BattleInstanceId == battleInstanceId &&
            strike.CommandId == commandId &&
            strike.GuardAgentId == expectedRoles.GuardAgentId &&
            strike.GuardAuthority == expectedRoles.GuardAuthority &&
            strike.StrikerAgentId == expectedRoles.StrikerAgentId &&
            strike.StrikerAuthority == expectedRoles.StrikerAuthority;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsValidMountedRoute(
        NetworkBattleGuardFixtureRoute route)
    {
        if (route == null ||
            string.IsNullOrEmpty(route.BattleInstanceId) ||
            route.CommandId == Guid.Empty ||
            route.GuardAgentId == Guid.Empty ||
            string.IsNullOrEmpty(route.GuardAuthority) ||
            route.Length <= BattleGuardMountedRoute.MinimumLength ||
            float.IsNaN(route.StartX) ||
            float.IsInfinity(route.StartX) ||
            float.IsNaN(route.StartY) ||
            float.IsInfinity(route.StartY) ||
            float.IsNaN(route.StartZ) ||
            float.IsInfinity(route.StartZ) ||
            float.IsNaN(route.DirectionX) ||
            float.IsInfinity(route.DirectionX) ||
            float.IsNaN(route.DirectionY) ||
            float.IsInfinity(route.DirectionY) ||
            float.IsNaN(route.Length) ||
            float.IsInfinity(route.Length))
        {
            return false;
        }

        float directionLengthSquared =
            (route.DirectionX * route.DirectionX) +
            (route.DirectionY * route.DirectionY);
        return directionLengthSquared > 0.0001f;
    }

    private static bool MatchesMountedRoute(
        NetworkBattleGuardFixtureRoute route,
        FixtureRoles expectedRoles,
        BattleGuardFixturePhase phase,
        Guid commandId)
    {
        return IsValidMountedRoute(route) &&
            expectedRoles != null &&
            route.CommandId == commandId &&
            route.GuardAgentId == expectedRoles.GuardAgentId &&
            route.GuardAuthority == expectedRoles.GuardAuthority &&
            route.Phase == phase;
    }

    private static bool TryGetMountedFixtureLane(
        Scene scene,
        Vec3 origin,
        Vec3 forward,
        GuardDriver driver,
        out Vec3 lane,
        out Vec3 position)
    {
        if (driver.HasFixtureLane)
        {
            lane = driver.FixtureLane;
            position = origin + (lane * FixtureLaneOffset);
            position.z = scene?.GetGroundHeightAtPosition(position)
                ?? position.z;
            if (driver.MountedRoute == null)
            {
                driver.MountedRoute = new BattleGuardMountedRoute(
                    position,
                    lane,
                    MountedRouteLength);
            }
            return true;
        }

        Vec3 perpendicular =
            new Vec3(forward.y, -forward.x, 0f);
        Vec3 forwardPerpendicular =
            GetHorizontalDirection(forward + perpendicular);
        Vec3 forwardOppositePerpendicular =
            GetHorizontalDirection(forward - perpendicular);
        var candidates = new[]
        {
            perpendicular,
            -perpendicular,
            forward,
            -forward,
            forwardPerpendicular,
            -forwardPerpendicular,
            forwardOppositePerpendicular,
            -forwardOppositePerpendicular
        };
        foreach (Vec3 candidate in candidates)
        {
            Vec3 candidatePosition =
                origin + (candidate * FixtureLaneOffset);
            if (scene != null)
            {
                candidatePosition.z =
                    scene.GetGroundHeightAtPosition(candidatePosition);
                if (!IsMountedRouteClear(
                        scene,
                        candidatePosition,
                        candidate,
                        driver))
                    continue;
            }

            driver.FixtureLane = candidate;
            driver.HasFixtureLane = true;
            driver.MountedRoute = new BattleGuardMountedRoute(
                candidatePosition,
                candidate,
                MountedRouteLength);
            lane = candidate;
            position = candidatePosition;
            return true;
        }

        lane = Vec3.Zero;
        position = Vec3.Zero;
        return false;
    }

    private static bool IsMountedRouteClear(
        Scene scene,
        Vec3 start,
        Vec3 direction,
        GuardDriver driver)
    {
        Vec3 previous = start;
        for (float distance = MountedRouteSampleLength;
             distance <= MountedRouteLength;
             distance += MountedRouteSampleLength)
        {
            Vec3 current = start + (direction * distance);
            current.z = scene.GetGroundHeightAtPosition(current);
            if (Math.Abs(current.z - previous.z) >
                MountedRouteMaximumRise)
            {
                return false;
            }

            var segmentStart = new WorldPosition(
                scene,
                UIntPtr.Zero,
                previous,
                hasValidZ: false);
            var segmentEnd = new WorldPosition(
                scene,
                UIntPtr.Zero,
                current,
                hasValidZ: false);
            if (!scene.IsLineToPointClear(
                    ref segmentStart,
                    ref segmentEnd,
                    MountedRouteRadius))
            {
                return false;
            }

            previous = current;
        }

        Mission mission = Mission.Current;
        if (mission == null)
            return true;

        foreach (Agent agent in mission.Agents)
        {
            if (agent == null ||
                !agent.IsActive() ||
                agent.Mission != mission ||
                ReferenceEquals(agent, driver.Agent) ||
                ReferenceEquals(agent, driver.OriginalMount))
            {
                continue;
            }

            if (IsInsideMountedRouteClearance(
                    agent.Position,
                    start,
                    direction,
                    MountedRouteLength,
                    MountedRouteRadius))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsInsideMountedRouteClearance(
        Vec3 position,
        Vec3 start,
        Vec3 direction,
        float length,
        float radius)
    {
        direction.z = 0f;
        if (direction.LengthSquared < 0.0001f ||
            length <= 0f ||
            radius < 0f)
        {
            return false;
        }

        direction.Normalize();
        Vec3 offset = position - start;
        offset.z = 0f;
        float progress = Math.Max(
            0f,
            Math.Min(
                length,
                Vec3.DotProduct(offset, direction)));
        Vec3 separation =
            position - (start + (direction * progress));
        separation.z = 0f;
        return separation.LengthSquared <= radius * radius;
    }

    internal static bool HasMountedStrikeRunway(
        BattleGuardMountedRoute route)
    {
        return route?.CanStageStrike == true &&
            Math.Abs(route.LateralOffset) <=
                MountedStrikeMaximumStageLateral &&
            route.RemainingDistance >= MountedStrikeMinimumRunway;
    }

    internal static bool HasMountedStrikeStagingRunway(
        BattleGuardMountedRoute route,
        bool guardLocallyDriven)
    {
        return !guardLocallyDriven ||
            HasMountedStrikeRunway(route);
    }

    internal static float GetMountedStrikeTravelAlignment(
        Vec3 travelDirection,
        Vec3 guardLookDirection)
    {
        travelDirection.z = 0f;
        guardLookDirection.z = 0f;
        if (travelDirection.LengthSquared < 0.0001f ||
            guardLookDirection.LengthSquared < 0.0001f)
        {
            return -1f;
        }

        travelDirection.Normalize();
        guardLookDirection.Normalize();
        return Vec3.DotProduct(
            travelDirection,
            guardLookDirection);
    }

    internal static bool HasMountedStrikeTravelAlignment(
        Vec3 travelDirection,
        Vec3 guardLookDirection)
    {
        return GetMountedStrikeTravelAlignment(
            travelDirection,
            guardLookDirection) >=
            MountedStrikeMinimumTravelGuardAlignment;
    }

    internal static bool HasMountedStrikeStagingAlignment(
        Vec3 travelDirection,
        Vec3 guardLookDirection,
        bool guardLocallyDriven)
    {
        float minimumAlignment = guardLocallyDriven
            ? MountedStrikeMinimumTravelGuardAlignment
            : MountedStrikeMinimumContactAlignment;
        return GetMountedStrikeTravelAlignment(
            travelDirection,
            guardLookDirection) >= minimumAlignment;
    }

    internal static bool ShouldApplyOwnedMountedStrikeLook(
        bool guardLocallyDriven,
        BattleGuardFixtureMode mode,
        BattleGuardFixturePhase phase)
    {
        return guardLocallyDriven &&
            mode == BattleGuardFixtureMode.Mounted &&
            phase == BattleGuardFixturePhase.Attack;
    }

    internal static bool HasMountedStrikeContactAlignment(float alignment)
    {
        return alignment >= MountedStrikeMinimumContactAlignment;
    }

    internal static bool HasObservedReplicatedMountedStrikeLook(
        bool guardLocallyDriven,
        bool hasReplicatedLook,
        long stagedUpdateSequence,
        long currentUpdateSequence,
        float replicatedLookAlignment)
    {
        return guardLocallyDriven ||
            (hasReplicatedLook &&
             currentUpdateSequence > stagedUpdateSequence &&
             replicatedLookAlignment >=
                MountedStrikeMinimumReplicatedLookAlignment);
    }

    internal static bool HasMountedStrikeChargeTimedOut(float chargeSeconds)
    {
        return chargeSeconds >= MountedStrikeMaximumChargeSeconds;
    }

    internal static bool ShouldReleaseTimedOutMountedStrike(
        float alignment,
        float alignedSeconds)
    {
        return HasMountedStrikeContactAlignment(alignment) &&
            alignedSeconds >= FixtureAttackPressSeconds;
    }

    internal static bool ShouldCommandMountedGuardState(
        bool guarding,
        bool guardCommandActive,
        BattleGuardFixtureDirection direction,
        BattleGuardFixtureDirection guardCommandDirection)
    {
        return guarding != guardCommandActive ||
            ShouldResetMountedGuardDirection(
                guarding,
                guardCommandActive,
                direction,
                guardCommandDirection);
    }

    internal static bool ShouldResetMountedGuardDirection(
        bool guarding,
        bool guardCommandActive,
        BattleGuardFixtureDirection direction,
        BattleGuardFixtureDirection guardCommandDirection)
    {
        return guarding &&
            guardCommandActive &&
            direction != guardCommandDirection;
    }

    internal static bool IsRemountStateReconciled(
        bool riderReferencesMount,
        bool mountReferencesRider)
    {
        return riderReferencesMount && mountReferencesRider;
    }

    internal static bool NeedsMountRestore(
        bool originalMountActive,
        bool riderReferencesMount,
        bool mountReferencesRider)
    {
        return originalMountActive &&
            !IsRemountStateReconciled(
                riderReferencesMount,
                mountReferencesRider);
    }

    internal static Vec3 GetMountedStrikeContactPoint(
        Vec3 guardPosition,
        Vec3 laneDirection,
        float leadDistance)
    {
        laneDirection = GetHorizontalDirection(laneDirection);
        return guardPosition +
            (laneDirection * leadDistance);
    }

    internal static Vec3 GetMountedStrikerPosition(
        Vec3 strikeTargetPoint,
        Vec3 laneDirection)
    {
        laneDirection = GetHorizontalDirection(laneDirection);
        return strikeTargetPoint +
            (laneDirection *
             MountedStrikeGuardedArcOffset);
    }

    internal static Vec3 GetMountedStrikeTargetPoint(
        Vec3 contactPoint,
        Vec3 laneDirection)
    {
        laneDirection = GetHorizontalDirection(laneDirection);
        var lateralDirection = new Vec3(
            laneDirection.y,
            -laneDirection.x,
            0f);
        return contactPoint +
            (lateralDirection *
             MountedStrikeGuardedArcOffset);
    }

    internal static Vec3 GetMountedStrikeGuardedLookDirection(
        Vec3 laneDirection)
    {
        laneDirection = GetHorizontalDirection(laneDirection);
        var lateralDirection = new Vec3(
            laneDirection.y,
            -laneDirection.x,
            0f);
        return GetHorizontalDirection(
            lateralDirection +
            (laneDirection * MountedStrikeForwardBias));
    }

    internal static Vec3 GetMountedStrikeTrackedLookDirection(
        Vec3 guardPosition,
        Vec3 strikerPosition,
        Vec3 fallbackDirection)
    {
        Vec3 direction = strikerPosition - guardPosition;
        direction.z = 0f;
        if (direction.LengthSquared < 0.0001f)
            direction = fallbackDirection;
        return GetHorizontalDirection(direction);
    }

    internal static bool ShouldReleaseMountedStrike(
        float longitudinalDistance,
        float speed,
        float chargeSeconds,
        float releaseLeadSeconds)
    {
        float timeToContact =
            longitudinalDistance / Math.Max(0.1f, speed);
        return timeToContact <= releaseLeadSeconds ||
            HasMountedStrikeChargeTimedOut(chargeSeconds);
    }

    internal static bool HasMountedStrikeSpeed(
        float speed,
        float calibratedPlateauSpeed)
    {
        return calibratedPlateauSpeed > 0f &&
            speed >=
                calibratedPlateauSpeed *
                MountedStrikeMinimumCalibratedSpeedRatio;
    }

    internal static float GetMountedStrikeSpeedBaseline(
        float calibratedPlateauSpeed,
        bool guardLocallyDriven)
    {
        if (calibratedPlateauSpeed > 0f ||
            guardLocallyDriven)
        {
            return calibratedPlateauSpeed;
        }

        return BattleGuardMountedSpeedLimiter.MaximumSpeed;
    }

    internal static float GetMountedStrikeReleaseLeadSeconds(
        int attempt)
    {
        int profile = Math.Max(
            1,
            Math.Min(
                MountedStrikeReleaseLeadProfiles,
                attempt));
        // Start with the profile that reaches the rider before the horse loses calibrated speed.
        if (profile == 1)
        {
            return MountedStrikeMinimumReleaseLeadSeconds +
                MountedStrikeReleaseLeadStepSeconds;
        }
        if (profile == 2)
            return MountedStrikeMinimumReleaseLeadSeconds;

        return MountedStrikeMinimumReleaseLeadSeconds +
            ((profile - 1) *
             MountedStrikeReleaseLeadStepSeconds);
    }

    internal static bool IsNativeAttackReady(
        Agent.ActionStage actionStage)
    {
        return actionStage == Agent.ActionStage.AttackReady ||
            actionStage == Agent.ActionStage.AttackQuickReady;
    }

    internal static bool ShouldCompleteStrikeFromScoreHit(
        bool isBlocked,
        bool isExactGuard)
    {
        return isBlocked && isExactGuard;
    }

    private static Vec3 GetHorizontalDirection(Vec3 direction)
    {
        direction.z = 0f;
        if (direction.LengthSquared < 0.0001f)
            return new Vec3(0f, 1f, 0f);

        direction.Normalize();
        return direction;
    }

    private static void ClearDefendFlags(
        Agent agent,
        Agent.MovementControlFlag originalMovementFlags)
    {
        agent.MovementFlags = originalMovementFlags & ~DriveFlags;
        AgentActionData.ApplyDefendMovementFlags(agent, Agent.MovementControlFlag.None);
    }

    private static void SetControllerDirect(
        Agent agent,
        AgentControllerType controller)
    {
        if (agent.Controller != controller)
            MBAPI.IMBAgent.SetController(agent.GetPtr(), controller);
    }

    private void TickStriker(INetworkAgentRegistry agentRegistry)
    {
        if (strikerDriver == null)
            return;

        if (controllerIdProvider.ControllerId != roles?.StrikerAuthority ||
            !TryGetExactAgent(agentRegistry, strikerDriver.AgentId, roles.StrikerAuthority, out CoopAgentInfo strikerInfo) ||
            !TryGetExactAgent(agentRegistry, roles.GuardAgentId, roles.GuardAuthority, out CoopAgentInfo guardInfo) ||
            !agentRegistry.IsLocallyControlled(strikerDriver.AgentId))
        {
            DetachMigratedStrikerDriver(agentRegistry);
            return;
        }

        Agent striker = strikerInfo.Agent;
        Agent guard = guardInfo.Agent;

        if (striker.Controller != AgentControllerType.AI)
        {
            SetControllerDirect(striker, AgentControllerType.AI);
            AgentAiWaker.Wake(striker);
        }
        striker.SetTargetAgent(guard);
        striker.SetWatchState(Agent.WatchState.Alarmed);
        striker.SetIsAIPaused(false);
        strikerDriver.ApplyFixtureSwingSpeed(
            striker,
            FixtureStrikerSwingSpeedMultiplier);
        strikerDriver.ObserveFixtureWeaponState(striker);
    }

    private void DetachMigratedStrikerDriver(
        INetworkAgentRegistry agentRegistry)
    {
        StrikerDriver driver = strikerDriver;
        Agent agent = driver?.Agent;
        if (driver?.HasAttackDriver != true ||
            !IsActiveMissionAgent(agent) ||
            !HasMigratedAway(agentRegistry, agent, roles?.StrikerAuthority))
        {
            return;
        }

        driver.DetachAttackDriver(agent);
        lastError = "striker authority changed during fixture";
    }

    private static bool EquipFixtureWeapon(Agent agent, string itemId, out string error)
    {
        ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
        if (item == null)
        {
            error = $"missing fixture weapon {itemId}";
            return false;
        }

        var weapon = new MissionWeapon(item, null, null);
        ReplaceWeapon(agent, EquipmentIndex.Weapon0, weapon);
        ReplaceWeapon(agent, EquipmentIndex.Weapon1, MissionWeapon.Invalid);
        agent.SetWieldedItemIndexAsClient(
            Agent.HandIndex.OffHand,
            EquipmentIndex.None,
            false,
            false,
            0);
        agent.SetWieldedItemIndexAsClient(
            Agent.HandIndex.MainHand,
            EquipmentIndex.Weapon0,
            false,
            false,
            0);
        error = null;
        return true;
    }

    internal static bool IsFixtureWieldState(
        BattleGuardFixtureMode mode,
        EquipmentIndex mainHandIndex,
        EquipmentIndex offHandIndex,
        int mainHandUsageIndex,
        string mainHandItemId)
    {
        int expectedUsageIndex =
            mode == BattleGuardFixtureMode.Foot ? 1 : 0;
        return mainHandIndex == EquipmentIndex.Weapon0 &&
            offHandIndex == EquipmentIndex.None &&
            mainHandUsageIndex == expectedUsageIndex &&
            mainHandItemId == GuardWeaponId;
    }

    internal static bool IsFixtureStrikerWieldState(
        BattleGuardFixtureMode mode,
        EquipmentIndex mainHandIndex,
        EquipmentIndex offHandIndex,
        int mainHandUsageIndex,
        string mainHandItemId)
    {
        string expectedWeaponId =
            GetFixtureStrikerWeaponId(mode);
        int expectedUsageIndex =
            GetFixtureStrikerWeaponUsageIndex(mode);
        return mainHandIndex == EquipmentIndex.Weapon0 &&
            offHandIndex == EquipmentIndex.None &&
            mainHandUsageIndex == expectedUsageIndex &&
            mainHandItemId == expectedWeaponId;
    }

    internal static bool ShouldRequestFixtureStrikerWield(
        bool weaponAvailable,
        bool weaponReady,
        int lastRequestAttempt,
        int attempt,
        bool retryDue) =>
        weaponAvailable &&
        (lastRequestAttempt != attempt ||
         (!weaponReady && retryDue));

    internal static bool ShouldSheathFixtureStrikerOffHand(
        bool weaponAvailable,
        EquipmentIndex offHandIndex) =>
        weaponAvailable &&
        offHandIndex != EquipmentIndex.None;

    private static string GetFixtureStrikerWeaponId(
        BattleGuardFixtureMode mode) =>
        mode == BattleGuardFixtureMode.Mounted
            ? MountedStrikerWeaponId
            : FootStrikerWeaponId;

    private static int GetFixtureStrikerWeaponUsageIndex(
        BattleGuardFixtureMode mode) =>
        mode == BattleGuardFixtureMode.Mounted ? 1 : 0;

    private static void ReplaceWeapon(
        Agent agent,
        EquipmentIndex slot,
        MissionWeapon replacement)
    {
        if (!agent.Equipment[slot].IsEmpty)
            agent.RemoveEquippedWeapon(slot);
        if (!replacement.IsEmpty)
            agent.EquipWeaponWithNewEntity(slot, ref replacement);
    }

    private void RestoreGuard(INetworkAgentRegistry agentRegistry)
    {
        GuardDriver driver = guardDriver;
        if (driver == null)
            return;

        Agent agent = null;
        if (TryGetExactAgent(
                agentRegistry,
                driver.AgentId,
                roles?.GuardAuthority,
                out CoopAgentInfo info))
        {
            agent = info.Agent;
        }
        else if (IsActiveMissionAgent(driver.Agent))
        {
            agent = driver.Agent;
        }

        if (agent == null)
        {
            driver.StopDriving();
            return;
        }

        bool drivesGuard = driver.DrivesAgent;
        if (drivesGuard &&
            HasMigratedAway(agentRegistry, agent, roles?.GuardAuthority))
        {
            StopDrivingGuard(agent, driver);
            drivesGuard = false;
        }

        RestoreGuardForCurrentAgent(agent, drivesGuard);
    }

    private void DetachMigratedGuardDriver(
        INetworkAgentRegistry agentRegistry)
    {
        GuardDriver driver = guardDriver;
        Agent agent = driver?.Agent;
        if (driver?.DrivesAgent != true ||
            !IsActiveMissionAgent(agent) ||
            !HasMigratedAway(agentRegistry, agent, roles?.GuardAuthority))
        {
            return;
        }

        StopDrivingGuard(agent, driver);
        lastError = "guard authority changed during fixture";
    }

    private static void StopDrivingGuard(Agent agent, GuardDriver driver)
    {
        agent.EventControlFlags &= ~Agent.EventControlFlag.Dismount;
        agent.MovementFlags &= ~DriveFlags;
        agent.MovementInputVector = Vec2.Zero;
        AgentActionData.ApplyDefendMovementFlags(
            agent,
            Agent.MovementControlFlag.None);
        driver.StopDriving();
    }

    private static bool HasMigratedAway(
        INetworkAgentRegistry agentRegistry,
        Agent agent,
        string expectedAuthority)
    {
        return agentRegistry.TryGetAgentInfo(agent, out CoopAgentInfo info) &&
            (info.CurrentAuthority != expectedAuthority ||
             !agentRegistry.IsLocallyControlled(agent));
    }

    private static bool IsActiveMissionAgent(Agent agent)
    {
        return agent != null &&
            agent.IsActive() &&
            Mission.Current != null &&
            agent.Mission == Mission.Current;
    }

    private void RestoreGuardForCurrentAgent(Agent agent, bool drivesGuard)
    {
        GuardDriver driver = guardDriver;
        if (driver.EquipmentReplaced)
        {
            ReplaceWeapon(agent, EquipmentIndex.Weapon0, driver.OriginalWeapon0);
            ReplaceWeapon(agent, EquipmentIndex.Weapon1, driver.OriginalWeapon1);
            driver.OriginalWieldedEquipment.Apply(agent);
        }
        bool originalMountActive =
            driver.OriginalMount?.IsActive() == true;
        bool needsRemount = NeedsMountRestore(
            originalMountActive,
            ReferenceEquals(agent.MountAgent, driver.OriginalMount),
            ReferenceEquals(driver.OriginalMount?.RiderAgent, agent));
        if (needsRemount)
        {
            pendingGuardRestore = new PendingGuardRestore(
                driver,
                driver.AgentId,
                roles.GuardAuthority,
                driver.OriginalMount,
                driver.OriginalPosition,
                driver.OriginalLookDirection,
                driver.OriginalMovementDirection,
                driver.OriginalMountPosition,
                driver.OriginalMountLookDirection,
                driver.OriginalMountMovementDirection,
                drivesGuard);
        }
        if (!drivesGuard)
            return;

        if (needsRemount)
        {
            BeginGuardRemount(agent, pendingGuardRestore);
            return;
        }

        CompleteGuardRestore(agent, driver);
        if (originalMountActive)
        {
            RemoveAllMountWithoutRiderEntries(driver.OriginalMount);
            driver.OriginalMount.TeleportToPosition(
                driver.OriginalMountPosition);
            driver.OriginalMount.LookDirection =
                driver.OriginalMountLookDirection;
            driver.OriginalMount.SetMovementDirection(
                driver.OriginalMountMovementDirection);
        }
        else
        {
            agent.TeleportToPosition(driver.OriginalPosition);
        }
        agent.LookDirection = driver.OriginalLookDirection;
        agent.SetMovementDirection(driver.OriginalMovementDirection);
    }

    private void TickPendingGuardRestore(
        INetworkAgentRegistry agentRegistry)
    {
        PendingGuardRestore restore = pendingGuardRestore;
        if (restore == null)
            return;
        if (!TryGetExactAgent(
                agentRegistry,
                restore.AgentId,
                restore.Authority,
                out CoopAgentInfo info))
        {
            Agent originalAgent = restore.Driver.Agent;
            if (restore.DrivesRestore &&
                IsActiveMissionAgent(originalAgent))
            {
                if (HasMigratedAway(
                        agentRegistry,
                        originalAgent,
                        restore.Authority))
                {
                    StopDrivingGuard(originalAgent, restore.Driver);
                }
                else
                {
                    CompleteGuardRestore(originalAgent, restore.Driver);
                }
            }
            pendingGuardRestore = null;
            lastError = "guard remount agent is unavailable";
            return;
        }

        Agent agent = info.Agent;
        if (IsRemountStateReconciled(
                ReferenceEquals(agent.MountAgent, restore.Mount),
                ReferenceEquals(restore.Mount.RiderAgent, agent)))
        {
            RemoveAllMountWithoutRiderEntries(restore.Mount);
            if (restore.DrivesRestore)
                CompleteGuardRestore(agent, restore.Driver);
            pendingGuardRestore = null;
            lastError = null;
            return;
        }
        if (!restore.Mount.IsActive() ||
            (restore.Mount.RiderAgent != null &&
             !ReferenceEquals(restore.Mount.RiderAgent, agent)) ||
            (agent.HasMount &&
             !ReferenceEquals(agent.MountAgent, restore.Mount)))
        {
            if (restore.DrivesRestore)
                CompleteGuardRestore(agent, restore.Driver);
            pendingGuardRestore = null;
            lastError = "guard remount is no longer possible";
            return;
        }
        if (!restore.DrivesRestore)
            return;
        if (!agentRegistry.IsLocallyControlled(restore.AgentId))
        {
            StopDrivingGuard(agent, restore.Driver);
            pendingGuardRestore = null;
            lastError = "guard remount authority is unavailable";
            return;
        }

        BeginGuardRemount(agent, restore);
    }

    private static void BeginGuardRemount(
        Agent agent,
        PendingGuardRestore restore)
    {
        restore.Mount.TeleportToPosition(restore.MountPosition);
        restore.Mount.LookDirection = restore.MountLookDirection;
        restore.Mount.SetMovementDirection(
            restore.MountMovementDirection);
        agent.TeleportToPosition(restore.AgentPosition);
        agent.LookDirection = restore.AgentLookDirection;
        agent.SetMovementDirection(restore.AgentMovementDirection);
        agent.EventControlFlags &=
            ~(Agent.EventControlFlag.Dismount |
              Agent.EventControlFlag.Mount);
        agent.MountAgent = restore.Mount;
    }

    private static void RemoveAllMountWithoutRiderEntries(Agent mount)
    {
        Mission mission = Mission.Current;
        if (mission == null || mount == null)
            return;

        int matchingEntries = 0;
        foreach (KeyValuePair<Agent, MissionTime> entry in mission.MountsWithoutRiders)
        {
            if (ReferenceEquals(entry.Key, mount))
                matchingEntries++;
        }

        for (int i = 0; i < matchingEntries; i++)
            mission.RemoveMountWithoutRider(mount);
    }

    private static void CompleteGuardRestore(
        Agent agent,
        GuardDriver driver)
    {
        driver.StopDriving();
        agent.EventControlFlags &= ~Agent.EventControlFlag.Dismount;
        agent.MovementFlags = driver.OriginalMovementFlags;
        agent.MovementInputVector = driver.OriginalMovementInputVector;
        AgentActionData.ApplyDefendMovementFlags(
            agent,
            driver.OriginalDefendFlags);
        AgentActionData.ApplyGuardState(
            agent,
            driver.OriginalGuardMode,
            force: true);
    }

    private void RestoreStriker(INetworkAgentRegistry agentRegistry)
    {
        StrikerDriver driver = strikerDriver;
        if (driver == null)
            return;

        Agent agent = null;
        if (TryGetExactAgent(
                agentRegistry,
                driver.AgentId,
                roles?.StrikerAuthority,
                out CoopAgentInfo info))
        {
            agent = info.Agent;
        }
        else if (IsActiveMissionAgent(driver.Agent))
        {
            agent = driver.Agent;
        }

        if (agent == null)
            return;

        bool drivesStriker = driver.HasAttackDriver;
        if (drivesStriker &&
            HasMigratedAway(agentRegistry, agent, roles?.StrikerAuthority))
        {
            driver.DetachAttackDriver(agent);
            drivesStriker = false;
        }

        RestoreStrikerForCurrentAgent(agent, drivesStriker);
    }

    private void RestoreStrikerForCurrentAgent(
        Agent agent,
        bool drivesStriker)
    {
        if (strikerDriver.EquipmentReplaced)
        {
            ReplaceWeapon(agent, EquipmentIndex.Weapon0, strikerDriver.OriginalWeapon0);
            ReplaceWeapon(agent, EquipmentIndex.Weapon1, strikerDriver.OriginalWeapon1);
            strikerDriver.OriginalWieldedEquipment.Apply(agent);
        }
        strikerDriver.RestoreSwingSpeed(agent);
        if (!drivesStriker)
            return;

        agent.MovementFlags = strikerDriver.OriginalMovementFlags;
        AgentActionData.ApplyDefendMovementFlags(agent, strikerDriver.OriginalDefendFlags);
        agent.SetTargetAgent(strikerDriver.OriginalTarget);
        agent.SetWatchState(strikerDriver.OriginalWatchState);
        agent.SetIsAIPaused(strikerDriver.WasPaused);
        strikerDriver.DetachAttackDriver(agent);
        SetControllerDirect(agent, strikerDriver.OriginalController);
        agent.TeleportToPosition(strikerDriver.OriginalPosition);
        agent.LookDirection = strikerDriver.OriginalLookDirection;
    }

    private static bool TryCreateRoles(
        NetworkBattleGuardFixtureCommand command,
        out FixtureRoles commandRoles)
    {
        commandRoles = null;
        if (command.CommandId == Guid.Empty ||
            command.GuardAgentId == Guid.Empty ||
            command.StrikerAgentId == Guid.Empty ||
            command.GuardAgentId == command.StrikerAgentId ||
            string.IsNullOrEmpty(command.GuardAuthority) ||
            string.IsNullOrEmpty(command.StrikerAuthority))
        {
            return false;
        }

        commandRoles = new FixtureRoles(
            command.GuardAgentId,
            command.GuardAuthority,
            command.StrikerAgentId,
            command.StrikerAuthority);
        return true;
    }

    private void CaptureAiPauseStates(INetworkAgentRegistry agentRegistry)
    {
        aiPauseStates.Clear();
        CaptureNewAiPauseStates(agentRegistry);
    }

    private void CaptureNewAiPauseStates(INetworkAgentRegistry agentRegistry)
    {
        if (agentRegistry == null ||
            roles == null ||
            Mission.Current == null)
            return;

        Agent guard = null;
        Agent guardMount = null;
        Agent striker = null;
        if (TryGetExactAgent(
                agentRegistry,
                roles.GuardAgentId,
                roles.GuardAuthority,
                out CoopAgentInfo guardInfo))
        {
            guard = guardInfo.Agent;
            guardMount = guard.MountAgent ?? guardDriver?.OriginalMount;
        }
        if (strikerDriver != null &&
            TryGetExactAgent(
                agentRegistry,
                strikerDriver.AgentId,
                roles.StrikerAuthority,
                out CoopAgentInfo strikerInfo))
        {
            striker = strikerInfo.Agent;
        }

        foreach (Agent agent in Mission.Current.Agents)
        {
            if (agent == null ||
                ReferenceEquals(agent, guard) ||
                ReferenceEquals(agent, guardMount) ||
                ReferenceEquals(agent, striker) ||
                !agent.IsActive() ||
                agent.Mission != Mission.Current ||
                agent.Controller != AgentControllerType.AI ||
                HasAiPauseState(agent))
            {
                continue;
            }

            CoopAgentInfo registeredInfo = null;
            if (agentRegistry.TryGetAgentInfo(agent, out registeredInfo) &&
                !agentRegistry.IsLocallyControlled(agent))
            {
                continue;
            }

            aiPauseStates.Add(
                new AiPauseState(
                    agent,
                    registeredInfo,
                    agent.Controller,
                    agent.MovementFlags,
                    agent.MovementInputVector,
                    agent.GetTargetAgent(),
                    agent.CurrentWatchState,
                    agent.IsPaused));
        }
    }

    private bool HasAiPauseState(Agent agent)
    {
        foreach (AiPauseState state in aiPauseStates)
        {
            if (ReferenceEquals(state.Agent, agent))
                return true;
        }

        return false;
    }

    private void PauseOtherAi(INetworkAgentRegistry agentRegistry)
    {
        CaptureNewAiPauseStates(agentRegistry);
        Agent striker = null;
        if (strikerDriver != null &&
            TryGetExactAgent(
                agentRegistry,
                strikerDriver.AgentId,
                roles?.StrikerAuthority,
                out CoopAgentInfo strikerInfo))
        {
            striker = strikerInfo.Agent;
        }

        foreach (AiPauseState state in aiPauseStates)
        {
            Agent agent = state.Agent;
            if (ReferenceEquals(agent, striker) ||
                agent == null ||
                !agent.IsActive() ||
                agent.Mission != Mission.Current ||
                !state.CanControl(agentRegistry))
            {
                continue;
            }

            agent.SetIsAIPaused(true);
            agent.MovementFlags = Agent.MovementControlFlag.None;
            agent.MovementInputVector = Vec2.Zero;
            agent.SetTargetAgent(null);
            SetControllerDirect(agent, AgentControllerType.None);
        }
    }

    private void RestoreAiPauseStates(
        INetworkAgentRegistry agentRegistry)
    {
        foreach (AiPauseState state in aiPauseStates)
        {
            Agent agent = state.Agent;
            if (agent != null &&
                agent.IsActive() &&
                agent.Mission == Mission.Current &&
                state.CanControl(agentRegistry))
                state.Restore();
        }
    }

    private static bool TryGetExactAgent(
        INetworkAgentRegistry agentRegistry,
        Guid agentId,
        string authority,
        out CoopAgentInfo agentInfo)
    {
        agentInfo = null;
        return agentRegistry != null &&
            agentRegistry.TryGetAgentInfo(agentId, out agentInfo) &&
            agentInfo != null &&
            agentInfo.CurrentAuthority == authority &&
            agentInfo.Agent != null &&
            agentInfo.Agent.IsActive() &&
            agentInfo.Agent.Mission == Mission.Current;
    }

    private void Sample(Agent agent, Guid agentId, float elapsed)
    {
        sample.Samples++;
        sample.AgentId = agentId;
        sample.Mounted = agent.HasMount;
        sample.GuardMode = agent.CurrentGuardMode.ToString();
        sample.NativeDefendMovementFlags =
            (uint)agent.GetDefendMovementFlag();
        sample.Action0Direction =
            agent.GetCurrentActionDirection(0).ToString();
        sample.Action1Direction =
            agent.GetCurrentActionDirection(1).ToString();
        sample.Health = agent.Health;
        if (!sample.HasBaselineHealth)
        {
            sample.BaselineHealth = agent.Health;
            sample.HasBaselineHealth = true;
        }
        sample.HealthDelta = agent.Health - sample.BaselineHealth;
        SampleEquipment(agent);
        SampleSpeed(agent, elapsed);
        SamplePose(agent);

        Skeleton skeleton = null;
        try
        {
            MBAgentVisuals visuals = agent.AgentVisuals;
            if (ReferenceEquals(visuals, null) || !visuals.IsValid())
            {
                sample.MarkMissing(elapsed);
                return;
            }

            skeleton = visuals.GetSkeleton();
            if (ReferenceEquals(skeleton, null))
            {
                sample.MarkMissing(elapsed);
                return;
            }

            if (sample.LatchedChannel < 0)
                LatchGuardPresentation(agent);
            if (sample.LatchedChannel < 0)
            {
                sample.MarkMissing(elapsed);
                return;
            }

            int channel = sample.LatchedChannel;
            ActionIndexCache rawAction = agent.GetCurrentAction(channel);
            sample.RawActionIndex = rawAction.Index;
            sample.RawActionName =
                AgentActionData.GetActionNameWithCode(rawAction.Index);
            sample.RawActionType =
                agent.GetCurrentActionType(channel).ToString();
            sample.RawActionDirection =
                agent.GetCurrentActionDirection(channel).ToString();
            sample.RawProgress = agent.GetCurrentActionProgress(channel);
            sample.VisualActionIndex = skeleton.GetActionAtChannel(channel).Index;
            sample.VisualAnimationIndex = skeleton.GetAnimationIndexAtChannel(channel);
            sample.VisualProgress = skeleton.GetAnimationParameterAtChannel(channel);
            sample.GuardVisible =
                sample.VisualAnimationIndex == sample.LatchedAnimationIndex;
            if (sample.GuardVisible)
            {
                sample.VisibleSamples++;
                sample.CurrentMissingGapSeconds = 0f;
                SampleVisualProgress();
            }
            else
            {
                sample.MarkMissing(elapsed);
            }
        }
        catch
        {
            sample.MarkMissing(elapsed);
        }
        finally
        {
            if (!ReferenceEquals(skeleton, null))
                skeleton.ManualInvalidate();
        }
    }

    private void SampleEquipment(Agent agent)
    {
        if (!AgentEquipmentData.HasSafeWeaponSlots(agent?.Equipment))
        {
            sample.MainHandIndex = (int)EquipmentIndex.None;
            sample.OffHandIndex = (int)EquipmentIndex.None;
            sample.MainHandUsageIndex = -1;
            sample.MainHandItemId = null;
            sample.GuardEquipmentReady = false;
            return;
        }

        EquipmentIndex mainHandIndex =
            agent.GetPrimaryWieldedItemIndex();
        EquipmentIndex offHandIndex =
            agent.GetOffhandWieldedItemIndex();
        int mainHandUsageIndex = -1;
        string mainHandItemId = null;
        if (mainHandIndex >= EquipmentIndex.WeaponItemBeginSlot &&
            mainHandIndex < EquipmentIndex.NumAllWeaponSlots)
        {
            MissionWeapon mainHandWeapon =
                agent.Equipment[mainHandIndex];
            mainHandUsageIndex = mainHandWeapon.CurrentUsageIndex;
            mainHandItemId = mainHandWeapon.Item?.StringId;
        }

        sample.MainHandIndex = (int)mainHandIndex;
        sample.OffHandIndex = (int)offHandIndex;
        sample.MainHandUsageIndex = mainHandUsageIndex;
        sample.MainHandItemId = mainHandItemId;
        sample.GuardEquipmentReady =
            guardDriver != null &&
            IsFixtureWieldState(
                guardDriver.Mode,
                mainHandIndex,
                offHandIndex,
                mainHandUsageIndex,
                mainHandItemId);
    }

    private void ObservePreReplayDisplayedState(
        Agent agent,
        float dt)
    {
        bool receivedReaction = TryGetReaction(
            agent,
            sample.LatchedActionIndex,
            out int reactionChannel,
            out int reactionActionIndex,
            out int reactionAnimationIndex);
        float reactionProgress = -1f;
        bool reactionCyclic = false;
        if (receivedReaction)
        {
            reactionProgress =
                agent.GetCurrentActionProgress(reactionChannel);
            reactionCyclic =
                (agent.GetCurrentAnimationFlag(reactionChannel) &
                 AnimFlags.anf_cyclic) != 0;
        }

        sample.ObserveReceivedReaction(
            receivedReaction,
            reactionChannel,
            reactionActionIndex,
            reactionAnimationIndex,
            reactionProgress,
            reactionCyclic);

        Skeleton skeleton = null;
        try
        {
            MBAgentVisuals visuals = agent.AgentVisuals;
            if (ReferenceEquals(visuals, null) || !visuals.IsValid())
            {
                sample.ReplayEvidence.ClearPre();
                return;
            }

            skeleton = visuals.GetSkeleton();
            if (ReferenceEquals(skeleton, null))
            {
                sample.ReplayEvidence.ClearPre();
                return;
            }

            BattleGuardAnimationFrame channel0 =
                GetAnimationFrame(agent, skeleton, 0);
            BattleGuardAnimationFrame channel1 =
                GetAnimationFrame(agent, skeleton, 1);
            sample.PreReplayAnimationEvidence.ObserveFrame(
                dt,
                channel0,
                channel1);
            sample.ReplayEvidence.CapturePre(channel0, channel1);
        }
        catch
        {
            sample.ReplayEvidence.ClearPre();
        }
        finally
        {
            if (!ReferenceEquals(skeleton, null))
                skeleton.ManualInvalidate();
        }
    }

    private void ObserveFinalDisplayedState(Agent agent, float dt)
    {
        if (sample.LatchedChannel < 0)
            LatchGuardPresentation(agent);

        Vec3 position = agent.Position;
        Vec3 visualPosition = agent.VisualPosition;
        sample.VisualRootEvidence.Observe(
            position.x,
            position.y,
            position.z,
            visualPosition.x,
            visualPosition.y,
            visualPosition.z,
            dt);

        if (sample.ReceivedReactionActive)
        {
            sample.Reaction = true;
            sample.ReactionSamples++;
        }

        Skeleton skeleton = null;
        bool exactReactionVisual = false;
        bool returnedToExactGuard = false;
        int reactionVisualAnimationIndex = -1;
        float reactionVisualProgress = -1f;
        bool guardContinuityObserved = false;
        try
        {
            MBAgentVisuals visuals = agent.AgentVisuals;
            if (!ReferenceEquals(visuals, null) && visuals.IsValid())
            {
                skeleton = visuals.GetSkeleton();
                if (!ReferenceEquals(skeleton, null))
                {
                    BattleGuardAnimationFrame channel0 =
                        GetAnimationFrame(agent, skeleton, 0);
                    BattleGuardAnimationFrame channel1 =
                        GetAnimationFrame(agent, skeleton, 1);
                    sample.AnimationEvidence.ObserveFrame(
                        dt,
                        channel0,
                        channel1);
                    ObserveVisualAnimations(skeleton);

                    if (sample.LatchedChannel >= 0)
                    {
                        bool guardExact =
                            skeleton.GetAnimationIndexAtChannel(
                                sample.LatchedChannel) ==
                            sample.LatchedAnimationIndex;
                        sample.GuardContinuityEvidence.Observe(
                            guardExact,
                            dt);
                        guardContinuityObserved = true;
                    }

                    if (sample.ExpectedReactionChannel >= 0)
                    {
                        int reactionChannel =
                            sample.ExpectedReactionChannel;
                        reactionVisualAnimationIndex =
                            skeleton.GetAnimationIndexAtChannel(
                                reactionChannel);
                        exactReactionVisual =
                            skeleton.GetActionAtChannel(
                                reactionChannel).Index ==
                            sample.ExpectedReactionActionIndex &&
                            reactionVisualAnimationIndex >= 0;
                        if (exactReactionVisual)
                        {
                            sample.ObserveReactionVisual(
                                reactionVisualAnimationIndex);
                            reactionVisualProgress =
                                skeleton.GetAnimationParameterAtChannel(
                                    reactionChannel);
                        }
                        returnedToExactGuard =
                            sample.LatchedChannel >= 0 &&
                            skeleton.GetAnimationIndexAtChannel(
                                sample.LatchedChannel) ==
                            sample.LatchedAnimationIndex;
                    }
                }
            }
        }
        catch
        {
        }
        finally
        {
            if (!ReferenceEquals(skeleton, null))
                skeleton.ManualInvalidate();
        }

        if (sample.LatchedChannel >= 0 && !guardContinuityObserved)
            sample.GuardContinuityEvidence.Observe(false, dt);
        sample.ReactionEvidence.Observe(
            sample.ReceivedReactionActive,
            exactReactionVisual,
            returnedToExactGuard,
            sample.CurrentReactionChannel,
            sample.CurrentReactionActionIndex,
            exactReactionVisual
                ? reactionVisualAnimationIndex
                : sample.CurrentReactionAnimationIndex,
            reactionVisualProgress,
            sample.VisualRootEvidence.CurrentPositionSpeed,
            dt);
    }

    private void ObservePostReplayDisplayedState(Agent agent)
    {
        Skeleton skeleton = null;
        try
        {
            MBAgentVisuals visuals = agent.AgentVisuals;
            if (ReferenceEquals(visuals, null) || !visuals.IsValid())
            {
                sample.ReplayEvidence.ClearPre();
                return;
            }

            skeleton = visuals.GetSkeleton();
            if (ReferenceEquals(skeleton, null))
            {
                sample.ReplayEvidence.ClearPre();
                return;
            }

            BattleGuardAnimationFrame channel0 =
                GetAnimationFrame(agent, skeleton, 0);
            BattleGuardAnimationFrame channel1 =
                GetAnimationFrame(agent, skeleton, 1);
            sample.ReplayEvidence.ObservePost(
                channel0,
                channel1);
        }
        catch
        {
            sample.ReplayEvidence.ClearPre();
        }
        finally
        {
            if (!ReferenceEquals(skeleton, null))
                skeleton.ManualInvalidate();
        }
    }

    private BattleGuardAnimationFrame GetAnimationFrame(
        Agent agent,
        Skeleton skeleton,
        int channel)
    {
        int animationIndex =
            skeleton.GetAnimationIndexAtChannel(channel);
        bool isCyclic =
            (agent.GetCurrentAnimationFlag(channel) &
             AnimFlags.anf_cyclic) != 0;
        if (channel == sample.ExpectedReactionChannel &&
            skeleton.GetActionAtChannel(channel).Index ==
                sample.ExpectedReactionActionIndex)
        {
            isCyclic = sample.ExpectedReactionCyclic;
        }
        else if (channel == sample.LatchedChannel &&
            animationIndex == sample.LatchedAnimationIndex)
        {
            isCyclic = sample.LatchedActionCyclic;
        }

        return new BattleGuardAnimationFrame(
            channel,
            animationIndex,
            skeleton.GetAnimationParameterAtChannel(channel),
            skeleton.GetAnimationSpeedAtChannel(channel),
            isCyclic);
    }

    private static bool TryGetReaction(
        Agent agent,
        int guardActionIndex,
        out int channel,
        out int actionIndex,
        out int animationIndex)
    {
        for (channel = 0; channel <= 1; channel++)
        {
            if (!IsReaction(agent, channel, guardActionIndex))
                continue;

            ActionIndexCache action = agent.GetCurrentAction(channel);
            actionIndex = action.Index;
            animationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            return true;
        }

        channel = -1;
        actionIndex = -1;
        animationIndex = -1;
        return false;
    }

    private void ObserveVisualAnimations(Skeleton skeleton)
    {
        var seenThisSample = new HashSet<int>();
        for (int channel = 0; channel <= 1; channel++)
        {
            int animation = skeleton.GetAnimationIndexAtChannel(channel);
            if (animation >= 0)
                seenThisSample.Add(animation);
        }

        foreach (int animation in seenThisSample)
        {
            sample.VisualAnimations.Add(animation);
            int run = sample.CurrentVisualRuns.TryGetValue(animation, out int previous)
                ? previous + 1
                : 1;
            sample.CurrentVisualRuns[animation] = run;
            if (!sample.MaxVisualRuns.TryGetValue(animation, out int maximum) ||
                run > maximum)
            {
                sample.MaxVisualRuns[animation] = run;
            }
        }

        var previousAnimations = new List<int>(sample.CurrentVisualRuns.Keys);
        foreach (int animation in previousAnimations)
        {
            if (!seenThisSample.Contains(animation))
                sample.CurrentVisualRuns[animation] = 0;
        }
    }

    private void LatchGuardPresentation(Agent agent)
    {
        for (int channel = 0; channel <= 1; channel++)
        {
            if (!AgentActionData.IsDefendingAction(agent.GetCurrentActionType(channel)))
                continue;

            if (guardDriver != null &&
                !ShouldLatchGuardPresentation(
                    guardDriver.Phase,
                    guardDriver.Direction,
                    AgentActionData.GetGuardModeFromDefendingAction(
                        agent,
                        channel)))
            {
                continue;
            }

            ActionIndexCache action = agent.GetCurrentAction(channel);
            if (action.Index < 0)
                continue;

            sample.LatchedChannel = channel;
            sample.LatchedActionIndex = action.Index;
            sample.LatchedAnimationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            sample.LatchedActionCyclic =
                (agent.GetCurrentAnimationFlag(channel) & AnimFlags.anf_cyclic) != 0;
            guardDriver?.CaptureGuardPresentation(sample);
            return;
        }
    }

    private void SampleVisualProgress()
    {
        if (sample.HasPreviousVisualProgress)
        {
            float delta = sample.VisualProgress - sample.PreviousVisualProgress;
            if (delta < -ProgressEpsilon)
            {
                if (sample.LatchedActionCyclic &&
                    sample.PreviousVisualProgress > 0.75f &&
                    sample.VisualProgress < 0.25f)
                {
                    sample.VisualProgressAdvances++;
                }
                else
                {
                    sample.VisualProgressResets++;
                }
            }
            else if (Math.Abs(delta) <= ProgressEpsilon)
                sample.VisualProgressStalls++;
            else
                sample.VisualProgressAdvances++;
        }
        sample.PreviousVisualProgress = sample.VisualProgress;
        sample.HasPreviousVisualProgress = true;
    }

    private void SampleSpeed(Agent agent, float elapsed)
    {
        if (sample.HasPreviousPosition)
        {
            Vec3 delta = agent.Position - sample.PreviousPosition;
            delta.z = 0f;
            sample.HorizontalSpeed = elapsed > 0f ? delta.Length / elapsed : 0f;
            if (sample.HorizontalSpeed > sample.PeakHorizontalSpeed)
                sample.PeakHorizontalSpeed = sample.HorizontalSpeed;
            sample.SpeedEvidence.Observe(
                sample.HorizontalSpeed,
                elapsed);
            if (guardDriver != null)
            {
                guardDriver.CurrentHorizontalSpeed =
                    sample.HorizontalSpeed;
                if (delta.LengthSquared > 0.0001f)
                {
                    delta.Normalize();
                    guardDriver.CurrentHorizontalDirection = delta;
                }
                if (!guardDriver.DrivesAgent &&
                    guardDriver.MountedRoute != null)
                {
                    Agent mount = agent.MountAgent;
                    guardDriver.MountedRoute.Update(
                        mount?.Position ?? agent.Position,
                        guardDriver.CurrentHorizontalDirection);
                }
                if (guardDriver.Phase ==
                        BattleGuardFixturePhase.Calibration &&
                    sample.SpeedEvidence.PlateauReady)
                {
                    guardDriver.CalibratedPlateauSpeed =
                        sample.SpeedEvidence.RecentMedian;
                }
            }
        }
        sample.PreviousPosition = agent.Position;
        sample.HasPreviousPosition = true;
    }

    private void SamplePose(Agent agent)
    {
        sample.RiderMovementFlags = (uint)agent.MovementFlags;
        sample.RiderBodyYaw =
            GetHorizontalYawDegrees(agent.Frame.rotation.f);
        sample.RiderLookYaw =
            GetHorizontalYawDegrees(agent.LookDirection);
        sample.RiderMovementYaw =
            GetHorizontalYawDegrees(agent.GetMovementDirection());

        Agent mount = agent.MountAgent;
        if (mount == null || !mount.IsActive())
        {
            sample.MountMovementFlags = 0;
            sample.MountBodyYaw = 0f;
            sample.MountLookYaw = 0f;
            return;
        }

        sample.MountMovementFlags = (uint)mount.MovementFlags;
        sample.MountBodyYaw =
            GetHorizontalYawDegrees(mount.Frame.rotation.f);
        sample.MountLookYaw =
            GetHorizontalYawDegrees(mount.LookDirection);
    }

    private static float GetHorizontalYawDegrees(Vec2 direction)
    {
        return GetHorizontalYawDegrees(
            new Vec3(direction.X, direction.Y, 0f));
    }

    private static float GetHorizontalYawDegrees(Vec3 direction)
    {
        direction.z = 0f;
        if (direction.LengthSquared < ProgressEpsilon)
            return 0f;

        return (float)(
            Math.Atan2(direction.y, direction.x) *
            (180d / Math.PI));
    }

    private static bool IsReaction(
        Agent agent,
        int guardActionIndex)
    {
        return IsReaction(agent, 0, guardActionIndex) ||
            IsReaction(agent, 1, guardActionIndex);
    }

    private static bool IsReaction(
        Agent agent,
        int channel,
        int guardActionIndex)
    {
        Agent.ActionCodeType actionType =
            agent.GetCurrentActionType(channel);
        if (IsReaction(actionType))
            return true;

        ActionIndexCache action = agent.GetCurrentAction(channel);
        return guardActionIndex >= 0
            && action.Index >= 0
            && action.Index != guardActionIndex
            && AgentActionData.IsDefendingAction(actionType)
            && agent.GetCurrentActionStage(channel)
                == Agent.ActionStage.DefendParry;
    }

    private static bool IsReaction(Agent.ActionCodeType actionType)
    {
        return actionType == Agent.ActionCodeType.BlockedMelee ||
            actionType == Agent.ActionCodeType.ParriedMelee;
    }

    private sealed class AiPauseState
    {
        public Agent Agent { get; }
        public bool WasRegistered { get; }
        public Guid AgentId { get; }
        public string Authority { get; }
        public AgentControllerType Controller { get; }
        public Agent.MovementControlFlag MovementFlags { get; }
        public Vec2 MovementInputVector { get; }
        public Agent Target { get; }
        public Agent.WatchState WatchState { get; }
        public bool WasPaused { get; }

        public AiPauseState(
            Agent agent,
            CoopAgentInfo registeredInfo,
            AgentControllerType controller,
            Agent.MovementControlFlag movementFlags,
            Vec2 movementInputVector,
            Agent target,
            Agent.WatchState watchState,
            bool wasPaused)
        {
            Agent = agent;
            WasRegistered = registeredInfo != null;
            AgentId = registeredInfo?.AgentId ?? Guid.Empty;
            Authority = registeredInfo?.CurrentAuthority;
            Controller = controller;
            MovementFlags = movementFlags;
            MovementInputVector = movementInputVector;
            Target = target;
            WatchState = watchState;
            WasPaused = wasPaused;
        }

        public bool CanControl(INetworkAgentRegistry agentRegistry)
        {
            bool isRegistered =
                agentRegistry.TryGetAgentInfo(Agent, out CoopAgentInfo info);
            if (!WasRegistered)
                return !isRegistered;

            return isRegistered &&
                info.AgentId == AgentId &&
                info.CurrentAuthority == Authority &&
                agentRegistry.IsLocallyControlled(Agent);
        }

        public void Restore()
        {
            SetControllerDirect(Agent, Controller);
            Agent.MovementFlags = MovementFlags;
            Agent.MovementInputVector = MovementInputVector;
            Agent.SetTargetAgent(Target);
            Agent.SetWatchState(WatchState);
            Agent.SetIsAIPaused(WasPaused);
        }
    }

    private sealed class PendingGuardRestore
    {
        public GuardDriver Driver { get; }
        public Guid AgentId { get; }
        public string Authority { get; }
        public Agent Mount { get; }
        public Vec3 AgentPosition { get; }
        public Vec3 AgentLookDirection { get; }
        public Vec2 AgentMovementDirection { get; }
        public Vec3 MountPosition { get; }
        public Vec3 MountLookDirection { get; }
        public Vec2 MountMovementDirection { get; }
        public bool DrivesRestore { get; }

        public PendingGuardRestore(
            GuardDriver driver,
            Guid agentId,
            string authority,
            Agent mount,
            Vec3 agentPosition,
            Vec3 agentLookDirection,
            Vec2 agentMovementDirection,
            Vec3 mountPosition,
            Vec3 mountLookDirection,
            Vec2 mountMovementDirection,
            bool drivesRestore)
        {
            Driver = driver;
            AgentId = agentId;
            Authority = authority;
            Mount = mount;
            AgentPosition = agentPosition;
            AgentLookDirection = agentLookDirection;
            AgentMovementDirection = agentMovementDirection;
            MountPosition = mountPosition;
            MountLookDirection = mountLookDirection;
            MountMovementDirection = mountMovementDirection;
            DrivesRestore = drivesRestore;
        }
    }

    private sealed class FixtureRoles
    {
        public Guid GuardAgentId { get; }
        public string GuardAuthority { get; }
        public Guid StrikerAgentId { get; }
        public string StrikerAuthority { get; }

        public FixtureRoles(
            Guid guardAgentId,
            string guardAuthority,
            Guid strikerAgentId,
            string strikerAuthority)
        {
            GuardAgentId = guardAgentId;
            GuardAuthority = guardAuthority;
            StrikerAgentId = strikerAgentId;
            StrikerAuthority = strikerAuthority;
        }
    }

    private sealed class GuardDriver
    {
        public Agent Agent { get; }
        public Guid AgentId { get; }
        public BattleGuardFixtureMode Mode { get; set; }
        public BattleGuardFixturePhase Phase { get; set; }
        public BattleGuardFixtureDirection Direction { get; set; }
        public bool UseMovementFlagGuardInput { get; set; }
        public Agent.MovementControlFlag OriginalMovementFlags { get; }
        public Vec2 OriginalMovementInputVector { get; }
        public Agent.MovementControlFlag OriginalDefendFlags { get; }
        public Agent.GuardMode OriginalGuardMode { get; }
        public Agent OriginalMount { get; }
        public Vec3 OriginalPosition { get; }
        public Vec3 OriginalLookDirection { get; }
        public Vec2 OriginalMovementDirection { get; }
        public Vec3 OriginalMountPosition { get; }
        public Vec3 OriginalMountLookDirection { get; }
        public Vec2 OriginalMountMovementDirection { get; }
        public MissionWeapon OriginalWeapon0 { get; }
        public MissionWeapon OriginalWeapon1 { get; }
        public AgentEquipmentData OriginalWieldedEquipment { get; }
        public bool EquipmentReplaced { get; set; }
        public bool GuardArmed { get; set; }
        public bool Positioned { get; set; }
        public bool HasFixtureLane { get; set; }
        public Vec3 FixtureLane { get; set; }
        public BattleGuardMountedRoute MountedRoute { get; set; }
        public float GuardBaselineHealth { get; set; }
        public bool HasGuardBaselineHealth { get; set; }
        public float CurrentHorizontalSpeed { get; set; }
        public Vec3 CurrentHorizontalDirection { get; set; }
        public float CalibratedPlateauSpeed { get; set; } = -1f;
        public bool MountedGuardCommandActive { get; set; }
        public BattleGuardFixtureDirection MountedGuardCommandDirection
        {
            get;
            set;
        }
        public bool MountedPostNativeGuardCommandPending { get; set; }
        public bool MountedPostNativeDirectionChanged { get; set; }
        public bool MountedPresentationActionPending { get; set; }
        public int MountedPresentationAttempts { get; set; }
        public bool MountedPresentationApplied { get; set; }
        public int MountedPresentationRequestedActionIndex { get; set; } = -1;
        public int MountedPresentationImmediateActionIndex { get; set; } = -1;
        public float MountedPresentationStartProgress { get; set; } = -1f;
        public string MountedPresentationImmediateGuardMode { get; set; } =
            "None";
        public uint AppliedRiderMovementFlags { get; private set; }
        public uint AppliedNativeDefendMovementFlags { get; private set; }
        public string AppliedAction0Direction { get; private set; } = "None";
        public string AppliedAction1Direction { get; private set; } = "None";
        public int MountedGuardStateChanges { get; set; }
        public BattleGuardMountedSpeedLimiter MountedSpeedLimiter { get; } =
            new();
        public bool DrivesAgent { get; private set; }
        public int GuardActionIndex => guardActionIndex;
        private Vec3 mountedStrikeTravelDirection;
        private Vec3 mountedStrikeLookDirection;
        private Vec3 mountedStrikeLookTarget;
        private bool hasMountedStrikeLookTarget;
        private bool hasMountedStrikeDirections;
        private int guardChannel = -1;
        private int guardActionIndex = -1;
        private int guardAnimationIndex = -1;
        private bool guardActionCyclic;

        public GuardDriver(
            Guid agentId,
            BattleGuardFixtureMode mode,
            BattleGuardFixturePhase phase,
            BattleGuardFixtureDirection direction,
            bool useMovementFlagGuardInput,
            Agent agent)
        {
            Agent = agent;
            AgentId = agentId;
            Mode = mode;
            Phase = phase;
            Direction = direction;
            UseMovementFlagGuardInput = useMovementFlagGuardInput;
            MountedGuardCommandDirection = direction;
            OriginalMovementFlags = agent.MovementFlags;
            OriginalMovementInputVector = agent.MovementInputVector;
            OriginalDefendFlags =
                AgentActionData.GetDefendMovementFlags(agent.MovementFlags);
            OriginalGuardMode = agent.CurrentGuardMode;
            OriginalMount = agent.MountAgent;
            OriginalPosition = agent.Position;
            OriginalLookDirection = agent.LookDirection;
            OriginalMovementDirection = agent.GetMovementDirection();
            OriginalMountPosition = OriginalMount?.Position ?? Vec3.Zero;
            OriginalMountLookDirection =
                OriginalMount?.LookDirection ?? Vec3.Zero;
            OriginalMountMovementDirection =
                OriginalMount?.GetMovementDirection() ?? Vec2.Zero;
            OriginalWeapon0 = agent.Equipment[EquipmentIndex.Weapon0];
            OriginalWeapon1 = agent.Equipment[EquipmentIndex.Weapon1];
            OriginalWieldedEquipment = new AgentEquipmentData(agent);
        }

        public void BeginDriving()
        {
            DrivesAgent = true;
        }

        public void StopDriving()
        {
            EndMountedStrike();
            MountedSpeedLimiter.Restore();
            DrivesAgent = false;
        }

        public void ObserveAppliedInput(Agent agent)
        {
            AppliedRiderMovementFlags = (uint)agent.MovementFlags;
            AppliedNativeDefendMovementFlags =
                (uint)agent.GetDefendMovementFlag();
            AppliedAction0Direction =
                agent.GetCurrentActionDirection(0).ToString();
            AppliedAction1Direction =
                agent.GetCurrentActionDirection(1).ToString();
        }

        public void BeginMountedStrike(
            Vec3 travelDirection,
            Vec3 lookDirection,
            Vec3 lookTarget)
        {
            EndMountedStrike();
            travelDirection.z = 0f;
            lookDirection.z = 0f;
            if (travelDirection.LengthSquared < 0.0001f ||
                lookDirection.LengthSquared < 0.0001f)
            {
                return;
            }

            travelDirection.Normalize();
            lookDirection.Normalize();
            mountedStrikeTravelDirection = travelDirection;
            mountedStrikeLookDirection = lookDirection;
            mountedStrikeLookTarget = lookTarget;
            hasMountedStrikeLookTarget = true;
            hasMountedStrikeDirections = true;
        }

        public bool TryGetMountedStrikeDirections(
            Agent guard,
            out Vec3 travelDirection,
            out Vec3 lookDirection)
        {
            travelDirection = mountedStrikeTravelDirection;
            lookDirection = hasMountedStrikeLookTarget
                ? GetMountedStrikeTrackedLookDirection(
                    guard.Position,
                    mountedStrikeLookTarget,
                    mountedStrikeLookDirection)
                : mountedStrikeLookDirection;
            return hasMountedStrikeDirections;
        }

        public void EndMountedStrike()
        {
            mountedStrikeTravelDirection = Vec3.Zero;
            mountedStrikeLookDirection = Vec3.Zero;
            mountedStrikeLookTarget = Vec3.Zero;
            hasMountedStrikeLookTarget = false;
            hasMountedStrikeDirections = false;
        }

        public void CaptureGuardPresentation(SampleState sample)
        {
            guardChannel = sample.LatchedChannel;
            guardActionIndex = sample.LatchedActionIndex;
            guardAnimationIndex = sample.LatchedAnimationIndex;
            guardActionCyclic = sample.LatchedActionCyclic;
        }

        public void CopyGuardPresentationTo(SampleState sample)
        {
            sample.LatchedChannel = guardChannel;
            sample.LatchedActionIndex = guardActionIndex;
            sample.LatchedAnimationIndex = guardAnimationIndex;
            sample.LatchedActionCyclic = guardActionCyclic;
        }

        public void ResetGuardEvidence()
        {
            EndMountedStrike();
            GuardArmed = false;
            HasGuardBaselineHealth = false;
            guardChannel = -1;
            guardActionIndex = -1;
            guardAnimationIndex = -1;
            guardActionCyclic = false;
        }
    }

    internal sealed class BattleGuardMountedSpeedLimiter
    {
        internal const float MaximumSpeed = 7.5f;
        private const float SpeedLimitEpsilon = 0.001f;
        private Agent mount;
        private float originalMaximumSpeed;

        public void Apply(Agent nextMount)
        {
            if (nextMount?.IsActive() != true)
                return;
            if (!ReferenceEquals(mount, nextMount))
            {
                Restore();
                mount = nextMount;
                originalMaximumSpeed = nextMount.GetMaximumSpeedLimit();
            }
            if (Math.Abs(
                    nextMount.GetMaximumSpeedLimit() -
                    MaximumSpeed) <= SpeedLimitEpsilon)
            {
                return;
            }

            nextMount.SetMaximumSpeedLimit(
                MaximumSpeed,
                isMultiplier: false);
        }

        public void Restore()
        {
            Agent limitedMount = mount;
            mount = null;
            if (limitedMount?.IsActive() == true)
            {
                limitedMount.SetMaximumSpeedLimit(
                    originalMaximumSpeed,
                    isMultiplier: false);
            }
            originalMaximumSpeed = 0f;
        }
    }

    private sealed class StrikerDriver
    {
        public Agent Agent { get; }
        public Guid AgentId { get; }
        public Agent.MovementControlFlag OriginalMovementFlags { get; }
        public Agent.MovementControlFlag OriginalDefendFlags { get; }
        public BattleGuardFixtureMode Mode { get; }
        public string ExpectedWeaponId =>
            GetFixtureStrikerWeaponId(Mode);
        public int ExpectedWeaponUsageIndex =>
            GetFixtureStrikerWeaponUsageIndex(Mode);
        public MissionWeapon OriginalWeapon0 { get; }
        public MissionWeapon OriginalWeapon1 { get; }
        public AgentEquipmentData OriginalWieldedEquipment { get; }
        public Agent OriginalTarget { get; }
        public Agent.WatchState OriginalWatchState { get; }
        public AgentControllerType OriginalController { get; }
        public bool WasPaused { get; }
        public Vec3 OriginalPosition { get; }
        public Vec3 OriginalLookDirection { get; }
        public bool EquipmentReplaced { get; set; }
        public bool FixtureWeaponAvailable { get; private set; }
        public bool FixtureWeaponReady { get; private set; }
        public int FixtureWieldRequests { get; private set; }
        public int FixtureOffHandSheathRequests { get; private set; }
        public EquipmentIndex FixtureMainHandIndex { get; private set; } =
            EquipmentIndex.None;
        public EquipmentIndex FixtureOffHandIndex { get; private set; } =
            EquipmentIndex.None;
        public int FixtureMainHandUsageIndex { get; private set; } = -1;
        public string FixtureMainHandItemId { get; private set; }
        public bool HasAttackDriver => attackDriver != null;
        public string AttackState =>
            attackDriver?.State ?? "none";
        public int AttackAttempts =>
            attackDriver?.Attempts ?? 0;
        public bool StrikeSpeedReady =>
            attackDriver?.SpeedReady ?? false;
        public bool StrikeRunwayReady =>
            attackDriver?.RunwayReady ?? false;
        public bool StrikeTravelAligned =>
            attackDriver?.TravelAligned ?? false;
        public float StrikeTravelLookAlignment =>
            attackDriver?.TravelLookAlignment ?? -1f;
        public float StrikeReadySeconds =>
            attackDriver?.ReadySeconds ?? 0f;
        public string StrikeStageRoute =>
            attackDriver?.StageRoute ?? "none";
        public float StrikeStageProgress =>
            attackDriver?.StageProgress ?? 0f;
        public float StrikeStageLateral =>
            attackDriver?.StageLateral ?? 0f;
        public float StrikeGuardLookAlignment =>
            attackDriver?.GuardLookAlignment ?? -1f;
        public float StrikeRouteAlignment =>
            attackDriver?.RouteAlignment ?? -1f;
        public float StrikeStandoff =>
            attackDriver?.Standoff ?? -1f;
        public float StrikeProfileReleaseLead =>
            attackDriver?.ProfileReleaseLead ?? -1f;
        public float StrikeReleaseDistance =>
            attackDriver?.ReleaseDistance ?? -1f;
        public float StrikeReleaseSpeed =>
            attackDriver?.ReleaseSpeed ?? -1f;
        public float StrikeReleaseLead =>
            attackDriver?.ReleaseLead ?? -1f;
        public string StrikeReleaseActionStage =>
            attackDriver?.ReleaseActionStage ?? "none";
        public string StrikeReleaseActionDirection =>
            attackDriver?.ReleaseActionDirection ?? "none";
        public int StrikeReleaseActionChannel =>
            attackDriver?.ReleaseActionChannel ?? -1;
        public float StrikeReleaseActionProgress =>
            attackDriver?.ReleaseActionProgress ?? -1f;
        public bool StrikeReleasedFromReady =>
            attackDriver?.ReleasedFromReady ?? false;
        public bool StrikeReleaseObserved =>
            attackDriver?.ReleaseObserved ?? false;
        public int StrikeGeometrySamples =>
            attackDriver?.GeometrySamples ?? 0;
        public float StrikeCurrentGuardLookAlignment =>
            attackDriver?.CurrentGuardLookAlignment ?? -1f;
        public bool StrikeReplicatedLookObserved =>
            attackDriver?.ReplicatedLookObserved ?? false;
        public float StrikeReplicatedLookAlignment =>
            attackDriver?.ReplicatedLookAlignment ?? -1f;
        public long StrikeStagedLookUpdateSequence =>
            attackDriver?.StagedLookUpdateSequence ?? 0;
        public long StrikeCurrentLookUpdateSequence =>
            attackDriver?.CurrentLookUpdateSequence ?? 0;
        public float StrikeCurrentStandoff =>
            attackDriver?.CurrentStandoff ?? -1f;
        public float StrikeClosestStandoff =>
            attackDriver?.ClosestStandoff ?? -1f;
        public int StrikeAttemptHitCount =>
            attackDriver?.AttemptHitCount ?? 0;
        public int StrikeLastHitAttempt =>
            attackDriver?.LastHitAttempt ?? 0;
        public float StrikeLastHitGuardLookAlignment =>
            attackDriver?.LastHitGuardLookAlignment ?? -1f;
        public float StrikeLastHitStandoff =>
            attackDriver?.LastHitStandoff ?? -1f;
        public int StrikeFirstGuardHitAttempt =>
            attackDriver?.FirstGuardHitAttempt ?? 0;
        public float StrikeFirstGuardHitAlignment =>
            attackDriver?.FirstGuardHitAlignment ?? -1f;
        public float StrikeFirstGuardHitStandoff =>
            attackDriver?.FirstGuardHitStandoff ?? -1f;
        public bool StrikeFirstGuardHitBlocked =>
            attackDriver?.FirstGuardHitBlocked ?? false;
        public float StrikeFirstGuardHitDamagedHp =>
            attackDriver?.FirstGuardHitDamagedHp ?? 0f;
        public int HitCount { get; private set; }
        public string LastHitTarget { get; private set; } = "none";
        public string LastHitCollision { get; private set; } = "none";
        public bool LastHitBlocked { get; private set; }
        public float LastHitDamagedHp { get; private set; }
        public float OriginalSwingSpeedMultiplier { get; }
        public float CurrentSwingSpeedMultiplier =>
            Agent?.AgentDrivenProperties?.SwingSpeedMultiplier ?? -1f;
        private readonly bool originalHasOnAiInputSetCallback;
        private int lastFixtureWieldRequestAttempt = -1;
        private bool swingSpeedNormalized;
        private GuardInterceptionStrikeComponent attackDriver;

        public StrikerDriver(
            Guid agentId,
            Agent agent,
            BattleGuardFixtureMode mode)
        {
            Agent = agent;
            AgentId = agentId;
            Mode = mode;
            OriginalMovementFlags = agent.MovementFlags;
            OriginalDefendFlags =
                AgentActionData.GetDefendMovementFlags(agent.MovementFlags);
            OriginalWeapon0 = agent.Equipment[EquipmentIndex.Weapon0];
            OriginalWeapon1 = agent.Equipment[EquipmentIndex.Weapon1];
            OriginalWieldedEquipment = new AgentEquipmentData(agent);
            OriginalTarget = agent.GetTargetAgent();
            OriginalWatchState = agent.CurrentWatchState;
            OriginalController = agent.Controller;
            WasPaused = agent.IsPaused;
            OriginalPosition = agent.Position;
            OriginalLookDirection = agent.LookDirection;
            OriginalSwingSpeedMultiplier =
                agent.AgentDrivenProperties?.SwingSpeedMultiplier ?? -1f;
            originalHasOnAiInputSetCallback =
                agent.GetHasOnAiInputSetCallback();
        }

        public bool EnsureFixtureWeaponReady(
            Agent agent,
            int attempt,
            bool retryDue)
        {
            ObserveFixtureWeaponState(agent);
            if (ShouldRequestFixtureStrikerWield(
                    FixtureWeaponAvailable,
                    FixtureWeaponReady,
                    lastFixtureWieldRequestAttempt,
                    attempt,
                    retryDue))
            {
                lastFixtureWieldRequestAttempt = attempt;
                FixtureWieldRequests++;
                if (ShouldSheathFixtureStrikerOffHand(
                        FixtureWeaponAvailable,
                        FixtureOffHandIndex))
                {
                    FixtureOffHandSheathRequests++;
                    agent.TryToSheathWeaponInHand(
                        Agent.HandIndex.OffHand,
                        Agent.WeaponWieldActionType.Instant);
                }
                agent.SetUsageIndexOfWeaponInSlotAsClient(
                    EquipmentIndex.Weapon0,
                    ExpectedWeaponUsageIndex);
                agent.TryToWieldWeaponInSlot(
                    EquipmentIndex.Weapon0,
                    Agent.WeaponWieldActionType.Instant,
                    false);
            }

            ObserveFixtureWeaponState(agent);
            return FixtureWeaponReady;
        }

        public void ObserveFixtureWeaponState(Agent agent)
        {
            if (!AgentEquipmentData.HasSafeWeaponSlots(agent?.Equipment))
            {
                FixtureWeaponAvailable = false;
                FixtureMainHandIndex = EquipmentIndex.None;
                FixtureOffHandIndex = EquipmentIndex.None;
                FixtureMainHandUsageIndex = -1;
                FixtureMainHandItemId = null;
                FixtureWeaponReady = false;
                return;
            }

            MissionWeapon fixtureWeapon =
                agent.Equipment[EquipmentIndex.Weapon0];
            FixtureWeaponAvailable =
                fixtureWeapon.Item?.StringId == ExpectedWeaponId &&
                ExpectedWeaponUsageIndex >= 0 &&
                ExpectedWeaponUsageIndex < fixtureWeapon.WeaponsCount;
            FixtureMainHandIndex =
                agent.GetPrimaryWieldedItemIndex();
            FixtureOffHandIndex =
                agent.GetOffhandWieldedItemIndex();
            FixtureMainHandUsageIndex = -1;
            FixtureMainHandItemId = null;
            if (FixtureMainHandIndex >=
                    EquipmentIndex.WeaponItemBeginSlot &&
                FixtureMainHandIndex <
                    EquipmentIndex.NumAllWeaponSlots)
            {
                MissionWeapon mainHandWeapon =
                    agent.Equipment[FixtureMainHandIndex];
                FixtureMainHandUsageIndex =
                    mainHandWeapon.CurrentUsageIndex;
                FixtureMainHandItemId =
                    mainHandWeapon.Item?.StringId;
            }

            FixtureWeaponReady =
                IsFixtureStrikerWieldState(
                    Mode,
                    FixtureMainHandIndex,
                    FixtureOffHandIndex,
                    FixtureMainHandUsageIndex,
                    FixtureMainHandItemId);
        }

        public void ApplyFixtureSwingSpeed(
            Agent agent,
            float multiplier)
        {
            AgentDrivenProperties properties =
                agent?.AgentDrivenProperties;
            if (properties == null)
                return;
            if (Math.Abs(
                    properties.SwingSpeedMultiplier -
                    multiplier) > 0.001f)
            {
                properties.SwingSpeedMultiplier = multiplier;
                agent.UpdateCustomDrivenProperties();
            }
            swingSpeedNormalized = true;
        }

        public void RestoreSwingSpeed(Agent agent)
        {
            AgentDrivenProperties properties =
                agent?.AgentDrivenProperties;
            if (!swingSpeedNormalized ||
                properties == null ||
                OriginalSwingSpeedMultiplier < 0f)
            {
                return;
            }

            properties.SwingSpeedMultiplier =
                OriginalSwingSpeedMultiplier;
            agent.UpdateCustomDrivenProperties();
            swingSpeedNormalized = false;
        }

        public void AttachAttackDriver(
            Agent agent,
            Agent guard,
            GuardDriver guardDriver,
            IAgentPositionInterpolator interpolator,
            Action<Vec3, Vec3, Vec3> mountedStrikeStarted,
            Action mountedStrikeEnded)
        {
            if (attackDriver != null)
                return;

            attackDriver = new GuardInterceptionStrikeComponent(
                agent,
                guard,
                guardDriver,
                this,
                interpolator,
                mountedStrikeStarted,
                mountedStrikeEnded);
            agent.AddComponent(attackDriver);
            agent.SetHasOnAiInputSetCallback(true);
        }

        public void StopAfterBlockedHit()
        {
            attackDriver?.StopAfterBlockedHit();
        }

        public void ObserveHit(
            Agent affectedAgent,
            bool isBlocked,
            CombatCollisionResult collisionResult,
            float damagedHp)
        {
            HitCount++;
            string targetId =
                affectedAgent?.Character?.StringId ??
                affectedAgent?.Name?.ToString() ??
                "unknown";
            LastHitTarget = affectedAgent?.IsMount == true
                ? $"mount:{targetId}"
                : targetId;
            LastHitCollision = collisionResult.ToString();
            LastHitBlocked = isBlocked;
            LastHitDamagedHp = damagedHp;
            attackDriver?.ObserveHit(
                affectedAgent,
                isBlocked,
                damagedHp);
        }

        public void DetachAttackDriver(Agent agent)
        {
            if (attackDriver == null)
                return;

            attackDriver.Stop();
            agent.RemoveComponent(attackDriver);
            agent.SetHasOnAiInputSetCallback(
                originalHasOnAiInputSetCallback);
            attackDriver = null;
        }
    }

    private sealed class GuardInterceptionStrikeComponent : AgentComponent
    {
        private const float OutcomeWaitSeconds = 1.25f;
        private const float MaximumOutcomeWaitSeconds = 2.5f;
        private const float RetryRecoverySeconds = 0.5f;
        private const float FixtureWieldRetrySeconds = 0.5f;
        private const float MountedSpeedReadySeconds = 0.5f;
        private const int MaximumAttempts = 5;

        public string State => state.ToString();
        public int Attempts { get; private set; }
        public bool SpeedReady { get; private set; }
        public bool RunwayReady { get; private set; }
        public bool TravelAligned { get; private set; }
        public float TravelLookAlignment { get; private set; } = -1f;
        public float ReadySeconds =>
            state == InterceptionState.WaitingForSpeed
                ? stateElapsed
                : 0f;
        public string StageRoute { get; private set; } = "none";
        public float StageProgress { get; private set; }
        public float StageLateral { get; private set; }
        public float GuardLookAlignment { get; private set; } = -1f;
        public float RouteAlignment { get; private set; } = -1f;
        public float Standoff { get; private set; } = -1f;
        public float ProfileReleaseLead { get; private set; } = -1f;
        public float ReleaseDistance { get; private set; } = -1f;
        public float ReleaseSpeed { get; private set; } = -1f;
        public float ReleaseLead { get; private set; } = -1f;
        public string ReleaseActionStage { get; private set; } = "none";
        public string ReleaseActionDirection { get; private set; } = "none";
        public int ReleaseActionChannel { get; private set; } = -1;
        public float ReleaseActionProgress { get; private set; } = -1f;
        public bool ReleasedFromReady { get; private set; }
        public bool ReleaseObserved { get; private set; }
        public int GeometrySamples { get; private set; }
        public float CurrentGuardLookAlignment { get; private set; } = -1f;
        public bool ReplicatedLookObserved { get; private set; }
        public float ReplicatedLookAlignment { get; private set; } = -1f;
        public long StagedLookUpdateSequence { get; private set; }
        public long CurrentLookUpdateSequence { get; private set; }
        public float CurrentStandoff { get; private set; } = -1f;
        public float ClosestStandoff { get; private set; } = -1f;
        public int AttemptHitCount =>
            Math.Max(0, hitEvidence.HitCount - attemptStartHitCount);
        public int LastHitAttempt { get; private set; }
        public float LastHitGuardLookAlignment { get; private set; } = -1f;
        public float LastHitStandoff { get; private set; } = -1f;
        public int FirstGuardHitAttempt { get; private set; }
        public float FirstGuardHitAlignment { get; private set; } = -1f;
        public float FirstGuardHitStandoff { get; private set; } = -1f;
        public bool FirstGuardHitBlocked { get; private set; }
        public float FirstGuardHitDamagedHp { get; private set; }

        private readonly Agent striker;
        private readonly Agent guard;
        private readonly GuardDriver guardDriver;
        private readonly StrikerDriver hitEvidence;
        private readonly IAgentPositionInterpolator interpolator;
        private readonly Action<Vec3, Vec3, Vec3> mountedStrikeStarted;
        private readonly Action mountedStrikeEnded;
        private InterceptionState state = InterceptionState.WaitingForSpeed;
        private Vec3 laneDirection;
        private Vec3 contactPoint;
        private float stateElapsed;
        private float chargeElapsed;
        private Vec3 stagedGuardLookDirection;
        private Vec3 stagedGuardLookTarget;
        private long stagedGuardLookUpdateSequence;
        private int attemptStartHitCount;
        private bool mountedStrikeLifecycleActive;

        public GuardInterceptionStrikeComponent(
            Agent striker,
            Agent guard,
            GuardDriver guardDriver,
            StrikerDriver hitEvidence,
            IAgentPositionInterpolator interpolator,
            Action<Vec3, Vec3, Vec3> mountedStrikeStarted,
            Action mountedStrikeEnded)
            : base(striker)
        {
            this.striker = striker;
            this.guard = guard;
            this.guardDriver = guardDriver;
            this.hitEvidence = hitEvidence;
            this.interpolator = interpolator;
            this.mountedStrikeStarted = mountedStrikeStarted;
            this.mountedStrikeEnded = mountedStrikeEnded;
        }

        public override void OnTick(float dt)
        {
            if (state == InterceptionState.Succeeded ||
                state == InterceptionState.Exhausted)
            {
                return;
            }
            if (!IsActiveMissionAgent(striker) ||
                !IsActiveMissionAgent(guard))
            {
                EndMountedStrikeLifecycle();
                state = InterceptionState.Exhausted;
                return;
            }
            ObserveNativeAttack();
            if (guardDriver.Mode == BattleGuardFixtureMode.Mounted &&
                (state == InterceptionState.Charging ||
                 state == InterceptionState.Released))
            {
                ObserveMountedGeometry();
            }
            float elapsedStep = Math.Max(0f, dt);
            stateElapsed += elapsedStep;
            if (state == InterceptionState.Charging)
                chargeElapsed += elapsedStep;
            switch (state)
            {
                case InterceptionState.WaitingForSpeed:
                    TickWaitingForSpeed();
                    break;
                case InterceptionState.Charging:
                    TickCharging();
                    break;
                case InterceptionState.Released:
                    TickReleased();
                    break;
                case InterceptionState.Recovery:
                    if (stateElapsed >= RetryRecoverySeconds)
                        BeginNextAttempt();
                    break;
            }
        }

        public override void OnAIInputSet(
            ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag,
            ref Vec2 inputVector)
        {
            eventFlag = Agent.EventControlFlag.None;
            movementFlag = state == InterceptionState.Charging
                ? GetAttackFlagForGuard(guardDriver.Direction)
                : Agent.MovementControlFlag.None;
            inputVector = Vec2.Zero;
        }

        public void StopAfterBlockedHit()
        {
            ObserveNativeAttack();
            if (guardDriver.Mode == BattleGuardFixtureMode.Mounted)
                ObserveMountedGeometry();
            EndMountedStrikeLifecycle();
            state = InterceptionState.Succeeded;
            stateElapsed = 0f;
        }

        public void Stop()
        {
            EndMountedStrikeLifecycle();
        }

        public void ObserveHit(
            Agent affectedAgent,
            bool isBlocked,
            float damagedHp)
        {
            LastHitAttempt = Attempts;
            if (guardDriver.Mode == BattleGuardFixtureMode.Mounted)
                ObserveMountedGeometry();
            LastHitGuardLookAlignment = CurrentGuardLookAlignment;
            LastHitStandoff = CurrentStandoff;
            if (FirstGuardHitAttempt > 0 ||
                !ReferenceEquals(affectedAgent, guard))
            {
                return;
            }

            FirstGuardHitAttempt = Attempts;
            FirstGuardHitAlignment = CurrentGuardLookAlignment;
            FirstGuardHitStandoff = CurrentStandoff;
            FirstGuardHitBlocked = isBlocked;
            FirstGuardHitDamagedHp = damagedHp;
        }

        private void TickWaitingForSpeed()
        {
            SpeedReady = false;
            RunwayReady = false;
            TravelAligned = false;
            TravelLookAlignment = -1f;
            if (Attempts >= MaximumAttempts)
                return;
            bool retryDue =
                stateElapsed >= FixtureWieldRetrySeconds;
            int wieldRequests = hitEvidence.FixtureWieldRequests;
            bool weaponReady =
                hitEvidence.EnsureFixtureWeaponReady(
                    striker,
                    Attempts,
                    retryDue);
            if (hitEvidence.FixtureWieldRequests != wieldRequests)
            {
                stateElapsed = 0f;
                return;
            }
            if (!weaponReady)
                return;
            if (TryGetNativeAttack(out _, out _, out _, out _))
            {
                stateElapsed = 0f;
                return;
            }
            if (guardDriver.Mode != BattleGuardFixtureMode.Mounted)
            {
                SpeedReady = true;
                RunwayReady = true;
                TravelAligned = true;
                TravelLookAlignment = 1f;
                StageAttempt();
                return;
            }
            // Attack is owner-gated, so collision authority trusts the route and shared speed cap.
            float speedBaseline =
                GetMountedStrikeSpeedBaseline(
                    guardDriver.CalibratedPlateauSpeed,
                    guardDriver.DrivesAgent);
            SpeedReady = HasMountedStrikeSpeed(
                guardDriver.CurrentHorizontalSpeed,
                speedBaseline);
            RunwayReady =
                HasMountedStrikeStagingRunway(
                    guardDriver.MountedRoute,
                    guardDriver.DrivesAgent);
            TravelLookAlignment =
                GetMountedStrikeTravelAlignment(
                    guardDriver.CurrentHorizontalDirection,
                    guard.LookDirection);
            TravelAligned =
                HasMountedStrikeStagingAlignment(
                    guardDriver.CurrentHorizontalDirection,
                    guard.LookDirection,
                    guardDriver.DrivesAgent);
            if (!guard.HasMount ||
                !SpeedReady ||
                !RunwayReady ||
                !TravelAligned)
            {
                stateElapsed = 0f;
                return;
            }

            if (stateElapsed >= MountedSpeedReadySeconds)
                StageAttempt();
        }

        private void StageAttempt()
        {
            Attempts++;
            stateElapsed = 0f;
            chargeElapsed = 0f;
            ResetAttemptEvidence();
            if (guardDriver.Mode == BattleGuardFixtureMode.Mounted)
                StageMountedInterception();
            else
                StageFootStrike();
            state = InterceptionState.Charging;
        }

        private void ResetAttemptEvidence()
        {
            GuardLookAlignment = -1f;
            RouteAlignment = -1f;
            Standoff = -1f;
            ProfileReleaseLead = -1f;
            ReleaseDistance = -1f;
            ReleaseSpeed = -1f;
            ReleaseLead = -1f;
            ReleaseActionStage = "none";
            ReleaseActionDirection = "none";
            ReleaseActionChannel = -1;
            ReleaseActionProgress = -1f;
            ReleasedFromReady = false;
            ReleaseObserved = false;
            GeometrySamples = 0;
            CurrentGuardLookAlignment = -1f;
            ReplicatedLookObserved = false;
            ReplicatedLookAlignment = -1f;
            StagedLookUpdateSequence = 0;
            CurrentLookUpdateSequence = 0;
            CurrentStandoff = -1f;
            ClosestStandoff = -1f;
            attemptStartHitCount = hitEvidence.HitCount;
        }

        private void StageMountedInterception()
        {
            laneDirection = GetHorizontalDirection(
                guardDriver.CurrentHorizontalDirection);
            Vec3 guardedLookDirection =
                GetMountedStrikeGuardedLookDirection(
                    laneDirection);
            stagedGuardLookDirection = guardedLookDirection;
            interpolator.TryGetTargetFrame(
                guard,
                out _,
                out _,
                out stagedGuardLookUpdateSequence);
            StagedLookUpdateSequence =
                stagedGuardLookUpdateSequence;
            StageRoute =
                guardDriver.MountedRoute?.State ?? "none";
            StageProgress =
                guardDriver.MountedRoute?.Progress ?? 0f;
            StageLateral =
                guardDriver.MountedRoute?.LateralOffset ?? 0f;
            float speed = Math.Max(
                1f,
                guardDriver.CurrentHorizontalSpeed);
            float leadDistance = Math.Max(
                MountedStrikeMinimumLeadDistance,
                Math.Min(
                    MountedStrikeMaximumLeadDistance,
                    speed * 0.85f));
            contactPoint = GetMountedStrikeContactPoint(
                guard.Position,
                laneDirection,
                leadDistance);

            Vec3 strikeTargetPoint =
                GetMountedStrikeTargetPoint(
                    contactPoint,
                    laneDirection);
            Vec3 strikerPosition =
                GetMountedStrikerPosition(
                    strikeTargetPoint,
                    laneDirection);
            SetGroundHeight(ref strikerPosition);
            stagedGuardLookTarget = strikerPosition;
            striker.TeleportToPosition(strikerPosition);
            FacePoint(striker, strikeTargetPoint);
            guardDriver.BeginMountedStrike(
                laneDirection,
                guardedLookDirection,
                strikerPosition);
            mountedStrikeLifecycleActive = true;
            mountedStrikeStarted?.Invoke(
                laneDirection,
                guardedLookDirection,
                strikerPosition);
            ProfileReleaseLead =
                GetMountedStrikeReleaseLeadSeconds(Attempts);

            Vec3 placementOffset =
                striker.Position - contactPoint;
            placementOffset.z = 0f;
            Standoff = placementOffset.AsVec2.Length;
            if (Standoff > 0.0001f)
                placementOffset.Normalize();
            else
                placementOffset = guardedLookDirection;
            GuardLookAlignment =
                Vec3.DotProduct(
                    placementOffset,
                    guardedLookDirection);
            RouteAlignment =
                Vec3.DotProduct(placementOffset, laneDirection);
        }

        private void StageFootStrike()
        {
            laneDirection = GetHorizontalDirection(guard.LookDirection);
            contactPoint = guard.Position;
            Vec3 strikerPosition =
                contactPoint + (laneDirection * 1.25f);
            SetGroundHeight(ref strikerPosition);
            striker.TeleportToPosition(strikerPosition);
            FacePoint(striker, contactPoint);
        }

        private void TickCharging()
        {
            if (guardDriver.Mode == BattleGuardFixtureMode.Mounted)
            {
                ObserveReplicatedMountedStrikeLook();
                if (HasMountedStrikeChargeTimedOut(chargeElapsed))
                {
                    if (ReplicatedLookObserved &&
                        ShouldReleaseTimedOutMountedStrike(
                            CurrentGuardLookAlignment,
                            stateElapsed))
                    {
                        ReleaseAttack();
                    }
                    else
                    {
                        RecordMiss();
                    }
                    return;
                }
                if (!ReplicatedLookObserved ||
                    !HasMountedStrikeContactAlignment(
                        CurrentGuardLookAlignment))
                {
                    stateElapsed = 0f;
                    return;
                }
            }
            if (stateElapsed < FixtureAttackPressSeconds)
                return;
            if (guardDriver.Mode != BattleGuardFixtureMode.Mounted)
            {
                ReleaseAttack();
                return;
            }

            float longitudinalDistance = GetLongitudinalDistance();
            if (ShouldReleaseMountedStrike(
                    longitudinalDistance,
                    guardDriver.CurrentHorizontalSpeed,
                    chargeElapsed,
                    ProfileReleaseLead) &&
                TryGetNativeAttack(
                      out _,
                      out Agent.ActionStage actionStage,
                      out _,
                      out _) &&
                IsNativeAttackReady(actionStage))
            {
                ReleaseAttack();
            }
        }

        private void ObserveReplicatedMountedStrikeLook()
        {
            bool hasReplicatedLook =
                interpolator.TryGetTargetFrame(
                    guard,
                    out Vec3 replicatedPosition,
                    out Vec3 replicatedLookDirection,
                    out long currentUpdateSequence);
            CurrentLookUpdateSequence = currentUpdateSequence;
            Vec3 expectedLookDirection =
                GetMountedStrikeTrackedLookDirection(
                    replicatedPosition,
                    stagedGuardLookTarget,
                    stagedGuardLookDirection);
            ReplicatedLookAlignment = hasReplicatedLook
                ? GetMountedStrikeTravelAlignment(
                    expectedLookDirection,
                    replicatedLookDirection)
                : -1f;
            ReplicatedLookObserved =
                HasObservedReplicatedMountedStrikeLook(
                    guardDriver.DrivesAgent,
                    hasReplicatedLook,
                    stagedGuardLookUpdateSequence,
                    currentUpdateSequence,
                    ReplicatedLookAlignment);
        }

        private void TickReleased()
        {
            bool passedContact =
                guardDriver.Mode != BattleGuardFixtureMode.Mounted ||
                GetLongitudinalDistance() < -1f;
            if ((stateElapsed >= OutcomeWaitSeconds && passedContact) ||
                stateElapsed >= MaximumOutcomeWaitSeconds)
            {
                RecordMiss();
            }
        }

        private void ReleaseAttack()
        {
            if (guardDriver.Mode == BattleGuardFixtureMode.Mounted)
            {
                ReleaseDistance = GetLongitudinalDistance();
                ReleaseSpeed =
                    guardDriver.CurrentHorizontalSpeed;
                ReleaseLead =
                    ReleaseDistance /
                    Math.Max(0.1f, ReleaseSpeed);
            }
            CaptureReleaseAction();
            state = InterceptionState.Released;
            stateElapsed = 0f;
        }

        private void ObserveMountedGeometry()
        {
            Vec3 guardedLookDirection =
                GetHorizontalDirection(guard.LookDirection);
            Vec3 standoff = striker.Position - guard.Position;
            standoff.z = 0f;
            CurrentStandoff = standoff.AsVec2.Length;
            if (CurrentStandoff > 0.0001f)
                standoff.Normalize();
            else
                standoff = guardedLookDirection;
            CurrentGuardLookAlignment =
                Vec3.DotProduct(standoff, guardedLookDirection);
            GeometrySamples++;
            if (ClosestStandoff < 0f ||
                CurrentStandoff < ClosestStandoff)
            {
                ClosestStandoff = CurrentStandoff;
            }
        }

        private void CaptureReleaseAction()
        {
            if (!TryGetNativeAttack(
                    out int channel,
                    out Agent.ActionStage actionStage,
                    out Agent.UsageDirection actionDirection,
                    out float actionProgress))
            {
                return;
            }

            ReleaseActionChannel = channel;
            ReleaseActionStage = actionStage.ToString();
            ReleaseActionDirection = actionDirection.ToString();
            ReleaseActionProgress = actionProgress;
            ReleasedFromReady = IsNativeAttackReady(actionStage);
        }

        private void ObserveNativeAttack()
        {
            if (!TryGetNativeAttack(
                    out _,
                    out Agent.ActionStage actionStage,
                    out _,
                    out _))
            {
                return;
            }

            if (actionStage == Agent.ActionStage.AttackRelease)
            {
                ReleaseObserved = true;
            }
        }

        private bool TryGetNativeAttack(
            out int channel,
            out Agent.ActionStage actionStage,
            out Agent.UsageDirection actionDirection,
            out float actionProgress)
        {
            for (int candidate = 1; candidate >= 0; candidate--)
            {
                Agent.ActionStage candidateStage =
                    striker.GetCurrentActionStage(candidate);
                Agent.UsageDirection candidateDirection =
                    striker.GetCurrentActionDirection(candidate);
                int directionValue = (int)candidateDirection;
                if (directionValue <
                        (int)Agent.UsageDirection.AttackBegin ||
                    directionValue >=
                        (int)Agent.UsageDirection.AttackEnd ||
                    candidateStage == Agent.ActionStage.None)
                {
                    continue;
                }

                channel = candidate;
                actionStage = candidateStage;
                actionDirection = candidateDirection;
                actionProgress =
                    striker.GetCurrentActionProgress(candidate);
                return true;
            }

            channel = -1;
            actionStage = Agent.ActionStage.None;
            actionDirection = Agent.UsageDirection.None;
            actionProgress = -1f;
            return false;
        }

        private void EndMountedStrikeLifecycle()
        {
            guardDriver.EndMountedStrike();
            if (!mountedStrikeLifecycleActive)
                return;

            mountedStrikeLifecycleActive = false;
            mountedStrikeEnded?.Invoke();
        }

        private void RecordMiss()
        {
            EndMountedStrikeLifecycle();
            state = InterceptionState.Recovery;
            stateElapsed = 0f;
        }

        private void BeginNextAttempt()
        {
            if (Attempts >= MaximumAttempts)
            {
                EndMountedStrikeLifecycle();
                state = InterceptionState.Exhausted;
                stateElapsed = 0f;
                return;
            }

            state = InterceptionState.WaitingForSpeed;
            stateElapsed = 0f;
        }

        private float GetLongitudinalDistance()
        {
            Vec3 delta = contactPoint - guard.Position;
            delta.z = 0f;
            return Vec3.DotProduct(delta, laneDirection);
        }

        private static Vec3 GetHorizontalDirection(Vec3 direction)
        {
            direction.z = 0f;
            if (direction.LengthSquared < 0.0001f)
                direction = new Vec3(0f, 1f, 0f);
            direction.Normalize();
            return direction;
        }

        private static void SetGroundHeight(ref Vec3 position)
        {
            Scene scene = Mission.Current?.Scene;
            if (scene != null)
                position.z = scene.GetGroundHeightAtPosition(position);
        }

        private static void FacePoint(Agent agent, Vec3 point)
        {
            Vec3 lookDirection = point - agent.Position;
            lookDirection.z = 0f;
            if (lookDirection.LengthSquared < 0.0001f)
                return;

            lookDirection.Normalize();
            agent.LookDirection = lookDirection;
        }

        private enum InterceptionState
        {
            WaitingForSpeed,
            Charging,
            Released,
            Recovery,
            Succeeded,
            Exhausted
        }
    }

    private sealed class SampleState
    {
        public Guid AgentId;
        public int Samples;
        public int MissingSamples;
        public int VisibleSamples;
        public bool Mounted;
        public float HorizontalSpeed;
        public float PeakHorizontalSpeed;
        public uint RiderMovementFlags;
        public uint MountMovementFlags;
        public uint NativeDefendMovementFlags;
        public float RiderBodyYaw;
        public float RiderLookYaw;
        public float RiderMovementYaw;
        public float MountBodyYaw;
        public float MountLookYaw;
        public readonly BattleGuardSpeedEvidence SpeedEvidence = new();
        public float Health;
        public float BaselineHealth;
        public bool HasBaselineHealth;
        public float HealthDelta;
        public int MainHandIndex = -2;
        public int OffHandIndex = -2;
        public int MainHandUsageIndex = -1;
        public string MainHandItemId;
        public bool GuardEquipmentReady;
        public string GuardMode = "None";
        public int RawActionIndex = -1;
        public string RawActionName;
        public string RawActionType = "None";
        public string RawActionDirection = "None";
        public string Action0Direction = "None";
        public string Action1Direction = "None";
        public float RawProgress = -1f;
        public int LatchedChannel = -1;
        public int LatchedActionIndex = -1;
        public int LatchedAnimationIndex = -1;
        public bool LatchedActionCyclic;
        public int VisualActionIndex = -1;
        public int VisualAnimationIndex = -1;
        public float VisualProgress = -1f;
        public bool GuardVisible;
        public float CurrentMissingGapSeconds;
        public float MaxMissingGapSeconds;
        public bool Reaction;
        public int ReactionSamples;
        public int ReactionActionIndex = -1;
        public int ReactionAnimationIndex = -1;
        public bool ReceivedReactionActive;
        public int CurrentReactionChannel = -1;
        public int CurrentReactionActionIndex = -1;
        public int CurrentReactionAnimationIndex = -1;
        public int ExpectedReactionChannel = -1;
        public int ExpectedReactionActionIndex = -1;
        public int ExpectedReactionAnimationIndex = -1;
        public bool ExpectedReactionCyclic;
        public float ReceivedReactionProgress = -1f;
        public bool ReceivedReactionCyclic;
        public int PairBlockedHitCount;
        public string PairCollisionResult = "none";
        public float PairBlockedDamagedHp;
        public readonly HashSet<int> VisualAnimations = new();
        public readonly Dictionary<int, int> CurrentVisualRuns = new();
        public readonly Dictionary<int, int> MaxVisualRuns = new();
        public int VisualProgressAdvances;
        public int VisualProgressStalls;
        public int VisualProgressResets;
        public readonly BattleGuardAnimationEvidence
            PreReplayAnimationEvidence = new();
        public readonly BattleGuardAnimationEvidence AnimationEvidence = new();
        public readonly BattleGuardReplayEvidence ReplayEvidence = new();
        public readonly BattleGuardContinuityEvidence
            GuardContinuityEvidence = new();
        public readonly BattleGuardReactionEvidence ReactionEvidence = new();
        public readonly BattleGuardVisualRootEvidence
            VisualRootEvidence = new();
        public Vec3 PreviousPosition;
        public bool HasPreviousPosition;
        public float PreviousVisualProgress;
        public bool HasPreviousVisualProgress;

        public void ObserveReceivedReaction(
            bool active,
            int channel,
            int actionIndex,
            int animationIndex,
            float progress,
            bool isCyclic)
        {
            ReceivedReactionActive = active;
            CurrentReactionChannel = active ? channel : -1;
            CurrentReactionActionIndex = active ? actionIndex : -1;
            CurrentReactionAnimationIndex = active ? animationIndex : -1;
            ReceivedReactionProgress = active ? progress : -1f;
            ReceivedReactionCyclic = active && isCyclic;
            if (!active || ExpectedReactionActionIndex >= 0)
                return;

            ExpectedReactionChannel = channel;
            ExpectedReactionActionIndex = actionIndex;
            ExpectedReactionCyclic = isCyclic;
            ReactionActionIndex = actionIndex;
        }

        public void ObserveReactionVisual(int animationIndex)
        {
            if (animationIndex < 0 ||
                ExpectedReactionAnimationIndex >= 0)
            {
                return;
            }

            ExpectedReactionAnimationIndex = animationIndex;
            ReactionAnimationIndex = animationIndex;
        }

        public void MarkMissing(float elapsed)
        {
            MissingSamples++;
            GuardVisible = false;
            CurrentMissingGapSeconds += elapsed;
            if (CurrentMissingGapSeconds > MaxMissingGapSeconds)
                MaxMissingGapSeconds = CurrentMissingGapSeconds;
        }

        public float GetMedianSpeed()
        {
            return SpeedEvidence.Median;
        }

        public string GetVisualAnimations()
        {
            var animations = new List<int>(VisualAnimations);
            animations.Sort();
            return animations.Count == 0 ? "none" : string.Join(",", animations);
        }

        public string GetVisualRuns()
        {
            var animations = new List<int>(MaxVisualRuns.Keys);
            animations.Sort();
            if (animations.Count == 0)
                return "none";

            var runs = new List<string>();
            foreach (int animation in animations)
                runs.Add($"{animation}:{MaxVisualRuns[animation]}");
            return string.Join(",", runs);
        }
    }
}
#endif
