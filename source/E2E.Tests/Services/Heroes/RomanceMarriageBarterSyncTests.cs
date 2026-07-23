using Common.Util;
using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Barters;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class RomanceMarriageBarterSyncTests : MapEventTestBase
{
    public RomanceMarriageBarterSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void MarriageBarterResult_WithoutMatchingRequest_DoesNotChangePlayerGold()
    {
        const int authoritativeGold = 640;
        var client = Clients.First();

        client.Call(() =>
        {
            using (new AllowedThread())
                Hero.MainHero.Gold = 1000;
        });

        client.SimulateMessage(
            Server.NetPeer,
            new NetworkMarriageBarterResult(
                "counterparty-hero",
                "hero-being-proposed-to",
                "proposing-hero",
                true,
                authoritativeGold));

        client.Call(() => Assert.Equal(1000, Hero.MainHero.Gold));
    }

    [Fact]
    public void MarriageBarterPresentation_AppliesAuthoritativePlayerGoldImmediately()
    {
        const int authoritativeGold = 640;
        var client = Clients.First();

        client.Call(() =>
        {
            using (new AllowedThread())
                Hero.MainHero.Gold = 1000;

            client.Resolve<IBarterClientPresentation>().SynchronizeMainHeroGold(authoritativeGold);
            Assert.Equal(authoritativeGold, Hero.MainHero.Gold);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PersonalMarriageBarter_ActiveMapConversation_AppliesPaymentAndExactSpouses(bool selfArranged)
    {
        const int initialPlayerGold = 1_000_000;
        const int initialCounterpartyGold = 50;
        const int offeredGold = 500_000;

        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var counterparty = CreatePartyWithRegisteredLeader();
        var spouseId = TestEnvironment.CreateRegisteredObject<Hero>();

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);

        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(counterparty.HeroId, out var counterpartyHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(spouseId, out var spouse));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(counterparty.MobilePartyId, out var counterpartyParty));

            playerHero.SetBirthDay(CampaignTime.YearsFromNow(-30f));
            spouse.SetBirthDay(CampaignTime.YearsFromNow(-25f));
            playerHero.Occupation = Occupation.Lord;
            spouse.Occupation = Occupation.Lord;
            spouse.IsFemale = !playerHero.IsFemale;
            spouse.Clan = counterpartyHero.Clan;
            playerHero.Gold = initialPlayerGold;
            counterpartyHero.Gold = initialCounterpartyGold;
            Assert.True(
                Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(playerHero, spouse),
                $"PlayerCanMarry={playerHero.CanMarry()}, SpouseCanMarry={spouse.CanMarry()}, " +
                $"PlayerLord={playerHero.IsLord}, SpouseLord={spouse.IsLord}, " +
                $"PlayerActive={playerHero.IsActive}, SpouseActive={spouse.IsActive}, " +
                $"PlayerAge={playerHero.Age}, SpouseAge={spouse.Age}, " +
                $"PlayerLeader={playerHero.Clan?.Leader == playerHero}, SpouseLeader={spouse.Clan?.Leader == spouse}, " +
                $"SameGender={playerHero.IsFemale == spouse.IsFemale}");
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                player.PartyId,
                counterpartyParty,
                counterparty.PartyId,
                engagerIsDefender: true));
        });
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(spouseId, out var spouse));
            var romanceLevel = selfArranged
                ? Romance.RomanceLevelEnum.MatchMadeByFamily
                : Romance.RomanceLevelEnum.CoupleAgreedOnMarriage;
            Romance.SetRomanticState(
                playerHero,
                spouse,
                romanceLevel);
            Assert.Equal(romanceLevel, Romance.GetRomanticLevel(playerHero, spouse));
            Assert.False(BarterManager.Instance.LastBarterIsAccepted);
        });
        Server.NetworkSentMessages.Clear();

        var heroBeingProposedToId = selfArranged ? player.HeroId : spouseId;
        var proposingHeroId = selfArranged ? spouseId : player.HeroId;
        var requestId = Guid.NewGuid().ToString("N");

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeMarriageBarter(
            requestId,
            counterparty.HeroId,
            MarriageConversationContext.MapParty,
            counterparty.PartyId,
            heroBeingProposedToId,
            proposingHeroId)));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMarriageBarterResult>());

        Server.Call(() => ConversationPartyHold.EndEngagement(
            Server.Resolve<ConversationPartyTracker>(),
            client.NetPeer));

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestMarriageBarter(
            counterparty.HeroId,
            MarriageConversationContext.MapParty,
            counterparty.PartyId,
            heroBeingProposedToId,
            proposingHeroId,
            new[]
            {
                new MarriageBarterTerm(
                    MarriageBarterTermType.Gold,
                    player.HeroId,
                    objectId: null,
                    itemModifierId: null,
                    itemModifierNull: true,
                    amount: offeredGold),
            },
            requestId)));
        TestEnvironment.FlushCoalescer();

        var result = Server.NetworkSentMessages.GetMessages<NetworkMarriageBarterResult>().Single();
        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(heroBeingProposedToId, result.HeroBeingProposedToId);
        Assert.Equal(proposingHeroId, result.ProposingHeroId);
        Assert.Equal(requestId, result.RequestId);
        Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);

        Server.Call(() => Assert.False(BarterManager.Instance.LastBarterIsAccepted));

        AssertMarriageAndGold(
            Server,
            player.HeroId,
            spouseId,
            counterparty.HeroId,
            initialPlayerGold - offeredGold,
            initialCounterpartyGold + offeredGold,
            assertMainHero: true);
        foreach (var environmentClient in Clients)
        {
            AssertMarriageAndGold(
                environmentClient,
                player.HeroId,
                spouseId,
                counterparty.HeroId,
                initialPlayerGold - offeredGold,
                initialCounterpartyGold + offeredGold,
                assertMainHero: true);
        }
    }

    [Fact]
    public void ArrangedMarriageBarter_ClanRelatives_AppliesPaymentAndExactSpouses()
    {
        const int initialPlayerGold = 1_000_000;
        const int initialCounterpartyGold = 75;
        const int offeredGold = 500_000;

        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var counterparty = CreatePartyWithRegisteredLeader();
        var playerRelativeId = TestEnvironment.CreateRegisteredObject<Hero>();
        var counterpartyRelativeId = TestEnvironment.CreateRegisteredObject<Hero>();

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);

        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(counterparty.HeroId, out var counterpartyHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerRelativeId, out var playerRelative));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(counterpartyRelativeId, out var counterpartyRelative));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(counterparty.MobilePartyId, out var counterpartyParty));

            playerRelative.SetBirthDay(CampaignTime.YearsFromNow(-28f));
            counterpartyRelative.SetBirthDay(CampaignTime.YearsFromNow(-24f));
            playerRelative.Occupation = Occupation.Lord;
            counterpartyRelative.Occupation = Occupation.Lord;
            playerRelative.IsFemale = false;
            counterpartyRelative.IsFemale = true;
            playerRelative.Clan = playerHero.Clan;
            counterpartyRelative.Clan = counterpartyHero.Clan;
            playerHero.Gold = initialPlayerGold;
            counterpartyHero.Gold = initialCounterpartyGold;

            Assert.True(Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(
                playerRelative,
                counterpartyRelative));
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                player.PartyId,
                counterpartyParty,
                counterparty.PartyId,
                engagerIsDefender: true));
        });
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerRelativeId, out var playerRelative));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(counterpartyRelativeId, out var counterpartyRelative));
            Romance.SetRomanticState(
                playerRelative,
                counterpartyRelative,
                Romance.RomanceLevelEnum.MatchMadeByFamily);
            Assert.False(BarterManager.Instance.LastBarterIsAccepted);
        });
        Server.NetworkSentMessages.Clear();

        var requestId = Guid.NewGuid().ToString("N");
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeMarriageBarter(
            requestId,
            counterparty.HeroId,
            MarriageConversationContext.MapParty,
            counterparty.PartyId,
            playerRelativeId,
            counterpartyRelativeId)));

        Server.Call(() => ConversationPartyHold.EndEngagement(
            Server.Resolve<ConversationPartyTracker>(),
            client.NetPeer));

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestMarriageBarter(
            counterparty.HeroId,
            MarriageConversationContext.MapParty,
            counterparty.PartyId,
            playerRelativeId,
            counterpartyRelativeId,
            new[]
            {
                new MarriageBarterTerm(
                    MarriageBarterTermType.Gold,
                    player.HeroId,
                    objectId: null,
                    itemModifierId: null,
                    itemModifierNull: true,
                    amount: offeredGold),
            },
            requestId)));
        TestEnvironment.FlushCoalescer();

        var result = Server.NetworkSentMessages.GetMessages<NetworkMarriageBarterResult>().Single();
        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(requestId, result.RequestId);
        Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);
        Server.Call(() => Assert.False(BarterManager.Instance.LastBarterIsAccepted));

        foreach (var instance in Clients.Prepend(Server))
        {
            AssertArrangedMarriageAndGold(
                instance,
                player.HeroId,
                playerRelativeId,
                counterpartyRelativeId,
                counterparty.HeroId,
                initialPlayerGold - offeredGold,
                initialCounterpartyGold + offeredGold);
        }
    }

    [Fact]
    public void MarriageBarter_DifferentActiveConversation_RejectsWithoutEffects()
    {
        const int initialPlayerGold = 1_000;
        const int initialCounterpartyGold = 25;

        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var activeCounterparty = CreatePartyWithRegisteredLeader();
        var requestedCounterparty = CreatePartyWithRegisteredLeader();
        var proposedSpouseId = TestEnvironment.CreateRegisteredObject<Hero>();

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(requestedCounterparty.HeroId, out var requestedHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(activeCounterparty.MobilePartyId, out var activeParty));
            playerHero.Gold = initialPlayerGold;
            requestedHero.Gold = initialCounterpartyGold;

            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                player.PartyId,
                activeParty,
                activeCounterparty.PartyId,
                engagerIsDefender: true));
        });
        TestEnvironment.FlushCoalescer();
        Server.NetworkSentMessages.Clear();

        var requestId = Guid.NewGuid().ToString("N");
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeMarriageBarter(
            requestId,
            requestedCounterparty.HeroId,
            MarriageConversationContext.MapParty,
            requestedCounterparty.PartyId,
            player.HeroId,
            proposedSpouseId)));

        Server.Call(() => ConversationPartyHold.EndEngagement(
            Server.Resolve<ConversationPartyTracker>(),
            client.NetPeer));

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestMarriageBarter(
            requestedCounterparty.HeroId,
            MarriageConversationContext.MapParty,
            requestedCounterparty.PartyId,
            player.HeroId,
            proposedSpouseId,
            new[]
            {
                new MarriageBarterTerm(
                    MarriageBarterTermType.Gold,
                    player.HeroId,
                    objectId: null,
                    itemModifierId: null,
                    itemModifierNull: true,
                    amount: 500),
            },
            requestId)));

        var result = Server.NetworkSentMessages.GetMessages<NetworkMarriageBarterResult>().Single();
        Assert.False(result.Accepted);
        Assert.Equal(requestId, result.RequestId);
        Assert.Equal(initialPlayerGold, result.PlayerGold);
        Assert.Contains("authorized", result.Reason, StringComparison.OrdinalIgnoreCase);

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(requestedCounterparty.HeroId, out var requestedHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(proposedSpouseId, out var proposedSpouse));
                Assert.Equal(initialPlayerGold, playerHero.Gold);
                Assert.Equal(initialCounterpartyGold, requestedHero.Gold);
                Assert.Null(playerHero.Spouse);
                Assert.Null(proposedSpouse.Spouse);
            });
        }
    }

    private (string HeroId, string MobilePartyId, string PartyId) CreatePartyWithRegisteredLeader()
    {
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string? heroId = null;
        string? partyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.NotNull(party.LeaderHero);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyId));
        });

        Assert.NotNull(heroId);
        Assert.NotNull(partyId);
        return (heroId!, mobilePartyId, partyId!);
    }

    private void RegisterPlayer(EnvironmentInstance client, string heroId, string mobilePartyId)
    {
        const string controllerId = "PlayerOne";
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        RegisterAsPlayerParty(controllerId, heroId, mobilePartyId);
        Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer);
    }

    private void SetMainHero(string heroId)
    {
        void Set(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Game.Current.PlayerTroop = hero.CharacterObject;
                Assert.Same(hero, Hero.MainHero);
            });
        }

        Set(Server);
        foreach (var client in Clients)
            Set(client);
    }

    private static void AssertMarriageAndGold(
        EnvironmentInstance instance,
        string playerHeroId,
        string spouseId,
        string counterpartyHeroId,
        int expectedPlayerGold,
        int expectedCounterpartyGold,
        bool assertMainHero)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(spouseId, out var spouse));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(counterpartyHeroId, out var counterpartyHero));
            Assert.Same(spouse, playerHero.Spouse);
            Assert.Same(playerHero, spouse.Spouse);
            Assert.Equal(expectedPlayerGold, playerHero.Gold);
            Assert.Equal(expectedCounterpartyGold, counterpartyHero.Gold);
            if (assertMainHero)
            {
                Assert.Same(playerHero, Hero.MainHero);
                Assert.Equal(expectedPlayerGold, Hero.MainHero.Gold);
            }
        });
    }

    private static void AssertArrangedMarriageAndGold(
        EnvironmentInstance instance,
        string playerHeroId,
        string playerRelativeId,
        string counterpartyRelativeId,
        string counterpartyHeroId,
        int expectedPlayerGold,
        int expectedCounterpartyGold)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(playerRelativeId, out var playerRelative));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(counterpartyRelativeId, out var counterpartyRelative));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(counterpartyHeroId, out var counterpartyHero));
            Assert.Same(counterpartyRelative, playerRelative.Spouse);
            Assert.Same(playerRelative, counterpartyRelative.Spouse);
            Assert.Same(playerHero, Hero.MainHero);
            Assert.Equal(expectedPlayerGold, playerHero.Gold);
            Assert.Equal(expectedCounterpartyGold, counterpartyHero.Gold);
        });
    }
}
