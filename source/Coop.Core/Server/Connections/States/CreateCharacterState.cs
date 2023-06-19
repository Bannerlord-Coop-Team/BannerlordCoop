using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
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
    public CreateCharacterState(IConnectionLogic connectionLogic)
        : base(connectionLogic)
    {
        messageBroker = connectionLogic.MessageBroker;
        network = connectionLogic.Network;

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
        var registerCommand = new RegisterNewPlayerHero(obj.What.PlayerHero);
        messageBroker.Publish(this, registerCommand);
    }
    internal void PlayerHeroRegisteredHandler(MessagePayload<NewPlayerHeroRegistered> obj)
    {
        var sendingPeer = (NetPeer)obj.Who;
        if (sendingPeer != ConnectionLogic.Peer) return;

        NetworkPlayerData playerData = new NetworkPlayerData(obj.What);
        // Send newly create player to all clients
        var peer = ConnectionLogic.Peer;
        network.Send(peer, playerData);

        ConnectionLogic.HeroStringId = obj.What.HeroStringId;
        Logger.Information("Hero StringId: {stringId}", obj.What.HeroStringId);
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
        ConnectionLogic.State = new TransferSaveState(ConnectionLogic);
    }
}
