using Common;
using Common.Logging;
using Common.Network;
using GameInterface.CoopSessionData;
using GameInterface.Services.CampaignService.Statistics.Data;
using GameInterface.Services.CampaignService.Statistics.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using SandBox.CampaignBehaviors;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.CampaignService.Statistics.Interfaces;

public interface IStatisticsCampaignBehaviorInterface : IGameAbstraction
{
    void OnHeroCreated(Hero hero);
    void OnIssueUpdated(IssueBase issue, IssueBase.IssueUpdateDetails details, Hero issueSolver);
    void OnTournamentFinished(CharacterObject winner);
    void OnClanInfluenceChanged(Clan clan, float change);
    void OnCrimeRatingChanged(float deltaCrimeAmount);
    void OnUnitRecruited(Hero playerHero, int amount);
    void OnPrisonersRecruited(Hero playerHero, int amount);
    void OnPrisonersTaken(Hero playerHero, int amount);
    void OnCraftingPartUnlocked(Hero playerHero);
    void OnNewItemCrafted(Hero playerHero, ItemObject itemObject, bool isCraftingOrderItem);
    void OnCompanionHired(Clan clan);
    void OnHeroOrPartyTradedGold(Hero recipientHero, PartyBase recipientParty, int goldAmount);
    void OnMapEventEnded(MapEvent mapEvent);
    void OnClanDestroyed(Clan clan);
    void OnArmyChanged(Army army);
    void OnHeroPrisonerTaken(PartyBase capturer);
    void OnRaidCompleted(BattleSideEnum winnerSide, MapEvent mapEvent);
    void OnHideoutBattleCompleted(BattleSideEnum winnerSide, MapEvent mapEvent);
    void OnHeroPrisonerReleased(Hero prisoner);
    void OnSimulatedTroopKnockedOrKilled(PartyBase strikerParty);
    void OnHeroKilled(Hero killer, KillCharacterAction.KillCharacterActionDetail detail);
    void OnPlayerEarnedGoldFromAsset(Hero earner, DefaultClanFinanceModel.AssetIncomeType assetType, int amount);
    void OnDefectionPersuasionSuccess(Hero playerHero);
    void OnPlayerAcceptedRansomOffer(Hero playerHero, int ransomPrice);
    void OnMissionStatisticsReceived(Hero playerHero, NetworkMissionStatistics statistics);

    /// <summary>
    /// Adds the real time each connected player played since the last update
    /// </summary>
    void UpdatePlayTime();

    StatisticsData GetStatistics(Hero playerHero);

    void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent);
    void SendMissionStatistics();
    void LoadStatistics(StatisticsData statistics);
}

/// <summary>
/// Per player version of StatisticsCampaignBehavior, which only tracks statistics for Hero.MainHero.
/// The server records every player's statistics in the CoopSession, clients only count their own mission kills.
/// </summary>
public class StatisticsCampaignBehaviorInterface : IStatisticsCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<StatisticsCampaignBehaviorInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnerResolver issueOwnerResolver;
    private readonly INetwork network;

    // Server: last real time each connected player's play time was added, by controller id
    private Dictionary<string, DateTime> playTimeChecks = new();

    // Client: kills counted since the last mission statistics were sent
    private int missionTroopsKnockedOrKilledByPlayer;
    private int missionTroopsKnockedOrKilledAsParty;
    private int missionHeroesKilledInBattle;
    private readonly Dictionary<string, int> missionCompanionKills = new();

    public StatisticsCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IIssueOwnerResolver issueOwnerResolver,
        INetwork network)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.issueOwnerResolver = issueOwnerResolver;
        this.network = network;
    }

    public void OnHeroCreated(Hero hero)
    {
        if (TryGetStatistics(hero.Mother, out var motherStatistics)) motherStatistics.NumberOfChildrenBorn++;
        if (TryGetStatistics(hero.Father, out var fatherStatistics)) fatherStatistics.NumberOfChildrenBorn++;
    }

    public void OnIssueUpdated(IssueBase issue, IssueBase.IssueUpdateDetails details, Hero issueSolver)
    {
        if (details != IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess &&
            details != IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest &&
            details != IssueBase.IssueUpdateDetails.IssueFinishedWithBetrayal) return;

        if (!issueOwnerResolver.TryResolveOwnerHero(issue, out var ownerHero)) return;
        if (!TryGetStatistics(ownerHero, out var statistics)) return;

        statistics.NumberOfIssuesSolved++;

        // Replaces issueSolver.IsPlayerCompanion, which checks Clan.PlayerClan
        if (issueSolver == null || issueSolver.CompanionOf == null || issueSolver.CompanionOf != ownerHero.Clan) return;
        if (!objectManager.TryGetIdWithLogging(issueSolver, out var companionId)) return;

        GetCompanionData(statistics, companionId).IssuesSolved++;
    }

    public void OnTournamentFinished(CharacterObject winner)
    {
        var winnerHero = winner.HeroObject;
        if (!TryGetStatistics(winnerHero, out var statistics)) return;

        statistics.NumberOfTournamentWins++;
        int leaderBoardRank = Campaign.Current.TournamentManager.GetLeaderBoardRank(winnerHero);

        // 0 is unranked, which vanilla only replaces by reseeding in OnAfterSessionLaunched on the next load
        if (leaderBoardRank > 0 && (statistics.HighestTournamentRank == 0 || leaderBoardRank < statistics.HighestTournamentRank))
        {
            statistics.HighestTournamentRank = leaderBoardRank;
        }
    }

    public void OnClanInfluenceChanged(Clan clan, float change)
    {
        if (change <= 0f) return;
        if (!TryGetStatistics(clan.Leader, out var statistics)) return;

        statistics.TotalInfluenceEarned += (int)change;
    }

    public void OnCrimeRatingChanged(float deltaCrimeAmount)
    {
        if (deltaCrimeAmount <= 0f) return;

        // CrimeRatingActionPatch only lets the server change a crime rating while the player is resolved
        if (!TryGetStatistics(ResolvedMainHeroContext.ResolvedMainHero, out var statistics)) return;

        statistics.TotalCrimeRatingGained += (int)deltaCrimeAmount;
    }

    public void OnUnitRecruited(Hero playerHero, int amount)
    {
        if (!TryGetStatistics(playerHero, out var statistics)) return;

        statistics.NumberOfTroopsRecruited += amount;
    }

    public void OnPrisonersRecruited(Hero playerHero, int amount)
    {
        if (!TryGetStatistics(playerHero, out var statistics)) return;

        statistics.NumberOfPrisonersRecruited += amount;
    }

    public void OnPrisonersTaken(Hero playerHero, int amount)
    {
        if (!TryGetStatistics(playerHero, out var statistics)) return;

        statistics.NumberOfTroopPrisonersTaken += amount;
    }

    public void OnCraftingPartUnlocked(Hero playerHero)
    {
        if (!TryGetStatistics(playerHero, out var statistics)) return;

        statistics.NumberOfCraftingPartsUnlocked++;
    }

    public void OnNewItemCrafted(Hero playerHero, ItemObject itemObject, bool isCraftingOrderItem)
    {
        if (!TryGetStatistics(playerHero, out var statistics)) return;

        statistics.NumberOfWeaponsCrafted++;
        if (isCraftingOrderItem)
        {
            statistics.NumberOfCraftingOrdersCompleted++;
        }
        if (statistics.MostExpensiveItemCraftedValue == 0 || statistics.MostExpensiveItemCraftedValue < itemObject.Value)
        {
            statistics.MostExpensiveItemCraftedName = itemObject.Name.ToString();
            statistics.MostExpensiveItemCraftedValue = itemObject.Value;
        }
    }

    public void OnCompanionHired(Clan clan)
    {
        if (!TryGetStatistics(clan.Leader, out var statistics)) return;

        statistics.NumberOfCompanionsHired++;
    }

    public void OnHeroOrPartyTradedGold(Hero recipientHero, PartyBase recipientParty, int goldAmount)
    {
        if (goldAmount <= 0) return;
        if (!TryGetStatistics(recipientHero, out var statistics) &&
            !TryGetStatistics(recipientParty?.LeaderHero, out statistics)) return;

        statistics.TotalDenarsEarned += (ulong)goldAmount;
    }

    public void OnMapEventEnded(MapEvent mapEvent)
    {
        if (!mapEvent.HasWinner) return;

        // Replaces the size vanilla records at MapEventStarted, which a player joining later never gets
        int battleSize = mapEvent.AttackerSide.HealthyTroopCountAtMapEventStart + mapEvent.DefenderSide.HealthyTroopCountAtMapEventStart;

        foreach (var (party, statistics) in GetPlayerParties(mapEvent))
        {
            if (mapEvent.WinningSide != party.Side)
            {
                statistics.NumberOfBattlesLost++;
                continue;
            }

            statistics.NumberOfBattlesWon++;

            // Replaces MapEvent.IsPlayerSergeant, which checks MainParty
            bool isSergeant = mapEvent.GetLeaderParty(party.Side) != party && party.MobileParty?.Army != null;
            if (isSergeant) continue;

            if (mapEvent.IsSiegeAssault && mapEvent.MapEventSettlement != null)
            {
                if (mapEvent.MapEventSettlement.IsTown)
                {
                    statistics.NumberOfTownsCaptured++;
                }
                else if (mapEvent.MapEventSettlement.IsCastle)
                {
                    statistics.NumberOfCastlesCaptured++;
                }
            }
            if (statistics.LargestBattleWonAsLeader < battleSize)
            {
                statistics.LargestBattleWonAsLeader = battleSize;
            }
        }
    }

    public void OnClanDestroyed(Clan clan)
    {
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObject<Hero>(player.HeroId, out var playerHero)) continue;

            // Clan statistics go to the player leading the clan
            if (playerHero.Clan?.Leader != playerHero) continue;
            if (!clan.MapFaction.IsAtWarWith(playerHero.MapFaction)) continue;
            if (!TryGetStatistics(playerHero, out var statistics)) continue;

            statistics.NumberOfEnemyClansDestroyed++;
        }
    }

    public void OnArmyChanged(Army army)
    {
        if (!TryGetStatistics(army?.LeaderParty?.LeaderHero, out var statistics)) return;

        if (statistics.LargestArmyFormedByPlayer < army.TotalManCount)
        {
            statistics.LargestArmyFormedByPlayer = army.TotalManCount;
        }
    }

    public void OnHeroPrisonerTaken(PartyBase capturer)
    {
        if (!TryGetStatistics(capturer?.LeaderHero, out var statistics)) return;

        statistics.NumberOfHeroPrisonersTaken++;
    }

    public void OnRaidCompleted(BattleSideEnum winnerSide, MapEvent mapEvent)
    {
        if (!mapEvent.HasWinner) return;

        foreach (var (party, statistics) in GetPlayerParties(mapEvent))
        {
            if (party.Side == winnerSide) statistics.NumberOfVillagesRaided++;
        }
    }

    public void OnHideoutBattleCompleted(BattleSideEnum winnerSide, MapEvent mapEvent)
    {
        foreach (var (party, statistics) in GetPlayerParties(mapEvent))
        {
            if (party.Side == winnerSide) statistics.NumberOfHideoutsCleared++;
        }
    }

    public void OnHeroPrisonerReleased(Hero prisoner)
    {
        if (!TryGetStatistics(prisoner, out var statistics)) return;

        // Replaces PlayerCaptivity.CaptivityStartTime, which only tracks Hero.MainHero
        statistics.TimeSpentAsPrisoner += (CampaignTime.Now - prisoner.CaptivityStartTime).NumTicks;
    }

    public void OnSimulatedTroopKnockedOrKilled(PartyBase strikerParty)
    {
        if (!TryGetStatistics(strikerParty?.LeaderHero, out var statistics)) return;

        statistics.NumberOfTroopsKnockedOrKilledAsParty++;
    }

    public void OnHeroKilled(Hero killer, KillCharacterAction.KillCharacterActionDetail detail)
    {
        if (detail != KillCharacterAction.KillCharacterActionDetail.DiedInBattle) return;
        if (!TryGetStatistics(killer?.PartyBelongedTo?.LeaderHero, out var statistics)) return;

        statistics.NumberOfHeroesKilledInBattle++;
    }

    public void OnPlayerEarnedGoldFromAsset(Hero earner, DefaultClanFinanceModel.AssetIncomeType assetType, int amount)
    {
        if (amount <= 0) return;
        if (!TryGetStatistics(earner, out var statistics)) return;

        switch (assetType)
        {
            case DefaultClanFinanceModel.AssetIncomeType.Workshop:
                statistics.DenarsEarnedFromWorkshops += (ulong)amount;
                break;
            case DefaultClanFinanceModel.AssetIncomeType.Caravan:
                statistics.DenarsEarnedFromCaravans += (ulong)amount;
                break;
            case DefaultClanFinanceModel.AssetIncomeType.Taxes:
                statistics.DenarsEarnedFromTaxes += (ulong)amount;
                break;
            case DefaultClanFinanceModel.AssetIncomeType.TributesEarned:
                statistics.DenarsEarnedFromTributes += (ulong)amount;
                break;
        }
    }

    public void OnDefectionPersuasionSuccess(Hero playerHero)
    {
        if (!TryGetStatistics(playerHero, out var statistics)) return;

        statistics.NumberOfClansDefected++;
    }

    public void OnPlayerAcceptedRansomOffer(Hero playerHero, int ransomPrice)
    {
        if (!TryGetStatistics(playerHero, out var statistics)) return;

        statistics.DenarsEarnedFromRansoms += (ulong)ransomPrice;
    }

    public void OnMissionStatisticsReceived(Hero playerHero, NetworkMissionStatistics missionStatistics)
    {
        if (!TryGetStatistics(playerHero, out var statistics)) return;

        statistics.NumberOfTroopsKnockedOrKilledByPlayer += missionStatistics.TroopsKnockedOrKilledByPlayer;
        statistics.NumberOfTroopsKnockedOrKilledAsParty += missionStatistics.TroopsKnockedOrKilledAsParty;
        statistics.NumberOfHeroesKilledInBattle += missionStatistics.HeroesKilledInBattle;

        var companionIds = missionStatistics.CompanionIds ?? Array.Empty<string>();
        var companionKills = missionStatistics.CompanionKills ?? Array.Empty<int>();
        for (int i = 0; i < Math.Min(companionIds.Length, companionKills.Length); i++)
        {
            GetCompanionData(statistics, companionIds[i]).Kills += companionKills[i];
        }
    }

    public void UpdatePlayTime()
    {
        if (ModInformation.IsClient) return;

        var now = DateTime.Now;
        var checks = new Dictionary<string, DateTime>();

        foreach (var player in playerManager.Players)
        {
            // A player that just disconnected is still credited up to now
            if (playTimeChecks.TryGetValue(player.ControllerId, out var lastCheck) &&
                objectManager.TryGetObject<Hero>(player.HeroId, out var playerHero) &&
                TryGetStatistics(playerHero, out var statistics))
            {
                statistics.TotalTimePlayedInSeconds += (ulong)Math.Max(0d, (now - lastCheck).TotalSeconds);
            }

            if (playerManager.IsConnected(player))
            {
                checks[player.ControllerId] = now;
            }
        }

        playTimeChecks = checks;
    }

    public StatisticsData GetStatistics(Hero playerHero)
    {
        UpdatePlayTime();

        return TryGetStatistics(playerHero, out var statistics) ? statistics : null;
    }

    public void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent)
    {
        if (affectorAgent == null) return;

        if (affectorAgent == Agent.Main)
        {
            missionTroopsKnockedOrKilledByPlayer++;
        }
        else if (affectorAgent.IsPlayerTroop || affectorAgent.Origin?.BattleCombatant == PartyBase.MainParty)
        {
            missionTroopsKnockedOrKilledAsParty++;
        }
        else if (affectorAgent.IsHero)
        {
            var hero = (affectorAgent.Character as CharacterObject)?.HeroObject;
            if (hero != null && hero.IsPlayerCompanion && objectManager.TryGetId(hero, out var companionId))
            {
                missionCompanionKills.TryGetValue(companionId, out var kills);
                missionCompanionKills[companionId] = kills + 1;
            }
        }

        if (affectedAgent.IsHero && affectedAgent.State == AgentState.Killed)
        {
            missionHeroesKilledInBattle++;
        }
    }

    public void SendMissionStatistics()
    {
        if (missionTroopsKnockedOrKilledByPlayer == 0 &&
            missionTroopsKnockedOrKilledAsParty == 0 &&
            missionHeroesKilledInBattle == 0 &&
            missionCompanionKills.Count == 0) return;

        if (objectManager.TryGetIdWithLogging(Hero.MainHero, out var playerHeroId))
        {
            network.SendAll(new NetworkMissionStatistics(
                playerHeroId,
                missionTroopsKnockedOrKilledByPlayer,
                missionTroopsKnockedOrKilledAsParty,
                missionHeroesKilledInBattle,
                missionCompanionKills.Keys.ToArray(),
                missionCompanionKills.Values.ToArray()));
        }

        missionTroopsKnockedOrKilledByPlayer = 0;
        missionTroopsKnockedOrKilledAsParty = 0;
        missionHeroesKilledInBattle = 0;
        missionCompanionKills.Clear();
    }

    public void LoadStatistics(StatisticsData statistics)
    {
        var behavior = Campaign.Current?.GetCampaignBehavior<StatisticsCampaignBehavior>();
        if (behavior == null)
        {
            Logger.Error("StatisticsCampaignBehavior was null; cannot load player statistics");
            return;
        }

        statistics ??= new StatisticsData();

        behavior._highestTournamentRank = statistics.HighestTournamentRank;
        behavior._numberOfTournamentWins = statistics.NumberOfTournamentWins;
        behavior._numberOfChildrenBorn = statistics.NumberOfChildrenBorn;
        behavior._numberOfPrisonersRecruited = statistics.NumberOfPrisonersRecruited;
        behavior._numberOfTroopsRecruited = statistics.NumberOfTroopsRecruited;
        behavior._numberOfClansDefected = statistics.NumberOfClansDefected;
        behavior._numberOfIssuesSolved = statistics.NumberOfIssuesSolved;
        behavior._totalInfluenceEarned = statistics.TotalInfluenceEarned;
        behavior._totalCrimeRatingGained = statistics.TotalCrimeRatingGained;
        behavior._totalTimePlayedInSeconds = statistics.TotalTimePlayedInSeconds;
        behavior._numberOfbattlesWon = statistics.NumberOfBattlesWon;
        behavior._numberOfbattlesLost = statistics.NumberOfBattlesLost;
        behavior._largestBattleWonAsLeader = statistics.LargestBattleWonAsLeader;
        behavior._largestArmyFormedByPlayer = statistics.LargestArmyFormedByPlayer;
        behavior._numberOfEnemyClansDestroyed = statistics.NumberOfEnemyClansDestroyed;
        behavior._numberOfHeroesKilledInBattle = statistics.NumberOfHeroesKilledInBattle;
        behavior._numberOfTroopsKnockedOrKilledAsParty = statistics.NumberOfTroopsKnockedOrKilledAsParty;
        behavior._numberOfTroopsKnockedOrKilledByPlayer = statistics.NumberOfTroopsKnockedOrKilledByPlayer;
        behavior._numberOfHeroPrisonersTaken = statistics.NumberOfHeroPrisonersTaken;
        behavior._numberOfTroopPrisonersTaken = statistics.NumberOfTroopPrisonersTaken;
        behavior._numberOfTownsCaptured = statistics.NumberOfTownsCaptured;
        behavior._numberOfHideoutsCleared = statistics.NumberOfHideoutsCleared;
        behavior._numberOfCastlesCaptured = statistics.NumberOfCastlesCaptured;
        behavior._numberOfVillagesRaided = statistics.NumberOfVillagesRaided;
        behavior._timeSpentAsPrisoner = new CampaignTime(statistics.TimeSpentAsPrisoner);
        behavior._totalDenarsEarned = statistics.TotalDenarsEarned;
        behavior._denarsEarnedFromCaravans = statistics.DenarsEarnedFromCaravans;
        behavior._denarsEarnedFromWorkshops = statistics.DenarsEarnedFromWorkshops;
        behavior._denarsEarnedFromRansoms = statistics.DenarsEarnedFromRansoms;
        behavior._denarsEarnedFromTaxes = statistics.DenarsEarnedFromTaxes;
        behavior._denarsEarnedFromTributes = statistics.DenarsEarnedFromTributes;
        behavior._denarsPaidAsTributes = statistics.DenarsPaidAsTributes;
        behavior._numberOfCraftingPartsUnlocked = statistics.NumberOfCraftingPartsUnlocked;
        behavior._numberOfWeaponsCrafted = statistics.NumberOfWeaponsCrafted;
        behavior._numberOfCraftingOrdersCompleted = statistics.NumberOfCraftingOrdersCompleted;
        behavior._mostExpensiveItemCrafted = (statistics.MostExpensiveItemCraftedName, statistics.MostExpensiveItemCraftedValue);
        behavior._numberOfCompanionsHired = statistics.NumberOfCompanionsHired;

        behavior._companionData = new Dictionary<Hero, (int, int)>();
        foreach (var companionData in statistics.CompanionData ?? new Dictionary<string, CompanionStatisticsData>())
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(companionData.Key, out var companion)) continue;

            behavior._companionData[companion] = (companionData.Value.IssuesSolved, companionData.Value.Kills);
        }

        // OnAfterSessionLaunched never runs on clients, otherwise the played time counts from DateTime.MinValue
        behavior._lastGameplayTimeCheck = DateTime.Now;
    }

    private bool TryGetStatistics(Hero playerHero, out StatisticsData statistics)
    {
        statistics = null;

        // Statistics are only recorded on the server
        if (ModInformation.IsClient) return false;
        if (playerHero == null || !PlayerManager.TryGetControlledObjectInfo(playerHero, out _)) return false;
        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId)) return false;

        var playerStatistics = coopSessionProvider.CoopSession?.StatisticsPlayerData?.PlayerStatistics;
        if (playerStatistics == null)
        {
            Logger.Error("StatisticsPlayerData was null; cannot track player statistics");
            return false;
        }

        if (playerStatistics.TryGetValue(playerHeroId, out statistics)) return true;

        // Matches OnAfterSessionLaunched seeding the rank, which is 0 when the hero is not on the leaderboard
        statistics = new StatisticsData
        {
            HighestTournamentRank = Campaign.Current.TournamentManager.GetLeaderBoardRank(playerHero)
        };
        playerStatistics[playerHeroId] = statistics;
        return true;
    }

    private IEnumerable<(PartyBase Party, StatisticsData Statistics)> GetPlayerParties(MapEvent mapEvent)
    {
        foreach (var party in mapEvent.InvolvedParties.ToList())
        {
            if (TryGetStatistics(party.LeaderHero, out var statistics)) yield return (party, statistics);
        }
    }

    private static CompanionStatisticsData GetCompanionData(StatisticsData statistics, string companionId)
    {
        if (!statistics.CompanionData.TryGetValue(companionId, out var companionData))
        {
            companionData = new CompanionStatisticsData();
            statistics.CompanionData[companionId] = companionData;
        }

        return companionData;
    }
}
