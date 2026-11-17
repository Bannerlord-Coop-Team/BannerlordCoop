using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
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
/// [Server] Holds the ship fates naval battle members report, and which member captured which ship in the mission, and applies
/// them when a member departs, when its battle finalizes without results or, through the result commit, before the loot reads the ships.
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

    // Takes the services that resolve the sender and battle, the ledger that holds the fates and the applier that writes them.
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
        messageBroker.Subscribe<NetworkBattleShipCaptured>(Handle_NetworkBattleShipCaptured);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    // Drops the handler's subscriptions.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleShipFates>(Handle_NetworkBattleShipFates);
        messageBroker.Unsubscribe<NetworkBattleShipCaptured>(Handle_NetworkBattleShipCaptured);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    // Resolves the sender here on the poll thread, as the peer's disconnect in the same batch clears it before the game thread
    // runs; the record is queued behind the sender's earlier messages, so a departure it sent after these fates applies them.
    private void Handle_NetworkBattleShipFates(MessagePayload<NetworkBattleShipFates> payload)
    {
        if (payload.Who is not NetPeer peer) return;

        var message = payload.What;
        if (!playerManager.TryGetCurrentController(peer, out var controllerId))
        {
            Logger.Warning("[NavalSync] Dropped ship fates for {Instance}: the sender is not a registered player's current peer",
                message.InstanceId);
            return;
        }

        GameThread.RunSafe(() => Record(controllerId, message), context: nameof(Handle_NetworkBattleShipFates));
    }

    // [Game thread] Holds each fate of a ship the sender may report for, while the battle is open and the sender is in it.
    private void Record(string controllerId, NetworkBattleShipFates message)
    {
        if (!TryGetOpenBattle(controllerId, message.InstanceId, "ship fates", out var player, out var mapEvent)) return;

        objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var senderParty);
        bool isHostAtEpoch = hostRegistry.TryGet(message.InstanceId, out var assignment)
            && assignment.HostControllerId == controllerId && assignment.Epoch == message.HostEpoch;
        foreach (var fate in message.Fates ?? Array.Empty<BattleShipFate>())
            RecordFate(message, mapEvent, controllerId, senderParty?.Party, isHostAtEpoch, fate);
    }

    // The sender as a registered player in an open naval battle it is a member of; logs why the message is dropped otherwise.
    private bool TryGetOpenBattle(string controllerId, string instanceId, string what, out Player player, out MapEvent mapEvent)
    {
        mapEvent = null;
        if (!playerManager.TryGetPlayer(controllerId, out player))
        {
            Logger.Warning("[NavalSync] Dropped {What} from {Controller}: it is no registered player", what, controllerId);
            return false;
        }

        if (!missionManager.TryGetControllers(instanceId, out var members) || !members.Contains(controllerId))
        {
            Logger.Information("[NavalSync] Dropped {What} from {Controller}: it is not in battle {Instance}", what, controllerId,
                instanceId);
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(instanceId, out mapEvent)) return false;
        if (mapEvent.IsNavalMapEvent && !mapEvent.IsFinalized && !mapEvent.HasCalculatedResults()) return true;

        Logger.Information("[NavalSync] Dropped {What} from {Controller}: battle {Instance} is not at sea or already has its results",
            what, controllerId, instanceId);
        return false;
    }

    // Holds one fate when the sender may report its ship: the ship's captor once one captured it, otherwise its own party's,
    // or as the battle host at the current epoch an AI party's in the battle. Logs why otherwise.
    private void RecordFate(NetworkBattleShipFates message, MapEvent mapEvent, string controllerId, PartyBase senderParty,
        bool isHostAtEpoch, BattleShipFate fate)
    {
        if (fate == null || !fate.IsValid) return;
        if (!objectManager.TryGetObjectWithLogging<Ship>(fate.CampaignShipId, out var ship)) return;

        // A late fate from the hull's earlier authority is older than the capture, and a sinking hull can't be captured.
        var captor = ledger.CaptorOf(message.InstanceId, fate.CampaignShipId);
        if (captor != null)
        {
            if (captor == controllerId) ledger.Record(message.InstanceId, fate, message.HostEpoch, message.Revision);
            else DropFate(fate, controllerId, "another member captured it");
            return;
        }

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

    // Resolves the sender on the poll thread like its fates, which follow on the same channel, so the record precedes them.
    private void Handle_NetworkBattleShipCaptured(MessagePayload<NetworkBattleShipCaptured> payload)
    {
        if (payload.Who is not NetPeer peer) return;

        var message = payload.What;
        if (!playerManager.TryGetCurrentController(peer, out var controllerId))
        {
            Logger.Warning("[NavalSync] Dropped a ship capture for {Instance}: the sender is not a registered player's current peer",
                message.InstanceId);
            return;
        }

        GameThread.RunSafe(() => RecordCapture(controllerId, message), context: nameof(Handle_NetworkBattleShipCaptured));
    }

    // [Game thread] The captor reports the ship's fates from now on; its own party's ship taken back needs no record. The owner's
    // party may have left the battle already (a PvP victim that disconnected right after confirming), so it isn't required in it.
    private void RecordCapture(string controllerId, NetworkBattleShipCaptured message)
    {
        if (!TryGetOpenBattle(controllerId, message.InstanceId, "a ship capture", out var player, out _)) return;
        if (!objectManager.TryGetObjectWithLogging<Ship>(message.CampaignShipId, out var ship)) return;

        var owner = ship.Owner;
        if (owner == null)
        {
            Logger.Warning("[NavalSync] Dropped the capture of ship {ShipId} by {Controller}: the ship has no owner",
                message.CampaignShipId, controllerId);
            return;
        }

        objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var senderParty);
        bool ownShip = owner == senderParty?.Party;
        ledger.RecordCapture(message.InstanceId, message.CampaignShipId, ownShip ? null : controllerId, message.CaptureSequence);
        Logger.Information("[NavalSync] Ship {ShipId} of {Owner} captured by {Controller} in battle {Instance} at capture sequence {Sequence}{Own}",
            message.CampaignShipId, owner.Id, controllerId, message.InstanceId, message.CaptureSequence, ownShip ? " (its own ship back)" : "");
    }

    // Logs a fate the sender may not report.
    private static void DropFate(BattleShipFate fate, string controllerId, string reason) =>
        Logger.Warning("[NavalSync] Dropped the fate of ship {ShipId} from {Controller}: {Reason}", fate.CampaignShipId,
            controllerId, reason);

    // A departing player's ships, and the ones it captured and took along, take their fates now, so a return or a later result
    // reads them; an emptied battle takes all.
    private void Handle_MissionMemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
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

        if (!playerManager.TryGetPlayer(departure.ControllerId, out var player))
        {
            Logger.Warning("[NavalSync] Departure of {Controller} from {Instance} applied no fates: no registered player",
                departure.ControllerId, departure.InstanceId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;

        applier.ApplyParty(mapEvent, party.Party, departure.ControllerId);
    }

    // A battle that ends without a result commit (abandoned, orphaned) still applies what its members reported; its captures
    // are done with then.
    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        var mapEvent = payload.What.MapEvent;
        GameThread.RunSafe(() =>
        {
            applier.ApplyAll(mapEvent);
            if (objectManager.TryGetId(mapEvent, out var mapEventId)) ledger.ForgetCaptures(mapEventId);
        }, context: nameof(Handle_MapEventFinalized));
    }
}
