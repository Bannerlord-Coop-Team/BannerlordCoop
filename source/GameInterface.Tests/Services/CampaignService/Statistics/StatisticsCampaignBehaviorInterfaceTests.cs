using Common;
using Common.Network;
using Common.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.CampaignService.Statistics.Data;
using GameInterface.Services.CampaignService.Statistics.Interfaces;
using GameInterface.Services.CampaignService.Statistics.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using Xunit;

namespace GameInterface.Tests.Services.CampaignService.Statistics;

[Collection(ModInformationRoleCollection.Name)]
public class StatisticsCampaignBehaviorInterfaceTests : IDisposable
{
    private readonly bool previousServer = ModInformation.IsServer;
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly CoopSessionProvider sessionProvider = new();
    private readonly PlayerManager playerManager;
    private readonly StatisticsCampaignBehaviorInterface statisticsInterface;
    private readonly List<Player> players = new();

    private Dictionary<string, StatisticsData> PlayerStatistics => sessionProvider.CoopSession.StatisticsPlayerData.PlayerStatistics;

    public StatisticsCampaignBehaviorInterfaceTests()
    {
        playerManager = new PlayerManager(Mock.Of<ILogger>(), objectManager.Object, new ControllerIdProvider());
        statisticsInterface = new StatisticsCampaignBehaviorInterface(
            sessionProvider,
            objectManager.Object,
            playerManager,
            Mock.Of<IIssueOwnerResolver>(),
            Mock.Of<INetwork>());
    }

    public void Dispose()
    {
        foreach (var player in players)
        {
            playerManager.RemovePlayer(player);
        }
        ModInformation.IsServer = previousServer;
    }

    [Fact]
    public void OnUnitRecruited_RecordsOnlyForThatPlayerOnTheServer()
    {
        var firstHero = AddPlayer("first", "firstHero");
        AddPlayer("second", "secondHero");
        var nonPlayerHero = ObjectHelper.SkipConstructor<Hero>();

        ModInformation.IsServer = true;
        statisticsInterface.OnUnitRecruited(firstHero, 3);
        statisticsInterface.OnUnitRecruited(nonPlayerHero, 5);

        ModInformation.IsServer = false;
        statisticsInterface.OnUnitRecruited(firstHero, 7);

        Assert.Equal(3, PlayerStatistics["firstHero"].NumberOfTroopsRecruited);
        Assert.Equal(0, PlayerStatistics["secondHero"].NumberOfTroopsRecruited);
        Assert.Equal(2, PlayerStatistics.Count);
    }

    [Fact]
    public void OnMissionStatisticsReceived_AddsKillsAndCompanionKills()
    {
        var hero = AddPlayer("first", "firstHero");
        PlayerStatistics["firstHero"].CompanionData["companion"] = new CompanionStatisticsData { IssuesSolved = 2, Kills = 1 };

        ModInformation.IsServer = true;
        statisticsInterface.OnMissionStatisticsReceived(hero, new NetworkMissionStatistics("firstHero", 4, 6, new[] { "companion" }, new[] { 3 }));
        // A mission without companion kills arrives with null arrays
        statisticsInterface.OnMissionStatisticsReceived(hero, Serializer.DeepClone(new NetworkMissionStatistics("firstHero", 1, 0, Array.Empty<string>(), Array.Empty<int>())));

        var statistics = PlayerStatistics["firstHero"];
        Assert.Equal(5, statistics.NumberOfTroopsKnockedOrKilledByPlayer);
        Assert.Equal(6, statistics.NumberOfTroopsKnockedOrKilledAsParty);
        Assert.Equal(2, statistics.CompanionData["companion"].IssuesSolved);
        Assert.Equal(4, statistics.CompanionData["companion"].Kills);
    }

    [Fact]
    public void OnHeroKilled_TwoPlayersInOneBattle_CountsTheHeroDeathOnceForTheKiller()
    {
        var firstHero = AddPlayer("first", "firstHero");
        var secondHero = AddPlayer("second", "secondHero");
        var partyComponent = ObjectHelper.SkipConstructor<LordPartyComponent>();
        partyComponent._leader = firstHero;
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party._partyComponent = partyComponent;
        firstHero._partyBelongedTo = party;

        ModInformation.IsServer = true;
        statisticsInterface.OnHeroKilled(firstHero, KillCharacterAction.KillCharacterActionDetail.DiedInBattle);
        // Both players report their mission kills after the battle
        statisticsInterface.OnMissionStatisticsReceived(firstHero, new NetworkMissionStatistics("firstHero", 1, 0, null, null));
        statisticsInterface.OnMissionStatisticsReceived(secondHero, new NetworkMissionStatistics("secondHero", 1, 0, null, null));

        Assert.Equal(1, PlayerStatistics["firstHero"].NumberOfHeroesKilledInBattle);
        Assert.Equal(0, PlayerStatistics["secondHero"].NumberOfHeroesKilledInBattle);
    }

    [Fact]
    public void MigratePlayerData_MovesStatisticsToTheHeir()
    {
        var originalHero = ObjectHelper.SkipConstructor<Hero>();
        var heir = ObjectHelper.SkipConstructor<Hero>();
        var originalHeroId = "originalHero";
        var heirId = "heir";
        objectManager.Setup(o => o.TryGetIdWithLogging(originalHero, out originalHeroId)).Returns(true);
        objectManager.Setup(o => o.TryGetIdWithLogging(heir, out heirId)).Returns(true);
        var statistics = new StatisticsData { NumberOfBattlesWon = 4 };
        PlayerStatistics[originalHeroId] = statistics;

        var migrator = new CoopSessionMigrator(sessionProvider, objectManager.Object, Mock.Of<INetwork>());
        migrator.MigratePlayerData(originalHero, heir);

        Assert.False(PlayerStatistics.ContainsKey(originalHeroId));
        Assert.Same(statistics, PlayerStatistics[heirId]);
    }

    [Fact]
    public void NetworkClientGameOver_RoundTripsStatistics()
    {
        var statistics = new StatisticsData
        {
            HighestTournamentRank = 2,
            TotalTimePlayedInSeconds = 3600,
            TimeSpentAsPrisoner = 1234,
            DenarsEarnedFromWorkshops = 500,
            MostExpensiveItemCraftedName = "Sword",
            MostExpensiveItemCraftedValue = 900,
        };
        statistics.CompanionData["companion"] = new CompanionStatisticsData { IssuesSolved = 1, Kills = 2 };

        var message = Serializer.DeepClone(new NetworkClientGameOver("hero", statistics: statistics));

        Assert.Equal(2, message.Statistics.HighestTournamentRank);
        Assert.Equal(3600ul, message.Statistics.TotalTimePlayedInSeconds);
        Assert.Equal(1234, message.Statistics.TimeSpentAsPrisoner);
        Assert.Equal(500ul, message.Statistics.DenarsEarnedFromWorkshops);
        Assert.Equal("Sword", message.Statistics.MostExpensiveItemCraftedName);
        Assert.Equal(900, message.Statistics.MostExpensiveItemCraftedValue);
        Assert.Equal(2, message.Statistics.CompanionData["companion"].Kills);
    }

    private Hero AddPlayer(string controllerId, string heroId)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        objectManager.Setup(o => o.TryGetObjectWithLogging<Hero>(heroId, out hero)).Returns(true);
        objectManager.Setup(o => o.TryGetIdWithLogging(hero, out heroId)).Returns(true);

        var player = new Player(controllerId, heroId, "", "", "");
        Assert.True(playerManager.AddPlayer(player));
        players.Add(player);

        // Seeded so a test never creates an entry, which reads the campaign's tournament leaderboard
        PlayerStatistics[heroId] = new StatisticsData();
        return hero;
    }
}
