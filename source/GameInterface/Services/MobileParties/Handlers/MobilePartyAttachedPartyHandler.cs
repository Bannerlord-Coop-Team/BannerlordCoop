using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;


/// <summary>
/// Handler for attached mobile parties
/// </summary>
internal class MobilePartyAttachedPartyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAttachedPartyHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public MobilePartyAttachedPartyHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<AddAttachedParty>(Handle_AddAttachedParty);
        messageBroker.Subscribe<RemoveAttachedParty>(Handle_RemoveAttachedParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddAttachedParty>(Handle_AddAttachedParty);
        messageBroker.Unsubscribe<RemoveAttachedParty>(Handle_RemoveAttachedParty);
    }

    private void Handle_RemoveAttachedParty(MessagePayload<RemoveAttachedParty> payload)
    {
        var data = payload.What.AttachedPartyData;
        var instanceId = data.PartyId;
        var removedPartyId = data.ListPartyId;

        if (objectManager.TryGetObject<MobileParty>(instanceId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), instanceId);
        }

        if (objectManager.TryGetObject<MobileParty>(removedPartyId, out var removedParty) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), removedPartyId);
        }

        instance._attachedParties.Remove(removedParty);
    }

    private void Handle_AddAttachedParty(MessagePayload<AddAttachedParty> payload)
    {
        var data = payload.What.AttachedPartyData;
        var instanceId = data.PartyId;
        var addedPartyId = data.ListPartyId;

        if (objectManager.TryGetObject<MobileParty>(instanceId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), instanceId);
        }

        if (objectManager.TryGetObject<MobileParty>(addedPartyId, out var addedParty) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), addedPartyId);
        }

        instance._attachedParties.Add(addedParty);
    }
}
