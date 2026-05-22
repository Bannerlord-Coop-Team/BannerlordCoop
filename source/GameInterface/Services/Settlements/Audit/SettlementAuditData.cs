using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Audit;

/// <summary>
/// Audit data for a settlement
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record SettlementAuditData
{
    [ProtoMember(1)]
    public string StringId { get; }
    [ProtoMember(2)]
    public float NumberOfEnemiesSpottedAround { get; }
    [ProtoMember(3)]
    public float NumberOfAlliesSpottedAround { get; }
    [ProtoMember(4)]
    public int BribePaid { get; }
    [ProtoMember(5)]
    public float SettlementHitPoints { get; }
    [ProtoMember(6)]
    public int GarrisonWagePaymentLimit { get; }
    [ProtoMember(7)]
    public string LastAttackerParty { get; }
    [ProtoMember(8)]
    public long LastThreatTime { get; }
    [ProtoMember(9)]
    public short CurrentSiegeState { get; }
    [ProtoMember(10)]
    public float Militia { get; }
    [ProtoMember(11)]
    public string[] NotablesCache { get; }
    [ProtoMember(12)]
    public string[] HeroesWithoutPartyCache { get; }
    [ProtoMember(13)]
    public float LastVisitTimeOfOwner { get; }
    [ProtoMember(14)]
    public string ClaimedBy { get; }
    [ProtoMember(15)]
    public float ClaimValue { get; }
    [ProtoMember(16)]
    public int CanBeClaimed { get; }
    [ProtoMember(17)]
    public float[] WallSectionHitPointsRatioList {get;}

    // TODO: add more fields/properties for now this will suffice

    public SettlementAuditData(Settlement settlement)
    {
        StringId = settlement.StringId;

        NumberOfEnemiesSpottedAround = settlement.NearbyLandAllyIntensity;
        NumberOfAlliesSpottedAround = settlement.NearbyLandThreatIntensity;
        BribePaid = settlement.BribePaid;
        SettlementHitPoints = settlement.SettlementHitPoints;
        GarrisonWagePaymentLimit = settlement.GarrisonWagePaymentLimit;
        // mobileParty StringID
        LastAttackerParty = settlement.LastAttackerParty?.StringId ?? "";
        LastThreatTime = settlement.LastThreatTime.NumTicks;
        CurrentSiegeState = (short)settlement.CurrentSiegeState;
        Militia = settlement.Militia;

        NotablesCache = settlement._notablesCache.Select(hero => hero.StringId).ToArray();
        HeroesWithoutPartyCache = settlement._heroesWithoutPartyCache.Select(heroCache => heroCache.StringId).ToArray();

        LastVisitTimeOfOwner = settlement.LastVisitTimeOfOwner;

        //ClaimedBy = settlement.ClaimedBy?.StringId ?? "";
        //ClaimValue = settlement.ClaimValue;
        //CanBeClaimed = settlement.CanBeClaimed;


        WallSectionHitPointsRatioList = settlement._settlementWallSectionHitPointsRatioList.ToArray();

    }
}
