using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class SmugglersTraitProgressTests : SyncTestBase
{
    public SmugglersTraitProgressTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailurePreservesPriorXpOtherPlayerAndServerPoolAcrossRepeatedInitialization(bool previousServerProgress)
    {
        var firstClient = Clients.First();
        var secondClient = Clients.Last();
        var first = CreatePlayer(firstClient, "smugglers-first", -975, 25);
        var second = CreatePlayer(secondClient, "smugglers-second", 375, 500);
        var initialHonor = previousServerProgress ? -990 : -975;
        PropertyOwner<PropertyObject> serverPool = null;
        Server.Call(() =>
        {
            serverPool = Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();
            serverPool.SetPropertyValue(DefaultTraits.Honor, 4500);
            if (previousServerProgress)
            {
                var progress = new PropertyOwner<PropertyObject>();
                progress.SetPropertyValue(DefaultTraits.Honor, initialHonor);
                GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.Set(Get<Hero>(Server, first.HeroId), progress);
            }
        });
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;

        firstClient.Call(() => firstClient.Resolve<QuestTraitProgressHandler>().Begin());
        secondClient.Call(() => secondClient.Resolve<QuestTraitProgressHandler>().Begin());
        Drain();

        Server.Call(() =>
        {
            var hero = Get<Hero>(Server, first.HeroId);
            using (new MainHeroSubstitutionScope(hero, Get<MobileParty>(Server, first.MobilePartyId)))
                TraitLevelingHelper.OnIssueFailed(hero, new[]
                {
                    Tuple.Create(DefaultTraits.Honor, -50),
                    Tuple.Create(DefaultTraits.Valor, -50),
                });
            Assert.Same(serverPool, Campaign.Current.PlayerTraitDeveloper);
            Assert.Equal(4500, serverPool.GetPropertyValue(DefaultTraits.Honor));
        });
        Drain();
        AssertPlayer(firstClient, first, initialHonor - 50, -25);
        AssertPlayer(secondClient, second, 375, 500);
        foreach (var instance in Clients.Prepend(Server))
            instance.Call(() => Assert.Equal(-1, Get<Hero>(instance, first.HeroId).GetTraitLevel(DefaultTraits.Honor)));

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(firstClient.NetPeer,
            new RequestQuestTraitProgress(first.HeroId, new Dictionary<string, int>
            {
                [DefaultTraits.Honor.StringId] = -975,
                [DefaultTraits.Valor.StringId] = 25,
            })));
        Drain();
        AssertPlayer(firstClient, first, initialHonor - 50, -25);

        firstClient.Call(() => TraitLevelingHelper.OnIssueSolvedThroughQuest(Hero.MainHero, DefaultTraits.Honor, 30));
        Drain();
        AssertPlayer(firstClient, first, initialHonor - 20, -25);
        AssertPlayer(secondClient, second, 375, 500);

        firstClient.Call(() =>
        {
            Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();
            firstClient.Resolve<QuestTraitProgressHandler>().RestoreLocalProgress();
        });
        AssertPlayer(firstClient, first, initialHonor - 20, -25);
    }

    private void Drain()
    {
        Server.PumpGameThread();
        TestEnvironment.FlushCoalescer();
        foreach (var client in Clients) client.PumpGameThread();
    }

    private static void AssertPlayer(EnvironmentInstance client, Player player, int honor, int valor)
    {
        client.Call(() =>
        {
            Assert.Same(Get<Hero>(client, player.HeroId), Hero.MainHero);
            Assert.Equal(honor, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(valor, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Valor));
        });
    }

    private Player CreatePlayer(EnvironmentInstance client, string controllerId, int honor, int valor)
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Player player = null;
        Server.Call(() =>
        {
            var hero = Get<MobileParty>(Server, partyId).LeaderHero;
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            player = new Player(controllerId, heroId, partyId, clanId, characterId);
        });
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var hero = Get<Hero>(instance, player.HeroId);
                hero.Clan._banner = new Banner();
                hero.SetTraitLevel(DefaultTraits.Honor, 0);
                hero.SetTraitLevel(DefaultTraits.Valor, 0);
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player(
                    controllerId, player.HeroId, partyId, player.ClanId, player.CharacterObjectId)));
                if (instance != client) return;
                instance.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
                Game.Current.PlayerTroop = hero.CharacterObject;
                Campaign.Current.MainParty = Get<MobileParty>(instance, partyId);
                Campaign.Current.PlayerDefaultFaction = hero.Clan;
                Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();
                Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, honor);
                Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Valor, valor);
            });
        }
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        return player;
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
