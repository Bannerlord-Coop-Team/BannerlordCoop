using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class LordBarterSyncTests : MapEventTestBase
{
    public LordBarterSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void GenericLordBarter_DuplicateRequest_AppliesPaymentOnceAndReplaysAcceptance()
    {
        const int initialPlayerGold = 1_000_000;
        const int initialTargetGold = 50;
        const int offeredGold = 500_000;
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var requestId = Guid.NewGuid().ToString("N");

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
            playerHero.Gold = initialPlayerGold;
            targetHero.Gold = initialTargetGold;
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                player.PartyId,
                targetParty,
                target.PartyId,
                engagerIsDefender: true));
        });
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestId,
            target.HeroId,
            PeaceConversationContext.MapParty,
            target.PartyId,
            LordBarterKind.Generic)));
        var request = new NetworkRequestLordBarter(
            target.HeroId,
            PeaceConversationContext.MapParty,
            target.PartyId,
            LordBarterKind.Generic,
            new[]
            {
                new PeaceBarterTerm(
                    PeaceBarterTermType.Gold,
                    player.HeroId,
                    null,
                    null,
                    true,
                    offeredGold),
            },
            requestId);

        client.Call(() => client.Resolve<INetwork>().SendAll(request));
        client.Call(() => client.Resolve<INetwork>().SendAll(request));
        TestEnvironment.FlushCoalescer();

        var results = Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>().ToList();
        Assert.Equal(2, results.Count);
        Assert.All(results, result =>
        {
            Assert.True(result.Accepted, result.Reason);
            Assert.Equal(requestId, result.RequestId);
            Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);
        });
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.Equal(initialPlayerGold - offeredGold, playerHero.Gold);
            Assert.Equal(initialTargetGold + offeredGold, targetHero.Gold);
        });
    }

    private (string HeroId, string MobilePartyId, string PartyId) CreatePartyWithRegisteredLeader()
    {
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string heroId = null;
        string partyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.NotNull(party.LeaderHero);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyId));
        });
        return (heroId, mobilePartyId, partyId);
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
            });
        }

        Set(Server);
        foreach (var client in Clients)
            Set(client);
    }
}
