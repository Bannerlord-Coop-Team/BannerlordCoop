using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Data;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Control;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles control of mobile party entities.
/// </summary>
internal class MobilePartyControlHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyInterface partyInterface;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IObjectManager objectManager;

    private bool controlPartiesByDefault = false;

    private Guid ownerId => controlledEntityRegistry.InstanceOwnerId;

    public MobilePartyControlHandler(
        IMessageBroker messageBroker, 
        IMobilePartyInterface partyInterface, 
        IControlledEntityRegistry controlledEntityRegistry,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.partyInterface = partyInterface;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.objectManager = objectManager;

        messageBroker.Subscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        messageBroker.Subscribe<UpdateMobilePartyControl>(Handle_UpdateMobilePartyControl);
        messageBroker.Subscribe<MobilePartyCreated>(Handle_MobilePartyCreated);
        messageBroker.Subscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        messageBroker.Unsubscribe<UpdateMobilePartyControl>(Handle_UpdateMobilePartyControl);
        messageBroker.Unsubscribe<MobilePartyCreated>(Handle_MobilePartyCreated);
        messageBroker.Unsubscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
    }

    private void Handle_RegisterAllPartiesAsControlled(MessagePayload<RegisterAllPartiesAsControlled> obj)
    {
        controlPartiesByDefault = true;

        partyInterface.RegisterAllPartiesAsControlled(ownerId);
    }

    private void Handle_UpdateMobilePartyControl(MessagePayload<UpdateMobilePartyControl> obj)
    {
        string partyId = obj.What.PartyId;

        if (obj.What.IsRevocation == false)
        {
            controlledEntityRegistry.RegisterAsControlled(ownerId, partyId);
            return;
        }

        controlledEntityRegistry.RemoveAsControlled(new ControlledEntity(ownerId, partyId));

        if (ModInformation.IsServer && objectManager.TryGetObject(partyId, out MobileParty party))
        {
            party.Ai.SetDoNotMakeNewDecisions(true);
        }
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
