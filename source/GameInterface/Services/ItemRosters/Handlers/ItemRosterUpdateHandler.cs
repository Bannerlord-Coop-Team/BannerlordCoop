﻿using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Serilog;
using Common.Logging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Services.ItemRosters.Handlers
{
    /// <summary>
    /// Handles ItemRosterUpdate.
    /// </summary>
    internal class ItemRosterUpdateHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ItemRosterUpdateHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly ILogger logger;
        private readonly IObjectManager objectManager;

        public ItemRosterUpdateHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<ItemRosterUpdate>(Handle);
        }

        public void Handle(MessagePayload<ItemRosterUpdate> payload)
        {
            var msg = payload.What;
            if (objectManager.TryGetObject(msg.ItemID, out ItemObject item) == false)
            {
                Logger.Error("Unable to find item with id: {itemId}", msg.ItemID);
                return;
            }

            
            ItemModifier modifier = null;
            if (msg.ItemModifierID != null)
            {
                if(objectManager.TryGetObject(msg.ItemModifierID, out modifier) == false)
                {
                    Logger.Error("Failed to update item roster, ItemModifier '{itemModifierId}' not found", msg.ItemModifierID);
                    return;
                }
            }

            ItemRoster roster;
            if (objectManager.TryGetObject(msg.PartyBaseID, out Settlement s))
            {
                roster = s.ItemRoster;
            }
            else if (objectManager.TryGetObject(msg.PartyBaseID, out MobileParty p))
            {
                roster = p.ItemRoster;
            }
            else
            {
                Logger.Error("Failed to update item roster, no Settlement nor Party with ID '{partyBaseId}' was found", msg.PartyBaseID);
                return;
            }

            ItemRosterPatch.AddToCountsOverride(roster, new EquipmentElement(item, modifier), msg.Amount);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemRosterUpdate>(Handle);
        }
    }
}
