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
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    private bool controlPartiesByDefault = false;

    private Guid ownerId => controlledEntityRegistry.InstanceOwnerId;

    public MobilePartyControlHandler(
        IMessageBroker messageBroker, 
        IMobilePartyInterface partyInterface, 
        IControlledEntityRegistry controlledEntityRegistry)
    {
        this.messageBroker = messageBroker;
        this.partyInterface = partyInterface;
        this.controlledEntityRegistry = controlledEntityRegistry;

        messageBroker.Subscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        messageBroker.Subscribe<MobilePartyCreated>(Handle_MobilePartyCreated);
        messageBroker.Subscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
    }

    private void Handle_RegisterAllPartiesAsControlled(MessagePayload<RegisterAllPartiesAsControlled> obj)
    {
        controlPartiesByDefault = true;

        partyInterface.RegisterAllPartiesAsControlled(ownerId);
    }

    private void Handle_MobilePartyCreated(MessagePayload<MobilePartyCreated> obj)
    {
        if (!controlPartiesByDefault) return;

        MobileParty party = obj.What.Party;
        controlledEntityRegistry.RegisterAsControlled(ownerId, party.StringId);
    }

    private void Handle_MobilePartyDestroyed(MessagePayload<MobilePartyDestroyed> obj)
    {
        MobileParty party = obj.What.Party;

        if (!controlledEntityRegistry.TryGetControlledEntity(party.StringId, out var controlledEntity))
            return;

        controlledEntityRegistry.RemoveAsControlled(controlledEntity);
    }
}
