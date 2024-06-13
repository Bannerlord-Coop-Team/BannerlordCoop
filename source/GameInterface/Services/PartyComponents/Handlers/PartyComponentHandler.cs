using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.PartyComponents.Data;
using GameInterface.Services.PartyComponents.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;

namespace GameInterface.Services.PartyComponents.Handlers;
internal class PartyComponentHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly PartyComponentRegistry registry;

    private readonly Type[] partyTypes = new Type[]
    {
        typeof(BanditPartyComponent),
        typeof(CaravanPartyComponent),
        typeof(CustomPartyComponent),
        typeof(GarrisonPartyComponent),
        typeof(LordPartyComponent),
        typeof(MilitiaPartyComponent),
        typeof(VillagerPartyComponent),
    };

    public PartyComponentHandler(IMessageBroker messageBroker, INetwork network, PartyComponentRegistry registry)
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

        var typeIndex = partyTypes.IndexOf(payload.What.Instance.GetType());
        var data = new PartyComponentData(typeIndex, id);
        network.SendAll(new NetworkCreatePartyComponent(data));
    }

    private void Handle(MessagePayload<NetworkCreatePartyComponent> payload)
    {
        var data = payload.What.Data;
        var typeIdx = data.TypeIndex;

        var obj = ObjectHelper.SkipConstructor(partyTypes[typeIdx]);

        registry.RegisterExistingObject(data.Id, obj);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyComponentCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreatePartyComponent>(Handle);
    }
}
