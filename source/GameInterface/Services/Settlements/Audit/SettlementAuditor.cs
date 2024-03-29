﻿using Common.Audit;
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
internal class SettlementAuditor : IAuditor
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementAuditor>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfiguration configuration;
    private TaskCompletionSource<string> tcs;

    public SettlementAuditor(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.configuration = configuration;

        messageBroker.Subscribe<ProcessSettlementAudit>(Handle_Request);
        messageBroker.Subscribe<SettlementAuditResponse>(Handle_Response);

    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<ProcessSettlementAudit>(Handle_Request);
        messageBroker.Unsubscribe<SettlementAuditResponse>(Handle_Response);
    }


    private void Handle_Response(MessagePayload<SettlementAuditResponse> payload)
    {
        var stringBuilder = new StringBuilder();
        var auditDatas = payload.What.Data;

        stringBuilder.AppendLine("Server Audit Results:");
        stringBuilder.AppendLine(payload.What.ServerAuditResults);

        stringBuilder.AppendLine("CLient Audit Results:");
        stringBuilder.AppendLine(AuditData(auditDatas));

        tcs.SetResult(stringBuilder.ToString());
    }

    private void Handle_Request(MessagePayload<ProcessSettlementAudit> payload)
    {
        var serverAuditResult = AuditData(payload.What.Data);
        var response = new SettlementAuditResponse(GetAuditData(), serverAuditResult);
        messageBroker.Publish(this, response);

    }

    public string Audit()
    {
        if(ModInformation.IsServer)
        {
            var errorMsg = "Audit is only client side";
            Logger.Error(errorMsg);
            return errorMsg;
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        tcs = new TaskCompletionSource<string>();

        cts.Token.Register(() =>
        {
            tcs.TrySetCanceled();
        });

        var request = new RequestSettlementAudit(GetAuditData());

        network.SendAll(request);

        try
        {
            tcs.Task.Wait();
            return tcs.Task.Result;

        }catch (AggregateException ex)
        {
            if (ex.InnerException is TaskCanceledException == false) throw ex;

            var errorMsg = "Audit timed out";
            Logger.Error(errorMsg);
            return errorMsg;
        }
    }
    private SettlementAuditData[] GetAuditData()
    {
        return GetSettlements().Select(h => new SettlementAuditData(h)).ToArray();
    }
    private IEnumerable<Settlement> GetSettlements() => Campaign.Current.CampaignObjectManager.Settlements;

    private string AuditData(SettlementAuditData[] dataToAudit)
    {

        var sb = new StringBuilder();
        var errorCountObjectFound = 0;

        var SettlementCount = GetSettlements().Count();

        sb.AppendLine($"Auditing {SettlementCount} objects");

        if(SettlementCount != dataToAudit.Length)
        {
            Logger.Error("Settlement count mismatch: {ArmyCount} != {dataToAudit.Length}", SettlementCount, dataToAudit.Length);
            sb.AppendLine($"Settlement count mismatch: {SettlementCount} != {dataToAudit.Length}");
        }

        foreach(var audit in dataToAudit)
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

        sb.AppendLine($"Found {errorCountObjectFound} errors in {dataToAudit.Length} objects");


        return sb.ToString();
    }

}
