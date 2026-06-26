using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ItemObjects;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Handlers;

/// <summary>
/// Handles UpdateItemRoster.
/// </summary>
internal class UpdateItemRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<UpdateItemRosterHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ItemObjectRegistry itemObjectRegistry;

    public UpdateItemRosterHandler(IMessageBroker messageBroker, IObjectManager objectManager, ItemObjectRegistry itemObjectRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.itemObjectRegistry = itemObjectRegistry;

        messageBroker.Subscribe<UpdateItemRoster>(Handle);
    }

    public void Handle(MessagePayload<UpdateItemRoster> payload)
    {
        var msg = payload.What;

        // AddToCountsOverride already defers its apply to the game loop, but the id
        // resolution below ran on the receive thread, so it could miss an item or roster
        // whose create was deferred and has not drained yet. Resolving inside the game
        // loop keeps this lookup ordered behind that create. The override's own
        // GameThread.Run then runs inline because we are already on the game-loop thread.
        GameThread.Run(() =>
        {
            try
            {
                if (TryGetItem(msg.ItemId, out ItemObject item) == false)
                {
                    Logger.Error("Unable to find item with id: {itemId}", msg.ItemId);
                    return;
                }

                ItemModifier modifier = null;
                if (msg.ItemModifierId != null && objectManager.TryGetObject(msg.ItemModifierId, out modifier) == false)
                {
                    Logger.Error("Failed to update item roster, ItemModifier '{itemModifierId}' not found", msg.ItemModifierId);
                    return;
                }

                if (!objectManager.TryGetObjectWithLogging<ItemRoster>(msg.ItemRosterId, out var itemRoster))
                {
                    return;
                }

                ItemRosterPatch.AddToCountsOverride(itemRoster, new EquipmentElement(item, modifier), msg.Amount);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(UpdateItemRoster));
            }
        });
    }

    private bool TryGetItem(string itemId, out ItemObject item)
    {
        if (objectManager.TryGetObject(itemId, out item))
            return true;

        return itemObjectRegistry.TryGetRegisteredItem(itemId, out item);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateItemRoster>(Handle);
    }
}
