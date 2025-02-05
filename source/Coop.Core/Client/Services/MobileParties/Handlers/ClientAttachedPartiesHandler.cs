using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Client handler for state change of attached parties
/// </summary>
public class ClientAttachedPartiesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientAttachedPartiesHandler(
        IMessageBroker messageBroker,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<NetworkAddAttachedParty>(Handle_NetworkAttachedPartyAdded);
        messageBroker.Subscribe<NetworkRemoveAttachedParty>(Handle_NetworkAttachedPartyRemoved);
    }

    private void Handle_NetworkAttachedPartyRemoved(MessagePayload<NetworkRemoveAttachedParty> payload)
    {
        var data = payload.What.AttachedPartyData;
        messageBroker.Publish(this, new RemoveAttachedParty(data));
    }

    private void Handle_NetworkAttachedPartyAdded(MessagePayload<NetworkAddAttachedParty> payload)
    {
        var data = payload.What.AttachedPartyData;
        messageBroker.Publish(this, new AddAttachedParty(data));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddAttachedParty>(Handle_NetworkAttachedPartyAdded);
        messageBroker.Unsubscribe<NetworkRemoveAttachedParty>(Handle_NetworkAttachedPartyRemoved);
    }
}