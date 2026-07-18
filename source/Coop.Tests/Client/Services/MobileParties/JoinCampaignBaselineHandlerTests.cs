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
        var partyStates = new[] { new MobilePartyJoinState() };
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

        var applied = Apply(new NetworkJoinCampaignBaseline(123456L, partyStates));

        mapTimeTracker.Verify(tracker => tracker.ApplyCampaignJoinBaseline(123456L), Times.Once);
        mobilePartyBehaviorSnapshot.Verify(
            snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()),
            Times.Once);
        Assert.True(applied.Success);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void IncompleteOrRejectedBaseline_PublishesFailureWithoutApplyingTime(
        bool isComplete,
        int expectedApplyAttempts)
    {
        var partyStates = Array.Empty<MobilePartyJoinState>();
        mobilePartyBehaviorSnapshot
            .Setup(snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()))
            .Returns(false);

        var applied = Apply(new NetworkJoinCampaignBaseline(123456L, partyStates, isComplete));

        mobilePartyBehaviorSnapshot.Verify(
            snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()),
            Times.Exactly(expectedApplyAttempts));
        mapTimeTracker.Verify(
            tracker => tracker.ApplyCampaignJoinBaseline(It.IsAny<long>()),
            Times.Never);
        Assert.False(applied.Success);
    }

    private JoinCampaignBaselineApplied Apply(NetworkJoinCampaignBaseline baseline)
    {
        messageBroker.Publish(this, baseline);
        GameThread.Run(() => { }, blocking: true);
        return Assert.Single(messageBroker.GetMessagesFromType<JoinCampaignBaselineApplied>());
    }
}
