using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Clans.Data;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

/// <summary>
/// The v1.5 party commands (join armies, raid, donate troops, fleet) are hero flags the server owns.
/// A client toggle is a request, and every server change, the vanilla resets included, reaches clients.
/// </summary>
public class PartyConfigurationSyncTests : SyncTestBase
{
    public PartyConfigurationSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ClientToggle_ReachesServerAndEveryClient()
    {
        var leaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        var client = Clients.First();

        client.Call(() => client.Resolve<IMessageBroker>().Publish(
            this,
            new PartyConfigurationChangedOnSelection(
                client.GetRegisteredObject<Hero>(leaderId),
                PartyConfigurationFlag.CanRaid,
                false)));

        foreach (var instance in Clients.Prepend(Server))
            instance.Call(() => Assert.False(instance.GetRegisteredObject<Hero>(leaderId).CanRaid));
    }

    [Fact]
    public void ClientToggle_ForAPlayerHero_IsRejected()
    {
        var playerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var client = Clients.First();
        AddPlayer(playerHeroId);

        client.Call(() => client.Resolve<IMessageBroker>().Publish(
            this,
            new PartyConfigurationChangedOnSelection(
                client.GetRegisteredObject<Hero>(playerHeroId),
                PartyConfigurationFlag.CanJoinArmy,
                false)));

        foreach (var instance in Clients.Prepend(Server))
            instance.Call(() => Assert.True(instance.GetRegisteredObject<Hero>(playerHeroId).CanJoinArmy));
    }

    [Fact]
    public void CompanionRemoved_ResetsCommandsOnEveryClient()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        SetAllFlagsOnServer(heroId, false);
        AssertAllFlags(heroId, false);

        Server.Call(() => new PartyConfigurationCampaignBehavior().OnCompanionRemoved(
            Server.GetRegisteredObject<Hero>(heroId),
            RemoveCompanionAction.RemoveCompanionDetail.AfterQuest));

        AssertAllFlags(heroId, true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HeroLeavingAClan_ResetsCommandsOnlyWhenAPlayerLedIt(bool playerClan)
    {
        var playerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddPlayer(playerHeroId);
        SetAllFlagsOnServer(heroId, false);

        Server.Call(() =>
        {
            var playerHero = Server.GetRegisteredObject<Hero>(playerHeroId);
            var hero = Server.GetRegisteredObject<Hero>(heroId);
            playerHero.Clan.SetLeader(playerHero);
            var clanLeft = playerClan ? playerHero.Clan : hero.Clan;

            // The event passes the clan the hero left.
            new PartyConfigurationCampaignBehavior().OnHeroChangedClan(hero, clanLeft);
        });

        AssertAllFlags(heroId, playerClan);
    }

    private void AddPlayer(string heroId)
    {
        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
            new Player("PlayerOne", heroId, string.Empty, string.Empty, string.Empty))));
    }

    private void SetAllFlagsOnServer(string heroId, bool value)
    {
        Server.Call(() =>
        {
            var hero = Server.GetRegisteredObject<Hero>(heroId);
            foreach (PartyConfigurationFlag flag in Enum.GetValues(typeof(PartyConfigurationFlag)))
                PartyConfigurationFlags.Set(hero, flag, value);
        });
    }

    private void AssertAllFlags(string heroId, bool expected)
    {
        foreach (EnvironmentInstance instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var hero = instance.GetRegisteredObject<Hero>(heroId);
                foreach (PartyConfigurationFlag flag in Enum.GetValues(typeof(PartyConfigurationFlag)))
                    Assert.Equal(expected, PartyConfigurationFlags.Get(hero, flag));
            });
        }
    }
}
