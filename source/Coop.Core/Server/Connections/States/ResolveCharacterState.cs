using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection determining if a character already
/// exists for this connection
/// </summary>
public class ResolveCharacterState : ConnectionStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<ResolveCharacterState>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    public ResolveCharacterState(IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network) 
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkClientValidate>(ClientValidateHandler);
        messageBroker.Subscribe<HeroResolved>(ResolveHeroHandler);
        messageBroker.Subscribe<ResolveHeroNotFound>(HeroNotFoundHandler);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkClientValidate>(ClientValidateHandler);
        messageBroker.Unsubscribe<HeroResolved>(ResolveHeroHandler);
        messageBroker.Unsubscribe<ResolveHeroNotFound>(HeroNotFoundHandler);
    }

    internal void ClientValidateHandler(MessagePayload<NetworkClientValidate> obj)
    {
        var peer = obj.Who as NetPeer;
        if (peer != ConnectionLogic.Peer) return;

        messageBroker.Publish(this, new ResolveHero(obj.What.PlayerId));
    }

    internal void ResolveHeroHandler(MessagePayload<HeroResolved> obj)
    {
        var validateMessage = new NetworkClientValidated(true, obj.What.HeroId);
        var playerPeer = ConnectionLogic.Peer;
        network.Send(playerPeer, validateMessage);
        ConnectionLogic.TransferSave();
    }

    internal void HeroNotFoundHandler(MessagePayload<ResolveHeroNotFound> obj)
    {
        var validateMessage = new NetworkClientValidated(false, string.Empty);
        var playerPeer = ConnectionLogic.Peer;
        network.Send(playerPeer, validateMessage);
        ConnectionLogic.CreateCharacter();
    }

    public override void CreateCharacter()
    {
        ConnectionLogic.SetState<CreateCharacterState>();
    }

    public override void TransferSave()
    {
        ConnectionLogic.SetState<TransferSaveState>();
    }

    public override void Load()
    {
    }

    public override void EnterCampaign()
    {
    }

    public override void EnterMission()
    {
    }
}
