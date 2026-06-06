using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Client handler for state change of attached parties
/// </summary>
public class ClientAttachedPartiesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public ClientAttachedPartiesHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkAddAttachedParty>(Handle_NetworkAttachedPartyAdded);
        messageBroker.Subscribe<NetworkRemoveAttachedParty>(Handle_NetworkAttachedPartyRemoved);
    }

    private void Handle_NetworkAttachedPartyAdded(MessagePayload<NetworkAddAttachedParty> payload)
    {
        messageBroker.Publish(this, new AddAttachedParty(payload.What.PartyId, payload.What.AttachedPartyId));
    }

    private void Handle_NetworkAttachedPartyRemoved(MessagePayload<NetworkRemoveAttachedParty> payload)
    {
        messageBroker.Publish(this, new RemoveAttachedParty(payload.What.PartyId, payload.What.AttachedPartyId));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddAttachedParty>(Handle_NetworkAttachedPartyAdded);
        messageBroker.Unsubscribe<NetworkRemoveAttachedParty>(Handle_NetworkAttachedPartyRemoved);
    }
}