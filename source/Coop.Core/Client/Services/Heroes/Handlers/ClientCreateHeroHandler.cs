using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages.Lifetime;
using System;

namespace Coop.Core.Client.Services.Heroes.Handlers;

/// <summary>
/// Client side handler for hero related messages.
/// </summary>
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
        messageBroker.Subscribe<NetworkNewChildrenAdded>(Handle_ChildrenAdded);
        messageBroker.Subscribe<NetworkNewSpecialItemAdded>(Handle_SpecialItem);


    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkCreateHero>(Handle_NetworkCreateHero);
        messageBroker.Unsubscribe<NetworkChangeHeroName>(Handle_NetworkChangeHeroName);
        messageBroker.Unsubscribe<HeroCreated>(Handle_HeroCreated);
        messageBroker.Unsubscribe<NetworkNewChildrenAdded>(Handle_ChildrenAdded);
        messageBroker.Unsubscribe<NetworkNewSpecialItemAdded>(Handle_SpecialItem);

    }

    private void Handle_SpecialItem(MessagePayload<NetworkNewSpecialItemAdded> payload)
    {
        var obj = payload.What;

        messageBroker.Publish(this, new HeroAddSpecialItem(obj.HeroId, obj.ItemObjectId));
    }



    private void Handle_ChildrenAdded(MessagePayload<NetworkNewChildrenAdded> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new AddNewChildren(obj.HeroId, obj.ChildId));
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
