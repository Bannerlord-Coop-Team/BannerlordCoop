#if DEBUG
using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Stances.Messages;

/// <summary>Restores the exact pre-fixture diplomacy state after the mounted battle live test.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkRestoreMountedBattleStance : ICommand
{
    [ProtoMember(1)] public string FixtureToken { get; }
    [ProtoMember(2)] public string Faction1Id { get; }
    [ProtoMember(3)] public string Faction2Id { get; }
    [ProtoMember(4)] public int StanceType { get; }
    [ProtoMember(5)] public int BehaviorPriority { get; }
    [ProtoMember(6)] public long WarStartDateTicks { get; }
    [ProtoMember(7)] public long PeaceDeclarationDateTicks { get; }
    [ProtoMember(8)] public int TroopCasualties1 { get; }
    [ProtoMember(9)] public int TroopCasualties2 { get; }
    [ProtoMember(10)] public int ShipCasualties1 { get; }
    [ProtoMember(11)] public int ShipCasualties2 { get; }
    [ProtoMember(12)] public int SuccessfulSieges1 { get; }
    [ProtoMember(13)] public int SuccessfulSieges2 { get; }
    [ProtoMember(14)] public int SuccessfulRaids1 { get; }
    [ProtoMember(15)] public int SuccessfulRaids2 { get; }
    [ProtoMember(16)] public int TotalTributePaidFrom1To2 { get; }
    [ProtoMember(17)] public int DailyTributeFrom1To2 { get; }
    [ProtoMember(18)] public int DailyTributeInstallments { get; }
    [ProtoMember(19)] public int SuccessfulTownSieges1 { get; }
    [ProtoMember(20)] public int SuccessfulTownSieges2 { get; }
    [ProtoMember(21)] public bool HasFaction1PoliticalStagnation { get; }
    [ProtoMember(22)] public int Faction1PoliticalStagnation { get; }
    [ProtoMember(23)] public bool HasFaction2PoliticalStagnation { get; }
    [ProtoMember(24)] public int Faction2PoliticalStagnation { get; }
    [ProtoMember(25)] public bool RestoreExactSnapshot { get; }

    public NetworkRestoreMountedBattleStance(
        string fixtureToken,
        string faction1Id,
        string faction2Id,
        int stanceType,
        int behaviorPriority,
        long warStartDateTicks,
        long peaceDeclarationDateTicks,
        int troopCasualties1,
        int troopCasualties2,
        int shipCasualties1,
        int shipCasualties2,
        int successfulSieges1,
        int successfulSieges2,
        int successfulRaids1,
        int successfulRaids2,
        int totalTributePaidFrom1To2,
        int dailyTributeFrom1To2,
        int dailyTributeInstallments,
        int successfulTownSieges1,
        int successfulTownSieges2,
        bool hasFaction1PoliticalStagnation,
        int faction1PoliticalStagnation,
        bool hasFaction2PoliticalStagnation,
        int faction2PoliticalStagnation,
        bool restoreExactSnapshot = true)
    {
        FixtureToken = fixtureToken;
        Faction1Id = faction1Id;
        Faction2Id = faction2Id;
        StanceType = stanceType;
        BehaviorPriority = behaviorPriority;
        WarStartDateTicks = warStartDateTicks;
        PeaceDeclarationDateTicks = peaceDeclarationDateTicks;
        TroopCasualties1 = troopCasualties1;
        TroopCasualties2 = troopCasualties2;
        ShipCasualties1 = shipCasualties1;
        ShipCasualties2 = shipCasualties2;
        SuccessfulSieges1 = successfulSieges1;
        SuccessfulSieges2 = successfulSieges2;
        SuccessfulRaids1 = successfulRaids1;
        SuccessfulRaids2 = successfulRaids2;
        TotalTributePaidFrom1To2 = totalTributePaidFrom1To2;
        DailyTributeFrom1To2 = dailyTributeFrom1To2;
        DailyTributeInstallments = dailyTributeInstallments;
        SuccessfulTownSieges1 = successfulTownSieges1;
        SuccessfulTownSieges2 = successfulTownSieges2;
        HasFaction1PoliticalStagnation = hasFaction1PoliticalStagnation;
        Faction1PoliticalStagnation = faction1PoliticalStagnation;
        HasFaction2PoliticalStagnation = hasFaction2PoliticalStagnation;
        Faction2PoliticalStagnation = faction2PoliticalStagnation;
        RestoreExactSnapshot = restoreExactSnapshot;
    }
}
#endif
