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
using GameInterface.Services.Ships.Messages;
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

    // Queued behind the sender's earlier messages, so a departure it sent after these fates applies them.
    private void Handle_NetworkBattleShipFates(MessagePayload<NetworkBattleShipFates> payload)
    {
        if (ModInformation.IsClient || payload.Who is not NetPeer peer) return;

        var message = payload.What;
        GameThread.RunSafe(() => Record(peer, message), context: nameof(Handle_NetworkBattleShipFates));
    }

    // [Game thread] Holds each fate of a ship the sender may report for, while the battle is open and the sender is in it.
    private void Record(NetPeer peer, NetworkBattleShipFates message)
    {
        if (!playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Warning("[NavalSync] Dropped ship fates for {Instance}: the sender has no registered player", message.InstanceId);
            return;
        }

        if (!missionManager.TryGetControllers(message.InstanceId, out var members) || !members.Contains(player.ControllerId))
        {
            Logger.Information("[NavalSync] Dropped ship fates from {Controller}: it is not in battle {Instance}",
                player.ControllerId, message.InstanceId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.InstanceId, out var mapEvent)) return;
        if (!mapEvent.IsNavalMapEvent || mapEvent.IsFinalized || mapEvent.HasCalculatedResults())
        {
            Logger.Information("[NavalSync] Dropped ship fates from {Controller}: battle {Instance} is not at sea or already has its results",
                player.ControllerId, message.InstanceId);
            return;
        }

        objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var senderParty);
        bool isHostAtEpoch = hostRegistry.TryGet(message.InstanceId, out var assignment)
            && assignment.HostControllerId == player.ControllerId && assignment.Epoch == message.HostEpoch;
        foreach (var fate in message.Fates ?? Array.Empty<BattleShipFate>())
            RecordFate(message, mapEvent, player.ControllerId, senderParty?.Party, isHostAtEpoch, fate);
    }

    // Holds one fate when the sender may report its ship; logs why it may not otherwise.
    private void RecordFate(NetworkBattleShipFates message, MapEvent mapEvent, string controllerId, PartyBase senderParty,
        bool isHostAtEpoch, BattleShipFate fate)
    {
        if (fate == null || !fate.IsValid) return;
        if (!objectManager.TryGetObjectWithLogging<Ship>(fate.CampaignShipId, out var ship)) return;

        var owner = ship.Owner;
        var rejection = RejectionOf(owner == senderParty, owner != null, owner?.MapEventSide?.MapEvent == mapEvent,
            owner.IsPlayerParty(), isHostAtEpoch);
        if (rejection != null)
        {
            Logger.Warning("[NavalSync] Dropped the fate of ship {ShipId} from {Controller}: {Reason}", fate.CampaignShipId,
                controllerId, rejection);
            return;
        }

        ledger.Record(message.InstanceId, fate, message.HostEpoch, message.Revision);
    }

    /// <summary>
    /// Why a sender may not report a ship's fate, or null: a player reports its own party's ships, and the battle host at the
    /// current epoch reports the ships of the AI parties in the battle.
    /// </summary>
    internal static string RejectionOf(bool ownedBySender, bool hasOwner, bool ownerInBattle, bool ownedByPlayer, bool isHostAtEpoch)
    {
        if (!hasOwner) return "the ship has no owner";
        if (ownedBySender) return null;
        if (!ownerInBattle) return "its owner is not in the battle";
        if (ownedByPlayer) return "another player owns it";
        if (!isHostAtEpoch) return "the sender is not the battle host at this epoch";
        return null;
    }

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
