using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using Coop.Core.Client.Services.Inventory.Messages;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Inventory.Handlers
{
    internal class ItemRosterHandler
    {
        private readonly IMessageBroker messageBroker;

        public ItemRosterHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<ItemRosterUpdated>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemRosterUpdated>(Handle);
        }

        private void Handle(MessagePayload<ItemRosterUpdated> obj)
        {
            var payload = obj.What;


            

        }
    }
}
