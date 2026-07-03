using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Serilog;
using System;

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
    /// Tear the instance down on mission end: end the spawn gate, clear this battle's troop suppliers,
    /// announce MissionLeft over the relay and stop the mesh socket.
    /// </summary>
    void Leave();
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
    private readonly IBattleSession session;

    public BattleInstanceLifecycle(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session)
    {
        this.network = network;
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;

        messageBroker.Subscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Subscribe<NetworkMissionLeft>(Handle_LeaveMission);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Unsubscribe<NetworkMissionLeft>(Handle_LeaveMission);
    }

    // The battle mission was opened locally and the controller attached by BattleMissionEntryPatch before the
    // event was published, so this is the live, mission-scoped owner of the P2P connection.
    // The spawn gate is engaged earlier, in BattleMissionEntryPatch's prefix (before the mission's troops
    // spawn). This handler only owns the P2P instance connect + host-election request.
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

        network.Start();
        network.ConnectToInstance(mapEventId);
        coopMissionComponent.AgentRegistry.Clear();

        relayNetwork.SendAll(new NetworkMissionEntered(session.OwnControllerId, mapEventId));
        Logger.Information("[Relay] Announced MissionEntered for battle instance {Instance}", mapEventId);
    }

    public void Leave()
    {
        BattleSpawnGate.EndBattle();

        if (session.HasInstance)
        {
            CoopTroopSupplierRegistry.ClearBattle(session.InstanceId);
            relayNetwork.SendAll(new NetworkMissionLeft(session.OwnControllerId, session.InstanceId));
            Logger.Information("[Relay] Announced MissionLeft for battle instance {Instance}", session.InstanceId);
        }

        network.Stop();
    }

    private void Handle_LeaveMission(MessagePayload<NetworkMissionLeft> payload)
    {
        // Our own broadcast echoed back by a peer — ignore. Later phases despawn the leaver's troops here
        // (see CoopLocationsController.Handle_LeaveMission); the retreat/adoption paths already cover troops.
        if (session.IsOwn(payload.What.ControllerId)) return;

        Logger.Information("[BattleSync] Peer {ControllerId} left battle instance {Instance}", payload.What.ControllerId, session.InstanceId);
    }
}
