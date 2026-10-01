using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Crime;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Crime;

public class CrimePaymentTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly MethodCallRecorder menu;
    private readonly MethodCallRecorder presentation;
    private readonly MethodCallRecorder notifications;
    private readonly Player payer;
    private readonly Player other;
    private readonly string settlementId;
    private readonly string factionId;
    private int fine;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Client => environment.Clients.First();
    private IEnumerable<EnvironmentInstance> Instances => environment.Clients.Prepend(Server);

    public CrimePaymentTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        menu = new MethodCallRecorder(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)));
        // Keep native presentation and unrelated skill progression outside this payment regression.
        presentation = new MethodCallRecorder(
            AccessTools.Method(typeof(SkillLevelingManager), nameof(SkillLevelingManager.OnBribeGiven)),
            AccessTools.Method(typeof(GameMenuManager), nameof(GameMenuManager.RefreshMenuOptionConditions)));
        notifications = new MethodCallRecorder(AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));
        payer = CreatePlayer(Client, "payer");
        other = CreatePlayer(environment.Clients.Last(), "other");
        settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        factionId = environment.CreateRegisteredObject<Clan>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var settlement = Get<Settlement>(instance, settlementId);
                var town = Get<Town>(instance, townId);
                settlement.SetSettlementComponent(town);
                town._ownerClan = Get<Clan>(instance, factionId);
                Assert.Same(Get<Clan>(instance, factionId), settlement.MapFaction);
                Get<MobileParty>(instance, payer.MobilePartyId).CurrentSettlement = settlement;
                Get<MobileParty>(instance, other.MobilePartyId).CurrentSettlement = settlement;
                var players = instance.Resolve<IPlayerManager>();
                Assert.True(players.TryGetPlayer(payer.ControllerId, out var localPayer));
                Assert.True(players.TryGetPlayer(other.ControllerId, out var localOther));
                localPayer.CrimeRatings[factionId] = 50f;
                localOther.CrimeRatings[factionId] = 45f;
            });
        }
        Client.Call(() =>
        {
            fine = (int)PayForCrimeAction.GetClearCrimeCost(Get<Clan>(Client, factionId), CrimeModel.PaymentMethod.Gold);
            Assert.InRange(fine, 1, 9999);
            var states = Game.Current.GameStateManager;
            var map = states.CreateState<MapState>();
            states._gameStates.Add(map);
            map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
            map._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
            map._menuContext.GameMenu.StringId = "town_inside_criminal";
        });
        Flush();
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
    }

    [Fact]
    public void FineMenu_ChargesAuthenticatedPlayerAndReplicatesGoldAndCrime()
    {
        SelectFine();
        Assert.Equal(0, menu.Count);
        Flush();

        AssertState(10000 - fine, 25f);
        Assert.Equal(new[] { "town_outside" }, menu.MenusFor(Client));
        Assert.Equal(0, menu.CountFor(environment.Clients.Last()));
        Assert.Equal(1, notifications.CountFor(Client));
        Assert.Equal(0, notifications.CountFor(environment.Clients.Last()));
        Assert.Equal(0, notifications.CountFor(Server));
        Assert.True(Assert.Single(Client.InternalMessages.GetMessages<NetworkCrimePaymentResult>()).Accepted);
        Assert.Empty(Client.NetworkSentMessages.GetMessages<RequestCrimeRatingChange>());
    }

    [Fact]
    public void FineMenu_WithStaleClientGold_RejectsWithoutReducingCrime()
    {
        Server.Call(() => Get<Hero>(Server, payer.HeroId).Gold = fine - 1);
        // The client can still select the option before the authoritative gold update arrives.
        SelectFine();
        Flush();

        AssertState(fine - 1, 50f);
        Assert.Equal(0, menu.Count);
        Assert.Equal(0, notifications.Count);
        Assert.False(Assert.Single(Client.InternalMessages.GetMessages<NetworkCrimePaymentResult>()).Accepted);
    }

    [Fact]
    public void FineMenu_RecalculatesTheFineFromServerCrime()
    {
        var authoritativeFine = 0;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(payer.ControllerId, out var player));
            player.CrimeRatings[factionId] = 60f;
            var hero = Get<Hero>(Server, payer.HeroId);
            using var scope = new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo);
            authoritativeFine = (int)PayForCrimeAction.GetClearCrimeCost(Get<Clan>(Server, factionId), CrimeModel.PaymentMethod.Gold);
            Assert.True(authoritativeFine > fine);
        });
        SelectFine();
        Flush();
        AssertState(10000 - authoritativeFine, 25f);
    }

    [Fact]
    public void CrimeReductionRequest_WithoutPayment_LeavesGoldAndCrimeUnchanged()
    {
        Client.Call(() => Client.Resolve<INetwork>().SendAll(new RequestCrimeRatingChange(factionId, -25f, true)));
        Flush();
        AssertState(10000, 50f);
        Assert.Equal(0, notifications.Count);
    }

    [Fact]
    public void RepeatedFineRequest_ChargesOnlyOnce()
    {
        Client.Call(() =>
        {
            var faction = Get<Clan>(Client, factionId);
            PayForCrimeAction.Apply(faction, CrimeModel.PaymentMethod.Gold);
            PayForCrimeAction.Apply(faction, CrimeModel.PaymentMethod.Gold);
        });
        Flush();
        AssertState(10000 - fine, 25f);
        Assert.Equal(new[] { true, false }, Client.InternalMessages.GetMessages<NetworkCrimePaymentResult>().Select(result => result.Accepted));
    }

    [Fact]
    public void FineRequest_AfterLeavingSettlement_IsRejected()
    {
        Server.Call(() => Get<MobileParty>(Server, payer.MobilePartyId).CurrentSettlement = null);
        SelectFine();
        Flush();
        AssertState(10000, 50f);
        Assert.False(Assert.Single(Client.InternalMessages.GetMessages<NetworkCrimePaymentResult>()).Accepted);
    }

    [Fact]
    public void FineRequest_WithoutAuthenticatedPlayer_IsIgnored()
    {
        Server.Call(() => Server.Resolve<IPlayerManager>().ClearPeer(Client.NetPeer));
        SelectFine();
        Flush();
        AssertState(10000, 50f);
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkCrimePaymentResult>());
    }

    private void SelectFine()
        => Client.Call(() => CrimeCampaignBehavior.criminal_inside_menu_give_money_on_consequence(null));

    private void AssertState(int gold, float crime)
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Equal(gold, Get<Hero>(instance, payer.HeroId).Gold);
                Assert.Equal(10000, Get<Hero>(instance, other.HeroId).Gold);
                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(payer.ControllerId, out var localPayer));
                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(other.ControllerId, out var localOther));
                Assert.Equal(crime, localPayer.CrimeRatings[factionId]);
                Assert.Equal(45f, localOther.CrimeRatings[factionId]);
            });
        }
    }

    private Player CreatePlayer(EnvironmentInstance client, string controllerId)
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Player player = null;
        Server.Call(() =>
        {
            var hero = Get<MobileParty>(Server, partyId).LeaderHero;
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            player = new Player(controllerId, heroId, partyId, clanId, characterId);
        });
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var hero = Get<Hero>(instance, player.HeroId);
                hero.Gold = 10000;
                hero.Clan._banner = new Banner();
                hero.SetBirthDay(CampaignTime.YearsFromNow(-30));
                hero.Occupation = Occupation.Lord;
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player(
                    controllerId, player.HeroId, partyId, player.ClanId, player.CharacterObjectId)));
                if (instance == client)
                {
                    instance.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
                    Game.Current.PlayerTroop = hero.CharacterObject;
                    Campaign.Current.MainParty = Get<MobileParty>(instance, partyId);
                    Campaign.Current.PlayerDefaultFaction = hero.Clan;
                    Campaign.Current.PlayerEncounter = null;
                }
            });
        }
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer));
        return player;
    }

    private void Flush()
    {
        Server.PumpGameThread();
        environment.FlushCoalescer();
        foreach (var client in environment.Clients) client.PumpGameThread();
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    public void Dispose()
    {
        notifications.Dispose();
        presentation.Dispose();
        menu.Dispose();
        environment.Dispose();
    }
}
