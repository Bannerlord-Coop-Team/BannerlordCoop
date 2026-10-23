using Common;
using Common.Network.Messages;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents;
using Coop.Core.Server.Services.SiegeEvents.Handlers;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents;
using HarmonyLib;
using LiteNetLib;
using Moq;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;

namespace Coop.Tests.Server.Services.SiegeEvents;

public class ServerSiegeBreakInHandlerTests : IDisposable
{
    private readonly TestMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly Mock<IObjectManager> objects = new();
    private readonly Mock<IPlayerManager> players = new();
    private readonly Mock<ISiegeBreakOut> action = new();
    private readonly Mock<ISiegeSacrifice> sacrifice = new();
    private readonly MobileParty party = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
    private readonly SiegeEvent siege = ObjectHelper.SkipConstructor<SiegeEvent>();
    private readonly NetPeer peer;
    private readonly BreakInReceipts receipts = new();
    private readonly ServerSiegeBreakInHandler handler;

    public ServerSiegeBreakInHandlerTests()
    {
        peer = network.CreatePeer();
        var player = new Player("requester", "hero", "party", "clan", "character");
        var controlled = party;
        var target = settlement;
        var currentSiege = siege;
        players.Setup(manager => manager.TryGetPlayer(peer, out player)).Returns(true);
        objects.Setup(manager => manager.TryGetObject("party", out controlled)).Returns(true);
        objects.Setup(manager => manager.TryGetObjectWithLogging(1u, out controlled)).Returns(true);
        objects.Setup(manager => manager.TryGetObjectWithLogging(2u, out target)).Returns(true);
        objects.Setup(manager => manager.TryGetObjectWithLogging(3u, out currentSiege)).Returns(true);
        AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)).SetValue(settlement, siege);

        action.Setup(service => service.CanJoinDefenders(party, settlement, false)).Returns(true);
        action.Setup(service => service.GetBreakInCasualties(party, siege)).Returns(40);
        SacrificePays();
        handler = new ServerSiegeBreakInHandler(broker, network, objects.Object, players.Object, action.Object,
            sacrifice.Object, receipts);
    }

    [Fact]
    public void ApprovedBreakIn_ChargesTheBreakInLossesAndRepliesWithCasualties()
    {
        Send("break-in");

        var result = Assert.Single(Results());
        Assert.True(result.Approved);
        Assert.Equal("break-in", result.RequestId);
        Assert.Equal(new[] { 5u }, result.CharacterIds);
        Assert.Equal(new[] { 40 }, result.Counts);
        Assert.Equal(-1, result.ArmyCasualties);
        Assert.True(receipts.IsPaid(party, siege));
        VerifyCharged(Times.Once());
    }

    [Fact]
    public void RepeatedRequest_ChargesOnce()
    {
        Send("break-in");
        Send("break-in");

        Assert.Equal(new[] { true, true }, Results().Select(result => result.Approved));
        Assert.All(Results(), result => Assert.Equal(new[] { 40 }, result.Counts));
        VerifyCharged(Times.Once());
    }

    [Fact]
    public void AcceptingAgainAfterLeavingTheMenu_ReusesTheFirstLossesAndKeepsThemPaid()
    {
        Send("first");
        // The first reply came too late for the menu, so the client asks again with a new request.
        Send("after-reject");

        Assert.Equal(new[] { "first", "after-reject" }, Results().Select(result => result.RequestId));
        Assert.All(Results(), result => Assert.True(result.Approved));
        Assert.All(Results(), result => Assert.Equal(new[] { 40 }, result.Counts));
        Assert.True(receipts.IsPaid(party, siege));
        VerifyCharged(Times.Once());
    }

    [Fact]
    public void RequestFromAnotherPartyOwner_DoesNotCharge()
    {
        var other = ObjectHelper.SkipConstructor<MobileParty>();
        objects.Setup(manager => manager.TryGetObject("party", out other)).Returns(true);

        Send("other-owner");

        Assert.False(Assert.Single(Results()).Approved);
        VerifyCharged(Times.Never());
    }

    [Fact]
    public void PartyThatCannotJoinDefenders_DoesNotCharge()
    {
        action.Setup(service => service.CanJoinDefenders(party, settlement, false)).Returns(false);

        Send("not-a-defender");

        Assert.False(Assert.Single(Results()).Approved);
        VerifyCharged(Times.Never());
    }

    [Fact]
    public void ChangedSiege_DoesNotCharge()
    {
        AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)).SetValue(
            settlement, ObjectHelper.SkipConstructor<SiegeEvent>());

        Send("old-siege");

        Assert.False(Assert.Single(Results()).Approved);
        VerifyCharged(Times.Never());
    }

    [Fact]
    public void RejectedRequest_CanBeRetriedOnceThePartyCanJoin()
    {
        action.Setup(service => service.CanJoinDefenders(party, settlement, false)).Returns(false);
        Send("retry");
        action.Setup(service => service.CanJoinDefenders(party, settlement, false)).Returns(true);
        Send("retry");
        Send("retry");

        Assert.Equal(new[] { false, true, true }, Results().Select(result => result.Approved));
        VerifyCharged(Times.Once());
    }

    [Fact]
    public void SacrificeThatCannotBePaid_IsRejectedWithoutReceipt_AndARetryPaysOnce()
    {
        sacrifice.Setup(step => step.TryApply(party, It.IsAny<int>(), It.IsAny<Action>())).Returns((SiegeSacrifice.Paid)null);
        Send("unpaid");
        Assert.False(Assert.Single(Results()).Approved);
        Assert.False(receipts.TryGet(party, siege, out _));

        SacrificePays();
        Send("paid");
        Send("duplicate");

        Assert.Equal(new[] { false, true, true }, Results().Select(result => result.Approved));
        VerifyCharged(Times.Exactly(2));
        Assert.True(receipts.IsPaid(party, siege));
    }

    [Fact]
    public void SacrificeFailure_IsNotRepeatedForTheSameRequest()
    {
        SacrificeThrowsAfterMarkingPaid();

        Send("first");
        Send("first");

        Assert.All(Results(), result => Assert.False(result.Approved));
        VerifyCharged(Times.Once());
    }

    [Theory]
    [InlineData(nameof(PartyEnterSettlementAttempted))]
    [InlineData(nameof(SettlementEncounterLeaveApplied))]
    [InlineData(nameof(PlayerDisconnected))]
    public void PaidReceipt_IsDroppedOnEveryExit(string exit)
    {
        Send("first");
        Assert.True(receipts.IsPaid(party, siege));

        Exit(exit);

        Assert.False(receipts.TryGet(party, siege, out _));
        Send("after-exit");
        Assert.Equal(new[] { true, true }, Results().Select(result => result.Approved));
        VerifyCharged(Times.Exactly(2));
    }

    [Theory]
    [InlineData(nameof(SettlementEncounterLeaveApplied))]
    [InlineData(nameof(PlayerDisconnected))]
    public void FailedSacrificeReceipt_IsDroppedOnExit_SoALaterBreakInCanPay(string exit)
    {
        SacrificeThrowsAfterMarkingPaid();
        Send("failed");
        Assert.True(receipts.TryGet(party, siege, out _));

        Exit(exit);
        SacrificePays();
        Send("later");

        Assert.Equal(new[] { false, true }, Results().Select(result => result.Approved));
        Assert.True(receipts.IsPaid(party, siege));
    }

    [Fact]
    public void OtherPeerDisconnecting_KeepsThePaidReceipt()
    {
        Send("first");

        var other = network.CreatePeer();
        broker.Publish(this, new PlayerDisconnected(other, default));
        GameThread.Run(() => { }, blocking: true);

        Assert.True(receipts.IsPaid(party, siege));
        Send("again");
        VerifyCharged(Times.Once());
    }

    private void SacrificePays()
    {
        sacrifice.Setup(step => step.TryApply(party, It.IsAny<int>(), It.IsAny<Action>()))
            .Returns((MobileParty current, int losses, Action markPaid) =>
            {
                markPaid();
                return new SiegeSacrifice.Paid { CharacterIds = new[] { 5u }, Counts = new[] { losses }, ArmyCasualties = -1 };
            });
    }

    private void SacrificeThrowsAfterMarkingPaid()
    {
        sacrifice.Setup(step => step.TryApply(party, It.IsAny<int>(), It.IsAny<Action>()))
            .Returns((MobileParty current, int losses, Action markPaid) =>
            {
                markPaid();
                throw new InvalidOperationException("partial sacrifice");
            });
    }

    private void VerifyCharged(Times times) =>
        sacrifice.Verify(step => step.TryApply(It.IsAny<MobileParty>(), 40, It.IsAny<Action>()), times);

    private void Exit(string exit)
    {
        if (exit == nameof(PlayerDisconnected))
            broker.Publish(this, new PlayerDisconnected(peer, default));
        else if (exit == nameof(PartyEnterSettlementAttempted))
            GameThread.Run(() => broker.Publish(this, new PartyEnterSettlementAttempted(settlement, party)), blocking: true);
        else
            GameThread.Run(() => broker.Publish(this, new SettlementEncounterLeaveApplied(party, settlement)), blocking: true);
        GameThread.Run(() => { }, blocking: true);
    }

    private void Send(string requestId)
    {
        broker.Publish(peer, new NetworkRequestBreakIn(requestId, 1, 2, 3));
        GameThread.Run(() => { }, blocking: true);
    }

    private NetworkBreakInResult[] Results() => network.GetPeerMessages(peer).OfType<NetworkBreakInResult>().ToArray();
    public void Dispose() => handler.Dispose();
}
