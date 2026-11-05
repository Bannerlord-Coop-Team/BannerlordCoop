using Common;
using Common.Tests.Utils;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Handlers;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using Moq;
using Serilog;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace Coop.Tests.Server.Services.SiegeEvents;

public sealed class ServerSiegeAftermathHandlerTests
{
    [Fact]
    public void EnteringPeer_ReceivesPendingAftermathPromptSnapshot()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var settlement = CreateUninitialized<Settlement>();
        var leaderParty = CreateUninitialized<MobileParty>();
        var previousOwnerClan = CreateUninitialized<Clan>();
        var objectManager = new Mock<IObjectManager>();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        string settlementId = "settlement-1";
        string leaderPartyId = "leader-party-1";
        string previousOwnerClanId = "previous-owner-clan-1";
        objectManager.Setup(manager => manager.TryGetIdWithLogging(settlement, out settlementId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(leaderParty, out leaderPartyId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetId(previousOwnerClan, out previousOwnerClanId)).Returns(true);
        siegeEventInterface.Setup(service => service.GetPendingSiegeAftermathPrompts())
            .Returns(new[] { new PendingSiegeAftermathPrompt(leaderParty, settlement, previousOwnerClan) });
        using var handler = new ServerSiegeAftermathHandler(
            broker, network, objectManager.Object, siegeEventInterface.Object, Mock.Of<IPlayerManager>());

        handler.SendPendingAftermathPrompts(peer);

        var prompt = Assert.Single(network.GetPeerMessages(peer).OfType<NetworkPromptSiegeAftermathChoice>());
        Assert.Equal(settlementId, prompt.SettlementId);
        Assert.Equal(leaderPartyId, prompt.LeaderPartyId);
        Assert.Equal(previousOwnerClanId, prompt.PreviousOwnerClanId);
    }

    [Fact]
    public void EnteringPeer_UnregisteredPreviousOwner_StillReceivesPrompt()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var settlement = CreateUninitialized<Settlement>();
        var leaderParty = CreateUninitialized<MobileParty>();
        var objectManager = new Mock<IObjectManager>();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        string settlementId = "settlement-1";
        string leaderPartyId = "leader-party-1";
        objectManager.Setup(manager => manager.TryGetIdWithLogging(settlement, out settlementId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(leaderParty, out leaderPartyId)).Returns(true);
        siegeEventInterface.Setup(service => service.GetPendingSiegeAftermathPrompts())
            .Returns(new[] { new PendingSiegeAftermathPrompt(leaderParty, settlement, CreateUninitialized<Clan>()) });
        using var handler = new ServerSiegeAftermathHandler(
            broker, network, objectManager.Object, siegeEventInterface.Object, Mock.Of<IPlayerManager>());

        handler.SendPendingAftermathPrompts(peer);

        var prompt = Assert.Single(network.GetPeerMessages(peer).OfType<NetworkPromptSiegeAftermathChoice>());
        Assert.Equal(settlementId, prompt.SettlementId);
        Assert.Null(prompt.PreviousOwnerClanId);
    }

    [Fact]
    public void DiscardedAftermath_IsBroadcastSoTheLeaderReleasesItsChoice()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var settlement = CreateUninitialized<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        string settlementId = "settlement-1";
        objectManager.Setup(manager => manager.TryGetIdWithLogging(settlement, out settlementId)).Returns(true);
        using var handler = new ServerSiegeAftermathHandler(
            broker, network, objectManager.Object, Mock.Of<ISiegeEventInterface>(), Mock.Of<IPlayerManager>());

        broker.Publish(this, new SiegeAftermathDiscarded(settlement));

        var discarded = Assert.Single(network.GetPeerMessages(peer).OfType<NetworkSiegeAftermathDiscarded>());
        Assert.Equal(settlementId, discarded.SettlementId);
    }

    // The aftermath choice belongs to the capturing leader's player.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AftermathChoice_IsAppliedOnlyForTheSendersParty(bool fromOwner)
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var ownerPeer = network.CreatePeer("10.0.0.1");
        var otherPeer = network.CreatePeer("10.0.0.2");
        var party = CreateUninitialized<MobileParty>();
        var settlement = CreateUninitialized<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(manager => manager.TryGetObjectWithLogging("party-1", out party)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObjectWithLogging("settlement-1", out settlement)).Returns(true);
        var players = new PlayerManager(Mock.Of<ILogger>(), objectManager.Object, new ControllerIdProvider());
        Assert.True(players.AddPlayer(new Player("owner", "hero-1", "party-1", "clan-1", "character-1")));
        Assert.True(players.AddPlayer(new Player("other", "hero-2", "party-2", "clan-2", "character-2")));
        players.SetPeer("owner", ownerPeer);
        players.SetPeer("other", otherPeer);
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        using var handler = new ServerSiegeAftermathHandler(
            broker, network, objectManager.Object, siegeEventInterface.Object, players);

        broker.Publish(fromOwner ? ownerPeer : otherPeer, new NetworkRequestSiegeAftermath("party-1", "settlement-1", 0));
        GameThread.Run(() => { }, blocking: true);

        siegeEventInterface.Verify(service => service.ApplySiegeAftermathChoice(party, settlement, 0), fromOwner ? Times.Once() : Times.Never());
    }

#pragma warning disable SYSLIB0050 // Identity-only test doubles; no native constructors are invoked.
    private static T CreateUninitialized<T>() where T : class =>
        (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
}
