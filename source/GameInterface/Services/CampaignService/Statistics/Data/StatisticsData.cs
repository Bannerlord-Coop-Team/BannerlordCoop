using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.CampaignService.Statistics.Data;

/// <summary>
/// One player's copy of the fields StatisticsCampaignBehavior tracks for a single player
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class StatisticsData
{
    [ProtoMember(1)]
    public int HighestTournamentRank { get; set; }
    
    [ProtoMember(2)]
    public int NumberOfTournamentWins { get; set; }
    
    [ProtoMember(3)]
    public int NumberOfChildrenBorn { get; set; }
    
    [ProtoMember(4)]
    public int NumberOfPrisonersRecruited { get; set; }
    
    [ProtoMember(5)]
    public int NumberOfTroopsRecruited { get; set; }
    
    [ProtoMember(6)]
    public int NumberOfClansDefected { get; set; }
    
    [ProtoMember(7)]
    public int NumberOfIssuesSolved { get; set; }
    
    [ProtoMember(8)]
    public int TotalInfluenceEarned { get; set; }
    
    [ProtoMember(9)]
    public int TotalCrimeRatingGained { get; set; }
    
    [ProtoMember(10)]
    public ulong TotalTimePlayedInSeconds { get; set; }
    
    [ProtoMember(11)]
    public int NumberOfBattlesWon { get; set; }
    
    [ProtoMember(12)]
    public int NumberOfBattlesLost { get; set; }
    
    [ProtoMember(13)]
    public int LargestBattleWonAsLeader { get; set; }
    
    [ProtoMember(14)]
    public int LargestArmyFormedByPlayer { get; set; }
    
    [ProtoMember(15)]
    public int NumberOfEnemyClansDestroyed { get; set; }
    
    [ProtoMember(16)]
    public int NumberOfHeroesKilledInBattle { get; set; }
    
    [ProtoMember(17)]
    public int NumberOfTroopsKnockedOrKilledAsParty { get; set; }
    
    [ProtoMember(18)]
    public int NumberOfTroopsKnockedOrKilledByPlayer { get; set; }
    
    [ProtoMember(19)]
    public int NumberOfHeroPrisonersTaken { get; set; }
    
    [ProtoMember(20)]
    public int NumberOfTroopPrisonersTaken { get; set; }
    
    [ProtoMember(21)]
    public int NumberOfTownsCaptured { get; set; }
    
    [ProtoMember(22)]
    public int NumberOfHideoutsCleared { get; set; }
    
    [ProtoMember(23)]
    public int NumberOfCastlesCaptured { get; set; }
    
    [ProtoMember(24)]
    public int NumberOfVillagesRaided { get; set; }
    
    // CampaignTime ticks
    [ProtoMember(25)]
    public long TimeSpentAsPrisoner { get; set; }
    
    [ProtoMember(26)]
    public ulong TotalDenarsEarned { get; set; }
    
    [ProtoMember(27)]
    public ulong DenarsEarnedFromCaravans { get; set; }
    
    [ProtoMember(28)]
    public ulong DenarsEarnedFromWorkshops { get; set; }
    
    [ProtoMember(29)]
    public ulong DenarsEarnedFromRansoms { get; set; }
    
    [ProtoMember(30)]
    public ulong DenarsEarnedFromTaxes { get; set; }
    
    [ProtoMember(31)]
    public ulong DenarsEarnedFromTributes { get; set; }
    
    [ProtoMember(32)]
    public ulong DenarsPaidAsTributes { get; set; }
    
    [ProtoMember(33)]
    public int NumberOfCraftingPartsUnlocked { get; set; }
    
    [ProtoMember(34)]
    public int NumberOfWeaponsCrafted { get; set; }
    
    [ProtoMember(35)]
    public int NumberOfCraftingOrdersCompleted { get; set; }
    
    [ProtoMember(36)]
    public string MostExpensiveItemCraftedName { get; set; }
    
    [ProtoMember(37)]
    public int MostExpensiveItemCraftedValue { get; set; }
    
    [ProtoMember(38)]
    public int NumberOfCompanionsHired { get; set; }

    // Dictionary<CompanionHeroId, CompanionStatisticsData>
    [ProtoMember(39)]
    public Dictionary<string, CompanionStatisticsData> CompanionData { get; set; } = new();
}

[ProtoContract(SkipConstructor = true)]
public class CompanionStatisticsData
{
    [ProtoMember(1)]
    public int IssuesSolved { get; set; }

    [ProtoMember(2)]
    public int Kills { get; set; }
}
