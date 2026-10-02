using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.ObjectManager;
using Missions.Agents;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// The battle's P2P instance lifecycle. On entering the battle (<see cref="PlayerEnteredBattle"/>) it
/// connects this client to the mission-scoped mesh instance keyed by the map event's object-manager id —
/// identical on every client in the battle, so the server creates the instance on the first NAT punch and no
/// assignment round-trip is needed — and announces the entry over the relay. On mission end it announces the
/// departure and stops the mesh socket.
/// </summary>
public interface IBattleInstanceLifecycle : IDisposable
{
    /// <summary>
    /// Tear the instance down on mission end: optionally announce an unresolved battle retreat, end the spawn gate, clear this battle's troop suppliers,
    /// announce MissionLeft over the relay, stop the mesh socket, and clear the battle agent registry and
    /// local mission-membership mirror so stale mission state cannot survive into a later re-entry.
    /// </summary>
    void Leave(bool wasRetreat);

    /// <summary>Retain an owned routed survivor before it leaves the agent registry.</summary>
    void RecordRoutedHealth(Agent agent);

    /// <summary>Retain an owned survivor before another player's party is withdrawn.</summary>
    void RecordWithdrawnHealth(Agent agent);
}

/// <inheritdoc cref="IBattleInstanceLifecycle"/>
public class BattleInstanceLifecycle : IBattleInstanceLifecycle
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleInstanceLifecycle>();

    private readonly IBattleNetwork network;
    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly INetworkWorldItemRegistry worldItemRegistry;
    private readonly IBattleSession session;
    private readonly IMissionContext missionContext;
    private bool healthReported;
    private readonly Dictionary<string, Dictionary<int, float>> healthByParty = new();
    private readonly Dictionary<string, Dictionary<int, float>> routedByParty = new();

    public BattleInstanceLifecycle(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        INetworkWorldItemRegistry worldItemRegistry,
        IBattleSession session,
        IMissionContext missionContext)
    {
        this.network = network;
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.worldItemRegistry = worldItemRegistry;
        this.session = session;
        this.missionContext = missionContext;

        messageBroker.Subscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Subscribe<NetworkMissionLeft>(Handle_LeaveMission);
        messageBroker.Subscribe<NetworkRequestBattleTroopHealth>(Handle_HealthRequest);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Unsubscribe<NetworkMissionLeft>(Handle_LeaveMission);
        messageBroker.Unsubscribe<NetworkRequestBattleTroopHealth>(Handle_HealthRequest);
    }

    // The battle mission was opened locally and the controller attached by BattleMissionEntryPatch before the
    // event was published, so this is the live, mission-scoped owner of the P2P connection.
    // The spawn gate is engaged earlier, in BattleMissionEntryPatch's prefix (before the mission's troops
    // spawn). This handler only owns the P2P instance connect; BattleHostHandler requests the entry reserves
    // here and the host election later, at mission-ready (CoopBattleController.AfterStart).
    private void Handle_PlayerEnteredBattle(MessagePayload<PlayerEnteredBattle> payload)
    {
        var mapEvent = payload.What.MapEvent;
        if (mapEvent == null)
        {
            Logger.Warning("[BattleSync] PlayerEnteredBattle with no map event — skipping instance request");
            return;
        }

        if (objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId) == false)
        {
            Logger.Warning("[BattleSync] Could not resolve map event id — skipping instance request");
            return;
        }

        // OpenBattleMission can fire more than once around an encounter; connect once per mission.
        if (!session.TryBegin(mapEventId)) return;

        Logger.Information("[BattleSync] Requesting P2P battle instance mapEvent={MapEventId}", mapEventId);

        worldItemRegistry.Clear();
        network.Start();
        network.ConnectToInstance(mapEventId);
        coopMissionComponent.AgentRegistry.Clear();

        relayNetwork.SendAll(new NetworkMissionEntered(session.OwnControllerId, mapEventId));
        Logger.Information("[Relay] Announced MissionEntered for battle instance {Instance}", mapEventId);
    }

    public void Leave(bool wasRetreat)
    {
        ReportFinalHealth();
        BattleSpawnGate.EndBattle();

        if (session.HasInstance)
        {
            if (wasRetreat)
            {
                relayNetwork.SendAll(new NetworkBattleRetreated(session.InstanceId));
                Logger.Information("[Relay] Announced battle retreat for instance {Instance}", session.InstanceId);
            }
            CoopTroopSupplierRegistry.ClearBattle(session.InstanceId);
            relayNetwork.SendAll(new NetworkMissionLeft(session.OwnControllerId, session.InstanceId));
            Logger.Information("[Relay] Announced MissionLeft for battle instance {Instance}", session.InstanceId);
        }

        network.Stop();
        coopMissionComponent.AgentRegistry.Clear();
        worldItemRegistry.Clear();

        // Wipe the local membership mirror on our way out. Stopping the socket clears only the direct peer
        // mappings; the server-announced membership set (which the absent-controller sweep consults) would
        // otherwise persist, and once we have left the server no longer fans this instance's churn to us — so
        // a controller that drops while we are away would keep looking present. On re-entry (BR-054) the
        // server re-announces the current members, so the mirror is rebuilt fresh.
        missionContext.EndInstance();
    }

    public void RecordRoutedHealth(Agent agent)
    {
        if (healthReported || agent?.Origin is not CoopAgentOrigin origin) return;
        origin.OnMissionEnded(agent.Health, ownsHealth: true);
        if (!(agent.Health > 0f) || origin.MapEventPartyId == null) return;
        if (!healthByParty.TryGetValue(origin.MapEventPartyId, out var survivors))
            healthByParty[origin.MapEventPartyId] = survivors = new Dictionary<int, float>();
        survivors[origin.UniqueSeed] = agent.Health;
        if (!routedByParty.TryGetValue(origin.MapEventPartyId, out var routed))
            routedByParty[origin.MapEventPartyId] = routed = new Dictionary<int, float>();
        routed[origin.UniqueSeed] = agent.Health;
    }

    public void RecordWithdrawnHealth(Agent agent)
    {
        if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info)
            && session.IsOwn(info.CurrentAuthority) && agent.IsActive())
            RecordRoutedHealth(agent);
    }

    private void Handle_HealthRequest(MessagePayload<NetworkRequestBattleTroopHealth> payload)
    {
        if (ModInformation.IsServer) return;
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (healthReported || !session.HasInstance || request.MapEventId != session.InstanceId
                || !session.IsLocalHost || request.SnapshotId == Guid.Empty || request.PartyIds == null) return;
            foreach (var partyId in request.PartyIds)
            {
                if (string.IsNullOrEmpty(partyId)) continue;
                var survivors = routedByParty.TryGetValue(partyId, out var routed)
                    ? new Dictionary<int, float>(routed) : new Dictionary<int, float>();
                foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(session.OwnControllerId))
                {
                    var agent = info.Agent;
                    if (agent?.Origin is CoopAgentOrigin origin && origin.MapEventPartyId == partyId
                        && agent.IsActive() && agent.Health > 0f)
                        survivors[origin.UniqueSeed] = agent.Health;
                }
                relayNetwork.SendAll(new NetworkBattleTroopHealth(session.InstanceId, partyId,
                    survivors, 0, routed, request.SnapshotId));
            }
        }, context: nameof(Handle_HealthRequest));
    }

    private void ReportFinalHealth()
    {
        if (healthReported || !session.HasInstance) return;
        healthReported = true;
        var suppliedByParty = new Dictionary<string, int>();
        foreach (var supplier in CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId))
            foreach (var (partyId, supplied) in supplier.GetSuppliedByParty())
            {
                if (!healthByParty.ContainsKey(partyId))
                    healthByParty[partyId] = new Dictionary<int, float>();
                suppliedByParty[partyId] = supplied;
            }

        var registry = coopMissionComponent.AgentRegistry;
        foreach (var controllerId in registry.GetControllerIds())
            foreach (var info in registry.GetAgents(controllerId))
            {
                var agent = info.Agent;
                if (agent?.Origin is not CoopAgentOrigin origin) continue;
                bool ownsHealth = info.CurrentAuthority == session.OwnControllerId;
                // Seal puppet callbacks too, before registry teardown removes the authority probe.
                origin.OnMissionEnded(agent.Health, ownsHealth);
                if (!ownsHealth || !agent.IsActive() || !(agent.Health > 0f) || origin.MapEventPartyId == null) continue;
                if (!healthByParty.TryGetValue(origin.MapEventPartyId, out var survivors))
                    healthByParty[origin.MapEventPartyId] = survivors = new Dictionary<int, float>();
                survivors[origin.UniqueSeed] = agent.Health;
            }

        foreach (var party in healthByParty)
            relayNetwork.SendAll(new NetworkBattleTroopHealth(session.InstanceId, party.Key, party.Value,
                suppliedByParty.TryGetValue(party.Key, out var supplied) ? supplied : 0,
                routedByParty.TryGetValue(party.Key, out var routed) ? routed : null));
    }

    private void Handle_LeaveMission(MessagePayload<NetworkMissionLeft> payload)
    {
        // Our own broadcast echoed back by a peer — ignore. Later phases despawn the leaver's troops here
        // (see CoopLocationsController.Handle_LeaveMission); the retreat/adoption paths already cover troops.
        if (session.IsOwn(payload.What.ControllerId)) return;

        Logger.Information("[BattleSync] Peer {ControllerId} left battle instance {Instance}", payload.What.ControllerId, session.InstanceId);
    }
}
