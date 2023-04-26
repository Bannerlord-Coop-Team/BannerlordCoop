using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class MobilePartyMovementHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyMovementHandler>();
        private readonly IObjectManager objectManager;
        private readonly IControlledHeroRegistry controlledHeroRegistry;
        private readonly IMessageBroker messageBroker;

        public MobilePartyMovementHandler(
            IObjectManager objectManager,
            IControlledHeroRegistry controlledHeroRegistry,
            IMessageBroker messageBroker)
        {
            this.objectManager = objectManager;
            this.controlledHeroRegistry = controlledHeroRegistry;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<UpdatePartyTargetPosition>(Handle_UpdatePartyTargetPosition);
            messageBroker.Subscribe<PartyTargetPositionChanged>(Handle_PartyTargetPositionChanged);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UpdatePartyTargetPosition>(Handle_UpdatePartyTargetPosition);
            messageBroker.Unsubscribe<PartyTargetPositionChanged>(Handle_PartyTargetPositionChanged);
        }

        private void Handle_PartyTargetPositionChanged(MessagePayload<PartyTargetPositionChanged> obj)
        {
            var payload = obj.What;

            if(objectManager.TryGetId(payload.Party.LeaderHero, out string heroId))
            {
                if (controlledHeroRegistry.IsControlled(heroId))
                {
                    var message = new ControlledPartyTargetPositionUpdated(heroId, payload.NewTargetPosition);
                    messageBroker.Publish(this, message);
                }
            }
            else
            {
                Logger.Error("Unable to find HeroId for {party}", payload.Party.Name);
            }
        }

        private void Handle_UpdatePartyTargetPosition(MessagePayload<UpdatePartyTargetPosition> obj)
        {
            var targetPositionData = obj.What.TargetPositionData;
            if (controlledHeroRegistry.IsControlled(targetPositionData.ControlledHeroId))
            {
                Logger.Error("Recieved hero update on controlled hero. Incoming updates should not be controlled");
                return;
            }


            if (objectManager.TryGetObject(targetPositionData.ControlledHeroId, out Hero resolvedHero) == false)
            {
                Logger.Error("Unable to find hero for {guid}", targetPositionData.ControlledHeroId);
                return;
            }

            Vec2 vec2 = new Vec2(targetPositionData.TargetPositionX, targetPositionData.TargetPositionY);

            Logger.Debug($"Setting {resolvedHero.PartyBelongedTo.StringId} to {vec2}");

            PartyMovementPatch.SetTargetPositionOverride(resolvedHero.PartyBelongedTo, ref vec2);
        }
    }
}
