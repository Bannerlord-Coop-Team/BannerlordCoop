using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class MobilePartyBehaviorHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IControlledEntityRegistery controlledEntityRegistery;

        public MobilePartyBehaviorHandler(
            IMessageBroker messageBroker, 
            IControlledEntityRegistery controlledEntityRegistery) 
        {
            this.messageBroker = messageBroker;
            this.controlledEntityRegistery = controlledEntityRegistery;

            messageBroker.Subscribe<PartyAiBehaviorChanged>(Handle_PartyAiBehaviorChanged);
            messageBroker.Subscribe<UpdatePartyAiBehavior>(Handle_UpdatePartyAiBehavior);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PartyAiBehaviorChanged>(Handle_PartyAiBehaviorChanged);
            messageBroker.Unsubscribe<UpdatePartyAiBehavior>(Handle_UpdatePartyAiBehavior);
        }

        public void Handle_PartyAiBehaviorChanged(MessagePayload<PartyAiBehaviorChanged> obj)
        {
            MobileParty party = obj.What.Party;

            if (controlledEntityRegistery.IsOwned(party.StringId) == false)
                return;

            AiBehaviorUpdateData data = obj.What.BehaviorUpdateData;

            messageBroker.Publish(this, new ControlledPartyAiBehaviorUpdated(data));
        }

        public void Handle_UpdatePartyAiBehavior(MessagePayload<UpdatePartyAiBehavior> obj)
        {
            PartyBehaviorPatch.SetAiBehavior(obj.What.BehaviorUpdateData);
        }
    }
}
