using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Server handler for state change of attached parties
/// </summary>
public class ServerAttachedPartiesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerAttachedPartiesHandler(
        IMessageBroker messageBroker,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<AttachedPartyAdded>(Handle_AttachedPartyAdded);
        messageBroker.Subscribe<AttachedPartyRemoved>(Handle_AttachedPartyRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AttachedPartyAdded>(Handle_AttachedPartyAdded);
        messageBroker.Unsubscribe<AttachedPartyRemoved>(Handle_AttachedPartyRemoved);
    }

    private void Handle_AttachedPartyRemoved(MessagePayload<AttachedPartyRemoved> payload)
    {
        var data = payload.What.AttachedPartyData;
        network.SendAll(new NetworkRemoveAttachedParty(data));
    }

    private void Handle_AttachedPartyAdded(MessagePayload<AttachedPartyAdded> payload)
    {
        var data = payload.What.AttachedPartyData;
        network.SendAll(new NetworkAddAttachedParty(data));
    }
}