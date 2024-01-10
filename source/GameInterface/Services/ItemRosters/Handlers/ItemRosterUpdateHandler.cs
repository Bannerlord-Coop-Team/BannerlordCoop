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

namespace GameInterface.Services.ItemRosters.Handlers
{
    /// <summary>
    /// Handles ItemRosterUpdated on client.
    /// </summary>
    internal class ItemRosterUpdateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly ILogger logger;
        private readonly IObjectManager objectManager;

        public ItemRosterUpdateHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            logger = LogManager.GetLogger<ItemRosterUpdateHandler>();

            messageBroker.Subscribe<ItemRosterUpdate>(Handle);
        }

        public void Handle(MessagePayload<ItemRosterUpdate> payload)
        {
            if (ModInformation.IsServer)
                return;

            ItemRoster roster = null;

            if (objectManager.TryGetObject(payload.What.ItemID, out ItemObject item))
            {
                ItemModifier modifier = null;
                if (payload.What.ItemModifierID != null)
                {
                    if(!objectManager.TryGetObject(payload.What.ItemModifierID, out modifier))
                    {
                        logger.Error("Failed to update item roster, ItemModifier '{0}' not found", payload.What.ItemModifierID);
                    }
                }

                if (objectManager.TryGetObject(payload.What.PartyBaseID, out Settlement s))
                {
                    roster = s.ItemRoster;
                }
                else if (objectManager.TryGetObject(payload.What.PartyBaseID, out MobileParty p))
                {
                    roster = p.ItemRoster;
                }
                else
                {
                    logger.Error("Failed to update item roster, no Settlement nor Party with ID '{0}' was found", payload.What.PartyBaseID);
                }

                ItemRosterPatch.AddToCountsOverride(roster, new EquipmentElement(item, modifier), payload.What.Amount);
            }
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemRosterUpdate>(Handle);
        }
    }
}
