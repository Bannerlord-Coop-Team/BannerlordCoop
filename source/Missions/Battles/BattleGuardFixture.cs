#if DEBUG
using Common;
using GameInterface.Services.Battles.Messages;
using GameInterface.Services.Entity;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace Missions.Battles;

public interface IBattleGuardFixture
{
    void Apply(NetworkBattleGuardFixtureCommand command, INetworkAgentRegistry agentRegistry);
    void ApplyMountedRoute(NetworkBattleGuardFixtureRoute route);
    void ApplyPlayerInput(INetworkAgentRegistry agentRegistry);
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
    string GetState(INetworkAgentRegistry agentRegistry);
    string GetCandidates(INetworkAgentRegistry agentRegistry, List<string> args);
    void Reset(INetworkAgentRegistry agentRegistry);
}

public class BattleGuardFixture : IBattleGuardFixture
{
    private const string GuardWeaponId = "empire_lance_1_t3_blunt";
    private const string FootStrikerWeaponId = "empire_sword_1_t2_blunt";
    private const string MountedStrikerWeaponId = "empire_menavlion_1_t3_blunt";
    private const string WaitingForMountedGuardRouteError =
        "waiting for mounted guard route";
    private const float SampleIntervalSeconds = 0.05f;
    private const float ProgressEpsilon = 0.001f;
    private const float FixtureLaneOffset = 25f;
    private const float MountedRouteLength = 40f;
    private const float MountedRouteRadius = 5f;
    private const float MountedRouteSampleLength = 5f;
    private const float MountedRouteMaximumRise = 1.5f;
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
    private const Agent.MovementControlFlag DefendFlags =
        Agent.MovementControlFlag.DefendBlock |
        Agent.MovementControlFlag.DefendUp;
    private const Agent.MovementControlFlag DriveFlags =
        TranslationFlags |
        TurnFlags |
        AttackFlags |
        DefendFlags;

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
        INetworkAgentRegistry agentRegistry)
    {
        if (command == null || agentRegistry == null)
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
            (guardDriver != null && guardDriver.Mode != command.Mode);
        if (fixtureChanged)
        {
            NetworkBattleGuardFixtureRoute nextRoute = pendingMountedRoute;
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
                drivesStriker);
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

    public void ApplyPlayerInput(INetworkAgentRegistry agentRegistry)
    {
        if (TryGetDrivenGuardAgent(agentRegistry, out Agent agent))
            DriveGuardInput(agent, guardDriver);
    }

    public void Tick(float dt, INetworkAgentRegistry agentRegistry)
    {
        PauseOtherAi(agentRegistry);
        TickPendingGuardRestore(agentRegistry);
        if (pendingGuardRestore == null &&
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
        if (!isBlocked ||
            guardDriver == null ||
            strikerDriver == null ||
            !ReferenceEquals(affectedAgent, guardDriver.Agent) ||
            !ReferenceEquals(affectorAgent, strikerDriver.Agent) ||
            !IsBlockedCollision(collisionData.CollisionResult))
        {
            return;
        }

        sample.PairBlockedHitCount++;
        sample.PairCollisionResult =
            collisionData.CollisionResult.ToString();
        sample.PairBlockedDamagedHp += damagedHp;
        strikerDriver.StopAfterReaction();
    }

    public string GetState(INetworkAgentRegistry agentRegistry)
    {
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
        return $"fixtureGuard={guard} fixtureStriker={striker} fixtureRestore={restore} " +
            $"trackedAgent={sample.AgentId} " +
            $"samples={sample.Samples} missing={sample.MissingSamples} visiblePct={visiblePercent:0.#} " +
            $"maxMissingGap={sample.MaxMissingGapSeconds:0.###} mounted={sample.Mounted} " +
            $"speed={sample.HorizontalSpeed:0.###} peakSpeed={sample.PeakHorizontalSpeed:0.###} " +
            $"medianSpeed={sample.GetMedianSpeed():0.###} health={sample.Health:0.###} " +
            $"plateauReady={plateauSpeed >= 0f} plateauSpeed={plateauSpeed:0.###} " +
            $"recentSpeed={sample.SpeedEvidence.RecentMedian:0.###} " +
            $"recentSpeedSamples={sample.SpeedEvidence.RecentSamples} " +
            $"recentSpeedSpread={sample.SpeedEvidence.RecentSpread:0.###} " +
            $"recentSpeedSlope={sample.SpeedEvidence.RecentSlope:0.###} " +
            $"routeState={route?.State ?? "none"} " +
            $"routeProgress={route?.Progress ?? 0f:0.###} " +
            $"routeLateral={route?.LateralOffset ?? 0f:0.###} " +
            $"routeRemaining={route?.RemainingDistance ?? 0f:0.###} " +
            $"routeTurns={route?.CompletedTurns ?? 0} " +
            $"routeStrikeReady={route?.CanStageStrike == true} " +
            $"healthDelta={sample.HealthDelta:0.###} rawAction={sample.RawActionIndex} " +
            $"rawProgress={sample.RawProgress:0.###} guardChannel={sample.LatchedChannel} " +
            $"guardAction={sample.LatchedActionIndex} guardAnimation={sample.LatchedAnimationIndex} " +
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

        float distance = guard.HasMount ? 9f : 5.5f;
        float height = guard.HasMount ? 1.5f : 0.7f;
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
            guardDriver = new GuardDriver(command.GuardAgentId, command.Mode, command.Phase, agent);
        else
        {
            guardDriver.Mode = command.Mode;
            guardDriver.Phase = command.Phase;
        }

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
        bool drivesStriker)
    {
        if (strikerDriver == null)
            strikerDriver = new StrikerDriver(roles.StrikerAgentId, agent);

        string weaponId =
            guardDriver.Mode == BattleGuardFixtureMode.Mounted
                ? MountedStrikerWeaponId
                : FootStrikerWeaponId;
        if (!strikerDriver.EquipmentReplaced &&
            !EquipFixtureWeapon(agent, weaponId, out string error))
        {
            lastError = error;
            return;
        }
        strikerDriver.EquipmentReplaced = true;
        if (!drivesStriker)
            return;

        strikerDriver.AttachAttackDriver(agent, guard, guardDriver);
        ClearDefendFlags(agent, strikerDriver.OriginalMovementFlags);
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
        bool guarding =
            !dismounting &&
            driver.Phase != BattleGuardFixturePhase.Calibration;

        agent.EventControlFlags &= ~Agent.EventControlFlag.Dismount;
        Agent.MovementControlFlag flags =
            agent.MovementFlags & ~DriveFlags;
        Vec2 movementInput = Vec2.Zero;
        if (moving)
        {
            flags |= Agent.MovementControlFlag.Forward;
            if (driver.MountedRoute != null)
            {
                Agent mount = agent.MountAgent;
                BattleGuardMountedRouteInput routeInput =
                    driver.MountedRoute.Update(
                        mount?.Position ?? agent.Position,
                        mount?.LookDirection ?? agent.LookDirection);
                flags |= routeInput.TurnFlag;
                movementInput = routeInput.Movement;
            }
            else
            {
                movementInput = new Vec2(0f, 1f);
            }
        }
        if (guarding)
            flags |= DefendFlags;
        agent.MovementFlags = flags;
        agent.MovementInputVector = movementInput;
        AgentActionData.ApplyDefendMovementFlags(
            agent,
            guarding
                ? DefendFlags
                : Agent.MovementControlFlag.None);
        if (dismounting)
            agent.EventControlFlags |= Agent.EventControlFlag.Dismount;
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
            agent.MountAgent.TeleportToPosition(position);
            agent.MountAgent.LookDirection = lane;
            agent.LookDirection = lane;
        }
        else
        {
            agent.TeleportToPosition(position);
            agent.LookDirection = lane;
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
                        candidate))
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
        Vec3 direction)
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

        return true;
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
        if (IsReaction(guard, guardDriver.GuardActionIndex))
            strikerDriver.StopAfterReaction();

        if (striker.Controller != AgentControllerType.AI)
        {
            SetControllerDirect(striker, AgentControllerType.AI);
            AgentAiWaker.Wake(striker);
        }
        striker.SetTargetAgent(guard);
        striker.SetWatchState(Agent.WatchState.Alarmed);
        striker.SetIsAIPaused(false);
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
            Agent.HandIndex.MainHand,
            EquipmentIndex.Weapon0,
            false,
            false,
            0);
        error = null;
        return true;
    }

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
            return;

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
        bool needsRemount =
            driver.OriginalMount?.IsActive() == true &&
            !ReferenceEquals(agent.MountAgent, driver.OriginalMount);
        if (needsRemount)
        {
            pendingGuardRestore = new PendingGuardRestore(
                driver,
                driver.AgentId,
                roles.GuardAuthority,
                driver.OriginalMount,
                driver.OriginalPosition,
                driver.OriginalLookDirection,
                driver.OriginalMountPosition,
                driver.OriginalMountLookDirection,
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
        if (driver.OriginalMount?.IsActive() == true)
        {
            driver.OriginalMount.TeleportToPosition(
                driver.OriginalMountPosition);
            driver.OriginalMount.LookDirection =
                driver.OriginalMountLookDirection;
        }
        else
        {
            agent.TeleportToPosition(driver.OriginalPosition);
        }
        agent.LookDirection = driver.OriginalLookDirection;
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
        if (ReferenceEquals(agent.MountAgent, restore.Mount))
        {
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
        agent.TeleportToPosition(restore.AgentPosition);
        agent.LookDirection = restore.AgentLookDirection;
        agent.EventControlFlags &=
            ~(Agent.EventControlFlag.Dismount |
              Agent.EventControlFlag.Mount);
        agent.MountAgent = restore.Mount;
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
        sample.Health = agent.Health;
        if (!sample.HasBaselineHealth)
        {
            sample.BaselineHealth = agent.Health;
            sample.HasBaselineHealth = true;
        }
        sample.HealthDelta = agent.Health - sample.BaselineHealth;
        SampleSpeed(agent, elapsed);

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

    private static bool IsBlockedCollision(
        CombatCollisionResult collisionResult)
    {
        return collisionResult == CombatCollisionResult.Blocked ||
            collisionResult == CombatCollisionResult.Parried ||
            collisionResult == CombatCollisionResult.ChamberBlocked;
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
        public Vec3 MountPosition { get; }
        public Vec3 MountLookDirection { get; }
        public bool DrivesRestore { get; }

        public PendingGuardRestore(
            GuardDriver driver,
            Guid agentId,
            string authority,
            Agent mount,
            Vec3 agentPosition,
            Vec3 agentLookDirection,
            Vec3 mountPosition,
            Vec3 mountLookDirection,
            bool drivesRestore)
        {
            Driver = driver;
            AgentId = agentId;
            Authority = authority;
            Mount = mount;
            AgentPosition = agentPosition;
            AgentLookDirection = agentLookDirection;
            MountPosition = mountPosition;
            MountLookDirection = mountLookDirection;
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
        public Agent.MovementControlFlag OriginalMovementFlags { get; }
        public Vec2 OriginalMovementInputVector { get; }
        public Agent.MovementControlFlag OriginalDefendFlags { get; }
        public Agent.GuardMode OriginalGuardMode { get; }
        public Agent OriginalMount { get; }
        public Vec3 OriginalPosition { get; }
        public Vec3 OriginalLookDirection { get; }
        public Vec3 OriginalMountPosition { get; }
        public Vec3 OriginalMountLookDirection { get; }
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
        public bool DrivesAgent { get; private set; }
        public int GuardActionIndex => guardActionIndex;
        private int guardChannel = -1;
        private int guardActionIndex = -1;
        private int guardAnimationIndex = -1;
        private bool guardActionCyclic;

        public GuardDriver(
            Guid agentId,
            BattleGuardFixtureMode mode,
            BattleGuardFixturePhase phase,
            Agent agent)
        {
            Agent = agent;
            AgentId = agentId;
            Mode = mode;
            Phase = phase;
            OriginalMovementFlags = agent.MovementFlags;
            OriginalMovementInputVector = agent.MovementInputVector;
            OriginalDefendFlags =
                AgentActionData.GetDefendMovementFlags(agent.MovementFlags);
            OriginalGuardMode = agent.CurrentGuardMode;
            OriginalMount = agent.MountAgent;
            OriginalPosition = agent.Position;
            OriginalLookDirection = agent.LookDirection;
            OriginalMountPosition = OriginalMount?.Position ?? Vec3.Zero;
            OriginalMountLookDirection =
                OriginalMount?.LookDirection ?? Vec3.Zero;
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
            DrivesAgent = false;
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
            GuardArmed = false;
            HasGuardBaselineHealth = false;
            guardChannel = -1;
            guardActionIndex = -1;
            guardAnimationIndex = -1;
            guardActionCyclic = false;
        }
    }

    private sealed class StrikerDriver
    {
        public Agent Agent { get; }
        public Guid AgentId { get; }
        public Agent.MovementControlFlag OriginalMovementFlags { get; }
        public Agent.MovementControlFlag OriginalDefendFlags { get; }
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
        public bool HasAttackDriver => attackDriver != null;
        public string AttackState =>
            attackDriver?.State ?? "none";
        public int AttackAttempts =>
            attackDriver?.Attempts ?? 0;
        private readonly bool originalHasOnAiInputSetCallback;
        private GuardInterceptionStrikeComponent attackDriver;

        public StrikerDriver(Guid agentId, Agent agent)
        {
            Agent = agent;
            AgentId = agentId;
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
            originalHasOnAiInputSetCallback =
                agent.GetHasOnAiInputSetCallback();
        }

        public void AttachAttackDriver(
            Agent agent,
            Agent guard,
            GuardDriver guardDriver)
        {
            if (attackDriver != null)
                return;

            attackDriver = new GuardInterceptionStrikeComponent(
                agent,
                guard,
                guardDriver);
            agent.AddComponent(attackDriver);
            agent.SetHasOnAiInputSetCallback(true);
        }

        public void StopAfterReaction()
        {
            attackDriver?.StopAfterReaction();
        }

        public void DetachAttackDriver(Agent agent)
        {
            if (attackDriver == null)
                return;

            agent.RemoveComponent(attackDriver);
            agent.SetHasOnAiInputSetCallback(
                originalHasOnAiInputSetCallback);
            attackDriver = null;
        }
    }

    private sealed class GuardInterceptionStrikeComponent : AgentComponent
    {
        private const float AttackPressSeconds = 0.35f;
        private const float ReleaseLeadSeconds = 0.25f;
        private const float MaximumChargeSeconds = 2.5f;
        private const float OutcomeWaitSeconds = 1.25f;
        private const float MaximumOutcomeWaitSeconds = 2.5f;
        private const float RetryRecoverySeconds = 0.5f;
        private const float MountedSpeedReadySeconds = 0.5f;
        private const float MinimumMountedStrikeSpeed = 5f;
        private const float LateralOffset = 1.15f;
        private const float MinimumLeadDistance = 6f;
        private const float MaximumLeadDistance = 10f;
        private const int MaximumAttempts = 5;

        public string State => state.ToString();
        public int Attempts { get; private set; }

        private readonly Agent striker;
        private readonly Agent guard;
        private readonly GuardDriver guardDriver;
        private InterceptionState state = InterceptionState.WaitingForSpeed;
        private Vec3 laneDirection;
        private Vec3 contactPoint;
        private float stateElapsed;

        public GuardInterceptionStrikeComponent(
            Agent striker,
            Agent guard,
            GuardDriver guardDriver)
            : base(striker)
        {
            this.striker = striker;
            this.guard = guard;
            this.guardDriver = guardDriver;
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
                state = InterceptionState.Exhausted;
                return;
            }
            if (IsReaction(guard, guardDriver.GuardActionIndex))
            {
                StopAfterReaction();
                return;
            }

            stateElapsed += Math.Max(0f, dt);
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
                ? Agent.MovementControlFlag.AttackUp
                : Agent.MovementControlFlag.None;
            inputVector = Vec2.Zero;
        }

        public void StopAfterReaction()
        {
            state = InterceptionState.Succeeded;
            stateElapsed = 0f;
        }

        private void TickWaitingForSpeed()
        {
            if (Attempts >= MaximumAttempts)
                return;
            if (guardDriver.Mode != BattleGuardFixtureMode.Mounted)
            {
                StageAttempt();
                return;
            }
            if (!guard.HasMount ||
                guardDriver.CurrentHorizontalSpeed <
                    MinimumMountedStrikeSpeed ||
                guardDriver.MountedRoute?.CanStageStrike != true)
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
            if (guardDriver.Mode == BattleGuardFixtureMode.Mounted)
                StageMountedInterception();
            else
                StageFootStrike();
            state = InterceptionState.Charging;
        }

        private void StageMountedInterception()
        {
            laneDirection = GetHorizontalDirection(
                guardDriver.CurrentHorizontalDirection);
            float speed = Math.Max(
                1f,
                guardDriver.CurrentHorizontalSpeed);
            float leadDistance = Math.Max(
                MinimumLeadDistance,
                Math.Min(MaximumLeadDistance, speed * 0.85f));
            contactPoint = guard.Position + (laneDirection * leadDistance);

            var lateral = new Vec3(
                laneDirection.y,
                -laneDirection.x,
                0f);
            Vec3 strikerPosition =
                contactPoint + (lateral * LateralOffset);
            SetGroundHeight(ref strikerPosition);
            striker.TeleportToPosition(strikerPosition);
            FacePoint(striker, contactPoint);
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
            if (stateElapsed < AttackPressSeconds)
                return;
            if (guardDriver.Mode != BattleGuardFixtureMode.Mounted)
            {
                ReleaseAttack();
                return;
            }

            float longitudinalDistance = GetLongitudinalDistance();
            float speed = Math.Max(
                0.1f,
                guardDriver.CurrentHorizontalSpeed);
            float timeToContact = longitudinalDistance / speed;
            if (timeToContact <= ReleaseLeadSeconds ||
                stateElapsed >= MaximumChargeSeconds)
            {
                ReleaseAttack();
            }
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
            state = InterceptionState.Released;
            stateElapsed = 0f;
        }

        private void RecordMiss()
        {
            state = InterceptionState.Recovery;
            stateElapsed = 0f;
        }

        private void BeginNextAttempt()
        {
            if (Attempts >= MaximumAttempts)
            {
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
        public readonly BattleGuardSpeedEvidence SpeedEvidence = new();
        public float Health;
        public float BaselineHealth;
        public bool HasBaselineHealth;
        public float HealthDelta;
        public int RawActionIndex = -1;
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
