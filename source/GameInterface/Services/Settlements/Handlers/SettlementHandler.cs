using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;

namespace GameInterface.Services.Settlements.Handlers;

/// <summary>
/// Handles Syncs for Settlement Patches
/// </summary>
public class SettlementHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SettlementHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ChangeSettlementBribePaid>(HandleBribePaid);
        messageBroker.Subscribe<ChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Subscribe<ChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Subscribe<ChangeSettlementLastAttackerParty>(HandleLastAttackerParty);
        messageBroker.Subscribe<ChangeSettlementLastThreatTime>(HandleLastThreatTime);
        messageBroker.Subscribe<ChangeSettlementCurrentSiegeState>(HandleCurrentSiegeState);
        messageBroker.Subscribe<ChangeSettlementMilitia>(HandleMilitia);
        messageBroker.Subscribe<ChangeSettlementGarrisonWagePaymentLimit>(HandleGarrisonWageLimit);
        messageBroker.Subscribe<ChangeSettlementNotablesCache>(HandleCollectNotablesToCache);
        messageBroker.Subscribe<ChangeSettlementHeroWithoutParty>(HandleHeroWithoutParty);
        messageBroker.Subscribe<ChangeSettlementHeroWithoutPartyRemove>(HandleHeroRemoveWithoutParty);
        messageBroker.Subscribe<ChangeMobileParty>(HandleMobileParty);
        messageBroker.Subscribe<ChangeSettlementWallHitPointsRatio>(HandleHitPointsRatio);

        messageBroker.Subscribe<ChangeSettlementLastVisitTimeOfOwner>(HandleLastVisitTimeOfOwner);

        messageBroker.Subscribe<ChangeLordConversationCampaignBehaviorPlayerClaim>(HandleLordConversationCampaignBehaviorPlayerClaim);
        //other clients ChangeLordConversationCampaignBehaviorPlayerClaimOthers
        messageBroker.Subscribe<ChangeLordConversationCampaignBehaviorPlayerClaimOthers>(HandleLordConversationCampaignBehaviorPlayerClaimOthers);

        //server claimvalue behave
        messageBroker.Subscribe<ChangeLordConversationCampaignBehaviourPlayerClaimValue>(HandleLordConversationCampaignBehaviorPlayerClaimValue);
        //other clients
        messageBroker.Subscribe<ChangeLordConversationCampaignBehaviorPlayerClaimValueOthers>(HandleLordConversationCampaignBehaviorPlayerClaimValueOthers);

        //Settlement.CanBeClaimed
        messageBroker.Subscribe<ChangeSettlementClaimantCanBeClaimed>(HandleSettlementClaimaintCanBeClaimed);

    }

    private void HandleSettlementClaimaintCanBeClaimed(MessagePayload<ChangeSettlementClaimantCanBeClaimed> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        SettlementClaimantCampaignBehaviorOnOwnerChangedPatch.RunCanBeClaimed(settlement, obj.CanBeClaimed);
    }

    private void HandleLordConversationCampaignBehaviorPlayerClaimValueOthers(MessagePayload<ChangeLordConversationCampaignBehaviorPlayerClaimValueOthers> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }
        ClaimLandAnswerOnConversationLordConversationsCampaignBehaviourPatch.RunClaimedValue(settlement, obj.ClaimValue);
    }

    private void HandleLordConversationCampaignBehaviorPlayerClaimValue(MessagePayload<ChangeLordConversationCampaignBehaviourPlayerClaimValue> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        ClaimLandAnswerOnConversationLordConversationsCampaignBehaviourPatch.RunClaimedValue(settlement, obj.ClaimValue);
    }

    private void HandleLordConversationCampaignBehaviorPlayerClaimOthers(MessagePayload<ChangeLordConversationCampaignBehaviorPlayerClaimOthers> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        if (objectManager.TryGetObject<Hero>(obj.HeroId, out var hero) == false)
        {
            Logger.Error("Unable to find Settlement ({HeroId})", obj.HeroId);
            return;
        }

        ClaimLandAnswerOnConversationLordConversationsCampaignBehaviourPatch.RunClaimedBy(settlement, hero);
    }

    private void HandleLordConversationCampaignBehaviorPlayerClaim(MessagePayload<ChangeLordConversationCampaignBehaviorPlayerClaim> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        if (objectManager.TryGetObject<Hero>(obj.HeroId, out var hero) == false)
        {
            Logger.Error("Unable to find Settlement ({HeroId})", obj.HeroId);
            return;
        }

        ClaimLandAnswerOnConversationLordConversationsCampaignBehaviourPatch.RunClaimedBy(settlement, hero);
    }

    private void HandleLastVisitTimeOfOwner(MessagePayload<ChangeSettlementLastVisitTimeOfOwner> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementID, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementID);
            return;
        }
        LastVisitTimeOwnerSettlementActionPatch.RunLastVisitTimeOfOwner(settlement, obj.CurrentTime);
    }

    private void HandleHitPointsRatio(MessagePayload<ChangeSettlementWallHitPointsRatio> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        SetWallHitPointsSettlementPatch.RunSetWallSectionHitPointsRatioAtIndex(settlement, obj.index, obj.hitPointsRatio);
        
    }

    private void HandleMobileParty(MessagePayload<ChangeMobileParty> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }


        if (objectManager.TryGetObject<MobileParty>(obj.MobilePartyId, out var mobileParty) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        MobilePartyCachePatch.RunMobileParty(settlement, mobileParty, obj.NumberOfLordParties, obj.AddMobileParty);
    }

    private void HandleHeroRemoveWithoutParty(MessagePayload<ChangeSettlementHeroWithoutPartyRemove> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        if (objectManager.TryGetObject<Hero>(obj.HeroId, out var hero) == false)
        {
            Logger.Error("Unable to find Hero ({HeroStringId})", obj.HeroId);
            return;
        }
        // may not need to run because its cached ~100ms
        //HeroWithoutPartyPatch.RunRemoveHeroWithoutParty(settlement, hero);

    }

    private void HandleHeroWithoutParty(MessagePayload<ChangeSettlementHeroWithoutParty> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        if (objectManager.TryGetObject<Hero>(obj.HeroId, out var hero) == false)
        {
            Logger.Error("Unable to find Hero ({HeroStringId})", obj.HeroId);
            return;
        }
        // may not need to run because its cached ~100ms
        //HeroWithoutPartyPatch.RunAddHeroWithoutParty(settlement, hero);
    }

    private void HandleCollectNotablesToCache(MessagePayload<ChangeSettlementNotablesCache> payload)
    {
        var obj = payload.What;

        MBList<Hero> notablesCache = new();
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        foreach (string heroStringId in obj.NotablesCache) {
            if (objectManager.TryGetObject<Hero>(heroStringId, out var hero) == false)
            {
                Logger.Error("Unable to find Hero ({HeroStringId})", heroStringId);
                return;
            }
            notablesCache.Add(hero);
        }

        CollectNotablesToCachePatch.RunNotablesCacheChange(settlement, notablesCache);
    }

    private void HandleGarrisonWageLimit(MessagePayload<ChangeSettlementGarrisonWagePaymentLimit> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        GarrisonWagePaymentLimitSettlementPatch.RunGarrisonWagePaymentLimitChange(settlement, obj.GarrisonWagePaymentLimit);
    }

    private void HandleMilitia(MessagePayload<ChangeSettlementMilitia> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        MilitiaSettlementPatch.RunMiltiiaChange(settlement, obj.Militia);
    }

    private void HandleCurrentSiegeState(MessagePayload<ChangeSettlementCurrentSiegeState> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        CurrentSiegeStateSettlementPatch.RunCurrentSiegeStateChange(settlement, (SiegeState)obj.CurrentSiegeState);
    }

    private void HandleLastThreatTime(MessagePayload<ChangeSettlementLastThreatTime> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        LastThreatTimeSettlementPatch.LastThreatTimeChange(settlement, obj.LastThreatTimeTicks);
    }

    private void HandleLastAttackerParty(MessagePayload<ChangeSettlementLastAttackerParty> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }
        if (objectManager.TryGetObject<MobileParty>(obj.AttackerPartyId, out var mobileParty) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        LastAttackerPartySettlementPatch.RunLastAttackerPartyChange(settlement, mobileParty);

    }

    private void HandleHitPoints(MessagePayload<ChangeSettlementHitPoints> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }

        SettlementHitPointsPatch.RunSettlementHitPointsChange(settlement, obj.SettlementHitPoints);
    }

    private void HandleBribePaid(MessagePayload<ChangeSettlementBribePaid> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Settlement>(obj.SettlementId, out var settlement) == false)
        {
            Logger.Error("Unable to find Settlement ({SettlementId})", obj.SettlementId);
            return;
        }
        BribePaidSettlementPatch.RunBribePaidChange(settlement, obj.BribePaid);
    }


    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeSettlementBribePaid>(HandleBribePaid);
        messageBroker.Unsubscribe<ChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Unsubscribe<ChangeSettlementHitPoints>(HandleHitPoints);
        messageBroker.Unsubscribe<ChangeSettlementLastAttackerParty>(HandleLastAttackerParty);
        messageBroker.Unsubscribe<ChangeSettlementLastThreatTime>(HandleLastThreatTime);
        messageBroker.Unsubscribe<ChangeSettlementCurrentSiegeState>(HandleCurrentSiegeState);
        messageBroker.Unsubscribe<ChangeSettlementMilitia>(HandleMilitia);
        messageBroker.Unsubscribe<ChangeSettlementGarrisonWagePaymentLimit>(HandleGarrisonWageLimit);
        messageBroker.Unsubscribe<ChangeSettlementNotablesCache>(HandleCollectNotablesToCache);
        messageBroker.Unsubscribe<ChangeSettlementHeroWithoutParty>(HandleHeroWithoutParty);
        messageBroker.Unsubscribe<ChangeSettlementHeroWithoutPartyRemove>(HandleHeroRemoveWithoutParty);
        messageBroker.Unsubscribe<ChangeSettlementWallHitPointsRatio>(HandleHitPointsRatio);
        messageBroker.Unsubscribe<ChangeSettlementLastVisitTimeOfOwner>(HandleLastVisitTimeOfOwner);


        messageBroker.Unsubscribe<ChangeLordConversationCampaignBehaviorPlayerClaim>(HandleLordConversationCampaignBehaviorPlayerClaim);
        //other clients ChangeLordConversationCampaignBehaviorPlayerClaimOthers
        messageBroker.Unsubscribe<ChangeLordConversationCampaignBehaviorPlayerClaimOthers>(HandleLordConversationCampaignBehaviorPlayerClaimOthers);

        messageBroker.Unsubscribe<ChangeLordConversationCampaignBehaviourPlayerClaimValue>(HandleLordConversationCampaignBehaviorPlayerClaimValue);
        messageBroker.Unsubscribe<ChangeLordConversationCampaignBehaviorPlayerClaimValueOthers>(HandleLordConversationCampaignBehaviorPlayerClaimValueOthers);


    }
}
