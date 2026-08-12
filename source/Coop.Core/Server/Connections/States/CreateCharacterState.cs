using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection creating a character
/// </summary>
public class CreateCharacterState : ConnectionStateBase
{
    private readonly ILogger Logger = LogManager.GetLogger<CreateCharacterState>();
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IHeroInterface heroInterface;
    private readonly IPlayerManager playerManager;
    private readonly IExistingPlayerSender existingPlayerSender;
    private readonly IConnectionMessageQueue connectionMessageQueue;
    private bool playerCommitted;

    public CreateCharacterState(
        IConnectionLogic connectionLogic,
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        INetwork network,
        IHeroInterface heroInterface,
        IPlayerManager playerManager,
        IExistingPlayerSender existingPlayerSender,
        IConnectionMessageQueue connectionMessageQueue)
        : base(connectionLogic)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.network = network;
        this.heroInterface = heroInterface;
        this.playerManager = playerManager;
        this.existingPlayerSender = existingPlayerSender;
        this.connectionMessageQueue = connectionMessageQueue;
        messageBroker.Subscribe<NetworkTransferNewHero>(Handle_NetworkTransferNewHero);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkTransferNewHero>(Handle_NetworkTransferNewHero);
        if (!playerCommitted)
            playerManager.ReleaseNewControllerReservation(ConnectionLogic.Peer);
    }

    internal void Handle_NetworkTransferNewHero(MessagePayload<NetworkTransferNewHero> obj)
    {
        var netPeer = obj.Who as NetPeer;

        if (netPeer != ConnectionLogic.Peer) return;
        if (netPeer.ConnectionState != ConnectionState.Connected)
        {
            playerManager.ReleaseNewControllerReservation(netPeer);
            return;
        }

        var controllerId = obj.What.PlayerId;
        var data = obj.What.PlayerHero;

        if (!playerManager.TryReserveNewController(
                controllerId, netPeer, out NetPeer existingPeer))
        {
            Logger.Warning(
                "Rejected character payload for unreserved controller {ControllerId} from peer {PeerId}; " +
                "reservation belongs to peer {ExistingPeerId}",
                controllerId,
                netPeer.Id,
                existingPeer?.Id);
            netPeer.Disconnect();
            return;
        }

        connectionMessageQueue.InvalidateJoinSnapshot();

        Logger.Debug("Unpacking hero for {ControllerId}", controllerId);

        Hero hero;
        try
        {
            hero = heroInterface.ServerUnpackHero(data);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Character creation failed for {ControllerId}; disconnecting", controllerId);
            netPeer.Disconnect();
            return;
        }

        if (netPeer.ConnectionState != ConnectionState.Connected)
        {
            heroInterface.DiscardUncommittedServerHero(hero);
            playerManager.ReleaseNewControllerReservation(netPeer);
            return;
        }

        if (!TryCreatePlayer(controllerId, hero, out var player))
        {
            Logger.Error("Failed to create player; disconnecting the joining peer");
            heroInterface.DiscardUncommittedServerHero(hero);
            netPeer.Disconnect();
            return;
        }

        if (!playerManager.TryCommitReservedPlayer(controllerId, netPeer, player))
        {
            Logger.Error(
                "Character creation reservation was lost for {ControllerId}; disconnecting",
                controllerId);
            heroInterface.DiscardUncommittedServerHero(hero);
            netPeer.Disconnect();
            return;
        }
        playerCommitted = true;
        connectionMessageQueue.InvalidateJoinSnapshot();

        // Once committed, every live client must learn about the authoritative hero even if the
        // creator disconnects immediately afterwards.
        var message = new NetworkNewPlayerHeroCreated(controllerId, player, data);
        network.SendAllBut(netPeer, message);

        if (netPeer.ConnectionState != ConnectionState.Connected)
            return;

        // Respond with ids for the creating client
        network.SendImmediate(netPeer, new NetworkHeroRecieved(player));

        ConnectionLogic.TransferSave();

        // TransferSave has taken the save snapshot and begun queueing this peer's broadcasts, so tell the
        // joiner about every other existing player. These queue and replay once it enters its campaign.
        existingPlayerSender.SendExistingPlayers(netPeer, controllerId);
    }

    private bool TryCreatePlayer(string controllerId, Hero hero, out Player player)
    {
        player = null;

        if (!objectManager.TryGetIdWithLogging(hero, out var heroId))
            return false;
        if (!objectManager.TryGetIdWithLogging(hero.PartyBelongedTo, out var mobilePartyId))
            return false;
        if (!objectManager.TryGetIdWithLogging(hero.Clan, out var clanId))
            return false;
        if (!objectManager.TryGetIdWithLogging(hero.CharacterObject, out var characterObjectId))
            return false;

        player = new Player(controllerId, heroId, mobilePartyId, clanId, characterObjectId);
        return true;
    }

    public override void CreateCharacter()
    {
    }

    public override void EnterCampaign()
    {
    }

    public override void EnterMission()
    {
    }

    public override void Load()
    {
    }

    public override void TransferSave()
    {
        var transfer = ConnectionLogic.SetState<TransferSaveState>();
        if (transfer.StartTransfer())
            ConnectionLogic.Load();
    }
}
