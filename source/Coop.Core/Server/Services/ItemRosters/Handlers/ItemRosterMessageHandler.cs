using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;

namespace Coop.Core.Server.Services.PartyBases.Handlers;

/// <summary>
/// Handles ItemRosterUpdated and sends network event to all clients.
/// </summary>
public class ItemRosterMessageHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ItemRosterMessageHandler(IMessageBroker broker, INetwork network, IObjectManager objectManager)
    {
        messageBroker = broker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<ItemRosterUpdated>(Handle);
        messageBroker.Subscribe<ItemRosterCleared>(Handle);
    }

    public void Handle(MessagePayload<ItemRosterUpdated> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetIdWithLogging(message.PartyBase, out var partyBaseId)) return;
        if (!objectManager.TryGetIdWithLogging(message.Item, out var itemId)) return;

        string? itemModifierId = null;
        if (message.ItemModifier != null &&
            !objectManager.TryGetIdWithLogging(message.ItemModifier, out itemModifierId))
        {
            return;
        }

        network.SendAll(new NetworkItemRosterUpdate(
            partyBaseId,
            itemId,
            itemModifierId,
            message.Amount));
    }

    public void Handle(MessagePayload<ItemRosterCleared> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetIdWithLogging(message.PartyBase, out var partyBaseId)) return;

        network.SendAll(new NetworkItemRosterClear(partyBaseId));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ItemRosterUpdated>(Handle);
        messageBroker.Unsubscribe<ItemRosterCleared>(Handle);
    }
}
