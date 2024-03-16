using Common.Audit;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Audit;

/// <summary>
/// Auditor for <see cref="Settlement"/> objects
/// </summary>
internal class SettlementAuditor : Auditor<ProcessSettlementAudit, SettlementAuditResponse, Settlement, SettlementAuditData, SettlementAuditor>
{
    public SettlementAuditor(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, INetworkConfiguration configuration) : base(messageBroker, network, objectManager, configuration)
    {
    }

    public override IEnumerable<SettlementAuditData> GetAuditData()
    {
        return Objects.Select(h => new SettlementAuditData(h)).ToArray();
    }
    public override IEnumerable<Settlement> Objects => Campaign.Current.CampaignObjectManager.Settlements;

    public override string DoAuditData(IEnumerable<IAuditData> dataToAudit)
    {

        var sb = new StringBuilder();
        var errorCountObjectFound = 0;

        var SettlementCount = Objects.Count();

        sb.AppendLine($"Auditing {SettlementCount} objects");

        if(SettlementCount != dataToAudit.Count())
        {
            Logger.Error("Settlement count mismatch: {ArmyCount} != {dataToAudit.Length}", SettlementCount, dataToAudit.Count());
            sb.AppendLine($"Settlement count mismatch: {SettlementCount} != {dataToAudit.Count()}");
        }

        foreach(var audit in dataToAudit.Cast<SettlementAuditData>())
        {
            var errorNumberOfEnemiesSpottedAround = 0;
            var errorNumberOfAlliesSpottedAround = 0;
            var errorBribePaid = 0;
            var errSettlementHitPoints = 0;
            var errGarrisonWagePaymentLimit = 0;
            var errLastAttackerParty = 0;
            var errLastThreatTime = 0;
            var errCurrentSiegeState = 0;
            var errMilitia = 0;
            var errSettlementHeroCache = 0;
            var errHeroesWithoutPartyCache = 0;
            var errNumberOfLordPartiesAt = 0;
            var errLastVisitTimeOfOwner = 0;
            var errClaimedBy = 0;
            var errClaimValue = 0;
            var errWallHitPointsRatio = 0;
            var errCanBeClaimed = 0;

            sb.AppendLine($"Attempting Audit for Settlement {audit.StringId}");
            if(objectManager.TryGetObject<Settlement>(audit.StringId, out var settlement) == false)
            {
                sb.AppendLine($"Settlement {audit.StringId} not found in {nameof(IObjectManager)}");
                sb.AppendLine($"Audit for Settlement {audit.StringId} done\n");
                errorCountObjectFound++;
                continue; 
            }

            if(settlement.NumberOfEnemiesSpottedAround != audit.NumberOfEnemiesSpottedAround)
            {
                sb.AppendLine($"settlement.NumberOfEnemiesSpottedAround {settlement.NumberOfEnemiesSpottedAround}!= {audit.NumberOfEnemiesSpottedAround}");
                errorNumberOfEnemiesSpottedAround++;
            }

            if (settlement.NumberOfAlliesSpottedAround != audit.NumberOfAlliesSpottedAround)
            {
                sb.AppendLine($"settlement.NumberOfAlliesSpottedAround {settlement.NumberOfAlliesSpottedAround}!= {audit.NumberOfAlliesSpottedAround}");
                errorNumberOfAlliesSpottedAround++;
            }

            if(settlement.BribePaid != audit.BribePaid)
            {
                sb.AppendLine($"settlement.BribePaid {settlement.BribePaid}!= {audit.BribePaid}");
                errorBribePaid++;
               
            }

            if (settlement.SettlementHitPoints != audit.SettlementHitPoints)
            {
                sb.AppendLine($"settlement.SettlementHitPoints {settlement.SettlementHitPoints}!= {audit.SettlementHitPoints}");
                errSettlementHitPoints++;

            }

            if (settlement.GarrisonWagePaymentLimit != audit.GarrisonWagePaymentLimit)
            {
                sb.AppendLine($"settlement.GarrisonWagePaymentLimit {settlement.GarrisonWagePaymentLimit}!= {audit.GarrisonWagePaymentLimit}");
                errGarrisonWagePaymentLimit++;
            }


            //check if null
            var lastAttackerParty = settlement.LastAttackerParty?.StringId ?? "";
            if (lastAttackerParty != audit.LastAttackerParty)
            {
                sb.AppendLine($"settlement.LastAttackerParty {settlement.LastAttackerParty.StringId}!= {audit.LastAttackerParty}");
                errLastAttackerParty++;
            }

            if (settlement.LastThreatTime.NumTicks != audit.LastThreatTime)
            {
                sb.AppendLine($"settlement.LastThreatTime {settlement.LastThreatTime.NumTicks}!= {audit.LastThreatTime}");
                errLastThreatTime++;
            }

            short currentSiegeState = (short)settlement.CurrentSiegeState;

            if (currentSiegeState != audit.CurrentSiegeState)
            {
                sb.AppendLine($"settlement.CurrentSiegeState {currentSiegeState}!= {audit.CurrentSiegeState}");
                errCurrentSiegeState++;
            }

            if (settlement.Militia != audit.Militia)
            {
                sb.AppendLine($"settlement.Militia {settlement.Militia}!= {audit.Militia}");
                errMilitia++;
            }


            List<Hero> notableCache = settlement._notablesCache.ToList();
            List<string> settlementNotableCache = notableCache.Select(hero => hero.StringId).ToList();

            // if the lists dont contain same elements

            // two caches can be null (e.g. TrainingField has null for both)

            var auditNotables = audit.NotablesCache ?? Array.Empty<string>();
            bool containSameNotables = settlementNotableCache.OrderBy(x => x).SequenceEqual(auditNotables.OrderBy(x => x));
            if (!containSameNotables)
            {
                sb.AppendLine($"settlement._notablesCache list dont contain same items");
                errSettlementHeroCache++;
            }
            

            List<Hero> heroCache = settlement._heroesWithoutPartyCache.ToList();
            List<string> settlementHeroCache = heroCache.Select(hero => hero.StringId).ToList();

            var auditHerosWithoutPartyCache = audit.HeroesWithoutPartyCache ?? Array.Empty<string>();

            bool containsSameHerosWithoutParty = settlementHeroCache.OrderBy(x => x).SequenceEqual(auditHerosWithoutPartyCache.OrderBy(x => x));

            if (!containsSameHerosWithoutParty)
            {
                sb.AppendLine($"settlement._herosWithoutPartyCache list dont contain same items");
                errHeroesWithoutPartyCache++;
            } 
            

            if (settlement.NumberOfLordPartiesAt != audit.NumberOfLordPartiesAt)
            {
                sb.AppendLine($"settlement.NumberOfLordPartiesAt {settlement.NumberOfLordPartiesAt}!= {audit.NumberOfLordPartiesAt}");
                errNumberOfLordPartiesAt++;
            }

            if (settlement.LastVisitTimeOfOwner != audit.LastVisitTimeOfOwner)
            {
                sb.AppendLine($"settlement.LastVisitTimeOfOwner {settlement.LastVisitTimeOfOwner}!= {audit.LastVisitTimeOfOwner}");
                errLastVisitTimeOfOwner++;
            }

            var claimedBy = settlement.ClaimedBy?.StringId ?? "";
            if (claimedBy != audit.ClaimedBy)
            {
                sb.AppendLine($"settlement.ClaimedBy {settlement.ClaimedBy.StringId}!= {audit.ClaimedBy}");
                errClaimedBy++;
            }

            if(settlement.ClaimValue != audit.ClaimValue)
            {
                sb.AppendLine($"settlement.ClaimValue {settlement.ClaimValue}!= {audit.ClaimValue}");
                errClaimValue++;
            }

            // value can be null sadly :(
            if(!(audit.WallSectionHitPointsRatioList is null))
            {
                if (!audit.WallSectionHitPointsRatioList.SequenceEqual(settlement._settlementWallSectionHitPointsRatioList))
                {
                    sb.AppendLine($"settlement._settlementWallSectionHitPointsRatioList {settlement._settlementWallSectionHitPointsRatioList} != {audit.WallSectionHitPointsRatioList}");
                    errWallHitPointsRatio++;
                }


            } else
            {
                if(settlement._settlementWallSectionHitPointsRatioList.Count > 0)
                {
                    sb.AppendLine($"settlement._settlementWallSectionHitPointsRatioList {settlement._settlementWallSectionHitPointsRatioList} != {audit.WallSectionHitPointsRatioList}");
                    errWallHitPointsRatio++;
                }
            }

            if(audit.CanBeClaimed != settlement.CanBeClaimed)
            {
                sb.AppendLine($"settlement.CanBeClaimed {settlement.CanBeClaimed} != {audit.CanBeClaimed}");

                errCanBeClaimed++;
            }

            sb.AppendLine($"\terrorNumberOfEnemiesSpottedAround {errorNumberOfEnemiesSpottedAround}");
            sb.AppendLine($"\terrorNumberOfAlliesSpottedAround: {errorNumberOfAlliesSpottedAround}");
            sb.AppendLine($"\tterrorBribePaid: {errorBribePaid}");
            sb.AppendLine($"\tterrSettlementHitPoints {errSettlementHitPoints}");
            sb.AppendLine($"\terrGarrisonWagePaymentLimit {errGarrisonWagePaymentLimit}");
            sb.AppendLine($"\tterrLastAttackerParty: {errLastAttackerParty}");
            sb.AppendLine($"\tterrLastThreatTime: {errLastThreatTime}");
            sb.AppendLine($"\tterrCurrentSiegeState: {errCurrentSiegeState}");
            sb.AppendLine($"\terrMilitia: {errMilitia}");
            sb.AppendLine($"\terrSettlementHeroCache: {errSettlementHeroCache}");
            sb.AppendLine($"\terrHeroesWithoutPartyCache: {errHeroesWithoutPartyCache}");
            sb.AppendLine($"\terrNumberOfLordPartiesAt: {errNumberOfLordPartiesAt}");
            sb.AppendLine($"\terrLastVisitTimeOfOwner: {errLastVisitTimeOfOwner}");
            sb.AppendLine($"\terrClaimedBy: {errClaimedBy}");
            sb.AppendLine($"\terrClaimValue: {errClaimValue}");
            sb.AppendLine($"\terrCanBeClaimed: {errCanBeClaimed}");
            sb.AppendLine($"\terrWallSectionHitPointsRatioList: {errWallHitPointsRatio}");

        }

        sb.AppendLine($"Found {errorCountObjectFound} errors in {dataToAudit.Count()} objects");


        return sb.ToString();
    }

    public override SettlementAuditResponse CreateResponseInstance(IEnumerable<SettlementAuditData> par1, string par2)
    {
        return new SettlementAuditResponse(par1.ToArray(), par2);
    }

    public override ProcessSettlementAudit CreateRequestInstance(IEnumerable<SettlementAuditData> par1)
    {
        return new ProcessSettlementAudit(par1.ToArray());
    }
}
