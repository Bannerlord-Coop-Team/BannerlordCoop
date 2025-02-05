using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Messages;

namespace Coop.Core.Server.Services.PartyBases.Handlers;

/// <summary>
/// Handles ItemRosterUpdated and sends network event to all clients.
/// </summary>
public class ItemRosterMessageHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ItemRosterMessageHandler(IMessageBroker broker, INetwork network)
    {
        messageBroker = broker;
        this.network = network;

        messageBroker.Subscribe<ItemRosterUpdated>(Handle);
        messageBroker.Subscribe<ItemRosterCleared>(Handle);
    }

    public void Handle(MessagePayload<ItemRosterUpdated> payload)
    {
        network.SendAll(new NetworkItemRosterUpdate(
                payload.What.PartyBaseID,
                payload.What.ItemID,
                payload.What.ItemModifierID,
                payload.What.Amount)
            );
    }

    public void Handle(MessagePayload<ItemRosterCleared> payload)
    {
        network.SendAll(new NetworkItemRosterClear(payload.What.PartyBaseID));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ItemRosterUpdated>(Handle);
        messageBroker.Unsubscribe<ItemRosterCleared>(Handle);
    }
}
