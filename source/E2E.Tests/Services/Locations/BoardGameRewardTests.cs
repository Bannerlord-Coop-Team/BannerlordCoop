using Common.Network;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Locations.BoardGames.Messages;
using GameInterface.Services.Locations.BoardGames.Patches;
using GameInterface.Services.Players;
using Helpers;
using SandBox.CampaignBehaviors;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

public sealed class BoardGameRewardTests : MapEventTestBase
{
    public BoardGameRewardTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void TavernWin_ForwardsCapturedBetAndServerAppliesGold()
    {
        var client = Clients.First();
        var (heroId, _) = CreatePlayerHeroParty("BoardGameRewards");
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer("BoardGameRewards", client.NetPeer);
        }, MapEventDisabledMethods);
        Server.Call(() => Server.GetRegisteredObject<Hero>(heroId).Gold = 1000);

        client.Call(() =>
        {
            var behavior = new BoardGameCampaignBehavior();
            behavior._betAmount = 500;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._betAmount = 0; // vanilla SetBetAmount(0) at the end of OnPlayerBoardGameOver
            BoardGameRewardPatches.Postfix(behavior, null, BoardGameHelper.BoardGameState.Win, bet);
        });

        var message = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBoardGameTavernResult>());
        Assert.Equal(500, message.BetAmount);
        Assert.Equal(BoardGameHelper.BoardGameState.Win, message.GameOver);

        int gold = 0;
        Server.Call(() => gold = Server.GetRegisteredObject<Hero>(heroId).Gold);
        Assert.Equal(1500, gold);
    }

    [Fact]
    public void TavernLoss_ForwardsCapturedBetAndServerDeductsGold()
    {
        var client = Clients.First();
        var (heroId, _) = CreatePlayerHeroParty("BoardGameRewards");
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer("BoardGameRewards", client.NetPeer);
        }, MapEventDisabledMethods);
        Server.Call(() => Server.GetRegisteredObject<Hero>(heroId).Gold = 1000);

        client.Call(() =>
        {
            var behavior = new BoardGameCampaignBehavior();
            behavior._betAmount = 200;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._betAmount = 0;
            BoardGameRewardPatches.Postfix(behavior, null, BoardGameHelper.BoardGameState.Loss, bet);
        });

        var message = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBoardGameTavernResult>());
        Assert.Equal(200, message.BetAmount);
        Assert.Equal(BoardGameHelper.BoardGameState.Loss, message.GameOver);

        int gold = 0;
        Server.Call(() => gold = Server.GetRegisteredObject<Hero>(heroId).Gold);
        Assert.Equal(800, gold);
    }

    [Fact]
    public void TavernWin_ZeroBet_SendsNothing()
    {
        var client = Clients.First();

        client.Call(() =>
        {
            var behavior = new BoardGameCampaignBehavior();
            behavior._betAmount = 0;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            BoardGameRewardPatches.Postfix(behavior, null, BoardGameHelper.BoardGameState.Win, bet);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkBoardGameTavernResult>());
    }

    [Fact]
    public void TavernWin_OnServer_SendsNothing()
    {
        Server.Call(() =>
        {
            var behavior = new BoardGameCampaignBehavior();
            behavior._betAmount = 500;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._betAmount = 0;
            BoardGameRewardPatches.Postfix(behavior, null, BoardGameHelper.BoardGameState.Win, bet);
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBoardGameTavernResult>());
    }

    [Fact]
    public void TavernDraw_SendsNothing()
    {
        var client = Clients.First();

        client.Call(() =>
        {
            var behavior = new BoardGameCampaignBehavior();
            behavior._betAmount = 500;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._betAmount = 0;
            BoardGameRewardPatches.Postfix(behavior, null, BoardGameHelper.BoardGameState.Draw, bet);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkBoardGameTavernResult>());
    }

    [Fact]
    public void TavernWin_InvalidBet_ServerDropsGold()
    {
        var client = Clients.First();
        var (heroId, _) = CreatePlayerHeroParty("BoardGameRewards");
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer("BoardGameRewards", client.NetPeer);
        }, MapEventDisabledMethods);
        Server.Call(() => Server.GetRegisteredObject<Hero>(heroId).Gold = 1000);

        client.Call(() =>
        {
            var behavior = new BoardGameCampaignBehavior();
            behavior._betAmount = 50;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._betAmount = 0;
            BoardGameRewardPatches.Postfix(behavior, null, BoardGameHelper.BoardGameState.Win, bet);
        });

        // Patch forwards any positive bet; the server validates the vanilla set.
        var message = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBoardGameTavernResult>());
        Assert.Equal(50, message.BetAmount);

        int gold = 0;
        Server.Call(() => gold = Server.GetRegisteredObject<Hero>(heroId).Gold);
        Assert.Equal(1000, gold);
    }

    [Fact]
    public void LordLoss_SendsNothing()
    {
        var client = Clients.First();
        var opposingHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(opposingHeroId, out var opposing));
            var behavior = new BoardGameCampaignBehavior();
            behavior._difficulty = BoardGameHelper.AIDifficulty.Normal;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            BoardGameRewardPatches.Postfix(behavior, opposing, BoardGameHelper.BoardGameState.Loss, bet);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkBoardGameLordResult>());
    }

    [Fact]
    public void LordDraw_SendsNothing()
    {
        var client = Clients.First();
        var opposingHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(opposingHeroId, out var opposing));
            var behavior = new BoardGameCampaignBehavior();
            behavior._difficulty = BoardGameHelper.AIDifficulty.Normal;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            BoardGameRewardPatches.Postfix(behavior, opposing, BoardGameHelper.BoardGameState.Draw, bet);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkBoardGameLordResult>());
    }

    [Fact]
    public void LordWin_Normal_None_ForwardsAndAppliesStewardXp()
    {
        var client = Clients.First();
        var (heroId, _) = CreatePlayerHeroParty("BoardGameRewards");
        var opposingHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer("BoardGameRewards", client.NetPeer);
        }, MapEventDisabledMethods);

        float xpBefore = 0;
        Server.Call(() => xpBefore = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Steward));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(opposingHeroId, out var opposing));
            var behavior = new BoardGameCampaignBehavior();
            behavior._difficulty = BoardGameHelper.AIDifficulty.Normal;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._opposingHeroExtraXPGained = false;
            BoardGameRewardPatches.Postfix(behavior, opposing, BoardGameHelper.BoardGameState.Win, bet);
        });

        var message = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBoardGameLordResult>());
        Assert.Equal(BoardGameHelper.AIDifficulty.Normal, message.Difficulty);
        Assert.Equal(LordBoardGameReward.None, message.Reward);
        Assert.False(message.ExtraXp);

        float xpAfter = 0;
        Server.Call(() => xpAfter = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Steward));
        Assert.True(xpAfter > xpBefore);
    }

    [Fact]
    public void LordWin_Normal_Relation_ForwardsReward()
    {
        var client = Clients.First();
        var (heroId, _) = CreatePlayerHeroParty("BoardGameRewards");
        var opposingHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer("BoardGameRewards", client.NetPeer);
        }, MapEventDisabledMethods);

        float xpBefore = 0;
        Server.Call(() => xpBefore = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Steward));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(opposingHeroId, out var opposing));
            var behavior = new BoardGameCampaignBehavior();
            behavior._difficulty = BoardGameHelper.AIDifficulty.Normal;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._relationGained = true;
            behavior._opposingHeroExtraXPGained = false;
            BoardGameRewardPatches.Postfix(behavior, opposing, BoardGameHelper.BoardGameState.Win, bet);
        });

        var message = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBoardGameLordResult>());
        Assert.Equal(LordBoardGameReward.Relation, message.Reward);

        // Steward XP runs for every lord win regardless of reward, proving the
        // MainHero-substituted server apply ran without throwing.
        float xpAfter = 0;
        Server.Call(() => xpAfter = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Steward));
        Assert.True(xpAfter > xpBefore);
    }

    [Fact]
    public void LordWin_InfluenceOnNormal_Dropped()
    {
        var client = Clients.First();
        var (heroId, _) = CreatePlayerHeroParty("BoardGameRewards");
        var opposingHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer("BoardGameRewards", client.NetPeer);
        }, MapEventDisabledMethods);

        float xpBefore = 0;
        Server.Call(() => xpBefore = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Steward));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(opposingHeroId, out var opposing));
            var behavior = new BoardGameCampaignBehavior();
            behavior._difficulty = BoardGameHelper.AIDifficulty.Normal;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._influenceGained = true;
            behavior._opposingHeroExtraXPGained = false;
            BoardGameRewardPatches.Postfix(behavior, opposing, BoardGameHelper.BoardGameState.Win, bet);
        });

        var message = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBoardGameLordResult>());
        Assert.Equal(LordBoardGameReward.Influence, message.Reward);

        // Influence only exists on Hard; the server drops the whole message.
        float xpAfter = 0;
        Server.Call(() => xpAfter = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Steward));
        Assert.Equal(xpBefore, xpAfter);
    }

    [Fact]
    public void LordWin_ExtraXpOnHard_Dropped()
    {
        var client = Clients.First();
        var (heroId, _) = CreatePlayerHeroParty("BoardGameRewards");
        var opposingHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer("BoardGameRewards", client.NetPeer);
        }, MapEventDisabledMethods);

        float xpBefore = 0;
        Server.Call(() => xpBefore = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Steward));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(opposingHeroId, out var opposing));
            var behavior = new BoardGameCampaignBehavior();
            behavior._difficulty = BoardGameHelper.AIDifficulty.Hard;
            BoardGameRewardPatches.Prefix(behavior, out int bet);
            behavior._opposingHeroExtraXPGained = true;
            BoardGameRewardPatches.Postfix(behavior, opposing, BoardGameHelper.BoardGameState.Win, bet);
        });

        var message = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBoardGameLordResult>());
        Assert.True(message.ExtraXp);

        float xpAfter = 0;
        Server.Call(() => xpAfter = Server.GetRegisteredObject<Hero>(heroId).HeroDeveloper.GetSkillXp(DefaultSkills.Steward));
        Assert.Equal(xpBefore, xpAfter);
    }
}
