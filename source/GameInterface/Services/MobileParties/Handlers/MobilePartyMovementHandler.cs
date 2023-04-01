using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes;
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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class MobilePartyMovementHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyMovementHandler>();
        private readonly IHeroRegistry heroRegistry;
        private readonly IControlledHeroRegistry controlledHeroRegistry;
        private readonly IMobilePartyInterface partyInterface;
        private readonly IMessageBroker messageBroker;

        public MobilePartyMovementHandler(
            IHeroRegistry heroRegistry,
            IControlledHeroRegistry controlledHeroRegistry,
            IMobilePartyInterface partyInterface,
            IMessageBroker messageBroker)
        {
            this.heroRegistry = heroRegistry;
            this.controlledHeroRegistry = controlledHeroRegistry;
            this.partyInterface = partyInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<UpdatePartyTargetPosition>(Handle);
            messageBroker.Subscribe<PartyTargetPositionChanged>(Handle_PartyTargetPositionChanged);
        }

        private void Handle_PartyTargetPositionChanged(MessagePayload<PartyTargetPositionChanged> obj)
        {
            var payload = obj.What;

            if(heroRegistry.TryGetValue(payload.Party.Owner, out Guid heroId))
            {
                if (controlledHeroRegistry.IsControlled(heroId))
                {
                    var message = new ControlledPartyTargetPositionUpdated(heroId, payload.NewTargetPosition);
                    messageBroker.Publish(this, message);
                }
            }
            else
            {
                Logger.Error("Unable to find HeroId for {hero}", payload.Party.Owner.Name);
            }
        }

        private void Handle(MessagePayload<UpdatePartyTargetPosition> obj)
        {
            var targetPositionData = obj.What.TargetPositionData;
            if (controlledHeroRegistry.IsControlled(targetPositionData.ControlledHeroId))
            {
                Logger.Error("Recieved hero update on controlled hero. Incoming updates should not be controlled");
                return;
            }


            if (heroRegistry.TryGetValue(targetPositionData.ControlledHeroId, out Hero resolvedHero) == false)
            {
                Logger.Error("Unable to find controlled hero for {guid}", targetPositionData.ControlledHeroId);
                return;
            }

            Vec2 vec2 = new Vec2(targetPositionData.TargetPositionX, targetPositionData.TargetPositionY);

            Logger.Debug($"Setting {resolvedHero.PartyBelongedTo.StringId} to {vec2}");

            PartyMovementPatch.SetTargetPositionOverride(resolvedHero.PartyBelongedTo, ref vec2);
        }
    }
}
