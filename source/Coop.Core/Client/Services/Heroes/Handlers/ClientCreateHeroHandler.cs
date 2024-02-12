using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
using System;

namespace Coop.Core.Client.Services.Heroes.Handlers;
internal class ClientCreateHeroHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientCreateHeroHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkCreateHero>(Handle_NetworkCreateHero);
        messageBroker.Subscribe<NetworkChangeHeroName>(Handle_NetworkChangeHeroName);
        messageBroker.Subscribe<HeroCreated>(Handle_HeroCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkCreateHero>(Handle_NetworkCreateHero);
    }

    private void Handle_NetworkCreateHero(MessagePayload<NetworkCreateHero> payload)
    {
        var message = new CreateHero(payload.What.Data);
        messageBroker.Publish(this, message);
    }

    private void Handle_NetworkChangeHeroName(MessagePayload<NetworkChangeHeroName> payload)
    {
        var message = new ChangeHeroName(payload.What.Data);
        messageBroker.Publish(this, message);
    }

    private void Handle_HeroCreated(MessagePayload<HeroCreated> payload)
    {
        var message = new NetworkHeroCreated();
        network.SendAll(message);
    }
}
