using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Handlers;
internal class PartyBaseLifetimeHandler : IHandler
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly ILogger logger;

    public static List<PartyBase> Instances = new List<PartyBase>();
    public PartyBaseLifetimeHandler(IObjectManager objectManager, INetwork network, IMessageBroker messageBroker, ILogger logger)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
        this.logger = logger;

        messageBroker.Subscribe<PartyBaseCreated>(Handle_PartyBaseCreated);
        messageBroker.Subscribe<NetworkCreatePartyBase>(Handle_NetworkCreatePartyBase);

        messageBroker.Subscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Subscribe<NetworkDestroyPartyBase>(Handle_NetworkDestroyPartyBase);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyBaseCreated>(Handle_PartyBaseCreated);
        messageBroker.Unsubscribe<NetworkCreatePartyBase>(Handle_NetworkCreatePartyBase);

        messageBroker.Unsubscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Unsubscribe<NetworkDestroyPartyBase>(Handle_NetworkDestroyPartyBase);
    }

    private void Handle_PartyBaseCreated(MessagePayload<PartyBaseCreated> payload)
    {
        var instance = payload.What.Instance;

        Instances.Add(instance); // TODO remove

        

        if (objectManager.AddNewObject(instance, out var newId) == false)
        {
            logger.Error("Unable to add new {type} to object manager", instance.GetType());
            return;
        }

        DebugMessageLogger.Write($"Created new PartyBase with id: {newId}");

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
