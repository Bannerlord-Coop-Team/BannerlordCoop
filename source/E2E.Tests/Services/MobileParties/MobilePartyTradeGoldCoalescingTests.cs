using Common.Messaging;
using Common.Network.Coalescing;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

/// <summary>
/// End-to-end coverage for coalescing non-lord party trade-gold updates within one server tick.
/// </summary>
public class MobilePartyTradeGoldCoalescingTests : SyncTestBase
{
    private const string NetworkMessageName = "MobileParty_PartyTradeGold_SetNetworkMessage";

    private readonly string mobilePartyId;

    public MobilePartyTradeGoldCoalescingTests(ITestOutputHelper output) : base(output)
    {
        string? partyId = null;

        Server.Call(() =>
        {
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            var party = BanditPartyComponent.CreateBanditParty(
                "TradeGoldCoalescingParty",
                clan,
                hideout,
                false,
                template,
                new CampaignVec2(new Vec2(2, 2), true));

            Assert.False(party.IsLordParty);
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
        });

        mobilePartyId = Assert.IsType<string>(partyId);
    }

    [Fact]
    public void Server_MultipleNonLordPartyTradeGoldSetsInOneTick_SendsLatestOnly()
    {
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.False(party.IsLordParty);

            party.PartyTradeGold = 100;
            party.PartyTradeGold = 250;
            party.PartyTradeGold = 777;
        });

        Assert.True(Server.Resolve<ISendCoalescer>().HasPending);
        Assert.DoesNotContain(Server.NetworkSentMessages,
            message => message.GetType().Name == NetworkMessageName);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var clientParty));
            Assert.Equal(0, clientParty.PartyTradeGold);
        }

        TestEnvironment.FlushCoalescer();

        Assert.False(Server.Resolve<ISendCoalescer>().HasPending);
        Assert.Single(Server.NetworkSentMessages,
            message => message.GetType().Name == NetworkMessageName);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var clientParty));
            Assert.Equal(777, clientParty.PartyTradeGold);
        }
    }

    [Fact]
    public void Server_PartyDestroyedBeforeFlush_DropsPendingTradeGold()
    {
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));

            party.PartyTradeGold = 777;
            MessageBroker.Instance.Publish(party, new InstanceDestroyed<MobileParty>(party));
        });

        TestEnvironment.FlushCoalescer();

        Assert.DoesNotContain(Server.NetworkSentMessages,
            message => message.GetType().Name == NetworkMessageName);
        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out _));
        }
    }
}
