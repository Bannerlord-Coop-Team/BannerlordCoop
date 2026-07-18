using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Coop.Core.Server.Services.ItemRosters.Handlers;

/// <summary>
/// Handles ItemRosterUpdated and sends network event to all clients.
/// </summary>
public class ItemRosterMessageHandler : IHandler
{
    // Coalescer channel for per-element ItemRoster updates; member is the item+modifier pair.
    private const string ItemRosterUpdateChannel = "ItemRosterUpdate";

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISendCoalescer coalescer;

    public ItemRosterMessageHandler(IMessageBroker broker, INetwork network, IObjectManager objectManager, ISendCoalescer coalescer)
    {
        messageBroker = broker;
        this.network = network;
        this.objectManager = objectManager;
        this.coalescer = coalescer;
        messageBroker.Subscribe<ItemRosterUpdated>(Handle);
        messageBroker.Subscribe<ItemRosterCleared>(Handle);
    }

    public void Handle(MessagePayload<ItemRosterUpdated> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetIdWithLogging(message.Instance, out var itemRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(message.Item, out var itemId)) return;

        string itemModifierId = null;
        if (message.ItemModifier != null &&
            !objectManager.TryGetIdWithLogging(message.ItemModifier, out itemModifierId))
        {
            return;
        }

        itemRosterId = Compact(itemRosterId, typeof(ItemRoster));
        itemId = Compact(itemId, typeof(ItemObject));
        itemModifierId = Compact(itemModifierId, typeof(ItemModifier));

        // Sum this tick's deltas for the element and send one update at flush instead of one per AddToCounts.
        var key = new CoalesceKey(ItemRosterUpdateChannel, itemRosterId, $"{itemId}:{itemModifierId}");
        coalescer.Enqueue(key, new SummedPayload<int>(
            message.Amount,
            (running, next) => running + next,
            total => new NetworkItemRosterUpdate(itemRosterId, itemId, itemModifierId, total)),
            // Clients must replay the roster callback before applying authoritative derived market data.
            CoalescedSendPriority.Prerequisite);
    }

    public void Handle(MessagePayload<ItemRosterCleared> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetIdWithLogging(message.ItemRoster, out var itemRosterId)) return;
        itemRosterId = Compact(itemRosterId, typeof(ItemRoster));

        // A clear supersedes this roster's pending updates; drop them so the clear isn't trailed by a stale update.
        coalescer.DropInstance(itemRosterId);

        network.SendAll(new NetworkItemRosterClear(itemRosterId));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ItemRosterUpdated>(Handle);
        messageBroker.Unsubscribe<ItemRosterCleared>(Handle);
    }
}
