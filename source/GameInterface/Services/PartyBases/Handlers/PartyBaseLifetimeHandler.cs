using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Handlers;
internal class PartyBaseLifetimeHandler : IHandler
{
    private readonly ILogger logger = LogManager.GetLogger<PartyBaseLifetimeHandler>();

    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;

    public PartyBaseLifetimeHandler(IObjectManager objectManager, INetwork network, IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;

        
        messageBroker.Subscribe<PartyBaseCreated>(Handle_PartyBaseCreated);
        messageBroker.Subscribe<NetworkCreatePartyBase>(Handle_NetworkCreatePartyBase);

        messageBroker.Subscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Subscribe<NetworkDestroyPartyBase>(Handle_NetworkDestroyPartyBase);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private void Handle_PartyBaseCreated(MessagePayload<PartyBaseCreated> payload)
    {
        var instance = payload.What.Instance;

        if(objectManager.AddNewObject(instance, out var newId) == false)
        {
            logger.Error("Unable to add new {type} to object manager", instance.GetType());
            return;
        }

        var message = new NetworkCreatePartyBase(newId);
        network.SendAll(message);
    }

    private void Handle_NetworkCreatePartyBase(MessagePayload<NetworkCreatePartyBase> payload)
    {
        var id = payload.What.Id;
        var newPartyBase = ObjectHelper.SkipConstructor<PartyBase>();


        if (objectManager.AddExisting(id, newPartyBase) == false)
        {
            logger.Error("Unable to create new {type} with id {id}", newPartyBase.GetType(), id);
            return;
        }
    }

    private void Handle_PartyDestroyed(MessagePayload<PartyDestroyed> payload)
    {
        var partyBase = payload.What.Instance.Party;

        if (objectManager.TryGetId(partyBase, out var id) == false)
        {
            logger.Error("Unable to get id for {type} attached to party with id {id}", typeof(PartyBase), payload.What.Instance.StringId);
            return;
        }

        if (objectManager.Remove(partyBase) == false)
        {
            logger.Error("Unable to remove {type} with id {baseId} attached to party with id {partyId}", typeof(PartyBase), id, payload.What.Instance.StringId);
            return;
        }

        var message = new NetworkDestroyPartyBase(id);
        network.SendAll(message);
    }

    private void Handle_NetworkDestroyPartyBase(MessagePayload<NetworkDestroyPartyBase> payload)
    {
        var id = payload.What.Id;

        if (objectManager.TryGetObject<PartyBase>(id, out var partyBase) == false)
        {
            logger.Error("Unable to get {type} with id {id}", typeof(PartyBase), id);
            return;
        }

        if (objectManager.Remove(partyBase) == false)
        {
            logger.Error("Unable to remove {type} with id {id}", typeof(PartyBase), id);
            return;
        }
    }
}
