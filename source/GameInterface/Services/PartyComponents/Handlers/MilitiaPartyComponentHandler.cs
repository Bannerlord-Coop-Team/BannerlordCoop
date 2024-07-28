using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.PartyComponents.Data;
using GameInterface.Services.PartyComponents.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Handlers;
internal class MilitiaPartyComponentHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly PartyComponentRegistry registry;

    public MilitiaPartyComponentHandler(IMessageBroker messageBroker, INetwork network, PartyComponentRegistry registry)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.registry = registry;
        messageBroker.Subscribe<PartyComponentCreated>(Handle);
        messageBroker.Subscribe<NetworkCreatePartyComponent>(Handle);
    }

    private void Handle(MessagePayload<PartyComponentCreated> payload)
    {
        registry.RegisterNewObject(payload.What.Instance, out var id);

        var data = new PartyComponentData(id);
        network.SendAll(new NetworkCreatePartyComponent(data));
    }

    private void Handle(MessagePayload<NetworkCreatePartyComponent> payload)
    {
        var data = payload.What.Data;

        // TODO add all types
        var obj = ObjectHelper.SkipConstructor<LordPartyComponent>();

        registry.RegisterExistingObject(data.Id, obj);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyComponentCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreatePartyComponent>(Handle);
    }
}
