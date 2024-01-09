using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages.Events;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using Serilog;
using Common.Logging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Common;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Services.ItemRosters.Patches;

namespace GameInterface.Services.ItemRosters.Handlers.Events
{
    /// <summary>
    /// Handles ItemRosterUpdated on client.
    /// </summary>
    internal class ItemRosterUpdateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly ILogger logger;

        public ItemRosterUpdateHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;

            logger = LogManager.GetLogger<ItemRosterUpdateHandler>();

            messageBroker.Subscribe<ItemRosterUpdate>(Handle);
        }

        public void Handle(MessagePayload<ItemRosterUpdate> payload)
        {
            if (ModInformation.IsServer)
                return;

            GameLoopRunner.RunOnMainThread(() =>
            {
                ItemObject item;
                ItemModifier modifier = null;
                ItemRoster roster = null;

                if ((item = MBObjectManager.Instance.GetObject<ItemObject>(payload.What.ItemID)) == null) {
                    logger.Error("Failed to update item roster, ItemObject '{0}' not found", payload.What.ItemID);
                    return;
                }

                if (payload.What.ItemModifierID != null &&
                    (modifier = MBObjectManager.Instance.GetObject<ItemModifier>(payload.What.ItemModifierID)) == null)
                {
                    logger.Error("Failed to update item roster, ItemModifier '{0}' not found", payload.What.ItemModifierID);
                    return;
                }

                if (MBObjectManager.Instance.ContainsObject<Settlement>(payload.What.PartyBaseID))
                {
                    roster = MBObjectManager.Instance.GetObject<Settlement>(payload.What.PartyBaseID).ItemRoster;
                }

                MobileParty party = Campaign.Current.CampaignObjectManager.Find<MobileParty>(payload.What.PartyBaseID);
                if (party != null)
                {
                    roster = party.ItemRoster;
                }
                
                if (roster == null)
                {
                    logger.Error("Failed to update item roster, no Settlement nor Party with ID '{0}' was found", payload.What.PartyBaseID);
                } else
                {
                    ItemRosterPatch.AddToCountsOverride(roster, new EquipmentElement(item, modifier), payload.What.Amount);
                }
            });
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemRosterUpdate>(Handle);
        }
    }
}
