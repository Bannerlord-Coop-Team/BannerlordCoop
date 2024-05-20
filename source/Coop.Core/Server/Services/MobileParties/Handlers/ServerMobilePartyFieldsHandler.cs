using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages.Fields;
using GameInterface.Services.MobileParties.Messages.Fields.Events;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Server handler for all fields of the MobileParty class
/// </summary>
public class ServerMobilePartyFieldsHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerMobilePartyFieldsHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        
        messageBroker.Subscribe<AttachedToChanged>(Handle);
        messageBroker.Subscribe<HasUnpaidWagesChanged>(Handle);
        messageBroker.Subscribe<LastCalculatedSpeedChanged>(Handle);
        messageBroker.Subscribe<DisorganizedUntilTimeChanged>(Handle);
        messageBroker.Subscribe<PartyPureSpeedLastCheckVersionChanged>(Handle);
        
        messageBroker.Subscribe<PartyLastCheckIsPrisonerChanged>(Handle);
        messageBroker.Subscribe<LastCalculatedBaseSpeedExplainedChanged>(Handle);
        messageBroker.Subscribe<PartyLastCheckAtNightChanged>(Handle);
        messageBroker.Subscribe<ItemRosterVersionNoChanged>(Handle);
        messageBroker.Subscribe<PartySizeRatioLastCheckVersionChanged>(Handle);
        
        messageBroker.Subscribe<LatestUsedPaymentRatioChanged>(Handle);
        messageBroker.Subscribe<CachedPartySizeRatioChanged>(Handle);
        messageBroker.Subscribe<CachedPartySizeLimitChanged>(Handle);
        messageBroker.Subscribe<DoNotAttackMainPartyChanged>(Handle);
        messageBroker.Subscribe<CustomHomeSettlementChanged>(Handle);
        
        messageBroker.Subscribe<IsDisorganizedChanged>(Handle);
        messageBroker.Subscribe<IsCurrentlyUsedByAQuestChanged>(Handle);
        messageBroker.Subscribe<PartyTradeGoldChanged>(Handle);
        messageBroker.Subscribe<IgnoredUntilTimeChanged>(Handle);
        messageBroker.Subscribe<AverageFleeTargetDirectionChanged>(Handle);
        
        messageBroker.Subscribe<BesiegerCampResetStartedChanged>(Handle);
        messageBroker.Subscribe<LastWeatherTerrainEffectChanged>(Handle);
        messageBroker.Subscribe<PartyComponentChanged>(Handle);
    }
    
    private void Handle(MessagePayload<AttachedToChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkAttachedToChanged(data.AttachedToId, data.MobilePartyId));
    }
    
    private void Handle(MessagePayload<HasUnpaidWagesChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkHasUnpaidWagesChanged(data.HasUnpaidWages, data.MobilePartyId));
    }
    
    private void Handle(MessagePayload<LastCalculatedSpeedChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkLastCalculatedSpeedChanged(data.LastCalculatedSpeed, data.MobilePartyId));
    }
    
    private void Handle(MessagePayload<DisorganizedUntilTimeChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkDisorganizedUntilTimeChanged(data.DisorganizedUntilTime, data.MobilePartyId));
    }
    
    private void Handle(MessagePayload<PartyPureSpeedLastCheckVersionChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkPartyPureSpeedLastCheckVersionChanged(data.PartyPureSpeedLastCheckVersion, data.MobilePartyId));
    }
    
    private void Handle(MessagePayload<PartyLastCheckIsPrisonerChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkPartyLastCheckIsPrisonerChanged(data.PartyLastCheckIsPrisoner, data.MobilePartyId));
    }
    
    private void Handle(MessagePayload<LastCalculatedBaseSpeedExplainedChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkLastCalculatedBaseSpeedExplainedChanged(data.Number, data.IncludeDescriptions, data.TextObjectValue, data.MobilePartyId));
    }
    
    private void Handle(MessagePayload<PartyLastCheckAtNightChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkPartyLastCheckAtNightChanged(data.PartyLastCheckAtNight, data.MobilePartyId));
    }

    private void Handle(MessagePayload<ItemRosterVersionNoChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkItemRosterVersionNoChanged(data.ItemRosterVersionNo, data.MobilePartyId));
    }

    private void Handle(MessagePayload<PartySizeRatioLastCheckVersionChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkPartySizeRatioLastCheckVersionChanged(data.PartySizeRatioLastCheckVersion, data.MobilePartyId));
    }

    private void Handle(MessagePayload<LatestUsedPaymentRatioChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkLatestUsedPaymentRatioChanged(data.LatestUsedPaymentRatio, data.MobilePartyId));
    }

    private void Handle(MessagePayload<CachedPartySizeRatioChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkCachedPartySizeRatioChanged(data.CachedPartySizeRatio, data.MobilePartyId));
    }

    private void Handle(MessagePayload<CachedPartySizeLimitChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkCachedPartySizeLimitChanged(data.CachedPartySizeLimit, data.MobilePartyId));
    }

    private void Handle(MessagePayload<DoNotAttackMainPartyChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkDoNotAttackMainPartyChanged(data.DoNotAttackMainParty, data.MobilePartyId));
    }

    private void Handle(MessagePayload<CustomHomeSettlementChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkCustomHomeSettlementChanged(data.CustomHomeSettlementId, data.MobilePartyId));
    }

    private void Handle(MessagePayload<IsDisorganizedChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkIsDisorganizedChanged(data.IsDisorganized, data.MobilePartyId));
    }

    private void Handle(MessagePayload<IsCurrentlyUsedByAQuestChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkIsCurrentlyUsedByAQuestChanged(data.IsCurrentlyUsedByAQuest, data.MobilePartyId));
    }

    private void Handle(MessagePayload<PartyTradeGoldChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkPartyTradeGoldChanged(data.PartyTradeGold, data.MobilePartyId));
    }

    private void Handle(MessagePayload<IgnoredUntilTimeChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkIgnoredUntilTimeChanged(data.IgnoredUntilTime, data.MobilePartyId));
    }

    private void Handle(MessagePayload<AverageFleeTargetDirectionChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkAverageFleeTargetDirectionChanged(data.VecX, data.VecY, data.MobilePartyId));
    }

    private void Handle(MessagePayload<BesiegerCampResetStartedChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkBesiegerCampResetStartedChanged(data.BesiegerCampResetStarted, data.MobilePartyId));
    }

    private void Handle(MessagePayload<LastWeatherTerrainEffectChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkLastWeatherTerrainEffectChanged(data.LastWeatherTerrainEffect, data.MobilePartyId));
    }

    private void Handle(MessagePayload<PartyComponentChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkPartyComponentChanged(data.PartyComponentId, data.MobilePartyId));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AttachedToChanged>(Handle);
        messageBroker.Unsubscribe<HasUnpaidWagesChanged>(Handle);
        messageBroker.Unsubscribe<LastCalculatedSpeedChanged>(Handle);
        messageBroker.Unsubscribe<DisorganizedUntilTimeChanged>(Handle);
        messageBroker.Unsubscribe<PartyPureSpeedLastCheckVersionChanged>(Handle);
        
        messageBroker.Unsubscribe<PartyLastCheckIsPrisonerChanged>(Handle);
        messageBroker.Unsubscribe<LastCalculatedBaseSpeedExplainedChanged>(Handle);
        messageBroker.Unsubscribe<PartyLastCheckAtNightChanged>(Handle);
        messageBroker.Unsubscribe<ItemRosterVersionNoChanged>(Handle);
        messageBroker.Unsubscribe<PartySizeRatioLastCheckVersionChanged>(Handle);
        
        messageBroker.Unsubscribe<LatestUsedPaymentRatioChanged>(Handle);
        messageBroker.Unsubscribe<CachedPartySizeRatioChanged>(Handle);
        messageBroker.Unsubscribe<CachedPartySizeLimitChanged>(Handle);
        messageBroker.Unsubscribe<DoNotAttackMainPartyChanged>(Handle);
        messageBroker.Unsubscribe<CustomHomeSettlementChanged>(Handle);
        
        messageBroker.Unsubscribe<IsDisorganizedChanged>(Handle);
        messageBroker.Unsubscribe<IsCurrentlyUsedByAQuestChanged>(Handle);
        messageBroker.Unsubscribe<PartyTradeGoldChanged>(Handle);
        messageBroker.Unsubscribe<IgnoredUntilTimeChanged>(Handle);
        messageBroker.Unsubscribe<AverageFleeTargetDirectionChanged>(Handle);
        
        messageBroker.Unsubscribe<BesiegerCampResetStartedChanged>(Handle);
        messageBroker.Unsubscribe<LastWeatherTerrainEffectChanged>(Handle);
        messageBroker.Unsubscribe<PartyComponentChanged>(Handle);
    }
}