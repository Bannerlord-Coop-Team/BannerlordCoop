using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class MobilePartyControlHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyInterface partyInterface;
    private readonly IControlledEntityRegistery controlledEntityRegistery;

    private bool controlPartiesByDefault = false;

    private Guid ownerId => controlledEntityRegistery.InstanceOwnerId;

    public MobilePartyControlHandler(
        IMessageBroker messageBroker, 
        IMobilePartyInterface partyInterface, 
        IControlledEntityRegistery controlledEntityRegistery)
    {
        this.messageBroker = messageBroker;
        this.partyInterface = partyInterface;
        this.controlledEntityRegistery = controlledEntityRegistery;

        messageBroker.Subscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        messageBroker.Subscribe<MobilePartyCreated>(Handle_MobilePartyCreated);
        messageBroker.Subscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
        messageBroker.Subscribe<RegisterOwnerId>(Handle_RegisterOwnerId);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
    }

    private void Handle_RegisterAllPartiesAsControlled(MessagePayload<RegisterAllPartiesAsControlled> obj)
    {
        var ownerId = obj.What.OwnerId;

        controlledEntityRegistery.InstanceOwnerId = ownerId;
        controlPartiesByDefault = true;

        partyInterface.RegisterAllPartiesAsControlled(ownerId);
    }

    private void Handle_MobilePartyCreated(MessagePayload<MobilePartyCreated> obj)
    {
        if (!controlPartiesByDefault) return;

        MobileParty party = obj.What.Party;
        controlledEntityRegistery.RegisterAsControlled(ownerId, party.StringId);
    }

    private void Handle_MobilePartyDestroyed(MessagePayload<MobilePartyDestroyed> obj)
    {
        MobileParty party = obj.What.Party;

        if (!controlledEntityRegistery.TryGetControlledEntity(party.StringId, out var controlledEntity))
            return;

        controlledEntityRegistery.RemoveAsControlled(controlledEntity);
    }


    private void Handle_RegisterOwnerId(MessagePayload<RegisterOwnerId> obj)
    {
        controlledEntityRegistery.InstanceOwnerId = obj.What.OwnerId;
    }

}
