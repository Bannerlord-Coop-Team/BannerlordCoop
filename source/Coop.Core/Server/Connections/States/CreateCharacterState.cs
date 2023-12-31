using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Entity.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection creating a character
/// </summary>
public class CreateCharacterState : ConnectionStateBase
{
    private readonly ILogger Logger = LogManager.GetLogger<CreateCharacterState>();

    private IMessageBroker messageBroker;
    private INetwork network;
    public CreateCharacterState(
        IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkTransferedHero>(PlayerTransferedHeroHandler);
        messageBroker.Subscribe<NewPlayerHeroRegistered>(PlayerHeroRegisteredHandler);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkTransferedHero>(PlayerTransferedHeroHandler);
        messageBroker.Unsubscribe<NewPlayerHeroRegistered>(PlayerHeroRegisteredHandler);
    }
    internal void PlayerTransferedHeroHandler(MessagePayload<NetworkTransferedHero> obj)
    {
        var netPeer = obj.Who as NetPeer;

        if (netPeer != ConnectionLogic.Peer) return;

        var controllerId = obj.What.PlayerId;
        var data = obj.What.PlayerHero;
        var registerCommand = new RegisterNewPlayerHero(netPeer, controllerId, data);
        messageBroker.Publish(this, registerCommand);

        var forwardMessage = new NetworkNewPartyCreated(controllerId, data);

        network.SendAllBut(netPeer, forwardMessage);
    }
    internal void PlayerHeroRegisteredHandler(MessagePayload<NewPlayerHeroRegistered> obj)
    {
        var sendingPeer = obj.What.SendingPeer;
        if (sendingPeer != ConnectionLogic.Peer) return;

        NetworkPlayerData playerData = new NetworkPlayerData(obj.What);

        // Send newly create player to all clients
        var peer = ConnectionLogic.Peer;
        network.Send(peer, playerData);

        var newPlayerData = obj.What.NewPlayerData;

        Logger.Information("Hero StringId: {stringId}", newPlayerData?.HeroStringId);
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
