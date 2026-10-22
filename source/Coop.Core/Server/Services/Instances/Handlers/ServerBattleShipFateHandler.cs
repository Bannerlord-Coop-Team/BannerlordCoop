using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.Players;
using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Data;
using LiteNetLib;
using Missions.Messages;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// [Server] Holds the ship fates naval battle members report and applies them when a member departs, when its battle
/// finalizes without results or, through the result commit, before the loot reads the ships.
/// </summary>
public class ServerBattleShipFateHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerBattleShipFateHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IMissionManager missionManager;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IBattleShipFateLedger ledger;
    private readonly IBattleShipFateApplier applier;

    public ServerBattleShipFateHandler(
        IMessageBroker messageBroker,
        IMissionManager missionManager,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IBattleHostRegistry hostRegistry,
        IBattleShipFateLedger ledger,
        IBattleShipFateApplier applier)
    {
        this.messageBroker = messageBroker;
        this.missionManager = missionManager;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.ledger = ledger;
        this.applier = applier;

        messageBroker.Subscribe<NetworkBattleShipFates>(Handle_NetworkBattleShipFates);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleShipFates>(Handle_NetworkBattleShipFates);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    // Resolves the sender here on the poll thread, as the peer's disconnect in the same batch clears it before the game thread
    // runs; the record is queued behind the sender's earlier messages, so a departure it sent after these fates applies them.
    private void Handle_NetworkBattleShipFates(MessagePayload<NetworkBattleShipFates> payload)
    {
        if (ModInformation.IsClient || payload.Who is not NetPeer peer) return;

        var message = payload.What;
        if (!TryGetCurrentController(peer, out var controllerId))
        {
            Logger.Warning("[NavalSync] Dropped ship fates for {Instance}: the sender is not a registered player's current peer",
                message.InstanceId);
            return;
        }

        GameThread.RunSafe(() => Record(controllerId, message), context: nameof(Handle_NetworkBattleShipFates));
    }

    // The controller a peer currently stands for; a stale peer of a player that reconnected stands for nobody.
    private bool TryGetCurrentController(NetPeer peer, out string controllerId)
    {
        controllerId = null;
        if (!playerManager.TryGetPlayer(peer, out var player) || !playerManager.TryGetPeer(player.ControllerId, out var currentPeer)
            || !ReferenceEquals(currentPeer, peer))
            return false;

        controllerId = player.ControllerId;
        return true;
    }

    // [Game thread] Holds each fate of a ship the sender may report for, while the battle is open and the sender is in it.
    private void Record(string controllerId, NetworkBattleShipFates message)
    {
        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            Logger.Warning("[NavalSync] Dropped ship fates from {Controller}: it is no registered player", controllerId);
            return;
        }

        if (!missionManager.TryGetControllers(message.InstanceId, out var members) || !members.Contains(controllerId))
        {
            Logger.Information("[NavalSync] Dropped ship fates from {Controller}: it is not in battle {Instance}",
                controllerId, message.InstanceId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.InstanceId, out var mapEvent)) return;
        if (!mapEvent.IsNavalMapEvent || mapEvent.IsFinalized || mapEvent.HasCalculatedResults())
        {
            Logger.Information("[NavalSync] Dropped ship fates from {Controller}: battle {Instance} is not at sea or already has its results",
                controllerId, message.InstanceId);
            return;
        }

        objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var senderParty);
        bool isHostAtEpoch = hostRegistry.TryGet(message.InstanceId, out var assignment)
            && assignment.HostControllerId == controllerId && assignment.Epoch == message.HostEpoch;
        foreach (var fate in message.Fates ?? Array.Empty<BattleShipFate>())
            RecordFate(message, mapEvent, controllerId, senderParty?.Party, isHostAtEpoch, fate);
    }

    // Holds one fate when the sender may report its ship: its own party's, or as the battle host at the current epoch an AI
    // party's in the battle. Logs why otherwise.
    private void RecordFate(NetworkBattleShipFates message, MapEvent mapEvent, string controllerId, PartyBase senderParty,
        bool isHostAtEpoch, BattleShipFate fate)
    {
        if (fate == null || !fate.IsValid) return;
        if (!objectManager.TryGetObjectWithLogging<Ship>(fate.CampaignShipId, out var ship)) return;

        var owner = ship.Owner;
        if (owner == null)
        {
            DropFate(fate, controllerId, "the ship has no owner");
            return;
        }

        if (owner == senderParty)
        {
            ledger.Record(message.InstanceId, fate, message.HostEpoch, message.Revision);
            return;
        }

        if (owner.MapEventSide?.MapEvent != mapEvent)
        {
            DropFate(fate, controllerId, "its owner is not in the battle");
            return;
        }

        if (owner.IsPlayerParty())
        {
            DropFate(fate, controllerId, "another player owns it");
            return;
        }

        if (!isHostAtEpoch)
        {
            DropFate(fate, controllerId, "the sender is not the battle host at this epoch");
            return;
        }

        ledger.Record(message.InstanceId, fate, message.HostEpoch, message.Revision);
    }

    // Logs a fate the sender may not report.
    private static void DropFate(BattleShipFate fate, string controllerId, string reason) =>
        Logger.Warning("[NavalSync] Dropped the fate of ship {ShipId} from {Controller}: {Reason}", fate.CampaignShipId,
            controllerId, reason);
    // A departing player's ships take their fates now, so a return or a later result reads them; an emptied battle takes all.
    private void Handle_MissionMemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
        if (ModInformation.IsClient) return;

        var departure = payload.What;
        GameThread.RunSafe(() => ApplyDeparture(departure), context: nameof(Handle_MissionMemberDeparted));
    }

    // [Game thread] Only a battle with held fates is looked up, since every mission instance (taverns too) departs here.
    private void ApplyDeparture(MissionMemberDeparted departure)
    {
        if (!ledger.HasFates(departure.InstanceId)) return;
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(departure.InstanceId, out var mapEvent)) return;

        if (departure.IsInstanceEmpty)
        {
            applier.ApplyAll(mapEvent);
            return;
        }

        if (!playerManager.TryGetPlayer(departure.ControllerId, out var player)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;

        applier.ApplyParty(mapEvent, party.Party);
    }

    // A battle that ends without a result commit (abandoned, orphaned) still applies what its members reported.
    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        if (ModInformation.IsClient) return;

        var mapEvent = payload.What.MapEvent;
        GameThread.RunSafe(() => applier.ApplyAll(mapEvent), context: nameof(Handle_MapEventFinalized));
    }
}
