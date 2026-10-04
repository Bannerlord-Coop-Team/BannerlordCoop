using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Services.ItemRosters.Messages;
using GameInterface.Services.ItemObjects;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using System.Collections.Generic;
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
    private readonly ItemObjectRegistry itemObjectRegistry;

    public ItemRosterMessageHandler(
        IMessageBroker broker,
        INetwork network,
        IObjectManager objectManager,
        ISendCoalescer coalescer,
        ItemObjectRegistry itemObjectRegistry)
    {
        messageBroker = broker;
        this.network = network;
        this.objectManager = objectManager;
        this.coalescer = coalescer;
        this.itemObjectRegistry = itemObjectRegistry;
        messageBroker.Subscribe<ItemRosterUpdated>(Handle);
        messageBroker.Subscribe<ItemRosterCleared>(Handle);
        messageBroker.Subscribe<ResendItemRoster>(Handle);
    }

    public void Handle(MessagePayload<ItemRosterUpdated> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetHandle(message.Instance, out var itemRosterId)) return;
        if (!TryGetItemHandle(message.Item, out var itemId)) return;

        uint itemModifierId = 0;
        if (message.ItemModifier != null &&
            !objectManager.TryGetHandleWithLogging(message.ItemModifier, out itemModifierId))
        {
            return;
        }

        // Sum this tick's deltas for the element and send one update at flush instead of one per AddToCounts.
        var key = new CoalesceKey(ItemRosterUpdateChannel, itemRosterId, $"{itemId}:{itemModifierId}");
        coalescer.Enqueue(key, new SummedPayload<int>(
            message.Amount,
            (running, next) => running + next,
            total => new NetworkItemRosterUpdate(itemRosterId, itemId, itemModifierId, total)));
    }

    private bool TryGetItemHandle(ItemObject item, out uint itemHandle)
    {
        if (!itemObjectRegistry.TryRegisterExistingItem(
                item,
                out _,
                out itemHandle,
                out var announceHandle))
        {
            itemHandle = 0;
            return false;
        }

        if (announceHandle)
        {
            network.SendAll(new NetworkRegisterItemHandle(item.StringId, itemHandle));
            itemObjectRegistry.MarkHandleKnownToClients(itemHandle);
        }

        return true;
    }

    public void Handle(MessagePayload<ItemRosterCleared> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetHandle(message.ItemRoster, out var itemRosterId)) return;

        // A clear supersedes this roster's pending updates; drop them so the clear isn't trailed by a stale update.
        coalescer.DropInstance(itemRosterId);

        network.SendAll(new NetworkItemRosterClear(itemRosterId));
    }

    public void Handle(MessagePayload<ResendItemRoster> payload)
    {
        if (payload.Who is not NetPeer peer) return;

        GameThread.RunSafe(() =>
        {
            var roster = payload.What.ItemRoster;
            if (!objectManager.TryGetHandleWithLogging(roster, out uint rosterId)) return;
            var updates = new List<NetworkItemRosterUpdate>();
            foreach (var item in roster)
            {
                if (!TryGetItemHandle(item.EquipmentElement.Item, out uint itemId)) return;
                uint modifierId = 0;
                if (item.EquipmentElement.ItemModifier != null &&
                    !objectManager.TryGetHandleWithLogging(item.EquipmentElement.ItemModifier, out modifierId)) return;
                updates.Add(new NetworkItemRosterUpdate(rosterId, itemId, modifierId, item.Amount));
            }

            // Pending deltas already changed the snapshot; deliver them before replacing the roster.
            coalescer.FlushInstance(rosterId, network);
            network.Send(peer, new NetworkItemRosterClear(rosterId));
            foreach (var update in updates) network.Send(peer, update);
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ItemRosterUpdated>(Handle);
        messageBroker.Unsubscribe<ItemRosterCleared>(Handle);
        messageBroker.Unsubscribe<ResendItemRoster>(Handle);
    }
}
