using Common.Messaging;
using Coop.Core.Client.Services.Players.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;

namespace Coop.Core.Client.Services.Players.Handlers;

internal class NewPlayerHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public NewPlayerHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkNewPlayerData>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNewPlayerData>(Handle);
    }

    private void Handle(MessagePayload<NetworkNewPlayerData> obj)
    {
        byte[] heroData = obj.What.HeroData;
        var controllerId = obj.What.PlayerId;
        var peer = obj.Who as NetPeer;

        messageBroker.Publish(this, new RegisterNewPlayerHero(peer, controllerId, heroData));
    }
}
