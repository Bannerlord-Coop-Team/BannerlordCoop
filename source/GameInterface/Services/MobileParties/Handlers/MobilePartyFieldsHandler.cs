using System.Collections.Generic;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Fields.Commands;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.MobileParties.Handlers;

public class MobilePartyFieldsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyFieldsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public MobilePartyFieldsHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ChangeAttachedTo>(Handle);
        messageBroker.Subscribe<ChangeHasUnpaidWages>(Handle);
        messageBroker.Subscribe<ChangeLastCalculatedSpeed>(Handle);
        messageBroker.Subscribe<ChangeDisorganizedUntilTime>(Handle);
        messageBroker.Subscribe<ChangePartyPureSpeedLastCheckVersion>(Handle);
        
        messageBroker.Subscribe<ChangePartyLastCheckIsPrisoner>(Handle);
        messageBroker.Subscribe<ChangeLastCalculatedBaseSpeedExplained>(Handle);
        messageBroker.Subscribe<ChangePartyLastCheckAtNight>(Handle);
        messageBroker.Subscribe<ChangeItemRosterVersionNo>(Handle);
        messageBroker.Subscribe<ChangePartySizeRatioLastCheckVersion>(Handle);
        
        messageBroker.Subscribe<ChangeLatestUsedPaymentRatio>(Handle);
        messageBroker.Subscribe<ChangeCachedPartySizeRatio>(Handle);
        messageBroker.Subscribe<ChangeCachedPartySizeLimit>(Handle);
        messageBroker.Subscribe<ChangeDoNotAttackMainParty>(Handle);
        messageBroker.Subscribe<ChangeCustomHomeSettlement>(Handle);
        
        messageBroker.Subscribe<ChangeIsDisorganized>(Handle);
        messageBroker.Subscribe<ChangeIsCurrentlyUsedByAQuest>(Handle);
        messageBroker.Subscribe<ChangePartyTradeGold>(Handle);
        messageBroker.Subscribe<ChangeIgnoredUntilTime>(Handle);
        messageBroker.Subscribe<ChangeAverageFleeTargetDirection>(Handle);
        
        messageBroker.Subscribe<ChangeBesiegerCampResetStarted>(Handle);
        messageBroker.Subscribe<ChangeLastWeatherTerrainEffect>(Handle);
    }

    private void Handle(MessagePayload<ChangeAttachedTo> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }
        if (objectManager.TryGetObject<MobileParty>(data.AttachedToId, out var attachedToMobileParty) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(Settlement), data.AttachedToId);
            return;
        }
        instance._attachedTo = attachedToMobileParty;
    }

    private void Handle(MessagePayload<ChangeHasUnpaidWages> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }
        
        instance.HasUnpaidWages = data.HasUnpaidWages;
    }

    private void Handle(MessagePayload<ChangeLastCalculatedSpeed> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }
        
        instance._lastCalculatedSpeed = data.LastCalculatedSpeed;
    }

    private void Handle(MessagePayload<ChangeDisorganizedUntilTime> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }
        
        instance._disorganizedUntilTime = new CampaignTime(data.DisorganizedUntilTime);
    }

    private void Handle(MessagePayload<ChangePartyPureSpeedLastCheckVersion> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }
        
        instance._partyPureSpeedLastCheckVersion = data.PartyPureSpeedLastCheckVersion;
    }

    private void Handle(MessagePayload<ChangePartyLastCheckIsPrisoner> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }
        
        instance._partyLastCheckIsPrisoner = data.PartyLastCheckIsPrisoner;
    }

    private void Handle(MessagePayload<ChangeLastCalculatedBaseSpeedExplained> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }
        
        instance._lastCalculatedBaseSpeedExplained = new ExplainedNumber(data.Number, data.IncludeDescriptions, data.TextObjectValue == null ? null : new TextObject(data.TextObjectValue, null));
    }

    private void Handle(MessagePayload<ChangePartyLastCheckAtNight> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._partyLastCheckAtNight = data.PartyLastCheckAtNight;
    }

    private void Handle(MessagePayload<ChangeItemRosterVersionNo> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._itemRosterVersionNo = data.ItemRosterVersionNo;
    }

    private void Handle(MessagePayload<ChangePartySizeRatioLastCheckVersion> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._partySizeRatioLastCheckVersion = data.PartySizeRatioLastCheckVersion;
    }

    private void Handle(MessagePayload<ChangeLatestUsedPaymentRatio> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._latestUsedPaymentRatio = data.LatestUsedPaymentRatio;
    }

    private void Handle(MessagePayload<ChangeCachedPartySizeRatio> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._cachedPartySizeRatio = data.CachedPartySizeRatio;
    }

    private void Handle(MessagePayload<ChangeCachedPartySizeLimit> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._cachedPartySizeLimit = data.CachedPartySizeLimit;
    }

    private void Handle(MessagePayload<ChangeDoNotAttackMainParty> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._doNotAttackMainParty = data.DoNotAttackMainParty;
    }

    private void Handle(MessagePayload<ChangeCustomHomeSettlement> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }
        
        if (objectManager.TryGetObject<Settlement>(data.CustomHomeSettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(Settlement), data.CustomHomeSettlementId);
            return;
        }

        instance._customHomeSettlement = settlement;
    }

    private void Handle(MessagePayload<ChangeIsDisorganized> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._isDisorganized = data.IsDisorganized;
    }

    private void Handle(MessagePayload<ChangeIsCurrentlyUsedByAQuest> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._isCurrentlyUsedByAQuest = data.IsCurrentlyUsedByAQuest;
    }

    private void Handle(MessagePayload<ChangePartyTradeGold> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._partyTradeGold = data.PartyTradeGold;
    }

    private void Handle(MessagePayload<ChangeIgnoredUntilTime> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._ignoredUntilTime = new CampaignTime(data.IgnoredUntilTime);
    }

    private void Handle(MessagePayload<ChangeAverageFleeTargetDirection> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance.AverageFleeTargetDirection = new Vec2(data.VecX, data.VecY);
    }

    private void Handle(MessagePayload<ChangeBesiegerCampResetStarted> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._besiegerCampResetStarted = data.BesiegerCampResetStarted;
    }

    private void Handle(MessagePayload<ChangeLastWeatherTerrainEffect> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.MobilePartyId);
            return;
        }

        instance._lastWeatherTerrainEffect = (MapWeatherModel.WeatherEventEffectOnTerrain) data.LastWeatherTerrainEffect;
    }
    
    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeAttachedTo>(Handle);
        messageBroker.Unsubscribe<ChangeHasUnpaidWages>(Handle);
        messageBroker.Unsubscribe<ChangeLastCalculatedSpeed>(Handle);
        messageBroker.Unsubscribe<ChangeDisorganizedUntilTime>(Handle);
        messageBroker.Unsubscribe<ChangePartyPureSpeedLastCheckVersion>(Handle);
        
        messageBroker.Unsubscribe<ChangePartyLastCheckIsPrisoner>(Handle);
        messageBroker.Unsubscribe<ChangeLastCalculatedBaseSpeedExplained>(Handle);
        messageBroker.Unsubscribe<ChangePartyLastCheckAtNight>(Handle);
        messageBroker.Unsubscribe<ChangeItemRosterVersionNo>(Handle);
        messageBroker.Unsubscribe<ChangePartySizeRatioLastCheckVersion>(Handle);
        
        messageBroker.Unsubscribe<ChangeLatestUsedPaymentRatio>(Handle);
        messageBroker.Unsubscribe<ChangeCachedPartySizeRatio>(Handle);
        messageBroker.Unsubscribe<ChangeCachedPartySizeLimit>(Handle);
        messageBroker.Unsubscribe<ChangeDoNotAttackMainParty>(Handle);
        messageBroker.Unsubscribe<ChangeCustomHomeSettlement>(Handle);
        
        messageBroker.Unsubscribe<ChangeIsDisorganized>(Handle);
        messageBroker.Unsubscribe<ChangeIsCurrentlyUsedByAQuest>(Handle);
        messageBroker.Unsubscribe<ChangePartyTradeGold>(Handle);
        messageBroker.Unsubscribe<ChangeIgnoredUntilTime>(Handle);
        messageBroker.Unsubscribe<ChangeAverageFleeTargetDirection>(Handle);
        
        messageBroker.Unsubscribe<ChangeBesiegerCampResetStarted>(Handle);
        messageBroker.Unsubscribe<ChangeLastWeatherTerrainEffect>(Handle);
    }
}