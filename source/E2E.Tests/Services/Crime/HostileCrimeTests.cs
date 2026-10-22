using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Crime;

public class HostileCrimeTests : SyncTestBase
{
    public HostileCrimeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CaravanCoercion_KeepsDifferentPlayerTraitHistoriesSeparate()
    {
        using var notifications = new MethodCallRecorder(
            AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));
        var first = CreatePlayer(Clients.First(), "first", 1);
        var second = CreatePlayer(Clients.Last(), "second", -1);
        var progress = new Dictionary<EnvironmentInstance, PropertyOwner<PropertyObject>>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var history = new PropertyOwner<PropertyObject>();
                var sign = instance == Clients.Last() ? -1 : 1;
                history.SetPropertyValue(DefaultTraits.Honor, sign * 1500);
                history.SetPropertyValue(DefaultTraits.Mercy, sign * 1750);
                Campaign.Current.PlayerTraitDeveloper = history;
                progress.Add(instance, history);
            });
        }

        MobileParty caravan = null;
        string factionId = null;
        Server.Call(() =>
        {
            var owner = GameObjectCreator.CreateInitializedObject<Hero>();
            owner.Clan.SetLeader(owner);
            owner.Clan._banner = new Banner();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            caravan = CaravanPartyComponent.CreateCaravanParty(owner, settlement, template, caravanLeader: owner);
            Assert.True(caravan.IsCaravan);
            Assert.True(Server.ObjectManager.TryGetId(caravan.MapFaction, out factionId));
        }, new MethodBase[] { AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyForParty)) });

        Server.PumpGameThread();
        TestEnvironment.FlushCoalescer();
        foreach (var client in Clients) client.PumpGameThread();
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;

        Coerce(first);
        AssertState(10f, 0f);
        Coerce(second);
        AssertState(10f, 10f);
        Coerce(first);
        AssertState(20f, 10f);

        void Coerce(Player player)
        {
            Server.Call(() =>
            {
                var party = Get<MobileParty>(Server, player.MobilePartyId);
                Assert.False(FactionManager.IsAtWarAgainstFaction(party.MapFaction, caravan.MapFaction));
                BeHostileAction.ApplyMinorCoercionHostileAction(party.Party, caravan.Party);
            });
            TestEnvironment.FlushCoalescer();
            foreach (var client in Clients) client.PumpGameThread();
        }

        void AssertState(float firstCrime, float secondCrime)
        {
            foreach (var instance in Clients.Prepend(Server))
            {
                instance.Call(() =>
                {
                    var players = instance.Resolve<IPlayerManager>();
                    Assert.True(players.TryGetPlayer(first.ControllerId, out var localFirst));
                    Assert.True(players.TryGetPlayer(second.ControllerId, out var localSecond));
                    Assert.Equal(firstCrime, localFirst.CrimeRatings.TryGetValue(factionId, out var firstRating) ? firstRating : 0f);
                    Assert.Equal(secondCrime, localSecond.CrimeRatings.TryGetValue(factionId, out var secondRating) ? secondRating : 0f);
                    Assert.Equal(1, Get<Hero>(instance, first.HeroId).GetTraitLevel(DefaultTraits.Honor));
                    Assert.Equal(1, Get<Hero>(instance, first.HeroId).GetTraitLevel(DefaultTraits.Mercy));
                    Assert.Equal(-1, Get<Hero>(instance, second.HeroId).GetTraitLevel(DefaultTraits.Honor));
                    Assert.Equal(-1, Get<Hero>(instance, second.HeroId).GetTraitLevel(DefaultTraits.Mercy));
                    Assert.Same(progress[instance], Campaign.Current.PlayerTraitDeveloper);
                    var sign = instance == Clients.Last() ? -1 : 1;
                    Assert.Equal(sign * 1500, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
                    Assert.Equal(sign * 1750, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Mercy));
                });
            }
        }
    }

    private Player CreatePlayer(EnvironmentInstance client, string controllerId, int traitLevel)
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
                hero.SetTraitLevel(DefaultTraits.Honor, traitLevel);
                hero.SetTraitLevel(DefaultTraits.Mercy, traitLevel);
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player(
                    controllerId, player.HeroId, partyId, player.ClanId, player.CharacterObjectId)));
                if (instance == client)
                {
                    instance.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
                    Game.Current.PlayerTroop = hero.CharacterObject;
                    Campaign.Current.MainParty = Get<MobileParty>(instance, partyId);
                    Campaign.Current.PlayerDefaultFaction = hero.Clan;
                }
            });
        }
        return player;
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
