using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Serilog;
using Common.Logging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Services.ItemRosters.Handlers;

/// <summary>
/// Handles UpdateItemRoster.
/// </summary>
internal class UpdateItemRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<UpdateItemRosterHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public UpdateItemRosterHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<UpdateItemRoster>(Handle);
    }

    public void Handle(MessagePayload<UpdateItemRoster> payload)
    {
        var msg = payload.What;
        if (objectManager.TryGetObject(msg.ItemId, out ItemObject item) == false)
        {
            Logger.Error("Unable to find item with id: {itemId}", msg.ItemId);
            return;
        }

        
        ItemModifier modifier = null;
        if(msg.ItemModifierId != null && objectManager.TryGetObject(msg.ItemModifierId, out modifier) == false)
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

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateItemRoster>(Handle);
    }
}
