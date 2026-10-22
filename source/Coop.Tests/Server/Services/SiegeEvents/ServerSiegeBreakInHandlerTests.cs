using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents;
using Coop.Core.Server.Services.SiegeEvents.Handlers;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents;
using HarmonyLib;
using LiteNetLib;
using Moq;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
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
    private readonly MobileParty party = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
    private readonly SiegeEvent siege = ObjectHelper.SkipConstructor<SiegeEvent>();
    private readonly CharacterObject troop = ObjectHelper.SkipConstructor<CharacterObject>();
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
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsActive)).SetValue(party, true);
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)).SetValue(party, partyBase);
        var roster = TroopRoster.CreateDummyTroopRoster();
        AccessTools.Property(typeof(PartyBase), nameof(PartyBase.MemberRoster)).SetValue(partyBase, roster);
        AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)).SetValue(settlement, siege);
        uint rosterId = 4;
        objects.Setup(manager => manager.TryGetHandleWithLogging(roster, out rosterId)).Returns(true);
        uint troopId = 5;
        objects.Setup(manager => manager.TryGetHandleWithLogging(troop, out troopId)).Returns(true);
        roster.AddToCounts(troop, 200);

        action.Setup(service => service.CanJoinDefenders(party, settlement, false)).Returns(true);
        action.Setup(service => service.GetBreakInCasualties(party, siege)).Returns(40);
        action.Setup(service => service.ApplySacrifice(party, It.IsAny<int>(), out It.Ref<int>.IsAny))
            .Returns((MobileParty current, int losses, out int armyLosses) =>
            {
                armyLosses = -1;
                current.MemberRoster.AddToCounts(troop, -losses);
                var casualties = TroopRoster.CreateDummyTroopRoster();
                casualties.AddToCounts(troop, losses);
                return casualties;
            });
        handler = new ServerSiegeBreakInHandler(broker, network, objects.Object, players.Object, action.Object, receipts);
    }

    [Fact]
    public void ApprovedBreakIn_AppliesLossesOnServerAndRepliesWithCasualties()
    {
        Send("break-in");

        var result = Assert.Single(Results());
        Assert.True(result.Approved);
        Assert.Equal("break-in", result.RequestId);
        Assert.Equal(new[] { 5u }, result.CharacterIds);
        Assert.Equal(new[] { 40 }, result.Counts);
        Assert.Equal(-1, result.ArmyCasualties);
        Assert.Equal(160, party.MemberRoster.TotalRegulars);
        action.Verify(service => service.ApplySacrifice(party, 40, out It.Ref<int>.IsAny), Times.Once);
    }

    [Fact]
    public void RepeatedRequest_AppliesLossesOnce()
    {
        Send("break-in");
        Send("break-in");

        Assert.Equal(new[] { true, true }, Results().Select(result => result.Approved));
        Assert.All(Results(), result => Assert.Equal(new[] { 40 }, result.Counts));
        Assert.Equal(160, party.MemberRoster.TotalRegulars);
        action.Verify(service => service.ApplySacrifice(party, It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Once);
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
        Assert.Equal(160, party.MemberRoster.TotalRegulars);
        Assert.True(receipts.IsPaid(party, siege));
        action.Verify(service => service.ApplySacrifice(party, It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Once);
    }

    [Fact]
    public void RequestFromAnotherPartyOwner_DoesNotApplyLosses()
    {
        var other = ObjectHelper.SkipConstructor<MobileParty>();
        objects.Setup(manager => manager.TryGetObject("party", out other)).Returns(true);

        Send("other-owner");

        Assert.False(Assert.Single(Results()).Approved);
        Assert.Equal(200, party.MemberRoster.TotalRegulars);
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void PartyThatCannotJoinDefenders_DoesNotApplyLosses()
    {
        action.Setup(service => service.CanJoinDefenders(party, settlement, false)).Returns(false);

        Send("not-a-defender");

        Assert.False(Assert.Single(Results()).Approved);
        Assert.Equal(200, party.MemberRoster.TotalRegulars);
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void ChangedSiege_DoesNotApplyLosses()
    {
        AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)).SetValue(
            settlement, ObjectHelper.SkipConstructor<SiegeEvent>());

        Send("old-siege");

        Assert.False(Assert.Single(Results()).Approved);
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Never);
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
        Assert.Equal(160, party.MemberRoster.TotalRegulars);
        action.Verify(service => service.ApplySacrifice(party, It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Once);
    }

    [Fact]
    public void PartyWithTooFewRegulars_IsRejectedWithoutLossesOrReceipt()
    {
        party.MemberRoster.AddToCounts(troop, -170);

        Send("too-few");

        Assert.False(Assert.Single(Results()).Approved);
        Assert.Equal(30, party.MemberRoster.TotalRegulars);
        Assert.False(receipts.TryGet(party, siege, out _));
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void PartyWithNoRegulars_IsRejectedWithoutLossesOrReceipt()
    {
        party.MemberRoster.AddToCounts(troop, -200);

        Send("empty");

        Assert.False(Assert.Single(Results()).Approved);
        Assert.Equal(0, party.MemberRoster.TotalRegulars);
        Assert.False(receipts.TryGet(party, siege, out _));
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void UnaffordableRequest_CanBeRetriedOnceTroopsArrive_AndPaysOnce()
    {
        party.MemberRoster.AddToCounts(troop, -170);
        Send("retry");
        party.MemberRoster.AddToCounts(troop, 20);
        Send("retry");
        Send("retry");

        Assert.Equal(new[] { false, true, true }, Results().Select(result => result.Approved));
        Assert.Equal(10, party.MemberRoster.TotalRegulars);
        Assert.True(receipts.IsPaid(party, siege));
        action.Verify(service => service.ApplySacrifice(party, 40, out It.Ref<int>.IsAny), Times.Once);
    }

    [Fact]
    public void ExactlyEnoughRegulars_IsApproved()
    {
        party.MemberRoster.AddToCounts(troop, -160);

        Send("exact");

        Assert.True(Assert.Single(Results()).Approved);
        Assert.Equal(0, party.MemberRoster.TotalRegulars);
    }

    [Fact]
    public void SacrificeFailure_IsNotRepeatedForTheSameRequest()
    {
        action.Setup(service => service.ApplySacrifice(party, It.IsAny<int>(), out It.Ref<int>.IsAny))
            .Throws(new InvalidOperationException("partial sacrifice"));

        Send("first");
        Send("first");

        Assert.All(Results(), result => Assert.False(result.Approved));
        action.Verify(service => service.ApplySacrifice(party, It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Once);
    }

    private void Send(string requestId)
    {
        broker.Publish(peer, new NetworkRequestBreakIn(requestId, 1, 2, 3));
        GameThread.Run(() => { }, blocking: true);
    }

    private NetworkBreakInResult[] Results() => network.GetPeerMessages(peer).OfType<NetworkBreakInResult>().ToArray();
    public void Dispose() => handler.Dispose();
}
