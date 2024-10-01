using Common.Logging;
using Common.Messaging;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Handlers;

/// <summary>
/// Handler for  <see cref="BesiegerCamp._besiegerParties"/>
/// </summary>
internal class BesiegerCampBesiegerPartyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BesiegerCampBesiegerPartyHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public BesiegerCampBesiegerPartyHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkAddBesiegerParty>(Handle_AddBesiegerParty);
        messageBroker.Subscribe<NetworkRemoveBesiegerParty>(Handle_RemoveBesiegerParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddBesiegerParty>(Handle_AddBesiegerParty);
        messageBroker.Unsubscribe<NetworkRemoveBesiegerParty>(Handle_RemoveBesiegerParty);
    }

    private void Handle_RemoveBesiegerParty(MessagePayload<NetworkRemoveBesiegerParty> payload)
    {
        var data = payload.What;
        var instanceId = data.BesiegerCampId;
        var removedPartyId = data.BesiegerPartyId;

        if (objectManager.TryGetObject<BesiegerCamp>(instanceId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(BesiegerCamp), instanceId);
        }

        if (objectManager.TryGetObject<MobileParty>(removedPartyId, out var removedParty) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), removedPartyId);
        }

        instance._besiegerParties.Remove(removedParty);
    }

    private void Handle_AddBesiegerParty(MessagePayload<NetworkAddBesiegerParty> payload)
    {
        var data = payload.What;
        var instanceId = data.BesiegerCampId;
        var addedPartyId = data.BesiegerPartyId;

        if (objectManager.TryGetObject<BesiegerCamp>(instanceId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(BesiegerCamp), instanceId);
        }

        if (objectManager.TryGetObject<MobileParty>(addedPartyId, out var addedParty) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), addedPartyId);
        }

        instance._besiegerParties.Add(addedParty);
    }
}