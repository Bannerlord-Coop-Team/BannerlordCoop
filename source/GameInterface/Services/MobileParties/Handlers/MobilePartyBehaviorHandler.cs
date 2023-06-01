using Common.Logging;
using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class MobilePartyBehaviorHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyBehaviorHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IControlledEntityRegistery controlledEntityRegistery;

        public MobilePartyBehaviorHandler(
            IMessageBroker messageBroker, 
            IControlledEntityRegistery controlledEntityRegistery) 
        {
            this.messageBroker = messageBroker;
            this.controlledEntityRegistery = controlledEntityRegistery;

            messageBroker.Subscribe<RequestTickInternal>(Handle_RequestTickInternal);
            messageBroker.Subscribe<PartyAiBehaviorChanged>(Handle_PartyAiBehaviorChanged);
            messageBroker.Subscribe<UpdatePartyAiBehavior>(Handle_UpdatePartyAiBehavior);
        }

        public void Dispose()
        {
            messageBroker.Subscribe<RequestTickInternal>(Handle_RequestTickInternal);
            messageBroker.Unsubscribe<PartyAiBehaviorChanged>(Handle_PartyAiBehaviorChanged);
            messageBroker.Unsubscribe<UpdatePartyAiBehavior>(Handle_UpdatePartyAiBehavior);
        }

        private void Handle_RequestTickInternal(MessagePayload<RequestTickInternal> obj)
        {

            MobilePartyAi partyAi = obj.What.PartyAi;
            if (ModInformation.IsServer && partyAi.GetMobileParty().StringId == "TransferredParty") return;

            if (!controlledEntityRegistery.IsOwned(partyAi.GetMobileParty().StringId))
            {
                return;
            }

            DisablePartyDecisionMaking.TickInternalOverride(partyAi);
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
            var data = obj.What.BehaviorUpdateData;
            //Logger.Debug($"Setting {data.PartyId} behavior to {data.Behavior}");
            PartyBehaviorPatch.SetAiBehavior(data);
        }
    }
}
