using Common.Messaging;
using Coop.Core.Client.Services.MobileParties.Messages.Fields;
using GameInterface.Services.MobileParties.Messages.Fields;
using GameInterface.Services.MobileParties.Messages.Fields.Commands;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

public class ClientMobilePartyFieldsHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

        public ClientMobilePartyFieldsHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
            messageBroker.Subscribe<NetworkAttachedToChanged>(Handle);
            messageBroker.Subscribe<NetworkHasUnpaidWagesChanged>(Handle);
            messageBroker.Subscribe<NetworkLastCalculatedSpeedChanged>(Handle);
            messageBroker.Subscribe<NetworkDisorganizedUntilTimeChanged>(Handle);
            messageBroker.Subscribe<NetworkPartyPureSpeedLastCheckVersionChanged>(Handle);
            
            messageBroker.Subscribe<NetworkPartyLastCheckIsPrisonerChanged>(Handle);
            messageBroker.Subscribe<NetworkLastCalculatedBaseSpeedExplainedChanged>(Handle);
            messageBroker.Subscribe<NetworkPartyLastCheckAtNightChanged>(Handle);
            messageBroker.Subscribe<NetworkItemRosterVersionNoChanged>(Handle);
            messageBroker.Subscribe<NetworkPartySizeRatioLastCheckVersionChanged>(Handle);
            
            messageBroker.Subscribe<NetworkLatestUsedPaymentRatioChanged>(Handle);
            messageBroker.Subscribe<NetworkCachedPartySizeRatioChanged>(Handle);
            messageBroker.Subscribe<NetworkCachedPartySizeLimitChanged>(Handle);
            messageBroker.Subscribe<NetworkDoNotAttackMainPartyChanged>(Handle);
            messageBroker.Subscribe<NetworkCustomHomeSettlementChanged>(Handle);
            
            messageBroker.Subscribe<NetworkIsDisorganizedChanged>(Handle);
            messageBroker.Subscribe<NetworkIsCurrentlyUsedByAQuestChanged>(Handle);
            messageBroker.Subscribe<NetworkPartyTradeGoldChanged>(Handle);
            messageBroker.Subscribe<NetworkIgnoredUntilTimeChanged>(Handle);
            messageBroker.Subscribe<NetworkAverageFleeTargetDirectionChanged>(Handle);
            
            messageBroker.Subscribe<NetworkBesiegerCampResetStartedChanged>(Handle);
            messageBroker.Subscribe<NetworkLastWeatherTerrainEffectChanged>(Handle);
        }

        private void Handle(MessagePayload<NetworkAttachedToChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeAttachedTo(data.AttachedToId, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkHasUnpaidWagesChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeHasUnpaidWages(data.HasUnpaidWages, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkLastCalculatedSpeedChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeLastCalculatedSpeed(data.LastCalculatedSpeed, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkDisorganizedUntilTimeChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeDisorganizedUntilTime(data.DisorganizedUntilTime, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkPartyPureSpeedLastCheckVersionChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangePartyPureSpeedLastCheckVersion(data.PartyPureSpeedLastCheckVersion, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkPartyLastCheckIsPrisonerChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangePartyLastCheckIsPrisoner(data.PartyLastCheckIsPrisoner, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkLastCalculatedBaseSpeedExplainedChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeLastCalculatedBaseSpeedExplained(data.Number, data.IncludeDescriptions, data.TextObjectValue, data.MobilePartyId));
        }
        
        private void Handle(MessagePayload<NetworkPartyLastCheckAtNightChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangePartyLastCheckAtNight(data.PartyLastCheckAtNight, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkItemRosterVersionNoChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeItemRosterVersionNo(data.ItemRosterVersionNo, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkPartySizeRatioLastCheckVersionChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangePartySizeRatioLastCheckVersion(data.PartySizeRatioLastCheckVersion, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkLatestUsedPaymentRatioChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeLatestUsedPaymentRatio(data.LatestUsedPaymentRatio, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkCachedPartySizeRatioChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeCachedPartySizeRatio(data.CachedPartySizeRatio, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkCachedPartySizeLimitChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeCachedPartySizeLimit(data.CachedPartySizeLimit, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkDoNotAttackMainPartyChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeDoNotAttackMainParty(data.DoNotAttackMainParty, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkCustomHomeSettlementChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeCustomHomeSettlement(data.CustomHomeSettlementId, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkIsDisorganizedChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeIsDisorganized(data.IsDisorganized, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkIsCurrentlyUsedByAQuestChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeIsCurrentlyUsedByAQuest(data.IsCurrentlyUsedByAQuest, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkPartyTradeGoldChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangePartyTradeGold(data.PartyTradeGold, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkIgnoredUntilTimeChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeIgnoredUntilTime(data.IgnoredUntilTime, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkAverageFleeTargetDirectionChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeAverageFleeTargetDirection(data.VecX, data.VecY, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkBesiegerCampResetStartedChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeBesiegerCampResetStarted(data.BesiegerCampResetStarted, data.MobilePartyId));
        }

        private void Handle(MessagePayload<NetworkLastWeatherTerrainEffectChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeLastWeatherTerrainEffect(data.LastWeatherTerrainEffect, data.MobilePartyId));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkAttachedToChanged>(Handle);
            messageBroker.Unsubscribe<NetworkHasUnpaidWagesChanged>(Handle);
            messageBroker.Unsubscribe<NetworkLastCalculatedSpeedChanged>(Handle);
            messageBroker.Unsubscribe<NetworkDisorganizedUntilTimeChanged>(Handle);
            messageBroker.Unsubscribe<NetworkPartyPureSpeedLastCheckVersionChanged>(Handle);
            
            messageBroker.Unsubscribe<NetworkPartyLastCheckIsPrisonerChanged>(Handle);
            messageBroker.Unsubscribe<NetworkLastCalculatedBaseSpeedExplainedChanged>(Handle);
            messageBroker.Unsubscribe<NetworkPartyLastCheckAtNightChanged>(Handle);
            messageBroker.Unsubscribe<NetworkItemRosterVersionNoChanged>(Handle);
            messageBroker.Unsubscribe<NetworkPartySizeRatioLastCheckVersionChanged>(Handle);
            
            messageBroker.Unsubscribe<NetworkLatestUsedPaymentRatioChanged>(Handle);
            messageBroker.Unsubscribe<NetworkCachedPartySizeRatioChanged>(Handle);
            messageBroker.Unsubscribe<NetworkCachedPartySizeLimitChanged>(Handle);
            messageBroker.Unsubscribe<NetworkDoNotAttackMainPartyChanged>(Handle);
            messageBroker.Unsubscribe<NetworkCustomHomeSettlementChanged>(Handle);
            
            messageBroker.Unsubscribe<NetworkIsDisorganizedChanged>(Handle);
            messageBroker.Unsubscribe<NetworkIsCurrentlyUsedByAQuestChanged>(Handle);
            messageBroker.Unsubscribe<NetworkPartyTradeGoldChanged>(Handle);
            messageBroker.Unsubscribe<NetworkIgnoredUntilTimeChanged>(Handle);
            messageBroker.Unsubscribe<NetworkAverageFleeTargetDirectionChanged>(Handle);
            
            messageBroker.Unsubscribe<NetworkBesiegerCampResetStartedChanged>(Handle);
            messageBroker.Unsubscribe<NetworkLastWeatherTerrainEffectChanged>(Handle);
        }
}