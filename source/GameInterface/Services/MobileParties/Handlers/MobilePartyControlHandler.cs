using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Data;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages.Control;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles control of mobile party entities.
/// </summary>
internal class MobilePartyControlHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyControlHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyInterface partyInterface;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private bool controlPartiesByDefault = false;

    private string ownerId => controllerIdProvider.ControllerId;

    public MobilePartyControlHandler(
        IMessageBroker messageBroker, 
        IMobilePartyInterface partyInterface, 
        IControlledEntityRegistry controlledEntityRegistry,
        IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.partyInterface = partyInterface;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        messageBroker.Subscribe<UpdateMobilePartyControl>(Handle_UpdateMobilePartyControl);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        messageBroker.Unsubscribe<UpdateMobilePartyControl>(Handle_UpdateMobilePartyControl);
    }

    private void Handle_RegisterAllPartiesAsControlled(MessagePayload<RegisterAllPartiesAsControlled> obj)
    {
        controlPartiesByDefault = true;

        partyInterface.RegisterAllPartiesAsControlled(ownerId);
    }

    private void Handle_UpdateMobilePartyControl(MessagePayload<UpdateMobilePartyControl> obj)
    {
        string partyId = obj.What.PartyId;
        var controllerId = obj.What.ControllerId;

        if (obj.What.IsRevocation == false)
        {
            controlledEntityRegistry.RegisterAsControlled(controllerId, partyId);
            messageBroker.Publish(this, new RegisterPartyController(controllerId, partyId));
        }
        else
        {
            controlledEntityRegistry.RemoveAsControlled(new ControlledEntity(controllerId, partyId));
            messageBroker.Publish(this, new RemovePartyController(controllerId, partyId));
        }

        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error($"Unable to resolve {typeof(IGameInterfaceConfig)}\n" +
                $"Callstack: {Environment.StackTrace}");
        }

        if (config.IsServer && objectManager.TryGetObject(partyId, out MobileParty party))
        {
            bool aiDisabled = obj.What.IsRevocation ? false : true;
            party.Ai.SetDoNotMakeNewDecisions(aiDisabled);
        }
    }
}
