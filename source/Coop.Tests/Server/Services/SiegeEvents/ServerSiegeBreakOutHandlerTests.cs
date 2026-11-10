using Common;
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
    private readonly Mock<ISiegeSacrifice> sacrifice = new();
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
        AccessTools.Property(typeof(PartyBase), nameof(PartyBase.MemberRoster)).SetValue(partyBase, TroopRoster.CreateDummyTroopRoster());
        AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)).SetValue(settlement, siege);
        action.Setup(service => service.GetRequiredCasualties(party, false)).Returns(5);
        action.Setup(service => service.CanBreakOutFromPort(party, settlement)).Returns(true);
        SacrificePays();
        handler = new ServerSiegeBreakOutHandler(broker, network, objects.Object, players.Object, action.Object, sacrifice.Object);
    }

    [Fact]
    public void RepeatedAcceptance_ChargesOnceAndRepliesToEachRequest()
    {
        Send("first");
        Send("first");
        Send("retry");
        Assert.Equal(new[] { "first", "first", "retry" }, Results().Select(result => result.RequestId));
        Assert.All(Results(), result => Assert.True(result.Approved));
        Assert.All(Results(), result => Assert.Equal(new[] { 5 }, result.Counts));
        sacrifice.Verify(step => step.TryApply(party, 5, It.IsAny<Action>()), Times.Once);
    }

    [Fact]
    public void SacrificeThatCannotBePaid_IsRejected_AndARetryPaysOnce()
    {
        sacrifice.Setup(step => step.TryApply(party, It.IsAny<int>(), It.IsAny<Action>())).Returns((SiegeSacrifice.Paid)null);
        Send("retry");
        Assert.False(Assert.Single(Results()).Approved);

        SacrificePays();
        Send("retry");
        Send("duplicate");

        Assert.Equal(new[] { false, true, true }, Results().Select(result => result.Approved));
        action.Verify(service => service.GetRequiredCasualties(party, false), Times.Exactly(2));
        VerifyCharged(Times.Exactly(2));
    }

    [Fact]
    public void LeavingAndReturning_AllowsANewSacrifice()
    {
        Send("first");
        GameThread.Run(() => broker.Publish(this, new PartyEnterSettlementAttempted(settlement, party)), blocking: true);
        Send("second-stay");
        Assert.All(Results(), result => Assert.True(result.Approved));
        VerifyCharged(Times.Exactly(2));
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
    public void PortBreakOut_ChargesPortLossesWithoutGateProtection()
    {
        action.Setup(service => service.GetRequiredCasualties(party, true)).Returns(3);

        Send("port", isFromPort: true);
        AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)).SetValue(siege, settlement);
        AccessTools.Field(typeof(MobileParty), "_currentSettlement").SetValue(party, null);
        GameThread.Run(() => broker.Publish(this, new SettlementEncounterLeaveApplied(party, settlement)), blocking: true);

        Assert.True(Assert.Single(Results()).Approved);
        sacrifice.Verify(step => step.TryApply(party, 3, It.IsAny<Action>()), Times.Once);
        action.Verify(service => service.GetRequiredCasualties(party, false), Times.Never);
        action.Verify(service => service.ProtectAfterLeave(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
    }

    [Fact]
    public void PortRequestWithoutVanillaPortPrerequisites_IsTurnedDownWithoutSacrifice()
    {
        action.Setup(service => service.CanBreakOutFromPort(party, settlement)).Returns(false);

        Send("port", isFromPort: true);

        Assert.False(Assert.Single(Results()).Approved);
        action.Verify(service => service.GetRequiredCasualties(It.IsAny<MobileParty>(), It.IsAny<bool>()), Times.Never);
        VerifyCharged(Times.Never());
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
        VerifyCharged(Times.Never());
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
    public void ChangedSiege_DoesNotChargeAgainstTheOldIdentity()
    {
        AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)).SetValue(
            settlement, ObjectHelper.SkipConstructor<SiegeEvent>());
        Send("old-siege");
        Assert.False(Assert.Single(Results()).Approved);
        VerifyCharged(Times.Never());
    }

    [Fact]
    public void SacrificeFailure_IsRetainedAndNotRepeated()
    {
        sacrifice.Setup(step => step.TryApply(party, It.IsAny<int>(), It.IsAny<Action>()))
            .Returns((MobileParty current, int losses, Action markPaid) =>
            {
                markPaid();
                throw new InvalidOperationException("partial sacrifice");
            });
        Send("first");
        Send("retry");
        Assert.All(Results(), result => Assert.False(result.Approved));
        VerifyCharged(Times.Once());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwitchingBetweenGateAndPort_IsTurnedDownInsteadOfReusingTheFirstResult(bool firstFromPort)
    {
        Send("first", firstFromPort);
        Send("switched", !firstFromPort);
        Send("same-again", firstFromPort);

        Assert.Equal(new[] { true, false, true }, Results().Select(result => result.Approved));
        VerifyCharged(Times.Once());
    }

    [Fact]
    public void PortCheckThatThrows_StillRepliesRejected()
    {
        action.Setup(service => service.CanBreakOutFromPort(party, settlement)).Throws(new InvalidOperationException("check failed"));

        Send("throws", isFromPort: true);

        var result = Assert.Single(Results());
        Assert.Equal("throws", result.RequestId);
        Assert.False(result.Approved);
        VerifyCharged(Times.Never());
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

    private void VerifyCharged(Times times) =>
        sacrifice.Verify(step => step.TryApply(It.IsAny<MobileParty>(), It.IsAny<int>(), It.IsAny<Action>()), times);

    private void Send(string requestId, bool isFromPort = false)
    {
        broker.Publish(peer, new NetworkRequestBreakOut(requestId, 1, 2, 3, isFromPort));
        GameThread.Run(() => { }, blocking: true);
    }

    private NetworkBreakOutResult[] Results() => network.GetPeerMessages(peer).OfType<NetworkBreakOutResult>().ToArray();
    public void Dispose() => handler.Dispose();
}
