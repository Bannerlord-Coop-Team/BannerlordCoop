using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
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
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;

namespace Coop.Tests.Server.Services.SiegeEvents;

public class ServerSiegeBreakOutHandlerTests : IDisposable
{
    private readonly TestMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly Mock<IObjectManager> objects = new();
    private readonly Mock<IPlayerManager> players = new();
    private readonly Mock<ISiegeBreakOut> action = new();
    private readonly MobileParty party = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
    private readonly SiegeEvent siege = ObjectHelper.SkipConstructor<SiegeEvent>();
    private readonly NetPeer peer;
    private readonly ServerSiegeBreakOutHandler handler;

    public ServerSiegeBreakOutHandlerTests()
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
        AccessTools.Field(typeof(MobileParty), "_currentSettlement").SetValue(party, settlement);
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsActive)).SetValue(party, true);
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)).SetValue(party, partyBase);
        var roster = TroopRoster.CreateDummyTroopRoster();
        AccessTools.Property(typeof(PartyBase), nameof(PartyBase.MemberRoster)).SetValue(partyBase, roster);
        AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)).SetValue(settlement, siege);
        uint rosterId = 4;
        objects.Setup(manager => manager.TryGetHandleWithLogging(roster, out rosterId)).Returns(true);
        int armyLosses = -1;
        action.Setup(service => service.ApplySacrifice(party, out armyLosses)).Returns(TroopRoster.CreateDummyTroopRoster());
        handler = new ServerSiegeBreakOutHandler(broker, network, objects.Object, players.Object, action.Object);
    }

    [Fact]
    public void RepeatedAcceptance_ChargesOnceAndRepliesToEachRequest()
    {
        Send("first");
        Send("first");
        Send("retry");
        Assert.Equal(new[] { "first", "first", "retry" }, Results().Select(result => result.RequestId));
        Assert.All(Results(), result => Assert.True(result.Approved));
        action.Verify(service => service.ApplySacrifice(party, out It.Ref<int>.IsAny), Times.Once);
    }

    [Fact]
    public void LeavingAndReturning_AllowsANewSacrifice()
    {
        Send("first");
        GameThread.Run(() => broker.Publish(this, new PartyEnterSettlementAttempted(settlement, party)), blocking: true);
        Send("second-stay");
        Assert.All(Results(), result => Assert.True(result.Approved));
        action.Verify(service => service.ApplySacrifice(party, out It.Ref<int>.IsAny), Times.Exactly(2));
    }

    [Fact]
    public void AppliedLeave_ProtectsOnlyAnApprovedBreakoutAfterExit()
    {
        Send("accepted");
        AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)).SetValue(siege, settlement);
        AccessTools.Field(typeof(MobileParty), "_currentSettlement").SetValue(party, null);
        GameThread.Run(() => broker.Publish(this, new SettlementEncounterLeaveApplied(party, settlement)), blocking: true);
        GameThread.Run(() => broker.Publish(this, new SettlementEncounterLeaveApplied(party, settlement)), blocking: true);
        action.Verify(service => service.ProtectAfterLeave(party, settlement), Times.Once);
    }

    [Fact]
    public void OrdinaryLeave_DoesNotApplyBreakoutProtection()
    {
        GameThread.Run(() => broker.Publish(this, new SettlementEncounterLeaveApplied(party, settlement)), blocking: true);
        action.Verify(service => service.ProtectAfterLeave(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
    }

    [Fact]
    public void StaleSettlementRequest_DoesNotCharge()
    {
        AccessTools.Field(typeof(MobileParty), "_currentSettlement").SetValue(party, null);
        Send("stale");
        Assert.False(Assert.Single(Results()).Approved);
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void RequestFromAnotherPartyOwner_DoesNotCharge()
    {
        var other = ObjectHelper.SkipConstructor<MobileParty>();
        objects.Setup(manager => manager.TryGetObject("party", out other)).Returns(true);
        Send("other-owner");
        Assert.False(Assert.Single(Results()).Approved);
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void ChangedSiege_DoesNotChargeAgainstTheOldIdentity()
    {
        AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)).SetValue(
            settlement, ObjectHelper.SkipConstructor<SiegeEvent>());
        Send("old-siege");
        Assert.False(Assert.Single(Results()).Approved);
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void SacrificeFailure_IsRetainedAndNotRepeated()
    {
        action.Setup(service => service.ApplySacrifice(party, out It.Ref<int>.IsAny))
            .Throws(new InvalidOperationException("partial sacrifice"));
        Send("first");
        Send("retry");
        Assert.All(Results(), result => Assert.False(result.Approved));
        action.Verify(service => service.ApplySacrifice(party, out It.Ref<int>.IsAny), Times.Once);
    }

    private void Send(string requestId)
    {
        broker.Publish(peer, new NetworkRequestBreakOut(requestId, 1, 2, 3));
        GameThread.Run(() => { }, blocking: true);
    }

    private NetworkBreakOutResult[] Results() => network.GetPeerMessages(peer).OfType<NetworkBreakOutResult>().ToArray();
    public void Dispose() => handler.Dispose();
}
