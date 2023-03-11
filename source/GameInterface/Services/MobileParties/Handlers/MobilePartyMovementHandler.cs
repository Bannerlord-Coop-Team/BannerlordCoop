using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class MobilePartyMovementHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyMovementHandler>();

        private readonly IMobilePartyInterface partyInterface;
        private readonly IMessageBroker messageBroker;

        public MobilePartyMovementHandler(
            IMobilePartyInterface partyInterface,
            IMessageBroker messageBroker)
        {
            this.partyInterface = partyInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<UpdatePartyTargetPosition>(Handle);
        }

        private void Handle(MessagePayload<UpdatePartyTargetPosition> obj)
        {
            var targetPositionData = obj.What.TargetPositionData;
            Hero resolvedHero = MBObjectManager.Instance.GetObject<Hero>(targetPositionData.ControlledHeroStringId);
            
            if (resolvedHero != null)
            {
                Vec2 vec2 = new Vec2(targetPositionData.TargetPositionX, targetPositionData.TargetPositionY);

                Logger.Debug($"Setting {resolvedHero.PartyBelongedTo.StringId} to {vec2}");

                PartyMovementPatch.SetTargetPositionOverride(resolvedHero.PartyBelongedTo, ref vec2);
            }
        }
    }
}
