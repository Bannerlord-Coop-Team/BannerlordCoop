using Common.Logging;
using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class MobilePartyBehaviorHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IControlledEntityRegistery controlledEntityRegistery;
        private readonly IObjectManager objectManager;

        public MobilePartyBehaviorHandler(
            IMessageBroker messageBroker, 
            IControlledEntityRegistery controlledEntityRegistery,
            IObjectManager objectManager) 
        {
            this.messageBroker = messageBroker;
            this.controlledEntityRegistery = controlledEntityRegistery;
            this.objectManager = objectManager;

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
            IMapEntity targetMapEntity = null;

            if (!objectManager.TryGetObject(data.PartyId, out MobileParty party) ||
                (data.HasTarget && !objectManager.TryGetObject(data.TargetId, out targetMapEntity)))
            {
                return;
            }

            Vec2 targetPoint = new Vec2(data.TargetPointX, data.TargetPointY);

            PartyBehaviorPatch.SetAiBehavior(
                party.Ai,
                data.Behavior,
                targetMapEntity,
                targetPoint
            );
        }
    }
}
