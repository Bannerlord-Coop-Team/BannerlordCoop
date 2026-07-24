#if DEBUG
using GameInterface.Services.Battles.Messages;
using GameInterface.Services.Entity;
using Missions.Agents.Packets;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Battles;

public interface IBattleGuardFixture
{
    void Apply(NetworkBattleGuardFixtureCommand command, INetworkAgentRegistry agentRegistry);
    void Tick(float dt, INetworkAgentRegistry agentRegistry);
    void SampleFinalDisplayedState(float dt, INetworkAgentRegistry agentRegistry);
    string GetState(INetworkAgentRegistry agentRegistry);
    string GetCandidates(INetworkAgentRegistry agentRegistry, List<string> args);
    void Reset(INetworkAgentRegistry agentRegistry);
}

public class BattleGuardFixture : IBattleGuardFixture
{
    private const string GuardWeaponId = "empire_lance_1_t3_blunt";
    private const string StrikerWeaponId = "empire_sword_1_t2_blunt";
    private const float SampleIntervalSeconds = 0.05f;
    private const float ProgressEpsilon = 0.001f;
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
    private const Agent.MovementControlFlag DefendFlags =
        Agent.MovementControlFlag.DefendBlock |
        Agent.MovementControlFlag.DefendUp;
    private const Agent.MovementControlFlag DriveFlags =
        TranslationFlags |
        AttackFlags |
        DefendFlags;

    private readonly IControllerIdProvider controllerIdProvider;
    private readonly List<AiPauseState> aiPauseStates = new();
    private GuardDriver guardDriver;
    private StrikerDriver strikerDriver;
    private PendingGuardRestore pendingGuardRestore;
    private FixtureRoles roles;
    private float sampleElapsed;
    private SampleState sample = new();
    private string lastError;

    public BattleGuardFixture(IControllerIdProvider controllerIdProvider)
    {
        if (controllerIdProvider == null)
            throw new ArgumentNullException(nameof(controllerIdProvider));

        this.controllerIdProvider = controllerIdProvider;
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

        if ((roles != null &&
             (roles.GuardAgentId != commandRoles.GuardAgentId ||
              roles.GuardAuthority != commandRoles.GuardAuthority ||
              roles.StrikerAgentId != commandRoles.StrikerAgentId ||
              roles.StrikerAuthority != commandRoles.StrikerAuthority)) ||
            (guardDriver != null && guardDriver.Mode != command.Mode))
        {
            Reset(agentRegistry);
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
                out CoopAgentInfo strikerInfo))
        {
            bool drivesStriker =
                controllerIdProvider.ControllerId == roles.StrikerAuthority &&
                agentRegistry.IsLocallyControlled(roles.StrikerAgentId);
            ApplyStriker(strikerInfo.Agent, drivesStriker);
        }
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
            ObserveFinalDisplayedState(info.Agent);
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

    public string GetState(INetworkAgentRegistry agentRegistry)
    {
        string guard = guardDriver == null
            ? "none"
            : $"{guardDriver.AgentId}:{guardDriver.Mode}:{guardDriver.Phase}:armed={guardDriver.GuardArmed}";
        string striker = strikerDriver == null
            ? "none"
            : $"{strikerDriver.AgentId}:positioned={strikerDriver.Positioned}";
        string restore = pendingGuardRestore == null
            ? "none"
            : $"{pendingGuardRestore.AgentId}:remount";
        float visiblePercent = sample.Samples == 0
            ? 0f
            : 100f * sample.VisibleSamples / sample.Samples;
        return $"fixtureGuard={guard} fixtureStriker={striker} fixtureRestore={restore} " +
            $"trackedAgent={sample.AgentId} " +
            $"samples={sample.Samples} missing={sample.MissingSamples} visiblePct={visiblePercent:0.#} " +
            $"maxMissingGap={sample.MaxMissingGapSeconds:0.###} mounted={sample.Mounted} " +
            $"speed={sample.HorizontalSpeed:0.###} peakSpeed={sample.PeakHorizontalSpeed:0.###} " +
            $"medianSpeed={sample.GetMedianSpeed():0.###} health={sample.Health:0.###} " +
            $"healthDelta={sample.HealthDelta:0.###} rawAction={sample.RawActionIndex} " +
            $"rawProgress={sample.RawProgress:0.###} guardChannel={sample.LatchedChannel} " +
            $"guardAction={sample.LatchedActionIndex} guardAnimation={sample.LatchedAnimationIndex} " +
            $"visualAction={sample.VisualActionIndex} visualAnimation={sample.VisualAnimationIndex} " +
            $"visualProgress={sample.VisualProgress:0.###} visible={sample.GuardVisible} " +
            $"reaction={sample.Reaction} reactionSamples={sample.ReactionSamples} " +
            $"reactionAction={sample.ReactionActionIndex} " +
            $"reactionAnimation={sample.ReactionAnimationIndex} " +
            $"visualAnimations={sample.GetVisualAnimations()} visualRuns={sample.GetVisualRuns()} " +
            $"visualProgressAdvances={sample.VisualProgressAdvances} " +
            $"visualProgressStalls={sample.VisualProgressStalls} " +
            $"visualProgressResets={sample.VisualProgressResets} error={lastError ?? "none"}";
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

    public void Reset(INetworkAgentRegistry agentRegistry)
    {
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
        sampleElapsed = 0f;
        sample = new SampleState();
        lastError = pendingGuardRestore == null
            ? null
            : "guard remount is still pending";
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

        if (!drivesGuard)
            return;

        if (command.Phase == BattleGuardFixturePhase.Calibration)
        {
            AgentActionData.ApplyGuardState(
                agent,
                Agent.GuardMode.None,
                force: true);
            guardDriver.GuardArmed = false;
        }
        if (command.Mode == BattleGuardFixtureMode.Foot && agent.HasMount)
            agent.Mount(agent.MountAgent);
        if ((command.Phase == BattleGuardFixturePhase.Guard ||
             command.Phase == BattleGuardFixturePhase.Attack) &&
            !guardDriver.GuardArmed)
        {
            AgentActionData.ApplyGuardState(agent, Agent.GuardMode.Up, force: true);
            guardDriver.GuardArmed = true;
            LatchGuardPresentation(agent);
        }
    }

    private void ApplyStriker(Agent agent, bool drivesStriker)
    {
        if (strikerDriver == null)
            strikerDriver = new StrikerDriver(roles.StrikerAgentId, agent);

        if (!strikerDriver.EquipmentReplaced &&
            !EquipFixtureWeapon(agent, StrikerWeaponId, out string error))
        {
            lastError = error;
            return;
        }
        strikerDriver.EquipmentReplaced = true;
        if (!drivesStriker)
            return;

        strikerDriver.AttachAttackDriver(agent);
        ClearDefendFlags(agent, strikerDriver.OriginalMovementFlags);
    }

    private void TickGuard(INetworkAgentRegistry agentRegistry)
    {
        if (guardDriver == null ||
            controllerIdProvider.ControllerId != roles?.GuardAuthority ||
            !TryGetExactAgent(agentRegistry, guardDriver.AgentId, roles.GuardAuthority, out CoopAgentInfo info) ||
            !agentRegistry.IsLocallyControlled(guardDriver.AgentId))
        {
            return;
        }

        Agent agent = info.Agent;
        if (guardDriver.Mode == BattleGuardFixtureMode.Foot && agent.HasMount)
        {
            ClearDefendFlags(agent, guardDriver.OriginalMovementFlags);
            agent.Mount(agent.MountAgent);
            return;
        }
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

        switch (guardDriver.Phase)
        {
            case BattleGuardFixturePhase.Calibration:
                ClearDefendFlags(agent, guardDriver.OriginalMovementFlags);
                agent.MovementFlags =
                    (agent.MovementFlags & ~DriveFlags) |
                    Agent.MovementControlFlag.Forward;
                break;
            case BattleGuardFixturePhase.Guard:
                DriveGuard(agent, false);
                break;
            case BattleGuardFixturePhase.Attack:
                DriveGuard(agent, true);
                break;
        }
    }

    private static void ClearDefendFlags(
        Agent agent,
        Agent.MovementControlFlag originalMovementFlags)
    {
        agent.MovementFlags = originalMovementFlags & ~DriveFlags;
        AgentActionData.ApplyDefendMovementFlags(agent, Agent.MovementControlFlag.None);
    }

    private static void DriveGuard(Agent agent, bool holdPosition)
    {
        Agent.MovementControlFlag flags = agent.MovementFlags & ~DriveFlags;
        if (!holdPosition)
            flags |= Agent.MovementControlFlag.Forward;
        agent.MovementFlags = flags;
        AgentActionData.ApplyDefendMovementFlags(agent, DefendFlags);
    }

    private void TickStriker(INetworkAgentRegistry agentRegistry)
    {
        if (strikerDriver == null ||
            controllerIdProvider.ControllerId != roles?.StrikerAuthority ||
            !TryGetExactAgent(agentRegistry, strikerDriver.AgentId, roles.StrikerAuthority, out CoopAgentInfo strikerInfo) ||
            !TryGetExactAgent(agentRegistry, roles.GuardAgentId, roles.GuardAuthority, out CoopAgentInfo guardInfo) ||
            !agentRegistry.IsLocallyControlled(strikerDriver.AgentId))
        {
            return;
        }

        Agent striker = strikerInfo.Agent;
        Agent guard = guardInfo.Agent;
        if (!strikerDriver.Positioned)
        {
            PositionStriker(striker, guard);
            strikerDriver.Positioned = true;
        }

        if (striker.Controller != AgentControllerType.AI)
        {
            striker.Controller = AgentControllerType.AI;
            AgentAiWaker.Wake(striker);
        }
        striker.SetTargetAgent(guard);
        striker.SetWatchState(Agent.WatchState.Alarmed);
        striker.SetIsAIPaused(false);
    }

    private static void PositionStriker(Agent striker, Agent guard)
    {
        Vec3 forward = guard.LookDirection;
        forward.z = 0f;
        if (forward.LengthSquared < 0.0001f)
            forward = new Vec3(0f, 1f, 0f);
        forward.Normalize();
        Vec3 position = guard.Position + (forward * 1.25f);
        Vec3 lookDirection = guard.Position - position;
        lookDirection.z = 0f;
        if (lookDirection.LengthSquared > 0.0001f)
        {
            lookDirection.Normalize();
            striker.LookDirection = lookDirection;
        }
        striker.TeleportToPosition(position);
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
        if (guardDriver == null ||
            !TryGetExactAgent(
                agentRegistry,
                guardDriver.AgentId,
                roles?.GuardAuthority,
                out CoopAgentInfo info))
        {
            return;
        }

        bool drivesGuard =
            controllerIdProvider.ControllerId == roles.GuardAuthority &&
            agentRegistry.IsLocallyControlled(guardDriver.AgentId);
        RestoreGuardForCurrentAgent(info.Agent, drivesGuard);
    }

    private void RestoreGuardForCurrentAgent(Agent agent, bool drivesGuard)
    {
        if (guardDriver.EquipmentReplaced)
        {
            ReplaceWeapon(agent, EquipmentIndex.Weapon0, guardDriver.OriginalWeapon0);
            ReplaceWeapon(agent, EquipmentIndex.Weapon1, guardDriver.OriginalWeapon1);
            guardDriver.OriginalWieldedEquipment.Apply(agent);
        }
        if (guardDriver.OriginalMount?.IsActive() == true)
        {
            pendingGuardRestore = new PendingGuardRestore(
                guardDriver.AgentId,
                roles.GuardAuthority,
                guardDriver.OriginalMount,
                guardDriver.OriginalPosition,
                guardDriver.OriginalLookDirection,
                guardDriver.OriginalMountPosition,
                guardDriver.OriginalMountLookDirection,
                drivesGuard);
        }
        if (!drivesGuard)
            return;

        agent.MovementFlags = guardDriver.OriginalMovementFlags;
        AgentActionData.ApplyDefendMovementFlags(agent, guardDriver.OriginalDefendFlags);
        AgentActionData.ApplyGuardState(agent, guardDriver.OriginalGuardMode, force: true);
        if (guardDriver.OriginalMount != null &&
            guardDriver.OriginalMount.IsActive())
        {
            guardDriver.OriginalMount.TeleportToPosition(
                guardDriver.OriginalMountPosition);
            guardDriver.OriginalMount.LookDirection =
                guardDriver.OriginalMountLookDirection;
            if (!agent.HasMount &&
                guardDriver.OriginalMount.RiderAgent == null)
            {
                agent.TeleportToPosition(guardDriver.OriginalPosition);
                agent.Mount(guardDriver.OriginalMount);
            }
        }
        else
        {
            agent.TeleportToPosition(guardDriver.OriginalPosition);
        }
        agent.LookDirection = guardDriver.OriginalLookDirection;
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
            pendingGuardRestore = null;
            lastError = "guard remount agent is unavailable";
            return;
        }

        Agent agent = info.Agent;
        if (ReferenceEquals(agent.MountAgent, restore.Mount))
        {
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
            pendingGuardRestore = null;
            lastError = "guard remount is no longer possible";
            return;
        }
        if (!restore.DrivesRestore)
            return;
        if (!agentRegistry.IsLocallyControlled(restore.AgentId))
        {
            pendingGuardRestore = null;
            lastError = "guard remount authority is unavailable";
            return;
        }

        restore.Mount.TeleportToPosition(restore.MountPosition);
        restore.Mount.LookDirection = restore.MountLookDirection;
        agent.TeleportToPosition(restore.AgentPosition);
        agent.LookDirection = restore.AgentLookDirection;
        agent.Mount(restore.Mount);
    }

    private void RestoreStriker(INetworkAgentRegistry agentRegistry)
    {
        if (strikerDriver == null ||
            !TryGetExactAgent(
                agentRegistry,
                strikerDriver.AgentId,
                roles?.StrikerAuthority,
                out CoopAgentInfo info))
        {
            return;
        }

        bool drivesStriker =
            controllerIdProvider.ControllerId == roles.StrikerAuthority &&
            agentRegistry.IsLocallyControlled(strikerDriver.AgentId);
        RestoreStrikerForCurrentAgent(info.Agent, drivesStriker);
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
        agent.Controller = strikerDriver.OriginalController;
        agent.TeleportToPosition(strikerDriver.OriginalPosition);
        agent.LookDirection = strikerDriver.OriginalLookDirection;
    }

    private static bool TryCreateRoles(
        NetworkBattleGuardFixtureCommand command,
        out FixtureRoles commandRoles)
    {
        commandRoles = null;
        if (command.GuardAgentId == Guid.Empty ||
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
        if (agentRegistry == null || roles == null)
            return;

        foreach (string authority in agentRegistry.GetControllerIds())
        {
            if (authority != controllerIdProvider.ControllerId)
                continue;

            foreach (CoopAgentInfo info in agentRegistry.GetAgents(authority))
            {
                Agent agent = info?.Agent;
                if (agent == null ||
                    info.AgentId == roles.GuardAgentId ||
                    info.CurrentAuthority != authority ||
                    !agentRegistry.IsLocallyControlled(info.AgentId) ||
                    !agent.IsActive() ||
                    agent.Mission != Mission.Current ||
                    !agent.IsHuman ||
                    agent.Controller != AgentControllerType.AI ||
                    HasAiPauseState(info.AgentId))
                {
                    continue;
                }

                aiPauseStates.Add(
                    new AiPauseState(info.AgentId, authority, agent.IsPaused));
            }
        }
    }

    private bool HasAiPauseState(Guid agentId)
    {
        foreach (AiPauseState state in aiPauseStates)
        {
            if (state.AgentId == agentId)
                return true;
        }

        return false;
    }

    private void PauseOtherAi(INetworkAgentRegistry agentRegistry)
    {
        CaptureNewAiPauseStates(agentRegistry);
        foreach (AiPauseState state in aiPauseStates)
        {
            if (strikerDriver != null && state.AgentId == strikerDriver.AgentId)
                continue;

            if (TryGetExactAgent(
                    agentRegistry,
                    state.AgentId,
                    state.Authority,
                    out CoopAgentInfo info) &&
                agentRegistry.IsLocallyControlled(state.AgentId))
            {
                info.Agent.SetIsAIPaused(true);
            }
        }
    }

    private void RestoreAiPauseStates(INetworkAgentRegistry agentRegistry)
    {
        foreach (AiPauseState state in aiPauseStates)
        {
            if (TryGetExactAgent(
                    agentRegistry,
                    state.AgentId,
                    state.Authority,
                    out CoopAgentInfo info) &&
                agentRegistry.IsLocallyControlled(state.AgentId))
            {
                info.Agent.SetIsAIPaused(state.WasPaused);
            }
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
                sample.VisualActionIndex == sample.LatchedActionIndex ||
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

    private void ObserveFinalDisplayedState(Agent agent)
    {
        if (sample.LatchedChannel < 0)
            LatchGuardPresentation(agent);
        if (IsReaction(agent))
        {
            sample.Reaction = true;
            sample.ReactionSamples++;
            if (controllerIdProvider.ControllerId == roles?.GuardAuthority)
                CaptureOwnerReaction(agent);
        }

        Skeleton skeleton = null;
        try
        {
            MBAgentVisuals visuals = agent.AgentVisuals;
            if (ReferenceEquals(visuals, null) || !visuals.IsValid())
                return;

            skeleton = visuals.GetSkeleton();
            if (!ReferenceEquals(skeleton, null))
                ObserveVisualAnimations(skeleton);
        }
        catch
        {
        }
        finally
        {
            if (!ReferenceEquals(skeleton, null))
                skeleton.ManualInvalidate();
        }
    }

    private void CaptureOwnerReaction(Agent agent)
    {
        for (int channel = 0; channel <= 1; channel++)
        {
            if (!IsReaction(agent.GetCurrentActionType(channel)))
                continue;

            ActionIndexCache action = agent.GetCurrentAction(channel);
            sample.ReactionActionIndex = action.Index;
            sample.ReactionAnimationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            return;
        }
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
            sample.SpeedSamples.Add(sample.HorizontalSpeed);
        }
        sample.PreviousPosition = agent.Position;
        sample.HasPreviousPosition = true;
    }

    private static bool IsReaction(Agent agent)
    {
        return IsReaction(agent.GetCurrentActionType(0)) ||
            IsReaction(agent.GetCurrentActionType(1));
    }

    private static bool IsReaction(Agent.ActionCodeType actionType)
    {
        return actionType == Agent.ActionCodeType.BlockedMelee ||
            actionType == Agent.ActionCodeType.ParriedMelee;
    }

    private sealed class AiPauseState
    {
        public Guid AgentId { get; }
        public string Authority { get; }
        public bool WasPaused { get; }

        public AiPauseState(Guid agentId, string authority, bool wasPaused)
        {
            AgentId = agentId;
            Authority = authority;
            WasPaused = wasPaused;
        }
    }

    private sealed class PendingGuardRestore
    {
        public Guid AgentId { get; }
        public string Authority { get; }
        public Agent Mount { get; }
        public Vec3 AgentPosition { get; }
        public Vec3 AgentLookDirection { get; }
        public Vec3 MountPosition { get; }
        public Vec3 MountLookDirection { get; }
        public bool DrivesRestore { get; }

        public PendingGuardRestore(
            Guid agentId,
            string authority,
            Agent mount,
            Vec3 agentPosition,
            Vec3 agentLookDirection,
            Vec3 mountPosition,
            Vec3 mountLookDirection,
            bool drivesRestore)
        {
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
        public Guid AgentId { get; }
        public BattleGuardFixtureMode Mode { get; set; }
        public BattleGuardFixturePhase Phase { get; set; }
        public Agent.MovementControlFlag OriginalMovementFlags { get; }
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
        public float GuardBaselineHealth { get; set; }
        public bool HasGuardBaselineHealth { get; set; }
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
            AgentId = agentId;
            Mode = mode;
            Phase = phase;
            OriginalMovementFlags = agent.MovementFlags;
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
        public bool Positioned { get; set; }
        private readonly bool originalHasOnAiInputSetCallback;
        private ForcedUpwardStrikeComponent attackDriver;

        public StrikerDriver(Guid agentId, Agent agent)
        {
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

        public void AttachAttackDriver(Agent agent)
        {
            if (attackDriver != null)
                return;

            attackDriver = new ForcedUpwardStrikeComponent(agent);
            agent.AddComponent(attackDriver);
            agent.SetHasOnAiInputSetCallback(true);
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

    private sealed class ForcedUpwardStrikeComponent : AgentComponent
    {
        private const float AttackPressSeconds = 0.35f;
        private const float AttackCycleSeconds = 1f;
        private float attackElapsed;

        public ForcedUpwardStrikeComponent(Agent agent)
            : base(agent)
        {
        }

        public override void OnTick(float dt)
        {
            attackElapsed += dt;
            if (attackElapsed >= AttackCycleSeconds)
                attackElapsed -= AttackCycleSeconds;
        }

        public override void OnAIInputSet(
            ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag,
            ref Vec2 inputVector)
        {
            eventFlag = Agent.EventControlFlag.None;
            movementFlag = attackElapsed < AttackPressSeconds
                ? Agent.MovementControlFlag.AttackUp
                : Agent.MovementControlFlag.None;
            inputVector = Vec2.Zero;
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
        public readonly List<float> SpeedSamples = new();
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
        public readonly HashSet<int> VisualAnimations = new();
        public readonly Dictionary<int, int> CurrentVisualRuns = new();
        public readonly Dictionary<int, int> MaxVisualRuns = new();
        public int VisualProgressAdvances;
        public int VisualProgressStalls;
        public int VisualProgressResets;
        public Vec3 PreviousPosition;
        public bool HasPreviousPosition;
        public float PreviousVisualProgress;
        public bool HasPreviousVisualProgress;

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
            if (SpeedSamples.Count == 0)
                return 0f;

            var sorted = new List<float>(SpeedSamples);
            sorted.Sort();
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[middle - 1] + sorted[middle]) / 2f
                : sorted[middle];
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
