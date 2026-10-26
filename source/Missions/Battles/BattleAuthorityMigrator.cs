using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Messages;
using Missions.Hideouts;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Moves agent authority when a player leaves a coop battle (a disconnect's agents to the host, an absent controller's to a
/// promoted host, which revives them as AI) and runs the player-hero handoff.
/// </summary>
public interface IBattleAuthorityMigrator : IDisposable
{
    /// <summary>
    /// [Game thread] Apply current authority to a retained former-host record that spawned after migration.
    /// Every peer corrects its registry; the promoted host also revives battle AI.
    /// </summary>
    void ApplyLateSpawnedPuppet(Agent agent, Guid agentId, Agent mount, Guid mountAgentId);
    bool TrySurrenderPlayerHero(string controllerId, BattleAgentSpawnData data);
    bool TrySurrenderPlayerHero(BattleAgentSpawnData data);
    void ReplayPlayerHandoff(string controllerId);
    void TickPlayerHandoffs(float dt);
    bool IsCurrentPlayerHandoff(NetworkRetainedPlayerHero handoff);
    bool ApplyPlayerHandoff(NetworkRetainedPlayerHero handoff);
    bool IsPlayerHandoffIdentityValid(NetworkRetainedPlayerHero handoff);
    bool ApplyGrantedPlayerHandoff(NetworkRetainedPlayerHero handoff);
}

/// <inheritdoc cref="IBattleAuthorityMigrator"/>
public class BattleAuthorityMigrator : IBattleAuthorityMigrator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleAuthorityMigrator>();

    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IAgentFormationAssigner formationAssigner;
    private readonly IMissionContext missionContext;
    private readonly IReinforcementFielder reinforcementFielder;
    private readonly IBattlePartyWithdrawal partyWithdrawal;
    private readonly IBattlePlayerPartyResolver playerParties;
    private readonly Action<IReadOnlyCollection<Guid>> authorityChanged;

    // [Game thread] Hosts whose own party withdrew, read when the promotion lands so the adoption leaves those troops to the
    // withdrawal. Consumed by the promotion, and cleared on re-entry so a later drop of the same controller is no retreat.
    private readonly HashSet<string> withdrawnHosts = new HashSet<string>();
    private readonly Dictionary<string, NetworkRetainedPlayerHero> playerHandoffs = new();
    private readonly HashSet<Guid> confirmedPlayerHandoffs = new();
    private readonly Dictionary<Guid, (Agent Agent, AgentControllerType Controller)> provisionalPlayerControllers = new();
    private float playerHandoffRetrySeconds;

    public BattleAuthorityMigrator(
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        IBattleDeploymentCoordinator deployment,
        IAgentFormationAssigner formationAssigner,
        IMissionContext missionContext,
        IReinforcementFielder reinforcementFielder,
        IBattlePartyWithdrawal partyWithdrawal,
        IBattlePlayerPartyResolver playerParties,
        Action<IReadOnlyCollection<Guid>> authorityChanged = null)
    {
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.deployment = deployment;
        this.formationAssigner = formationAssigner;
        this.missionContext = missionContext;
        this.reinforcementFielder = reinforcementFielder;
        this.partyWithdrawal = partyWithdrawal;
        this.playerParties = playerParties;
        this.authorityChanged = authorityChanged;

        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    // A controller (re-)entered the instance: clear any stale withdrawal marker. Its party is supplied from
    // the server reserve and spawned fresh; withdrawn agents are never reclaimed.
    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;
        var controllerId = payload.What.ControllerId;
        GameThread.RunSafe(() => withdrawnHosts.Remove(controllerId), context: nameof(Handle_PeerEntered));
    }

    public bool TrySurrenderPlayerHero(BattleAgentSpawnData data)
    {
        if (data == null) return false;
        foreach (var player in playerManager.Players)
            if (player.CharacterObjectId == data.CharacterId)
                return TrySurrenderPlayerHero(player.ControllerId, data);
        return false;
    }

    // Suspend the holder locally; only the server grant may advance the shared authority revision.
    public bool TrySurrenderPlayerHero(string controllerId, BattleAgentSpawnData data)
    {
        var handoff = new NetworkRetainedPlayerHero(session.InstanceId, session.HostEpoch, controllerId, data);
        if (!session.IsLocalHost || !IsCurrentPlayerHandoff(handoff)) return false;
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(data.AgentId, out var info)
            || info.CurrentAuthority != session.OwnControllerId || info.AuthorityRevision != data.AuthorityRevision
            || info.OriginalOwner != data.OriginalOwnerControllerId
            || info.MovementScopeId != data.MovementScopeId || info.MovementId != data.MovementId
            || info.Agent == null || !info.Agent.IsActive()) return false;
        if (data.MountAgentId != Guid.Empty
            && (!registry.TryGetAgentInfo(data.MountAgentId, out var mount)
                || mount.CurrentAuthority != session.OwnControllerId
                || mount.AuthorityRevision != data.MountAuthorityRevision
                || mount.Agent == null || !mount.Agent.IsActive())) return false;
        if (playerHandoffs.TryGetValue(controllerId, out var pending)
            && pending.Previous.AgentId == data.AgentId
            && pending.Previous.AuthorityRevision == data.AuthorityRevision
            && pending.HostEpoch == handoff.HostEpoch) return true;
        playerHandoffs[controllerId] = handoff;
        confirmedPlayerHandoffs.Remove(data.AgentId);
        Suspend(data.AgentId);
        if (data.MountAgentId != Guid.Empty) Suspend(data.MountAgentId);
        relayNetwork.SendAll(new NetworkRequestRetainedPlayerHero(handoff));
        return true;

        void Suspend(Guid agentId)
        {
            if (!registry.TryGetAgentInfo(agentId, out var suspended)) return;
            if (!provisionalPlayerControllers.TryGetValue(agentId, out var original)
                || original.Agent != suspended.Agent)
                provisionalPlayerControllers[agentId] = (suspended.Agent, suspended.Agent.Controller);
            suspended.Agent.Controller = AgentControllerType.None;
        }
    }

    public void ReplayPlayerHandoff(string controllerId)
    {
        if (!playerHandoffs.TryGetValue(controllerId, out var handoff)
            || !IsCurrentPlayerHandoff(handoff)) return;
        if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(handoff.Previous.AgentId, out var info)
            && ((info.CurrentAuthority == handoff.Previous.OwnerControllerId
                    && info.AuthorityRevision == handoff.Previous.AuthorityRevision)
                || (info.CurrentAuthority == controllerId
                    && info.AuthorityRevision == handoff.Previous.AuthorityRevision + 1)))
            relayNetwork.SendAll(new NetworkRequestRetainedPlayerHero(handoff));
    }

    // The campaign membership or supplied pointer can arrive after the mesh replay. Retry the same grant.
    public void TickPlayerHandoffs(float dt)
    {
        if (!session.IsLocalHost || playerHandoffs.Count == 0) return;
        playerHandoffRetrySeconds += dt;
        if (playerHandoffRetrySeconds < 0.5f) return;
        playerHandoffRetrySeconds = 0;
        foreach (var pair in playerHandoffs)
            if (!confirmedPlayerHandoffs.Contains(pair.Value.Previous.AgentId))
                ReplayPlayerHandoff(pair.Key);
    }

    public bool IsCurrentPlayerHandoff(NetworkRetainedPlayerHero handoff)
    {
        if (handoff == null || !handoff.HasValidAuthorityTransition || !session.HasInstance
            || handoff.BattleInstanceId != session.InstanceId || handoff.HostEpoch != session.HostEpoch
            || handoff.Previous.OwnerControllerId != session.HostControllerId) return false;
        return IsPlayerHandoffIdentityValid(handoff);
    }

    public bool IsPlayerHandoffIdentityValid(NetworkRetainedPlayerHero handoff)
    {
        if (handoff == null || !handoff.HasValidAuthorityTransition || !session.HasInstance
            || handoff.BattleInstanceId != session.InstanceId) return false;
        bool returningPlayerPresent = session.IsOwn(handoff.ReturningControllerId)
            || new HashSet<string>(missionContext.ControllersInMission).Contains(handoff.ReturningControllerId);
        if (!returningPlayerPresent || !playerManager.TryGetPlayer(handoff.ReturningControllerId, out var player)
            || player.CharacterObjectId != handoff.Previous.CharacterId
            || string.IsNullOrEmpty(handoff.Previous.OriginalOwnerControllerId)
            || !(handoff.Previous.Health > 0) || float.IsInfinity(handoff.Previous.Health)
            || handoff.Previous.IsRunningAway) return false;
        if (!playerParties.TryGetPlayerParty(handoff.ReturningControllerId, out var party, out var hero)
            || hero == null || !objectManager.TryGetObject<CharacterObject>(handoff.Previous.CharacterId, out var character)
            || character != hero.CharacterObject
            || !objectManager.TryGetObject<MapEventParty>(handoff.Previous.MapEventPartyId, out var mapEventParty)
            || mapEventParty?.Party != party) return false;
        return true;
    }

    public bool ApplyPlayerHandoff(NetworkRetainedPlayerHero handoff)
    {
        return IsCurrentPlayerHandoff(handoff) && ApplyGrantedPlayerHandoff(handoff);
    }

    // Once the server grant is applied in order, later host epochs cannot revoke the present player's authority.
    public bool ApplyGrantedPlayerHandoff(NetworkRetainedPlayerHero handoff)
    {
        if (!IsPlayerHandoffIdentityValid(handoff)) return false;
        var data = handoff.Previous;
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(data.AgentId, out var info)
            || info.OriginalOwner != data.OriginalOwnerControllerId
            || info.MovementScopeId != data.MovementScopeId || info.MovementId != data.MovementId
            || info.Agent == null || !info.Agent.IsActive()) return false;
        bool wasLocal = session.IsOwn(info.CurrentAuthority);
        if (!registry.TryReturnRetainedPlayer(data.OwnerControllerId, handoff.ReturningControllerId,
                data.AgentId, data.AuthorityRevision, data.MountAgentId, data.MountAuthorityRevision)) return false;
        provisionalPlayerControllers.Remove(data.AgentId);
        provisionalPlayerControllers.Remove(data.MountAgentId);
        if (playerHandoffs.TryGetValue(handoff.ReturningControllerId, out var sent)
            && sent.Previous.AgentId == data.AgentId && sent.Previous.AuthorityRevision == data.AuthorityRevision)
            confirmedPlayerHandoffs.Add(data.AgentId);
        if (session.IsOwn(handoff.ReturningControllerId))
        {
            PromoteReturnedPlayer(info.Agent);
            if (data.MountAgentId != Guid.Empty && registry.TryGetAgentInfo(data.MountAgentId, out var mount)
                && mount.Agent != null && mount.Agent.IsActive())
                coopMissionComponent.AgentMovementHandler.Interpolator.Forget(mount.Agent);
        }
        else if (wasLocal)
        {
            Demote(info.Agent);
            if (data.MountAgentId != Guid.Empty && registry.TryGetAgentInfo(data.MountAgentId, out var mount))
                Demote(mount.Agent);
        }
        return true;

        void PromoteReturnedPlayer(Agent agent)
        {
            var mission = Mission.Current;
            if (agent == null || !agent.IsActive() || agent.Mission != mission
                || agent.Character != Hero.MainHero?.CharacterObject) return;
            coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
            if (mission.InitialPlayerAgent == null)
                mission._initialPlayerAgent = agent;
            if (!deployment.IsCommitted && mission.GetMissionBehavior<DeploymentMissionController>() != null)
            {
                agent.Controller = AgentControllerType.None;
                agent.SetIsAIPaused(true);
            }
            else
            {
                agent.Controller = AgentControllerType.Player;
                agent.SetIsAIPaused(false);
            }
            mission.MainAgent = agent;
        }

        void Demote(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            agent.Controller = AgentControllerType.None;
            coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
            agent.SetIsAIPaused(false);
        }
    }

    private void CancelProvisionalPlayerHandoff(string controllerId)
    {
        GameThread.RunSafe(() =>
        {
            if (!playerHandoffs.TryGetValue(controllerId, out var handoff)) return;
            playerHandoffs.Remove(controllerId);
            confirmedPlayerHandoffs.Remove(handoff.Previous.AgentId);
            Restore(handoff.Previous.AgentId, handoff.Previous.AuthorityRevision);
            Restore(handoff.Previous.MountAgentId, handoff.Previous.MountAuthorityRevision);
        }, context: nameof(CancelProvisionalPlayerHandoff));

        void Restore(Guid agentId, long revision)
        {
            if (!provisionalPlayerControllers.TryGetValue(agentId, out var suspended)) return;
            provisionalPlayerControllers.Remove(agentId);
            if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(agentId, out var info)
                && session.IsLocalHost && info.CurrentAuthority == session.OwnControllerId
                && info.AuthorityRevision == revision && info.Agent == suspended.Agent
                && info.Agent != null && info.Agent.IsActive())
                info.Agent.Controller = suspended.Controller;
        }
    }

    // A graceful leave withdraws the player's party on every client.
    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        HandlePartyWithdrawal(payload.What.ControllerId, payload.What.InstanceId, "retreated");
    }

    // A disconnect preserves both the remaining reserve and already-fielded agents. The current host adopts a
    // non-host's agents immediately; a departed host's successor adopts them when the migration assignment lands.
    // The server sends a naval disconnect as a leave (D4), so this is only ever a land one.
    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        var controllerId = payload.What.ControllerId;
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;
        CancelProvisionalPlayerHandoff(controllerId);
        if (session.IsHostController(controllerId)) return;
        if (!session.IsLocalHost)
        {
            TransferRemoteAuthority(controllerId, session.HostControllerId, sweepAbsentControllers: false);
            return;
        }

        reinforcementFielder.PrepareForReserveOwnershipExpansion();
        AdoptAgentsFrom(controllerId, "player disconnect", withdrawOwnParty: false);
    }

    // Bookkeeping that must precede the host migration runs now; on land the party fades now too, while at sea the ship
    // replicator owns the departure and withdraws the party once the leaver's hulls are gone (WithdrawParty).
    private void HandlePartyWithdrawal(string controllerId, string instanceId, string reason)
    {
        if (instanceId != null && instanceId != session.InstanceId) return;
        CancelProvisionalPlayerHandoff(controllerId);
        GameThread.RunSafe(() => MarkPartyWithdrawal(controllerId, reason), context: nameof(HandlePartyWithdrawal));
    }

    // [Game thread] A departed HOST's own party withdraws, while the promoted successor adopts only the NPC forces it ran.
    // Host assignments apply on this thread, so the mark is read and made before the successor's migration.
    private void MarkPartyWithdrawal(string controllerId, string reason)
    {
        bool wasHost = session.IsHostController(controllerId);
        if (wasHost)
        {
            withdrawnHosts.Add(controllerId);
            Logger.Information("[BattleSync] Host {Controller} {Reason} — withdrawing its own party; migration adopts the AI it ran", controllerId, reason);
        }

        // At sea the hull removal must take the leaver's crew first (the removal invariant), so the ship replicator withdraws
        // the party after it.
        if (session.IsNaval) return;

        partyWithdrawal.WithdrawParty(controllerId, wasHost);
    }

    // [All remaining clients] Advance each adopted agent's registry authority. The promoted host also
    // revives the adopted agents as battle AI.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        if (payload.What.MapEventId != session.InstanceId) return;

        var previousHost = payload.What.PreviousHostControllerId;
        var newHost = payload.What.NewHostControllerId;
        // Keep the two-argument event constructor usable by focused tests and older local publishers.
        if (string.IsNullOrEmpty(newHost))
            newHost = session.OwnControllerId;


        if (!session.IsOwn(newHost))
        {
            TransferRemoteAuthority(previousHost, newHost);
            withdrawnHosts.Remove(previousHost);
            return;
        }

        AdoptAgentsFrom(
            previousHost,
            "host migration",
            withdrawOwnParty: withdrawnHosts.Remove(previousHost));

        // The departed host may have inherited NPC agents through an earlier host migration. Other clients can
        // still key those agents to an older absent host, so adopting only the latest host would leave them
        // frozen. Sweep every absent authority in the migration chain. Idempotent: once swept, the agents are
        // keyed to us and a duplicate migration event finds nothing absent-keyed.
        SweepAgentsOfAbsentControllers(previousHost);

        // If the battle was already live when we were promoted, release the NPC AI we just adopted — a still-
        // deploying new host has AI ticking off, which would otherwise hold them frozen even though they were
        // moving under the previous host. If the battle was not live yet, this is a no-op (the gate holds).
        deployment.OnPromotedToHost();
    }

    // [New host] Adopt the agents of every controller that holds registry entries here but is no longer in
    // the mission — NPC orphans left behind by earlier adoptions in the host line that just departed. The
    // withdrawal record is consumed per swept controller for the same reason as the migration adoption above:
    // a departed host's own-party troops withdraw, and only the AI it ran should be (re-)adopted.
    private void SweepAgentsOfAbsentControllers(string previousHost)
    {
        var present = new HashSet<string>(missionContext.ControllersInMission);

        foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
        {
            if (string.IsNullOrEmpty(controllerId)) continue;
            if (controllerId == previousHost) continue; // the migration adoption above already took these
            if (session.IsOwn(controllerId)) continue;
            if (present.Contains(controllerId)) continue; // still connected — its owner drives them

            AdoptAgentsFrom(
                controllerId,
                "host migration orphan sweep",
                withdrawOwnParty: withdrawnHosts.Remove(controllerId));
        }
    }

    private void TransferRemoteAuthority(
        string previousHost,
        string newHost,
        bool sweepAbsentControllers = true)
    {
        if (string.IsNullOrEmpty(newHost)) return;

        var absentControllers = new HashSet<string>();
        if (!string.IsNullOrEmpty(previousHost))
            absentControllers.Add(previousHost);

        if (sweepAbsentControllers)
        {
            var present = new HashSet<string>(missionContext.ControllersInMission);
            foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
            {
                if (string.IsNullOrEmpty(controllerId)) continue;
                if (session.IsOwn(controllerId)) continue;
                if (present.Contains(controllerId)) continue;
                absentControllers.Add(controllerId);
            }
        }

        var registry = coopMissionComponent.AgentRegistry;
        GameThread.RunSafe(() =>
        {
            var changedIds = new List<Guid>();
            foreach (var controllerId in absentControllers)
            {
                foreach (var info in registry.GetAgents(controllerId))
                {
                    if (info.CurrentAuthority != newHost && registry.TryTransferAuthority(newHost, info.AgentId))
                        changedIds.Add(info.AgentId);
                }
            }
            if (changedIds.Count > 0) authorityChanged?.Invoke(changedIds);
        }, context: nameof(TransferRemoteAuthority));
    }

    public void ApplyLateSpawnedPuppet(
        Agent agent,
        Guid agentId,
        Agent mount,
        Guid mountAgentId)
    {
        if (agent == null || agentId == Guid.Empty) return;

        string hostControllerId = session.HostControllerId;
        if (string.IsNullOrEmpty(hostControllerId)) return;

        var changedIds = new List<Guid>();
        if (mount != null && mountAgentId != Guid.Empty &&
            !TransferLateAuthority(hostControllerId, mountAgentId, changedIds)) return;
        if (!TransferLateAuthority(hostControllerId, agentId, changedIds)) return;
        if (changedIds.Count > 0) authorityChanged?.Invoke(changedIds);
        var registry = coopMissionComponent.AgentRegistry;
        if (!session.IsLocalHost || Mission.Current == null || !agent.IsActive()) return;

        bool activateAi = deployment.IsActivated;
        if (activateAi)
            Mission.Current.AllowAiTicking = true;

        var interpolator = coopMissionComponent.AgentMovementHandler.Interpolator;
        interpolator.Forget(agent);
        if (mount != null) interpolator.Forget(mount);

        ConvertPuppetToHostAi(agent, activateAi);
        if (!agent.IsRunningAway && agent.Formation != null && !IsShipCrew(agent.Formation) &&
            Mission.Current.GetMissionBehavior<CoopHideoutMissionLogic>()?.PreserveStealthPosture != true)
            agent.Formation.SetMovementOrder(MovementOrder.MovementOrderCharge);

        Logger.Information(
            "[BattleSync] Late-adopted agent {AgentId} spawned after host migration at revision {Revision}",
            agentId,
            registry.TryGetAgentInfo(agentId, out CoopAgentInfo info)
                ? info.AuthorityRevision
                : -1L);
    }

    private bool TransferLateAuthority(string controllerId, Guid agentId, List<Guid> changedIds)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(agentId, out var info)) return false;
        bool changed = info.CurrentAuthority != controllerId;
        if (!registry.TryTransferAuthority(controllerId, agentId)) return false;
        if (changed) changedIds.Add(agentId);
        return true;
    }

    // Take over the agents owned by the departed controller: move authority to us (so the movement poller
    // broadcasts them and the death/casualty path owns them — their attribution was captured at spawn) and
    // convert each inert puppet into a host AI combatant. Other peers keep them as puppets that follow our
    // movement. On departure the old host's own-party troops withdraw instead (despawned by
    // IBattlePartyWithdrawal on every client), so they are excluded here — the disjoint sets are what make the
    // despawn and this adoption race-free.
    private void AdoptAgentsFrom(
        string controllerId,
        string reason,
        bool withdrawOwnParty)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (session.IsOwn(controllerId)) return;

        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            PartyBase retreatedParty = null;
            Hero retreatedHero = null;
            if (withdrawOwnParty && !playerParties.TryGetPlayerParty(controllerId, out retreatedParty, out retreatedHero))
                Logger.Warning("[BattleSync] Cannot resolve the withdrawn party of host {Controller}; adopting all of its agents", controllerId);

            var adopted = new List<CoopAgentInfo>();
            foreach (var info in registry.GetAgents(controllerId))
            {
                // A departed host's own-party troop withdraws, so it is not ours to adopt.
                // Neither is the horse under such a troop: it fades out with its rider (CleanUpDepartedMounts).
                if (retreatedParty != null && info.Agent != null && IsRetreatersAgent(info.Agent, retreatedParty, retreatedHero))
                    continue;
                adopted.Add(info);
            }

            if (adopted.Count > 0)
            {
                var transferred = new List<CoopAgentInfo>();
                foreach (var info in adopted)
                {
                    if (registry.TryTransferAuthority(session.OwnControllerId, info.AgentId))
                        transferred.Add(info);
                }

                adopted = transferred;
                if (adopted.Count == 0) return;
                var changedIds = new List<Guid>();
                foreach (var info in adopted) changedIds.Add(info.AgentId);
                authorityChanged?.Invoke(changedIds);
                if (Mission.Current == null) return;

                // Keep an adoption during Order of Battle behind the same activation gate as every other NPC.
                // The normal deployment activation path wakes it later; a live battle wakes it immediately here.
                bool activateAi = deployment.IsActivated;
                if (activateAi)
                    Mission.Current.AllowAiTicking = true;

                var interpolator = coopMissionComponent.AgentMovementHandler.Interpolator;
                var formations = new HashSet<Formation>();
                int aiCount = 0;
                foreach (var info in adopted)
                {
                    var agent = info.Agent;
                    if (agent == null || !agent.IsActive()) continue;

                    // The agent is no longer a puppet: stop reconciling it toward its former owner's last-reported
                    // position. The interpolator teleport-lerps every tracked agent back toward its target each
                    // frame, so a stale target overrides the AI and pins the agent in place — the "adopted troops
                    // don't move after host migration" freeze. A mounted puppet is tracked by its MOUNT's target,
                    // so forget that too.
                    interpolator.Forget(agent);
                    if (agent.MountAgent != null) interpolator.Forget(agent.MountAgent);

                    // An adopted MOUNT only changes authority (damage routing + death broadcast now answer to
                    // us) — it is not a combatant: no AI controller, no formation, no wake.
                    if (agent.IsMount) continue;

                    ConvertPuppetToHostAi(agent, activateAi);
                    if (agent.Controller == AgentControllerType.AI) aiCount++;
                    if (agent.Formation != null) formations.Add(agent.Formation);
                }

                // The converted agents are AI-controlled now, but in a coop battle no general commands their
                // formation — and Formation.SetControlledByAI only issues a movement order when the formation AI
                // has an active behavior (there is none here), so they'd stand idle. Give each adopted formation
                // an explicit Charge so the NPCs actually engage. (Freshly spawned troops move because their
                // OWNER's team AI drives them; these adopted puppets have no such driver and need the order.)
                if (Mission.Current.GetMissionBehavior<Missions.Hideouts.CoopHideoutMissionLogic>()?.PreserveStealthPosture != true)
                {
                    foreach (var formation in formations)
                    {
                        if (!IsShipCrew(formation))
                            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                    }
                }

                // TEMP diagnostic: how many adopted agents actually became AI-controlled, across how many
                // formations (each ordered to Charge above). The per-formation state is covered by the
                // BattleMigrationMirror E2E test.
                Logger.Information("[BattleSync] Adopt-AI: {AI}/{Total} now AI-controlled across {Forms} formation(s)",
                    aiCount, adopted.Count, formations.Count);

                Logger.Information("[BattleSync] Adopted {Count} agent(s) from {Controller} ({Reason})",
                    adopted.Count, controllerId, reason);
            }
        });

        // We now own the departed controller's parties — pull our updated reserve from the server (the full
        // owned set at the current ledger pointers). ReinforcementFielder recovers newly-owned parties that had
        // no agents to adopt and continues them from those pointers. Runs even when nothing was adopted.
        RequestReserves();
    }

    // A hull's crew gets no adoption Charge: its ship order drives it, a charge would send it off the deck, and the ship
    // replicator re-crews the hull it takes over instead.
    private bool IsShipCrew(Formation formation) =>
        coopMissionComponent.ShipRegistry != null && coopMissionComponent.ShipRegistry.TryGetByFormation(formation, out _);

    // An agent that withdraws with the retreating player rather than being adopted: an own-party troop, or the horse one of
    // them is riding (a horse has no origin party of its own, so it follows its rider out).
    private bool IsRetreatersAgent(Agent agent, PartyBase playerParty, Hero playerHero)
    {
        if (agent.IsMount)
            return agent.RiderAgent is Agent rider && playerParties.IsOwnPartyAgent(rider, playerParty, playerHero);
        return playerParties.IsOwnPartyAgent(agent, playerParty, playerHero);
    }

    // [Owner, game thread] Ask the server for our current owned reserve (after adopting a departed owner's
    // parties). The reply re-sets our suppliers at the ledger pointers.
    private void RequestReserves()
    {
        if (!session.HasInstance) return;
        var id = session.InstanceId;
        GameThread.RunSafe(() => relayNetwork.SendAll(new NetworkRequestBattleReserves(id, session.OwnControllerId)));
    }

    // Turn an inert puppet (driven only by replicated movement) into a real AI combatant under the host's
    // command: keep the formation slot its owner placed it in (mirrored at spawn) and hand it to the engine AI
    // so it maneuvers and fights like the host's own AI troops. The formation is set AI-controlled because in a
    // coop battle the host fights as a hero, not a general, so nothing would otherwise order it to engage.
    private void ConvertPuppetToHostAi(Agent agent, bool wakeAi)
    {
        agent.Controller = AgentControllerType.AI;

        // A fleeing puppet already left its formation when the authoritative transition was mirrored. Keep it
        // routed during migration instead of assigning it to a formation and charging it back into combat.
        if (agent.IsRunningAway)
        {
            if (wakeAi)
                agent.Retreat(Mission.Current.GetClosestFleePositionForAgent(agent));
            return;
        }

        if (Mission.Current.GetMissionBehavior<Missions.Hideouts.CoopHideoutMissionLogic>()?.RestoreAdoptedAgent(agent) == true)
            return;

        // Fall back to the troop-class default only if the puppet has no formation yet.
        var formation = agent.Formation ?? formationAssigner.Assign(agent);
        formation?.SetControlledByAI(true);
        // Published once its formation is set, so a naval battle can enlist the agent as its hull's crew.
        messageBroker.Publish(this, new BattleAgentAdopted(agent));

        // In a live battle, wake the AI the same way the NPC-release path does. Without this an adopted agent
        // is AI-controlled but not alarmed and holds stale enemy caches, so it ignores its formation's Charge
        // order. During deployment the normal activation path owns this wake instead. Only combat troops reach
        // this conversion, so the CanWieldWeapon guard the NPC release uses is unnecessary here.
        if (wakeAi)
            AgentAiWaker.Wake(agent);
    }
}
