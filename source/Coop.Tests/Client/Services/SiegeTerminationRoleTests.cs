using Common;
using Common.Network;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Handlers;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Interfaces;
using Moq;
using ProtoBuf;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace Coop.Tests.Client.Services;

public class SiegeTerminationRoleTests
{
    [Theory]
    [InlineData("leader", SiegeTerminationRole.AttackerLeader)]
    [InlineData("member", SiegeTerminationRole.AttackerMember)]
    [InlineData("defender", SiegeTerminationRole.Defender)]
    [InlineData("other", SiegeTerminationRole.None)]
    public void ResolveTerminationRole_UsesCapturedParticipantSnapshot(
        string partyId,
        SiegeTerminationRole expected)
    {
        var message = new NetworkPromptSiegeEnded(
            "town_ES1",
            besiegerDefeated: false,
            "leader",
            new[] { "leader", "member" },
            new[] { "defender" });

        var result = ClientSiegeEntryHandler.ResolveTerminationRole(message, partyId);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveTerminationRole_LeaderDoesNotDependOnAttackerArray()
    {
        var message = RoundTrip(new NetworkPromptSiegeEnded(
            "town_ES1",
            besiegerDefeated: false,
            "leader",
            Array.Empty<string>(),
            Array.Empty<string>()));

        var result = ClientSiegeEntryHandler.ResolveTerminationRole(message, "leader");

        Assert.Equal(SiegeTerminationRole.AttackerLeader, result);
    }

    [Fact]
    public void ResolveTerminationRole_MissingProtobufArraysReturnsNone()
    {
        var message = RoundTrip(new NetworkPromptSiegeEnded(
            "town_ES1",
            besiegerDefeated: false,
            leaderPartyId: null!,
            Array.Empty<string>(),
            Array.Empty<string>()));

        var result = ClientSiegeEntryHandler.ResolveTerminationRole(message, "other");

        Assert.Null(message.AttackerPartyIds);
        Assert.Null(message.DefenderPartyIds);
        Assert.Equal(SiegeTerminationRole.None, result);
    }

    [Fact]
    public void InterruptedActiveAssault_ProtobufPreservesFlagAndParticipants()
    {
        var message = RoundTrip(new NetworkPromptSiegeEnded(
            "town_ES1",
            besiegerDefeated: false,
            "leader",
            new[] { "leader", "member" },
            new[] { "defender" },
            interruptedActiveAssault: true));

        Assert.True(message.InterruptedActiveAssault);
        Assert.Equal(new[] { "leader", "member" }, message.AttackerPartyIds);
        Assert.Equal(new[] { "defender" }, message.DefenderPartyIds);
    }

    [Theory]
    [InlineData(SiegeBreakOutcome.Applied)]
    [InlineData(SiegeBreakOutcome.AlreadyLeft)]
    public void BreakApproval_WhenMenusNeedContinuation_FinishesLocalSiegeLeave(
        SiegeBreakOutcome outcome)
    {
        var broker = new TestMessageBroker();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        using var handler = new ClientSiegeEntryHandler(
            broker,
            Mock.Of<INetwork>(),
            Mock.Of<INetworkConfig>(),
            Mock.Of<IObjectManager>(),
            siegeEventInterface.Object);

        broker.Publish(this, new NetworkBreakSiegeApproved(
            outcome,
            finishLocalMenus: true));
        DrainGameThread();

        siegeEventInterface.Verify(
            value => value.FinishLocalPlayerSiegeLeave(),
            Times.Once);
    }

    [Theory]
    [InlineData(SiegeBreakOutcome.Applied, false, false)]
    [InlineData(SiegeBreakOutcome.Applied, true, true)]
    [InlineData(SiegeBreakOutcome.Rejected, true, false)]
    public void BreakApproval_WhenAnotherFlowOwnsContinuation_DoesNotFinishLocalMenus(
        SiegeBreakOutcome outcome,
        bool finishLocalMenus,
        bool battleLeaveApplied)
    {
        var broker = new TestMessageBroker();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        using var handler = new ClientSiegeEntryHandler(
            broker,
            Mock.Of<INetwork>(),
            Mock.Of<INetworkConfig>(),
            Mock.Of<IObjectManager>(),
            siegeEventInterface.Object);

        broker.Publish(this, new NetworkBreakSiegeApproved(
            outcome,
            finishLocalMenus,
            battleLeaveApplied));
        DrainGameThread();

        siegeEventInterface.Verify(
            value => value.FinishLocalPlayerSiegeLeave(),
            Times.Never);
    }

    [Fact]
    public void BreakApproval_ProtobufPreservesBothContinuationFlags()
    {
        var message = RoundTrip(new NetworkBreakSiegeApproved(
            SiegeBreakOutcome.Applied,
            finishLocalMenus: false,
            battleLeaveApplied: true));

        Assert.Equal(SiegeBreakOutcome.Applied, message.Outcome);
        Assert.False(message.FinishLocalMenus);
        Assert.True(message.BattleLeaveApplied);
    }

    [Fact]
    public void InterruptedActiveAssaultPrompt_WhenPromptThrows_IsNotRetried()
    {
        var broker = new TestMessageBroker();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var expectedException = new InvalidOperationException();
        siegeEventInterface
            .Setup(value => value.PromptSiegeEnded(
                settlement,
                false,
                SiegeTerminationRole.AttackerLeader,
                true))
            .Throws(expectedException);
        using var handler = new ClientSiegeEntryHandler(
            broker,
            Mock.Of<INetwork>(),
            Mock.Of<INetworkConfig>(),
            Mock.Of<IObjectManager>(),
            siegeEventInterface.Object);

        var pendingType = typeof(ClientSiegeEntryHandler).GetNestedType(
            "PendingInterruptedAssault",
            BindingFlags.NonPublic)!;
        var pending = Activator.CreateInstance(
            pendingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[]
            {
                settlement,
                false,
                SiegeTerminationRole.AttackerLeader,
            },
            culture: null);
        var pendingField = typeof(ClientSiegeEntryHandler).GetField(
            "pendingInterruptedAssault",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        pendingField.SetValue(handler, pending);
        var finish = typeof(ClientSiegeEntryHandler).GetMethod(
            "FinishPendingInterruptedAssault",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var exception = Assert.Throws<TargetInvocationException>(() => finish.Invoke(handler, null));

        Assert.Same(expectedException, exception.InnerException);
        Assert.Null(pendingField.GetValue(handler));
        finish.Invoke(handler, null);
        siegeEventInterface.Verify(value => value.PromptSiegeEnded(
            settlement,
            false,
            SiegeTerminationRole.AttackerLeader,
            true), Times.Once);
    }

    private static T RoundTrip<T>(T message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
}
