using Common;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.MobileParties.Handlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.Time.Interfaces;
using Moq;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.Services.MobileParties;

public class JoinCampaignBaselineHandlerTests
{
    private readonly TestMessageBroker messageBroker = new TestMessageBroker();
    private readonly Mock<IMapTimeTrackerInterface> mapTimeTracker = new Mock<IMapTimeTrackerInterface>();
    private readonly Mock<IMobilePartyBehaviorSnapshot> mobilePartyBehaviorSnapshot =
        new Mock<IMobilePartyBehaviorSnapshot>();
    private readonly JoinCampaignBaselineHandler handler;

    public JoinCampaignBaselineHandlerTests(ITestOutputHelper output)
    {
        _ = output;
        handler = new JoinCampaignBaselineHandler(
            messageBroker,
            mapTimeTracker.Object,
            mobilePartyBehaviorSnapshot.Object);
    }

    [Fact]
    public void CompleteBaseline_AppliesAllPartyStateBeforePublishingSuccess()
    {
        var partyStates = new[]
        {
            new MobilePartyJoinState
            {
                Behavior = new PartyBehaviorUpdateData(
                    "main_party",
                    default,
                    null,
                    default,
                    default,
                    default,
                    default,
                    default),
            },
        };
        bool timeApplied = false;
        bool partyStateApplied = false;
        mapTimeTracker
            .Setup(tracker => tracker.ApplyCampaignJoinBaseline(123456L))
            .Callback(() => timeApplied = true);
        mobilePartyBehaviorSnapshot
            .Setup(snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()))
            .Callback<MobilePartyJoinState[], Action>((_, beforeApply) =>
            {
                beforeApply();
                Assert.True(timeApplied);
                partyStateApplied = true;
            })
            .Returns(true);
        messageBroker.Subscribe<JoinCampaignBaselineApplied>(payload =>
        {
            Assert.True(partyStateApplied);
            Assert.True(payload.What.Success);
        });

        messageBroker.Publish(
            this,
            new NetworkJoinCampaignBaseline(123456L, partyStates));
        GameThread.Run(() => { }, blocking: true);

        mapTimeTracker.Verify(tracker => tracker.ApplyCampaignJoinBaseline(123456L), Times.Once);
        mobilePartyBehaviorSnapshot.Verify(
            snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()),
            Times.Once);
        var applied = Assert.Single(messageBroker.GetMessagesFromType<JoinCampaignBaselineApplied>());
        Assert.True(applied.Success);
    }

    [Fact]
    public void IncompleteBaseline_PublishesFailureWithoutApplyingState()
    {
        var partyStates = Array.Empty<MobilePartyJoinState>();

        messageBroker.Publish(
            this,
            new NetworkJoinCampaignBaseline(123456L, partyStates, isComplete: false));
        GameThread.Run(() => { }, blocking: true);

        mobilePartyBehaviorSnapshot.Verify(
            snapshot => snapshot.TryApplyJoinBaseline(
                It.IsAny<MobilePartyJoinState[]>(),
                It.IsAny<Action>()),
            Times.Never);
        mapTimeTracker.Verify(
            tracker => tracker.ApplyCampaignJoinBaseline(It.IsAny<long>()),
            Times.Never);
        var applied = Assert.Single(messageBroker.GetMessagesFromType<JoinCampaignBaselineApplied>());
        Assert.False(applied.Success);
    }

    [Fact]
    public void RejectedPartyState_PublishesFailureWithoutApplyingTime()
    {
        var partyStates = Array.Empty<MobilePartyJoinState>();
        mobilePartyBehaviorSnapshot
            .Setup(snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()))
            .Returns(false);

        messageBroker.Publish(
            this,
            new NetworkJoinCampaignBaseline(123456L, partyStates));
        GameThread.Run(() => { }, blocking: true);

        mapTimeTracker.Verify(
            tracker => tracker.ApplyCampaignJoinBaseline(It.IsAny<long>()),
            Times.Never);
        var applied = Assert.Single(messageBroker.GetMessagesFromType<JoinCampaignBaselineApplied>());
        Assert.False(applied.Success);
    }
}
