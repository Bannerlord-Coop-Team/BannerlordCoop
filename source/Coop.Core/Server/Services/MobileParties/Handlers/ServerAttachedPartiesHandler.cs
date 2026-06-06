using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Server handler for state change of attached parties
/// </summary>
public class ServerAttachedPartiesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ServerAttachedPartiesHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<AttachedPartyAdded>(Handle_AttachedPartyAdded);
        messageBroker.Subscribe<AttachedPartyRemoved>(Handle_AttachedPartyRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AttachedPartyAdded>(Handle_AttachedPartyAdded);
        messageBroker.Unsubscribe<AttachedPartyRemoved>(Handle_AttachedPartyRemoved);
    }

    private void Handle_AttachedPartyAdded(MessagePayload<AttachedPartyAdded> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Instance, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Value, out var attachedPartyId)) return;

        network.SendAll(new NetworkAddAttachedParty(partyId, attachedPartyId));
    }

    private void Handle_AttachedPartyRemoved(MessagePayload<AttachedPartyRemoved> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Instance, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Value, out var attachedPartyId)) return;

        network.SendAll(new NetworkRemoveAttachedParty(partyId, attachedPartyId));
    }
}