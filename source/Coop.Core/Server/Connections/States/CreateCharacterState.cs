using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection creating a character
/// </summary>
public class CreateCharacterState : ConnectionStateBase
{
    private readonly ILogger Logger = LogManager.GetLogger<CreateCharacterState>();
    private readonly IObjectManager objectManager;
    private IMessageBroker messageBroker;
    private INetwork network;
    private readonly IHeroInterface heroInterface;
    private readonly IPlayerRegistry playerRegistry;

    public CreateCharacterState(
        IConnectionLogic connectionLogic,
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        INetwork network,
        IHeroInterface heroInterface,
        IPlayerRegistry playerRegistry)
        : base(connectionLogic)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.network = network;
        this.heroInterface = heroInterface;
        this.playerRegistry = playerRegistry;
        messageBroker.Subscribe<NetworkTransferNewHero>(Handle_NetworkTransferNewHero);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkTransferNewHero>(Handle_NetworkTransferNewHero);
    }

    internal void Handle_NetworkTransferNewHero(MessagePayload<NetworkTransferNewHero> obj)
    {
        var netPeer = obj.Who as NetPeer;

        if (netPeer != ConnectionLogic.Peer) return;

        var controllerId = obj.What.PlayerId;
        var data = obj.What.PlayerHero;

        var hero = heroInterface.UnpackHero(controllerId, data);

        if (!objectManager.TryGetIdWithLogging(hero, out var heroId))
            return;
        if (!objectManager.TryGetIdWithLogging(hero.PartyBelongedTo, out var partyId))
            return;

        var player = new Player(heroId, partyId);

        Logger.Debug("New hero created with id: {id}", heroId);

        if (!playerRegistry.AddPlayer(player))
            Logger.Error("Player has been already added.");

        // Send created to all other clients
        network.SendAllBut(netPeer, new NetworkNewPlayerHeroCreated(controllerId, player, data));

        // Respond with ids for the creating client
        network.Send(netPeer, new NetworkHeroRecieved(player));

        ConnectionLogic.TransferSave();
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
        ConnectionLogic.SetState<TransferSaveState>();
    }
}
