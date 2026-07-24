using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using LiteNetLib;
using Missions.Data;
using Missions.Agents;
using Missions.Agents.Patches;
using Missions.Battles;
using Missions.Messages;
using Missions.Tournaments.Messages;
using Missions.Tournaments.Spectators;
using SandBox.Tournaments.MissionLogics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments;

public class CoopTournamentController : CoopMissionController
{
    private const int MaximumPendingTournamentPackets = 256;
    private static readonly ILogger Logger = LogManager.GetLogger<CoopTournamentController>();
    private readonly INetwork relayNetwork;
    private readonly INetworkWorldItemRegistry worldItemRegistry;
    private readonly TournamentMissionSession session;
    private readonly TournamentMatchLifecycle matchLifecycle;
    private readonly TournamentAgentSpawner agentSpawner;
    private readonly ITournamentSpectatorAgentManager spectatorAgentManager;
    private readonly TournamentSpawnManifestBuilder manifestBuilder;
#if DEBUG
    private readonly ITournamentCombatFixture combatFixture;
#endif
    private TournamentSessionSnapshot snapshot;
    private CoopTournamentBehavior tournamentBehavior;
    private CoopTournamentFightMissionController fightController;
    private string startedMatchId;
    private string submittedManifestMatchId;
    private string submittedResultMatchId;
    private string publishedRoundResultMatchId;
    private string presentedRoundResultMatchId;
    private long manifestSequence;
    private long resultSequence;
    private long appliedManifestSequence;
    private TournamentSpawnManifestData latestManifest;
    private TournamentSpawnManifestData pendingManifest;
    private TournamentSpawnManifestData pendingApplyManifest;
    private TournamentMatchResultData pendingResult;
    private bool missionReadyForManifest;
    private bool leaveRequested;
    private long leaveRequestRevision = -1;
    private readonly TournamentMessageSequenceLedger receivedDamageSequences = new();
    private readonly TournamentMessageSequenceLedger appliedDamageSequences = new();
    private readonly TournamentMessageSequenceLedger receivedKnockoutSequences = new();
    private readonly TournamentMessageSequenceLedger receivedRuntimeSequences = new();
    private readonly System.Collections.Generic.HashSet<Guid> applyingDamage = new();
    private readonly List<NetworkTournamentAgentKnockedOut> pendingKnockouts = new();
    private readonly List<IEvent> pendingTournamentPackets = new();
    private long damageSequence;
    private long knockoutSequence;
    private long runtimeSequence;
    private NetworkTournamentRuntimeState latestRuntimeState;
    private bool applyingRuntimeState;
    private NetworkApplyTournamentDamage activeDamageMessage;
    private float resultReadyElapsed;
    private readonly Dictionary<Agent, TournamentAgentSpawnData> manifestAgentData = new();
    private readonly Dictionary<Guid, Agent> manifestAgentInstances = new();

    public CoopTournamentController(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        INetworkWorldItemRegistry worldItemRegistry,
        ITournamentSpectatorAgentManagerFactory spectatorAgentManagerFactory
#if DEBUG
        , ITournamentCombatFixture combatFixture
#endif
        )
        : base(network, messageBroker, objectManager, coopMissionComponent)
    {
        this.relayNetwork = relayNetwork;
        this.worldItemRegistry = worldItemRegistry;
        spectatorAgentManager = spectatorAgentManagerFactory.Create(coopMissionComponent);
        session = new TournamentMissionSession(controllerIdProvider);
        matchLifecycle = new TournamentMatchLifecycle(coopMissionComponent, worldItemRegistry);
        agentSpawner = new TournamentAgentSpawner(objectManager, controllerIdProvider, coopMissionComponent);
        manifestBuilder = new TournamentSpawnManifestBuilder(objectManager, coopMissionComponent);
#if DEBUG
        this.combatFixture = combatFixture;
#endif

        messageBroker.Subscribe<TournamentSessionUpdated>(Handle_SessionUpdated);
        messageBroker.Subscribe<TournamentSpawnManifestUpdated>(Handle_ManifestUpdated);
        messageBroker.Subscribe<NetworkApplyTournamentDamage>(Handle_ApplyTournamentDamage);
        messageBroker.Subscribe<NetworkTournamentAgentKnockedOut>(Handle_AgentKnockedOut);
        messageBroker.Subscribe<NetworkTournamentRuntimeState>(Handle_RuntimeState);
        messageBroker.Subscribe<NetworkTournamentRoundEnded>(Handle_RoundEnded);
#if DEBUG
        messageBroker.Subscribe<NetworkTournamentCombatFixtureCommand>(Handle_CombatFixtureCommand);
#endif
    }

    public ITournamentMissionSession Session => session;
    public string LastLocalSpectatorSpawnName => spectatorAgentManager.LastLocalSpawnName;

    public void Initialize(
        TournamentSessionSnapshot initialSnapshot,
        CoopTournamentBehavior behavior,
        CoopTournamentFightMissionController nativeFightController,
        Mission mission)
    {
        snapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
        tournamentBehavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
        fightController = nativeFightController ?? throw new ArgumentNullException(nameof(nativeFightController));
        fightController.SetAgentRemovalProvider(() => !matchLifecycle.IsClearing);
        fightController.SetHitProgressionRecorder(CaptureHitProgression);
        tournamentBehavior.SetLeaveRequest(RequestAuthoritativeLeave);

        ApplySessionState(initialSnapshot);
        mission.AllowAiTicking = session.IsLocalHost;
        if (!string.IsNullOrEmpty(initialSnapshot.CurrentMatchId))
            matchLifecycle.TryBeginMatch(initialSnapshot.CurrentMatchId, initialSnapshot.BracketRevision, false);
        network.Start();
        network.ConnectToInstance(initialSnapshot.MissionInstanceId);
        coopMissionComponent.AgentRegistry.Clear();
        relayNetwork.SendAll(new NetworkMissionEntered(session.OwnControllerId, initialSnapshot.MissionInstanceId));
        relayNetwork.SendAll(new NetworkTournamentMissionEntered(initialSnapshot.SessionId, initialSnapshot.Revision));
        DrainPendingTournamentPackets();
    }

    private void Handle_SessionUpdated(MessagePayload<TournamentSessionUpdated> payload)
    {
        TournamentSessionSnapshot updated = payload.What.Snapshot;
        if (updated == null || snapshot == null || updated.SessionId != snapshot.SessionId) return;
        if (updated.Revision <= snapshot.Revision) return;

        GameThread.RunSafe(() => ApplySessionUpdate(updated));
    }

    private void ApplySessionUpdate(TournamentSessionSnapshot updated)
    {
        bool wasLocalMember = HasLocalMissionMember(snapshot);
        string previousMatchId = snapshot.CurrentMatchId;
        string previousHost = snapshot.HostControllerId;
        TournamentSessionSnapshot previous = snapshot;
        ResetSubmittedState(updated, previousMatchId);
        snapshot = updated;
        ApplySessionState(updated);
        Mission.AllowAiTicking = session.IsLocalHost;
        if (missionReadyForManifest)
            spectatorAgentManager.Reconcile(updated);
        tournamentBehavior.ApplySnapshot(updated);
        KnockOutDepartingContestants(previous, updated);
        if (wasLocalMember && !HasLocalMissionMember(updated))
        {
            Mission.Current?.EndMission();
            return;
        }

        if (leaveRequested)
            SendAuthoritativeLeaveRequest();
        if (updated.HostControllerId != previousHost)
            TransferHostAuthority(previousHost);

        BeginUpdatedMatch(previous, updated);
        if (updated.CurrentMatchId != previousMatchId)
            ResetMatchRuntimeState();
        TryStartHostMatch();
        DrainPendingTournamentPackets();
    }

    private void ResetSubmittedState(TournamentSessionSnapshot updated, string previousMatchId)
    {
        if (updated.CurrentMatchId == previousMatchId && pendingManifest != null &&
            updated.Revision > pendingManifest.Revision)
            submittedManifestMatchId = null;
        if (updated.CurrentMatchId == previousMatchId && pendingResult != null &&
            updated.Revision > pendingResult.Revision)
            submittedResultMatchId = null;
    }

    private void BeginUpdatedMatch(
        TournamentSessionSnapshot previous,
        TournamentSessionSnapshot updated)
    {
        if (string.IsNullOrEmpty(updated.CurrentMatchId) ||
            TournamentMatchTransitionRules.PreservesRunningMatch(previous, updated))
            return;

        matchLifecycle.TryBeginMatch(
            updated.CurrentMatchId,
            updated.BracketRevision,
            TournamentMatchTransitionRules.RequiresArenaCleanup(previous, updated));
    }

    private void ResetMatchRuntimeState()
    {
        startedMatchId = null;
        submittedManifestMatchId = null;
        submittedResultMatchId = null;
        publishedRoundResultMatchId = null;
        presentedRoundResultMatchId = null;
        appliedManifestSequence = 0;
        latestManifest = null;
        pendingManifest = null;
        pendingApplyManifest = null;
        pendingResult = null;
        receivedDamageSequences.Clear();
        appliedDamageSequences.Clear();
        receivedKnockoutSequences.Clear();
        receivedRuntimeSequences.Clear();
        pendingKnockouts.Clear();
        RemovePendingTournamentPacketsForOtherMatches();
        damageSequence = 0;
        knockoutSequence = 0;
        runtimeSequence = 0;
        latestRuntimeState = null;
        resultReadyElapsed = 0f;
        manifestAgentData.Clear();
        manifestAgentInstances.Clear();
        ResetNativeFightState();
        agentSpawner.Reset();
    }
    private bool HasLocalMissionMember(TournamentSessionSnapshot state)
    {
        if (state.SpectatorControllerIds.Contains(session.OwnControllerId)) return true;
        return state.Contestants.Any(contestant =>
            contestant.ControllerId == session.OwnControllerId &&
            contestant.IsHuman &&
            !contestant.IsReplaced);
    }

    private void KnockOutDepartingContestants(
        TournamentSessionSnapshot previous,
        TournamentSessionSnapshot updated)
    {
        if (latestManifest == null || previous.Phase != TournamentSessionPhase.LiveMatch) return;
        if (previous.CurrentMatchId != updated.CurrentMatchId) return;

        TournamentMatchData current = previous.Rounds
            .SelectMany(round => round.Matches)
            .FirstOrDefault(match => match.MatchId == previous.CurrentMatchId);
        if (current == null) return;

        foreach (TournamentContestantData oldContestant in previous.Contestants)
        {
            if (!TryResolveDepartingAgent(oldContestant, updated, current, out var data, out var agent))
                continue;
            RemoveDepartingAgent(data, agent);
        }
    }

    private bool TryResolveDepartingAgent(
        TournamentContestantData oldContestant,
        TournamentSessionSnapshot updated,
        TournamentMatchData current,
        out TournamentAgentSpawnData data,
        out Agent agent)
    {
        data = null;
        agent = null;
        if (!oldContestant.IsHuman || oldContestant.IsReplaced) return false;

        TournamentContestantData replacement = updated.Contestants
            .FirstOrDefault(contestant => contestant.SlotId == oldContestant.SlotId);
        if (replacement == null || !replacement.IsReplaced) return false;
        if (!current.Teams.Any(team => team.ParticipantSlotIds.Contains(oldContestant.SlotId))) return false;

        data = latestManifest.Agents
            .FirstOrDefault(candidate => candidate.SlotId == oldContestant.SlotId);
        if (data == null ||
            !coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.AgentId, out var info))
            return false;

        agent = info.Agent;
        return true;
    }

    private void RemoveDepartingAgent(TournamentAgentSpawnData data, Agent agent)
    {
        Agent mount = agent?.MountAgent;
        if (data.MountAgentId != Guid.Empty &&
            coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.MountAgentId, out var mountInfo))
            mount = mountInfo.Agent;
        RemoveDepartingAgentInstance(mount);
        RemoveDepartingAgentInstance(agent);

        if (Mission.Current?.MainAgent == agent)
            Mission.Current.MainAgent = null;
        coopMissionComponent.AgentRegistry.RemoveAgent(data.AgentId);
        if (data.MountAgentId != Guid.Empty)
            coopMissionComponent.AgentRegistry.RemoveAgent(data.MountAgentId);
    }

    private void RemoveDepartingAgentInstance(Agent agent)
    {
        if (agent == null || !agent.IsActive()) return;

        coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
        agent.Controller = AgentControllerType.None;
        agent.FadeOut(false, true);
    }
    private void ApplySessionState(TournamentSessionSnapshot state)
    {
        session.TryApplyState(
            state.SessionId,
            state.MissionInstanceId,
            state.Revision,
            state.BracketRevision,
            state.CurrentMatchId,
            state.HostControllerId,
            state.SuccessorControllerIds);
    }

    private void Handle_ManifestUpdated(MessagePayload<TournamentSpawnManifestUpdated> payload)
    {
        TournamentSpawnManifestData manifest = TournamentManifestAuthority.Normalize(payload.What.Manifest, snapshot);
        if (manifest == null || snapshot == null) return;
        if (manifest.SessionId != snapshot.SessionId) return;
        if (manifest.MatchId != snapshot.CurrentMatchId) return;
        if (manifest.BracketRevision != snapshot.BracketRevision) return;
        if (manifest.Revision > snapshot.Revision) return;
        if (manifest.Sequence <= appliedManifestSequence) return;

        appliedManifestSequence = manifest.Sequence;
        pendingApplyManifest = manifest;
        if (!missionReadyForManifest)
        {
            Logger.Information(
                "[Tournament] Deferring spawn manifest for {MatchId} until the mission reaches its first tick: agents={AgentCount}, sequence={Sequence}",
                manifest.MatchId,
                manifest.Agents?.Length ?? 0,
                manifest.Sequence);
            return;
        }
        ApplyPendingManifest();
    }

    private void ApplyPendingManifest()
    {
        TournamentSpawnManifestData manifest = pendingApplyManifest;
        if (manifest == null || !missionReadyForManifest) return;

        pendingApplyManifest = null;
        latestManifest = manifest;
        pendingManifest = null;
        Logger.Information(
            "[Tournament] Applying spawn manifest for {MatchId} after mission readiness: agents={AgentCount}, sequence={Sequence}",
            manifest.MatchId,
            manifest.Agents?.Length ?? 0,
            manifest.Sequence);
        agentSpawner.ApplyManifest(manifest, snapshot, session);
        CaptureManifestAgents(manifest);
        RefreshNativeFightState();
        if (latestRuntimeState?.MatchId == manifest.MatchId)
            ApplyRuntimeState(latestRuntimeState);
        DrainPendingTournamentPackets();
        if (session.IsLocalHost && startedMatchId == manifest.MatchId)
        {
            Mission.Current.AllowAiTicking = true;
            PublishRuntimeState();
        }
    }

    private void Handle_RuntimeState(MessagePayload<NetworkTournamentRuntimeState> payload)
    {
        NetworkTournamentRuntimeState state = payload.What;
        if (state == null) return;
        GameThread.RunSafe(() => QueueOrApplyTournamentPacket(state));
    }

    public bool InterceptBlow(Agent victim, Blow blow, AttackCollisionData collisionData)
    {
        Agent attacker = Mission.Current?.FindAgentWithIndex(blow.OwnerId);
        if (spectatorAgentManager.IsSpectatorAgent(victim) || spectatorAgentManager.IsSpectatorAgent(attacker))
            return false;
        if (snapshot == null || snapshot.Phase != TournamentSessionPhase.LiveMatch) return true;
        if (victim == null || !coopMissionComponent.AgentRegistry.TryGetAgentInfo(victim, out var victimInfo))
            return true;
        if (applyingDamage.Contains(victimInfo.AgentId)) return true;

        Guid attackerId = Guid.Empty;
        if (attacker == null)
        {
            if (victimInfo.CurrentAuthority != session.OwnControllerId) return false;
        }
        else
        {
            if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(attacker, out var attackerInfo)) return true;
            if (attackerInfo.CurrentAuthority != session.OwnControllerId) return false;
            attackerId = attackerInfo.AgentId;
        }

        if (blow.InflictedDamage > 0)
        {
            long sequence = ++damageSequence;
            var message = new NetworkApplyTournamentDamage(
                snapshot.SessionId,
                snapshot.CurrentMatchId,
                snapshot.Revision,
                session.OwnControllerId,
                sequence,
                victimInfo.AgentId,
                attackerId,
                blow,
                collisionData);
            receivedDamageSequences.TryAccept(session.OwnControllerId, sequence);
            network.SendAll(message);
            ApplyTournamentDamage(message);
        }
        return false;
    }

    private void Handle_ApplyTournamentDamage(MessagePayload<NetworkApplyTournamentDamage> payload)
    {
        NetworkApplyTournamentDamage message = payload.What;
        if (message == null) return;
        GameThread.RunSafe(() => QueueOrApplyTournamentPacket(message));
    }

    private void ApplyTournamentDamage(NetworkApplyTournamentDamage message)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(message.VictimAgentId, out var victimInfo)) return;
        CoopAgentInfo attackerInfo = null;
        if (message.AttackerAgentId != Guid.Empty &&
            !registry.TryGetAgentInfo(message.AttackerAgentId, out attackerInfo)) return;
        if (!TournamentDamageAuthority.IsValidOrigin(
                message.OriginControllerId,
                victimInfo.CurrentAuthority,
                message.AttackerAgentId,
                attackerInfo?.CurrentAuthority)) return;

        Agent victim = victimInfo.Agent;
        if (victim == null || !victim.IsActive() || victim.Health <= 0) return;
        Blow blow = message.Blow;
        AttackCollisionData collisionData = message.CollisionData;
        if (message.AttackerAgentId != Guid.Empty &&
            attackerInfo != null)
            blow.OwnerId = attackerInfo.Agent.Index;
        else
            blow.OwnerId = -1;

        if (blow.IsMissile)
        {
            blow.WeaponRecord._isMissile = false;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
        }

        applyingDamage.Add(message.VictimAgentId);
        NetworkApplyTournamentDamage previousDamageMessage = activeDamageMessage;
        activeDamageMessage = message;
        try
        {
            RegisterBlowPatch.RunOriginalRegisterBlow(victim, blow, collisionData);
        }
        finally
        {
            activeDamageMessage = previousDamageMessage;
            applyingDamage.Remove(message.VictimAgentId);
        }

        appliedDamageSequences.TryAccept(message.OriginControllerId, message.Sequence);
        ApplyPendingKnockouts();
    }

    private void CaptureHitProgression(
        Agent affectedAgent,
        Agent affectorAgent,
        WeaponComponentData attackerWeapon,
        in Blow blow,
        in AttackCollisionData collisionData,
        float shotDifficulty)
    {
        NetworkApplyTournamentDamage damage = activeDamageMessage;
        if (!session.IsLocalHost || damage == null ||
            snapshot?.Phase != TournamentSessionPhase.LiveMatch ||
            affectedAgent == null || affectorAgent == null ||
            damage.MatchId != snapshot.CurrentMatchId) return;
        TournamentAgentSpawnData attackerSpawn = FindManifestAgent(damage.AttackerAgentId);
        TournamentAgentSpawnData victimSpawn = FindManifestAgent(damage.VictimAgentId);
        if (attackerSpawn == null || victimSpawn == null) return;
        TournamentContestantData attacker = snapshot.Contestants.FirstOrDefault(data =>
            data.SlotId == attackerSpawn.SlotId);
        if (attacker == null || !attacker.IsHuman || attacker.IsReplaced ||
            attacker.ControllerId != damage.OriginControllerId) return;

        float damageAmount = Math.Min(blow.InflictedDamage, affectedAgent.HealthLimit);
        if (damageAmount <= 0 || affectedAgent.HealthLimit <= 0) return;
        if (!TryResolveHitWeapon(
                affectorAgent,
                attackerWeapon,
                out string weaponItemId,
                out int weaponUsageIndex)) return;

        var progression = new TournamentHitProgressionData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            damage.OriginControllerId,
            damage.Sequence,
            damage.AttackerAgentId,
            damage.VictimAgentId,
            weaponItemId,
            weaponUsageIndex,
            blow.MovementSpeedDamageModifier,
            shotDifficulty,
            0.5f * damageAmount / affectedAgent.HealthLimit,
            damageAmount,
            (int)blow.AttackType,
            affectorAgent.MountAgent != null,
            affectorAgent.Team == affectedAgent.Team,
            affectedAgent.Health < 1f,
            affectorAgent.MountAgent != null &&
            blow.AttackType == AgentAttackType.Collision,
            collisionData.IsSneakAttack);
        relayNetwork.SendAll(new NetworkSubmitTournamentHitProgression(progression));
    }

    private bool TryResolveHitWeapon(
        Agent affectorAgent,
        WeaponComponentData attackerWeapon,
        out string itemId,
        out int usageIndex)
    {
        itemId = null;
        usageIndex = -1;
        if (attackerWeapon == null) return true;
        foreach (Agent agent in new[] { affectorAgent, affectorAgent?.MountAgent })
        {
            if (agent?.Equipment == null) continue;
            for (int slot = 0; slot < (int)EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                MissionWeapon weapon = agent.Equipment[(EquipmentIndex)slot];
                if (weapon.IsEmpty || weapon.Item == null) continue;
                for (int usage = 0; usage < weapon.Item.Weapons.Count; usage++)
                {
                    WeaponComponentData candidate = weapon.Item.Weapons[usage];
                    if (!ReferenceEquals(candidate, attackerWeapon) &&
                        (candidate.ItemUsage != attackerWeapon.ItemUsage ||
                         candidate.WeaponClass != attackerWeapon.WeaponClass)) continue;
                    if (!objectManager.TryGetId(weapon.Item, out itemId)) return false;
                    usageIndex = usage;
                    return true;
                }
            }
        }
        return false;
    }

    private TournamentAgentSpawnData FindManifestAgent(Guid agentId) =>
        latestManifest?.Agents?.FirstOrDefault(data =>
            data != null && (data.AgentId == agentId || data.MountAgentId == agentId));

    public override void OnAgentRemoved(
        Agent affectedAgent,
        Agent affectorAgent,
        AgentState agentState,
        KillingBlow killingBlow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
        if (matchLifecycle.IsClearing || applyingRuntimeState ||
            snapshot?.Phase != TournamentSessionPhase.LiveMatch ||
            affectedAgent == null || affectedAgent.IsMount) return;

        Guid agentId;
        string authority;
        if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(affectedAgent, out var info))
        {
            agentId = info.AgentId;
            authority = info.CurrentAuthority;
        }
        else if (manifestAgentData.TryGetValue(affectedAgent, out var manifestData))
        {
            agentId = manifestData.AgentId;
            authority = manifestData.ControllerId;
        }
        else
        {
            Logger.Warning(
                "[Tournament] Agent removed in {MatchId}, but no manifest identity was found",
                snapshot.CurrentMatchId);
            return;
        }

        Logger.Information(
            "[Tournament] Agent removed in {MatchId}: agent={AgentId}, authority={Authority}, local={LocalController}, host={IsHost}",
            snapshot.CurrentMatchId,
            agentId,
            authority,
            session.OwnControllerId,
            session.IsLocalHost);

        if (authority == session.OwnControllerId)
        {
            NetworkApplyTournamentDamage damage = activeDamageMessage?.VictimAgentId == agentId
                ? activeDamageMessage : null;
            long sequence = ++knockoutSequence;
            var message = new NetworkTournamentAgentKnockedOut(
                snapshot.SessionId,
                snapshot.CurrentMatchId,
                snapshot.Revision,
                session.OwnControllerId,
                sequence,
                agentId,
                damage?.OriginControllerId,
                damage?.Sequence ?? 0);
            receivedKnockoutSequences.TryAccept(session.OwnControllerId, sequence);
            network.SendAll(message);
            RemoveOwnedKnockoutRegistration(agentId);
        }

        if (session.IsLocalHost)
            PublishRuntimeState();
    }

    private void RemoveOwnedKnockoutRegistration(Guid agentId)
    {
        coopMissionComponent.AgentRegistry.RemoveAgent(agentId);
        TournamentAgentSpawnData data = latestManifest?.Agents?
            .FirstOrDefault(record => record.AgentId == agentId);
        if (data != null && data.MountAgentId != Guid.Empty)
            coopMissionComponent.AgentRegistry.RemoveAgent(data.MountAgentId);
    }

    private void Handle_AgentKnockedOut(MessagePayload<NetworkTournamentAgentKnockedOut> payload)
    {
        NetworkTournamentAgentKnockedOut message = payload.What;
        if (message == null) return;
        GameThread.RunSafe(() => QueueOrApplyTournamentPacket(message));
    }

    private void ApplyKnockout(NetworkTournamentAgentKnockedOut message)
    {
        TournamentAgentSpawnData data = latestManifest?.Agents
            .FirstOrDefault(record => record.AgentId == message.AgentId);
        if (data == null || data.ControllerId != message.OriginControllerId) return;
        if (data.ControllerId == session.OwnControllerId) return;
        if (ShouldDeferKnockout(message))
        {
            pendingKnockouts.Add(message);
            return;
        }

        ApplyKnockout(message, data);
    }

    private void ApplyKnockout(NetworkTournamentAgentKnockedOut message, TournamentAgentSpawnData data)
    {
        var registry = coopMissionComponent.AgentRegistry;
        Agent agent;
        if (registry.TryGetAgentInfo(message.AgentId, out var info))
            agent = info.Agent;
        else
            manifestAgentInstances.TryGetValue(message.AgentId, out agent);

        Logger.Information(
            "[Tournament] Applying remote knockout in {MatchId}: agent={AgentId}, authority={Authority}, host={IsHost}",
            snapshot.CurrentMatchId,
            message.AgentId,
            message.OriginControllerId,
            session.IsLocalHost);

        applyingRuntimeState = true;
        try
        {
            if (agent != null && agent.IsActive()) agent.FadeOut(false, true);
            registry.RemoveAgent(message.AgentId);

            if (data.MountAgentId != Guid.Empty)
            {
                Agent mount;
                if (registry.TryGetAgentInfo(data.MountAgentId, out var mountInfo))
                    mount = mountInfo.Agent;
                else
                    manifestAgentInstances.TryGetValue(data.MountAgentId, out mount);
                if (mount != null && mount.IsActive()) mount.FadeOut(false, true);
                registry.RemoveAgent(data.MountAgentId);
            }
        }
        finally
        {
            applyingRuntimeState = false;
        }

        if (session.IsLocalHost)
            PublishRuntimeState();
    }

    private bool ShouldDeferKnockout(NetworkTournamentAgentKnockedOut message) =>
        session.IsLocalHost &&
        !string.IsNullOrEmpty(message.DamageOriginControllerId) &&
        message.DamageSequence > 0 &&
        !appliedDamageSequences.HasReached(message.DamageOriginControllerId, message.DamageSequence);

    private void ApplyPendingKnockouts()
    {
        if (!session.IsLocalHost || pendingKnockouts.Count == 0) return;
        for (int index = pendingKnockouts.Count - 1; index >= 0; index--)
        {
            NetworkTournamentAgentKnockedOut message = pendingKnockouts[index];
            if (ShouldDeferKnockout(message)) continue;
            pendingKnockouts.RemoveAt(index);
            ApplyKnockout(message);
        }
    }

    private void QueueOrApplyTournamentPacket(IEvent packet)
    {
        if (ShouldRetryTournamentPacket(packet))
            BufferTournamentPacket(packet);
        else
            pendingTournamentPackets.RemoveAll(candidate => IsSameTournamentPacket(candidate, packet));
    }

    private void DrainPendingTournamentPackets()
    {
        if (pendingTournamentPackets.Count == 0) return;

        IEvent[] pending = pendingTournamentPackets.ToArray();
        pendingTournamentPackets.Clear();
        foreach (IEvent packet in pending)
            if (ShouldRetryTournamentPacket(packet))
                BufferTournamentPacket(packet);
    }

    private bool ShouldRetryTournamentPacket(IEvent packet)
    {
        return packet switch
        {
            NetworkApplyTournamentDamage damage => ShouldRetryDamage(damage),
            NetworkTournamentRuntimeState runtime => ShouldRetryRuntimeState(runtime),
            NetworkTournamentAgentKnockedOut knockout => ShouldRetryKnockout(knockout),
            _ => false
        };
    }

    private bool ShouldRetryDamage(NetworkApplyTournamentDamage message)
    {
        if (string.IsNullOrEmpty(message.OriginControllerId) || message.VictimAgentId == Guid.Empty)
            return false;

        TournamentPacketState state = GetTournamentPacketState(
            message.SessionId,
            message.MatchId,
            message.Revision);
        if (state != TournamentPacketState.Ready)
            return state == TournamentPacketState.Pending;

        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(message.VictimAgentId, out var victimInfo)) return true;
        CoopAgentInfo attackerInfo = null;
        if (message.AttackerAgentId != Guid.Empty &&
            !registry.TryGetAgentInfo(message.AttackerAgentId, out attackerInfo)) return true;
        if (!TournamentDamageAuthority.IsValidOrigin(
                message.OriginControllerId,
                victimInfo.CurrentAuthority,
                message.AttackerAgentId,
                attackerInfo?.CurrentAuthority)) return false;
        if (!receivedDamageSequences.TryAccept(message.OriginControllerId, message.Sequence)) return false;

        ApplyTournamentDamage(message);
        return false;
    }

    private bool ShouldRetryRuntimeState(NetworkTournamentRuntimeState state)
    {
        if (string.IsNullOrEmpty(state.OriginControllerId)) return false;

        TournamentPacketState packetState = GetTournamentPacketState(
            state.SessionId,
            state.MatchId,
            state.Revision);
        if (packetState != TournamentPacketState.Ready)
            return packetState == TournamentPacketState.Pending;
        if (state.OriginControllerId != session.HostControllerId) return false;
        if (latestManifest == null || latestManifest.MatchId != state.MatchId) return true;
        if (!AreRuntimeStateAgentsResolved(state)) return true;
        if (!receivedRuntimeSequences.TryAccept(state.OriginControllerId, state.Sequence)) return false;

        latestRuntimeState = state;
        ApplyRuntimeState(state);
        return false;
    }

    private bool AreRuntimeStateAgentsResolved(NetworkTournamentRuntimeState state)
    {
        foreach (Guid agentId in TournamentRuntimeStateRules.GetAgents(state).Keys)
        {
            bool belongsToManifest = latestManifest.Agents.Any(data =>
                data.AgentId == agentId || data.MountAgentId == agentId);
            if (!belongsToManifest ||
                !TournamentRuntimeAuthority.ShouldApplyHostAggregate(
                    agentId,
                    latestManifest,
                    snapshot,
                    session.OwnControllerId)) continue;
            if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(agentId, out _)) return false;
        }
        return true;
    }

    private bool ShouldRetryKnockout(NetworkTournamentAgentKnockedOut message)
    {
        if (string.IsNullOrEmpty(message.OriginControllerId) || message.AgentId == Guid.Empty)
            return false;

        TournamentPacketState state = GetTournamentPacketState(
            message.SessionId,
            message.MatchId,
            message.Revision);
        if (state != TournamentPacketState.Ready)
            return state == TournamentPacketState.Pending;
        if (latestManifest == null || latestManifest.MatchId != message.MatchId) return true;

        TournamentAgentSpawnData data = latestManifest.Agents
            .FirstOrDefault(record => record.AgentId == message.AgentId);
        if (data == null || data.ControllerId != message.OriginControllerId) return false;
        if (data.ControllerId == session.OwnControllerId) return false;
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(message.AgentId, out _) &&
            !manifestAgentInstances.ContainsKey(message.AgentId)) return true;
        if (!receivedKnockoutSequences.TryAccept(message.OriginControllerId, message.Sequence)) return false;

        ApplyKnockout(message);
        return false;
    }

    private TournamentPacketState GetTournamentPacketState(
        string sessionId,
        string matchId,
        long revision)
    {
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(matchId))
            return TournamentPacketState.Rejected;
        if (snapshot == null) return TournamentPacketState.Pending;
        if (sessionId != snapshot.SessionId) return TournamentPacketState.Rejected;
        if (revision > snapshot.Revision) return TournamentPacketState.Pending;
        if (matchId != snapshot.CurrentMatchId) return TournamentPacketState.Rejected;
        return TournamentPacketState.Ready;
    }

    private void BufferTournamentPacket(IEvent packet)
    {
        if (pendingTournamentPackets.Any(candidate => IsSameTournamentPacket(candidate, packet))) return;
        if (pendingTournamentPackets.Count >= MaximumPendingTournamentPackets)
        {
            pendingTournamentPackets.RemoveAt(0);
            Logger.Warning(
                "[Tournament] Dropping the oldest pending tournament packet after reaching the {Maximum} packet limit",
                MaximumPendingTournamentPackets);
        }
        pendingTournamentPackets.Add(packet);
    }

    private void RemovePendingTournamentPacketsForOtherMatches()
    {
        if (snapshot == null)
        {
            pendingTournamentPackets.Clear();
            return;
        }

        pendingTournamentPackets.RemoveAll(packet =>
            !TryGetTournamentPacketIdentity(packet, out string sessionId, out string matchId, out _, out _) ||
            sessionId != snapshot.SessionId ||
            matchId != snapshot.CurrentMatchId);
    }

    private static bool IsSameTournamentPacket(IEvent first, IEvent second)
    {
        if (first?.GetType() != second?.GetType()) return false;
        return TryGetTournamentPacketIdentity(first, out string firstSession, out string firstMatch,
                out string firstOrigin, out long firstSequence) &&
            TryGetTournamentPacketIdentity(second, out string secondSession, out string secondMatch,
                out string secondOrigin, out long secondSequence) &&
            firstSession == secondSession &&
            firstMatch == secondMatch &&
            firstOrigin == secondOrigin &&
            firstSequence == secondSequence;
    }

    private static bool TryGetTournamentPacketIdentity(
        IEvent packet,
        out string sessionId,
        out string matchId,
        out string originControllerId,
        out long sequence)
    {
        switch (packet)
        {
            case NetworkApplyTournamentDamage damage:
                sessionId = damage.SessionId;
                matchId = damage.MatchId;
                originControllerId = damage.OriginControllerId;
                sequence = damage.Sequence;
                return true;
            case NetworkTournamentRuntimeState runtime:
                sessionId = runtime.SessionId;
                matchId = runtime.MatchId;
                originControllerId = runtime.OriginControllerId;
                sequence = runtime.Sequence;
                return true;
            case NetworkTournamentAgentKnockedOut knockout:
                sessionId = knockout.SessionId;
                matchId = knockout.MatchId;
                originControllerId = knockout.OriginControllerId;
                sequence = knockout.Sequence;
                return true;
            default:
                sessionId = null;
                matchId = null;
                originControllerId = null;
                sequence = 0;
                return false;
        }
    }

    private enum TournamentPacketState
    {
        Ready,
        Pending,
        Rejected
    }

    protected override void SendJoinInfo(string controllerId)
    {
        var joinInfo = new NetworkMissionJoinInfo(
            session.OwnControllerId,
            Mission.Current?.MainAgent?.IsActive() == true,
            spectatorAgentManager.GetLocalSpawnData());
        network.Send(controllerId, joinInfo);

        if (!session.IsLocalHost || latestManifest == null) return;
        PublishRuntimeState();
        if (latestRuntimeState != null)
            network.Send(controllerId, latestRuntimeState);
    }

    protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo)
    {
        spectatorAgentManager.HandleJoinInfo(joinInfo);
    }

    public override void OnMissionTick(float dt)
    {
        if (!missionReadyForManifest)
        {
            missionReadyForManifest = true;
            ApplyPendingManifest();
            spectatorAgentManager.Reconcile(snapshot);
        }
        base.OnMissionTick(dt);
#if DEBUG
        combatFixture.Tick(dt, coopMissionComponent.AgentRegistry);
#endif
        if (snapshot == null || !session.IsLocalHost) return;

        TryStartHostMatch();
        TrySubmitSpawnManifest();
        TrySubmitMatchResult(dt);
    }

    private void TryStartHostMatch()
    {
        if (snapshot.Phase != TournamentSessionPhase.LiveMatch) return;
        if (!session.IsLocalHost || string.IsNullOrEmpty(snapshot.CurrentMatchId)) return;
        if (startedMatchId == snapshot.CurrentMatchId) return;

        Mission.Current.AllowAiTicking = false;
        bool isLastRound = tournamentBehavior.CurrentRoundIndex == tournamentBehavior.Rounds.Length - 1;
        tournamentBehavior.CurrentMatch.Start();
        Mission.Current.SetMissionMode(TaleWorlds.Core.MissionMode.Tournament, true);
        fightController.StartMatch(tournamentBehavior.CurrentMatch, isLastRound);
        startedMatchId = snapshot.CurrentMatchId;
    }

    private void TrySubmitSpawnManifest()
    {
        if (startedMatchId != snapshot.CurrentMatchId) return;
        if (submittedManifestMatchId == snapshot.CurrentMatchId) return;
        if (TournamentManifestAuthority.CanResume(latestManifest, snapshot))
        {
            submittedManifestMatchId = snapshot.CurrentMatchId;
            pendingManifest = null;
            return;
        }

        TournamentSpawnManifestData manifest;
        if (pendingManifest == null)
        {
            manifest = manifestBuilder.Build(
                snapshot,
                tournamentBehavior,
                fightController,
                ++manifestSequence,
                session);
        }
        else
        {
            manifest = new TournamentSpawnManifestData(
                pendingManifest.SessionId,
                pendingManifest.MatchId,
                snapshot.Revision,
                snapshot.BracketRevision,
                ++manifestSequence,
                pendingManifest.Agents);
        }
        if (manifest == null) return;

        latestManifest = manifest;
        CaptureManifestAgents(manifest);
        submittedManifestMatchId = snapshot.CurrentMatchId;
        pendingManifest = manifest;
        relayNetwork.SendAll(new NetworkSubmitTournamentSpawnManifest(manifest));
    }

    private void TrySubmitMatchResult(float dt)
    {
        if (startedMatchId != snapshot.CurrentMatchId) return;
        if (submittedResultMatchId == snapshot.CurrentMatchId) return;
        if (!IsMatchReadyForResult(dt)) return;
        if (!TryCreateMatchResult(out TournamentMatchResultData result)) return;

        submittedResultMatchId = snapshot.CurrentMatchId;
        pendingResult = result;
        relayNetwork.SendAll(new NetworkSubmitTournamentMatchResult(result));
    }

    private bool IsMatchReadyForResult(float dt)
    {
        bool resultsWereReady = fightController._cheerStarted;
        bool matchEnded = fightController.IsMatchEnded();
        if (!resultsWereReady && fightController._cheerStarted)
            PublishRoundResult();
        if (fightController._cheerStarted && !matchEnded)
        {
            resultReadyElapsed += Math.Max(dt, 0f);
            if (resultReadyElapsed > 5.25f)
            {
                Logger.Warning(
                    "[Tournament] Native completion timer stalled for {MatchId}; forcing completion after the vanilla result delay",
                    snapshot.CurrentMatchId);
                fightController._forceEndMatch = true;
                matchEnded = fightController.IsMatchEnded();
            }
        }
        return matchEnded;
    }

    private bool TryCreateMatchResult(out TournamentMatchResultData result)
    {
        if (pendingResult != null)
        {
            result = new TournamentMatchResultData(
                pendingResult.SessionId,
                pendingResult.MatchId,
                snapshot.Revision,
                snapshot.BracketRevision,
                ++resultSequence,
                pendingResult.WinnerTeamIds,
                pendingResult.WinnerSlotIds,
                pendingResult.TeamScores);
            return true;
        }

        if (!TryGetCurrentMatchData(out TournamentMatchData matchData))
        {
            result = null;
            return false;
        }

        var nativeTeams = tournamentBehavior.CurrentMatch.Teams.ToArray();
        var winners = tournamentBehavior.CurrentMatch.GetWinners();
        string[] winnerSlots = tournamentBehavior._participants
            .Select((participant, index) => new { participant, index })
            .Where(entry => winners.Contains(entry.participant))
            .Select(entry => snapshot.Contestants[entry.index].SlotId)
            .ToArray();
        string[] winnerTeams = matchData.Teams
            .Where((team, index) => nativeTeams[index].Participants.Any(winners.Contains))
            .Select(team => team.TeamId)
            .ToArray();
        TournamentTeamScoreData[] scores = matchData.Teams
            .Select((team, index) => new TournamentTeamScoreData(team.TeamId, nativeTeams[index].Score))
            .ToArray();
        Logger.Information(
            "[Tournament] Match ended for {MatchId}: aliveParticipants={AliveParticipantCount}, aliveTeams={AliveTeamCount}, winnerTeams={WinnerTeams}, winnerSlots={WinnerSlots}",
            snapshot.CurrentMatchId,
            fightController._aliveParticipants?.Count ?? 0,
            fightController._aliveTeams?.Count ?? 0,
            string.Join(",", winnerTeams),
            string.Join(",", winnerSlots));
        result = new TournamentMatchResultData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            ++resultSequence,
            winnerTeams,
            winnerSlots,
            scores);
        return true;
    }

    private void PublishRoundResult()
    {
        if (publishedRoundResultMatchId == snapshot.CurrentMatchId) return;

        var winners = tournamentBehavior.CurrentMatch.GetWinners();
        string[] winnerSlots = tournamentBehavior._participants
            .Select((participant, index) => new { participant, index })
            .Where(entry => winners.Contains(entry.participant))
            .Select(entry => snapshot.Contestants[entry.index].SlotId)
            .ToArray();
        bool isLastRound = tournamentBehavior.CurrentRoundIndex == tournamentBehavior.Rounds.Length - 1;
        bool isTeamQualification = tournamentBehavior.CurrentMatch.QualificationMode ==
            TournamentGame.QualificationMode.TeamScore;

        publishedRoundResultMatchId = snapshot.CurrentMatchId;
        Logger.Information(
            "[Tournament] Publishing round result for match {MatchId}: winners={WinnerSlots}",
            snapshot.CurrentMatchId,
            string.Join(",", winnerSlots));
        network.SendAll(new NetworkTournamentRoundEnded(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            session.OwnControllerId,
            winnerSlots,
            isLastRound,
            isTeamQualification));
    }

    private void Handle_RoundEnded(MessagePayload<NetworkTournamentRoundEnded> payload)
    {
        NetworkTournamentRoundEnded result = payload.What;
        if (snapshot == null || result == null || result.SessionId != snapshot.SessionId ||
            result.MatchId != snapshot.CurrentMatchId || result.Revision != snapshot.Revision ||
            result.OriginControllerId != snapshot.HostControllerId ||
            presentedRoundResultMatchId == result.MatchId) return;

        GameThread.RunSafe(() =>
        {
            if (presentedRoundResultMatchId == result.MatchId) return;
            presentedRoundResultMatchId = result.MatchId;
            if (session.IsLocalHost) return;

            string text = TournamentRoundResultPresentation.GetText(
                snapshot,
                session.OwnControllerId,
                result);
            Logger.Information(
                "[Tournament] Presenting round result for match {MatchId} to {ControllerId}",
                result.MatchId,
                session.OwnControllerId);
            MBInformationManager.AddQuickInformation(new TextObject(text));
        });
    }

    private void PublishRuntimeState()
    {
        if (!session.IsLocalHost || latestManifest == null) return;
        TournamentMatchData match = snapshot.Rounds
            .SelectMany(round => round.Matches)
            .FirstOrDefault(data => data.MatchId == snapshot.CurrentMatchId);
        if (match == null) return;

        RefreshNativeFightState();
        if (!TryBuildRuntimeAgents(out TournamentAgentRuntimeData[] agents))
        {
            Logger.Warning("[Tournament] Failed to build runtime agents for {MatchId}", snapshot.CurrentMatchId);
            return;
        }
        if (!TrySerializeRuntimeWorldItems(out TournamentWorldItemRuntimeData[] worldItems))
        {
            Logger.Warning(
                "[Tournament] Failed to serialize runtime world items for {MatchId}; publishing alive state without them",
                snapshot.CurrentMatchId);
            worldItems = Array.Empty<TournamentWorldItemRuntimeData>();
        }
        var nativeTeams = tournamentBehavior.CurrentMatch.Teams.ToArray();
        TournamentTeamScoreData[] scores = match.Teams
            .Select((team, index) => new TournamentTeamScoreData(team.TeamId, nativeTeams[index].Score))
            .ToArray();
        long sequence = ++runtimeSequence;
        var state = new NetworkTournamentRuntimeState(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            session.OwnControllerId,
            sequence,
            scores,
            agents,
            worldItems);

        latestRuntimeState = state;
        receivedRuntimeSequences.TryAccept(session.OwnControllerId, sequence);
        Logger.Information(
            "[Tournament] Runtime state for {MatchId}: agents={AgentCount}",
            snapshot.CurrentMatchId,
            agents.Length);
        network.SendAll(state);
    }

    private bool TryBuildRuntimeAgents(out TournamentAgentRuntimeData[] agents)
    {
        var records = new List<TournamentAgentRuntimeData>();
        foreach (TournamentAgentSpawnData data in latestManifest.Agents)
        {
            if (!TryCreateRuntimeAgent(data.AgentId, out var rider))
            {
                agents = null;
                return false;
            }
            if (rider != null) records.Add(rider);
            if (data.MountAgentId == Guid.Empty) continue;
            if (!TryCreateRuntimeAgent(data.MountAgentId, out var mount))
            {
                agents = null;
                return false;
            }
            if (mount != null) records.Add(mount);
        }
        agents = records.ToArray();
        return true;
    }

    private bool TryCreateRuntimeAgent(
        Guid agentId,
        out TournamentAgentRuntimeData runtime)
    {
        runtime = null;
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(agentId, out var info)) return true;
        Agent agent = info.Agent;
        if (agent == null || !agent.IsActive() || agent.Health <= 0) return true;
        if (!TrySerializeRuntimeEquipment(agent.Equipment, out var equipment))
        {
            Logger.Warning(
                "[Tournament] Failed to serialize equipment for live agent {AgentId} in {MatchId}; publishing alive identity without equipment",
                agentId,
                snapshot.CurrentMatchId);
            equipment = Array.Empty<TournamentMissionWeaponData>();
        }
        runtime = new TournamentAgentRuntimeData(
            agentId,
            agent.Health,
            equipment);
        return true;
    }

    private void ApplyRuntimeState(NetworkTournamentRuntimeState state)
    {
        if (state == null || latestManifest == null || latestManifest.MatchId != state.MatchId) return;
        IReadOnlyDictionary<Guid, TournamentAgentRuntimeData> runtimeAgents =
            TournamentRuntimeStateRules.GetAgents(state);
        IReadOnlyDictionary<Guid, TournamentWorldItemRuntimeData> runtimeWorldItems =
            TournamentRuntimeStateRules.GetWorldItems(state);
        applyingRuntimeState = true;
        try
        {
            foreach (TournamentAgentSpawnData data in latestManifest.Agents)
            {
                ReconcileRuntimeAgent(data.AgentId, runtimeAgents);
                if (data.MountAgentId != Guid.Empty)
                    ReconcileRuntimeAgent(data.MountAgentId, runtimeAgents);
            }
            ReconcileRuntimeWorldItems(runtimeWorldItems);
            ApplyRuntimeTeamScores(state);
            RefreshNativeFightState();
        }
        finally
        {
            applyingRuntimeState = false;
        }
    }

    private void ReconcileRuntimeAgent(
        Guid agentId,
        IReadOnlyDictionary<Guid, TournamentAgentRuntimeData> runtimeAgents)
    {
        if (!TournamentRuntimeAuthority.ShouldApplyHostAggregate(
                agentId,
                latestManifest,
                snapshot,
                session.OwnControllerId)) return;

        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(agentId, out var info)) return;
        Agent agent = info.Agent;
        if (runtimeAgents.TryGetValue(agentId, out var runtime))
        {
            if (agent != null && agent.IsActive())
            {
                agent.Health = runtime.Health;
                ReconcileRuntimeEquipment(agent, runtime.Equipment);
            }
            return;
        }

        if (agent != null && agent.IsActive()) agent.FadeOut(false, true);
        registry.RemoveAgent(agentId);
    }

    private bool TrySerializeRuntimeEquipment(
        MissionEquipment equipment,
        out TournamentMissionWeaponData[] serialized)
    {
        if (equipment == null)
        {
            serialized = Array.Empty<TournamentMissionWeaponData>();
            return true;
        }

        var records = new List<TournamentMissionWeaponData>();
        for (int i = 0; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
        {
            MissionWeapon weapon = equipment[(EquipmentIndex)i];
            if (weapon.IsEmpty) continue;
            if (weapon.Item == null || !objectManager.TryGetId(weapon.Item, out string itemId))
            {
                serialized = null;
                return false;
            }

            string modifierId = null;
            if (weapon.ItemModifier != null &&
                !objectManager.TryGetId(weapon.ItemModifier, out modifierId))
            {
                serialized = null;
                return false;
            }
            records.Add(new TournamentMissionWeaponData(
                i,
                itemId,
                modifierId,
                weapon.Banner?.Serialize(),
                weapon.RawDataForNetwork));
        }
        serialized = records.ToArray();
        return true;
    }

    private bool TrySerializeRuntimeWorldItems(
        out TournamentWorldItemRuntimeData[] serialized)
    {
        var records = new List<TournamentWorldItemRuntimeData>();
        foreach (SpawnedItemEntity item in Mission.Current.MissionObjects.OfType<SpawnedItemEntity>())
        {
            Guid worldItemId = worldItemRegistry.GetOrCreateId(item);
            if (worldItemId == Guid.Empty || item == null || item.IsRemoved ||
                !item.GameEntity.IsValid) continue;

            MissionWeapon weapon = item.WeaponCopy;
            if (weapon.IsEmpty) continue;
            if (weapon.Item == null || !objectManager.TryGetId(weapon.Item, out string itemId))
            {
                serialized = null;
                return false;
            }
            string modifierId = null;
            if (weapon.ItemModifier != null &&
                !objectManager.TryGetId(weapon.ItemModifier, out modifierId))
            {
                serialized = null;
                return false;
            }
            MatrixFrame frame = item.GameEntity.GetGlobalFrame();
            records.Add(new TournamentWorldItemRuntimeData(
                worldItemId,
                itemId,
                modifierId,
                weapon.Banner?.Serialize(),
                weapon.RawDataForNetwork,
                frame.origin,
                frame.rotation,
                (int)item.SpawnFlags,
                item.HasLifeTime));
        }
        serialized = records.ToArray();
        return true;
    }

    private void ReconcileRuntimeEquipment(
        Agent agent,
        TournamentMissionWeaponData[] runtimeEquipment)
    {
        if (agent?.Equipment == null) return;
        if (runtimeEquipment != null &&
            runtimeEquipment.Length > (int)EquipmentIndex.NumAllWeaponSlots) return;

        Dictionary<int, TournamentMissionWeaponData> canonical =
            BuildCanonicalRuntimeEquipment(runtimeEquipment);
        for (int i = 0; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
            ReconcileRuntimeEquipmentSlot(agent, i, canonical);
    }

    private static Dictionary<int, TournamentMissionWeaponData> BuildCanonicalRuntimeEquipment(
        TournamentMissionWeaponData[] runtimeEquipment)
    {
        var canonical = new Dictionary<int, TournamentMissionWeaponData>();
        foreach (TournamentMissionWeaponData data in
                 runtimeEquipment ?? Array.Empty<TournamentMissionWeaponData>())
        {
            if (!IsValidRuntimeEquipment(data) || canonical.ContainsKey(data.SlotIndex))
                continue;
            canonical.Add(data.SlotIndex, data);
        }
        return canonical;
    }

    private static bool IsValidRuntimeEquipment(TournamentMissionWeaponData data)
    {
        return data != null &&
            data.SlotIndex >= 0 &&
            data.SlotIndex < (int)EquipmentIndex.NumAllWeaponSlots &&
            !string.IsNullOrEmpty(data.ItemId) &&
            data.ItemId.Length <= 256 &&
            (data.ItemModifierId?.Length ?? 0) <= 256 &&
            (data.BannerCode?.Length ?? 0) <= 4096;
    }

    private void ReconcileRuntimeEquipmentSlot(
        Agent agent,
        int slotIndex,
        IReadOnlyDictionary<int, TournamentMissionWeaponData> canonical)
    {
        EquipmentIndex slot = (EquipmentIndex)slotIndex;
        MissionWeapon current = agent.Equipment[slot];
        if (!canonical.TryGetValue(slotIndex, out var data))
        {
            if (!current.IsEmpty)
                agent.RemoveEquippedWeapon(slot);
            return;
        }
        if (!TryBuildRuntimeWeapon(data, out MissionWeapon weapon)) return;
        if (RuntimeWeaponMatches(current, weapon)) return;

        if (!current.IsEmpty)
            agent.RemoveEquippedWeapon(slot);
        agent.EquipWeaponWithNewEntity(slot, ref weapon);
    }
    private void ReconcileRuntimeWorldItems(
        IReadOnlyDictionary<Guid, TournamentWorldItemRuntimeData> canonical)
    {
        var registry = worldItemRegistry;
        foreach (var pair in registry.GetAll())
        {
            if (canonical.TryGetValue(pair.Key, out var data) &&
                RuntimeWorldItemMatches(pair.Value, data))
            {
                var frame = new MatrixFrame(data.Rotation, data.Position);
                pair.Value.StopPhysicsAndSetFrameForClient(frame, null);
                continue;
            }

            RemoveRuntimeWorldItem(pair.Key, pair.Value);
        }

        foreach (var pair in canonical)
        {
            if (registry.TryGet(pair.Key, out _)) continue;
            SpawnRuntimeWorldItem(pair.Value);
        }
    }

    private bool RuntimeWorldItemMatches(
        SpawnedItemEntity item,
        TournamentWorldItemRuntimeData data)
    {
        if (item == null || item.IsRemoved) return false;
        MissionWeapon current = item.WeaponCopy;
        return TryBuildRuntimeWeapon(data, out MissionWeapon canonical) &&
               RuntimeWeaponMatches(current, canonical);
    }

    private void SpawnRuntimeWorldItem(TournamentWorldItemRuntimeData data)
    {
        if (Mission.Current == null || !TryBuildRuntimeWeapon(data, out MissionWeapon weapon)) return;

        var frame = new MatrixFrame(data.Rotation, data.Position);
        GameEntity entity = Mission.Current.SpawnWeaponWithNewEntity(
            ref weapon,
            (Mission.WeaponSpawnFlags)data.SpawnFlags,
            frame);
        SpawnedItemEntity item = entity?.GetFirstScriptOfType<SpawnedItemEntity>();
        if (item == null)
        {
            entity?.Remove(0);
            return;
        }

        item.HasLifeTime = data.HasLifeTime;
        item.StopPhysicsAndSetFrameForClient(frame, null);
        worldItemRegistry.Register(data.WorldItemId, item);
    }

    private void RemoveRuntimeWorldItem(Guid itemId, SpawnedItemEntity item)
    {
        worldItemRegistry.Remove(itemId);
        if (item != null && !item.IsRemoved && item.GameEntity.IsValid)
            item.GameEntity.Remove(0);
    }

    private bool TryBuildRuntimeWeapon(
        TournamentMissionWeaponData data,
        out MissionWeapon weapon)
    {
        weapon = default;
        if (data == null || string.IsNullOrEmpty(data.ItemId) ||
            !objectManager.TryGetObject(data.ItemId, out ItemObject item)) return false;

        ItemModifier modifier = null;
        if (!string.IsNullOrEmpty(data.ItemModifierId) &&
            !objectManager.TryGetObject(data.ItemModifierId, out modifier)) return false;
        Banner banner = string.IsNullOrEmpty(data.BannerCode)
            ? null
            : new Banner(data.BannerCode);
        weapon = new MissionWeapon(item, modifier, banner, data.DataValue);
        return true;
    }

    private bool TryBuildRuntimeWeapon(
        TournamentWorldItemRuntimeData data,
        out MissionWeapon weapon)
    {
        if (data == null)
        {
            weapon = default;
            return false;
        }
        return TryBuildRuntimeWeapon(
            new TournamentMissionWeaponData(
                -1,
                data.ItemId,
                data.ItemModifierId,
                data.BannerCode,
                data.DataValue),
            out weapon);
    }

    private static bool RuntimeWeaponMatches(MissionWeapon current, MissionWeapon canonical) =>
        current.Item == canonical.Item &&
        current.ItemModifier == canonical.ItemModifier &&
        current.RawDataForNetwork == canonical.RawDataForNetwork &&
        current.Banner?.Serialize() == canonical.Banner?.Serialize();

    private void ApplyRuntimeTeamScores(NetworkTournamentRuntimeState state)
    {
        TournamentMatchData match = snapshot.Rounds
            .SelectMany(round => round.Matches)
            .FirstOrDefault(data => data.MatchId == state.MatchId);
        if (match == null) return;

        var scores = new Dictionary<string, TournamentTeamScoreData>();
        int inspectedScores = 0;
        foreach (TournamentTeamScoreData data in
                 state.TeamScores ?? Array.Empty<TournamentTeamScoreData>())
        {
            if (++inspectedScores > 4) break;
            if (data == null || string.IsNullOrEmpty(data.TeamId) ||
                scores.ContainsKey(data.TeamId)) continue;
            scores.Add(data.TeamId, data);
        }
        var nativeTeams = tournamentBehavior.CurrentMatch.Teams.ToArray();
        for (int i = 0; i < match.Teams.Length && i < nativeTeams.Length; i++)
        {
            if (!scores.TryGetValue(match.Teams[i].TeamId, out var score)) continue;
            var participant = nativeTeams[i].Participants.FirstOrDefault();
            if (participant != null && nativeTeams[i].Score != score.Score)
                participant.AddScore(score.Score - nativeTeams[i].Score);
        }
    }

    private void TransferHostAuthority(string previousHost)
    {
        if (string.IsNullOrEmpty(previousHost) ||
            !TournamentManifestAuthority.CanResume(latestManifest, snapshot)) return;

        TransferPreviousHostAgents(previousHost);
        // Carry the elected owner into the canonical local manifest. Without this rewrite a second
        // promotion would still look for the first host and strand every NPC under stale authority.
        latestManifest = TournamentManifestAuthority.Normalize(latestManifest, snapshot);
        if (session.IsLocalHost)
            ResumeHostFight();
    }

    private void TransferPreviousHostAgents(string previousHost)
    {
        var contestants = snapshot.Contestants.ToDictionary(contestant => contestant.SlotId);
        foreach (TournamentAgentSpawnData data in latestManifest.Agents)
        {
            if (!ShouldTransferAgent(data, previousHost, contestants)) continue;
            if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.AgentId, out var info)) continue;
            if (!coopMissionComponent.AgentRegistry.TryTransferAuthority(snapshot.HostControllerId, data.AgentId)) continue;
            if (data.MountAgentId != Guid.Empty)
                coopMissionComponent.AgentRegistry.TryTransferAuthority(snapshot.HostControllerId, data.MountAgentId);
            WakeTransferredAgent(info.Agent);
        }
    }

    private static bool ShouldTransferAgent(
        TournamentAgentSpawnData data,
        string previousHost,
        IReadOnlyDictionary<string, TournamentContestantData> contestants)
    {
        if (data.ControllerId != previousHost) return false;
        return !contestants.TryGetValue(data.SlotId, out var contestant) ||
            !contestant.IsHuman ||
            contestant.IsReplaced;
    }

    private void WakeTransferredAgent(Agent agent)
    {
        if (agent == null || agent.IsMount || !agent.IsActive() || !session.IsLocalHost) return;

        coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
        if (agent.MountAgent != null)
        {
            coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent.MountAgent);
            agent.MountAgent.SetMaximumSpeedLimit(-1f, isMultiplier: false);
            agent.MountAgent.Controller = AgentControllerType.AI;
        }
        agent.Controller = AgentControllerType.AI;
        AgentAiWaker.Wake(agent);
    }

    private void ResumeHostFight()
    {
        if (latestRuntimeState?.MatchId == snapshot.CurrentMatchId)
            ApplyRuntimeTeamScores(latestRuntimeState);

        List<Agent> liveAgents = ResolveLiveManifestAgents();
        tournamentBehavior.CurrentMatch.State = TournamentMatch.MatchState.Started;
        Mission.Current.SetMissionMode(MissionMode.Tournament, true);
        fightController._match = tournamentBehavior.CurrentMatch;
        fightController._currentTournamentAgents = liveAgents.Where(agent => !agent.IsMount).ToList();
        fightController._currentTournamentMountAgents = liveAgents.Where(agent => agent.IsMount).ToList();
        fightController._aliveParticipants = ResolveAliveParticipants();
        fightController._aliveTeams = ResolveAliveTeams();
        fightController._isLastRound = tournamentBehavior.CurrentRoundIndex == tournamentBehavior.Rounds.Length - 1;
        startedMatchId = snapshot.CurrentMatchId;
        submittedManifestMatchId = snapshot.CurrentMatchId;
        Mission.Current.AllowAiTicking = true;
        PublishRuntimeState();
    }

    private List<Agent> ResolveLiveManifestAgents()
    {
        bool hasRuntimeState = latestRuntimeState?.MatchId == snapshot.CurrentMatchId;
        var aliveAgentIds = new HashSet<Guid>(
            TournamentRuntimeStateRules.GetAgents(latestRuntimeState).Keys);
        return latestManifest.Agents
            .Select(data => coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.AgentId, out var info)
                ? info.Agent
                : null)
            .Where(agent => IsLiveManifestAgent(agent, hasRuntimeState, aliveAgentIds))
            .ToList();
    }

    private bool IsLiveManifestAgent(Agent agent, bool hasRuntimeState, HashSet<Guid> aliveAgentIds)
    {
        if (agent == null || !agent.IsActive()) return false;
        if (!hasRuntimeState) return true;
        return coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info) &&
            aliveAgentIds.Contains(info.AgentId);
    }
    private void ResetNativeFightState()
    {
        if (fightController == null) return;

        fightController._match = null;
        fightController._currentTournamentAgents = new List<Agent>();
        fightController._currentTournamentMountAgents = new List<Agent>();
        fightController._aliveParticipants = new List<TournamentParticipant>();
        fightController._aliveTeams = new List<TournamentTeam>();
        resultReadyElapsed = 0f;
        fightController._endTimer = null;
        fightController._cheerTimer = null;
        fightController._forceEndMatch = false;
        fightController._isSimulated = false;
        fightController._cheerStarted = false;
        fightController._isLastRound = false;
    }

    private void CaptureManifestAgents(TournamentSpawnManifestData manifest)
    {
        if (manifest?.Agents == null) return;

        foreach (TournamentAgentSpawnData data in manifest.Agents)
        {
            if (data == null) continue;
            if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.AgentId, out var riderInfo) &&
                riderInfo.Agent != null)
            {
                manifestAgentData[riderInfo.Agent] = data;
                manifestAgentInstances[data.AgentId] = riderInfo.Agent;
            }
            if (data.MountAgentId != Guid.Empty &&
                coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.MountAgentId, out var mountInfo) &&
                mountInfo.Agent != null)
            {
                manifestAgentData[mountInfo.Agent] = data;
                manifestAgentInstances[data.MountAgentId] = mountInfo.Agent;
            }
        }
    }

    private void RefreshNativeFightState()
    {
        if (latestManifest == null || tournamentBehavior?.CurrentMatch == null || fightController == null) return;

        var registry = coopMissionComponent.AgentRegistry;
        var liveAgents = latestManifest.Agents
            .SelectMany(data => new[] { data.AgentId, data.MountAgentId })
            .Where(agentId => agentId != Guid.Empty)
            .Select(agentId => registry.TryGetAgentInfo(agentId, out var info) ? info.Agent : null)
            .Where(agent => agent != null && agent.IsActive())
            .ToList();
        tournamentBehavior.CurrentMatch.State = TournamentMatch.MatchState.Started;
        fightController._match = tournamentBehavior.CurrentMatch;
        fightController._currentTournamentAgents = liveAgents.Where(agent => !agent.IsMount).ToList();
        fightController._currentTournamentMountAgents = liveAgents.Where(agent => agent.IsMount).ToList();
        fightController._aliveParticipants = ResolveAliveParticipants();
        fightController._aliveTeams = ResolveAliveTeams();
        fightController._isLastRound = tournamentBehavior.CurrentRoundIndex == tournamentBehavior.Rounds.Length - 1;
    }

    private System.Collections.Generic.List<TournamentParticipant> ResolveAliveParticipants()
    {
        string[] aliveSlots = GetAliveSlotIds();

        return snapshot.Contestants
            .Select((contestant, index) => new { contestant, index })
            .Where(entry => aliveSlots.Contains(entry.contestant.SlotId))
            .Select(entry => tournamentBehavior._participants[entry.index])
            .ToList();
    }

    private System.Collections.Generic.List<TournamentTeam> ResolveAliveTeams()
    {
        if (!TryGetCurrentMatchData(out TournamentMatchData match))
            return new List<TournamentTeam>();
        string[] aliveSlots = GetAliveSlotIds();
        string[] aliveTeams = match.Teams
            .Where(team => team.ParticipantSlotIds.Any(aliveSlots.Contains))
            .Select(team => team.TeamId)
            .ToArray();
        var nativeTeams = tournamentBehavior.CurrentMatch.Teams.ToArray();
        return match.Teams
            .Select((team, index) => new { team, index })
            .Where(entry => aliveTeams.Contains(entry.team.TeamId))
            .Select(entry => nativeTeams[entry.index])
            .ToList();
    }

    private bool TryGetCurrentMatchData(out TournamentMatchData match)
    {
        match = null;
        if (snapshot?.Rounds == null || string.IsNullOrEmpty(snapshot.CurrentMatchId))
            return false;

        TournamentMatchData[] matches = snapshot.Rounds
            .Where(round => round?.Matches != null)
            .SelectMany(round => round.Matches)
            .Where(data => data?.MatchId == snapshot.CurrentMatchId)
            .Take(2)
            .ToArray();
        if (matches.Length == 1)
        {
            match = matches[0];
            return true;
        }

        Logger.Error(
            "[Tournament] Expected one current match for {MatchId}, found {MatchCount}; session={SessionId}, revision={Revision}, bracketRevision={BracketRevision}",
            snapshot.CurrentMatchId,
            matches.Length,
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.BracketRevision);
        return false;
    }

    private string[] GetAliveSlotIds()
    {
        if (!session.IsLocalHost && latestRuntimeState?.MatchId == snapshot.CurrentMatchId)
        {
            var aliveAgentIds = new HashSet<Guid>(
                TournamentRuntimeStateRules.GetAgents(latestRuntimeState).Keys);
            return latestManifest.Agents
                .Where(data => aliveAgentIds.Contains(data.AgentId))
                .Select(data => data.SlotId)
                .ToArray();
        }
        return latestManifest.Agents
            .Where(data => coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.AgentId, out var info) &&
                           info.Agent != null && info.Agent.IsActive())
            .Select(data => data.SlotId)
            .ToArray();
    }

    public bool IsSpectatorAgent(Agent agent) => spectatorAgentManager.IsSpectatorAgent(agent);

    public bool IsSpectatorOrange(ItemObject item) => spectatorAgentManager.IsOrange(item);

    private void RequestAuthoritativeLeave()
    {
        leaveRequested = true;
        SendAuthoritativeLeaveRequest();
    }

    private void SendAuthoritativeLeaveRequest()
    {
        if (snapshot == null || snapshot.IsCompleted || !HasLocalMissionMember(snapshot)) return;
        if (leaveRequestRevision == snapshot.Revision) return;

        leaveRequestRevision = snapshot.Revision;
        relayNetwork.SendAll(new NetworkRequestLeaveActiveTournament(snapshot.SessionId, snapshot.Revision));
    }

    protected override void OnLeaving()
    {
        if (snapshot != null && !snapshot.IsCompleted && HasLocalMissionMember(snapshot) && !leaveRequested)
            relayNetwork.SendAll(new NetworkRequestLeaveActiveTournament(snapshot.SessionId, snapshot.Revision));

        if (session.HasInstance)
            relayNetwork.SendAll(new NetworkMissionLeft(session.OwnControllerId, session.InstanceId));

        spectatorAgentManager.Clear();
        matchLifecycle.Dispose();
        manifestAgentData.Clear();
        manifestAgentInstances.Clear();
        pendingKnockouts.Clear();
        pendingTournamentPackets.Clear();
        receivedDamageSequences.Clear();
        appliedDamageSequences.Clear();
        pendingApplyManifest = null;
        missionReadyForManifest = false;
        agentSpawner.Reset();
        network.Stop();
        session.Reset();
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<TournamentSessionUpdated>(Handle_SessionUpdated);
        messageBroker.Unsubscribe<TournamentSpawnManifestUpdated>(Handle_ManifestUpdated);
        messageBroker.Unsubscribe<NetworkApplyTournamentDamage>(Handle_ApplyTournamentDamage);
        messageBroker.Unsubscribe<NetworkTournamentAgentKnockedOut>(Handle_AgentKnockedOut);
        messageBroker.Unsubscribe<NetworkTournamentRuntimeState>(Handle_RuntimeState);
        messageBroker.Unsubscribe<NetworkTournamentRoundEnded>(Handle_RoundEnded);
#if DEBUG
        messageBroker.Unsubscribe<NetworkTournamentCombatFixtureCommand>(Handle_CombatFixtureCommand);
        combatFixture.Dispose();
#endif
        base.Dispose();
    }

#if DEBUG
    private void Handle_CombatFixtureCommand(
        MessagePayload<NetworkTournamentCombatFixtureCommand> payload)
    {
        NetworkTournamentCombatFixtureCommand command = payload.What;
        GameThread.RunSafe(() =>
        {
            string result = combatFixture.Apply(
                command,
                session,
                snapshot,
                latestManifest,
                coopMissionComponent.AgentRegistry);
            if (!string.IsNullOrEmpty(result))
                Logger.Information("[TournamentFixture] {Result}", result);
        }, context: nameof(Handle_CombatFixtureCommand));
    }

    internal string GetCombatFixtureState()
    {
        INetworkAgentRegistry agentRegistry = coopMissionComponent.AgentRegistry;
        var activeManifestAgents = latestManifest?.Agents?
            .Where(data =>
                data != null &&
                agentRegistry.TryGetAgentInfo(data.AgentId, out CoopAgentInfo info) &&
                info.Agent != null &&
                info.Agent.IsActive())
            .ToArray() ?? Array.Empty<TournamentAgentSpawnData>();
        var humanSlots = new HashSet<string>(
            snapshot?.Contestants?
                .Where(contestant => contestant.IsHuman && !contestant.IsReplaced)
                .Select(contestant => contestant.SlotId) ??
            Array.Empty<string>());
        string activeHumans = string.Join(",",
            activeManifestAgents
                .Where(data => humanSlots.Contains(data.SlotId))
                .Select(data => data.ControllerId)
                .Distinct());
        string registeredControllers = string.Join(",",
            agentRegistry.GetControllerIds());
        int activeAiCount = activeManifestAgents.Count(data => !humanSlots.Contains(data.SlotId));

        return combatFixture.GetState(session, agentRegistry) +
            $" manifestReady={latestManifest != null} " +
            $"activeHumans={(activeHumans.Length == 0 ? "none" : activeHumans)} " +
            $"activeAiCount={activeAiCount} " +
            $"registeredControllers={(registeredControllers.Length == 0 ? "none" : registeredControllers)}";
    }
#endif
}
