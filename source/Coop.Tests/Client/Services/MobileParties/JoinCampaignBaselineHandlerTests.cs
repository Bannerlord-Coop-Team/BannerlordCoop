using Common;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.MobileParties;
using Coop.Core.Client.Services.MobileParties.Handlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
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
    private readonly Mock<ITimeControlInterface> timeControl = new Mock<ITimeControlInterface>();
    private readonly Mock<IPlayerPartyTroopXpBaselineApplier> troopXpBaselineApplier =
        new Mock<IPlayerPartyTroopXpBaselineApplier>();
    private readonly JoinCampaignBaselineHandler handler;

    public JoinCampaignBaselineHandlerTests(ITestOutputHelper output)
    {
        _ = output;
        troopXpBaselineApplier
            .Setup(applier => applier.TryApply(It.IsAny<TroopRosterXpBaseline[]>()))
            .Returns(true);
        handler = new JoinCampaignBaselineHandler(
            messageBroker,
            mapTimeTracker.Object,
            mobilePartyBehaviorSnapshot.Object,
            timeControl.Object,
            troopXpBaselineApplier.Object);
    }

    [Fact]
    public void CompleteBaseline_AppliesAllPartyStateBeforePublishingSuccess()
    {
        var partyStates = new[] { new MobilePartyJoinState() };
        bool timeControlApplied = false;
        bool timeApplied = false;
        bool partyStateApplied = false;
        bool troopXpApplied = false;
        var troopXpBaselines = new[]
        {
            new TroopRosterXpBaseline("member_roster", new[]
            {
                new TroopXpBaselineEntry("troop", 42),
            }),
        };
        timeControl
            .Setup(time => time.ClientSetTimeControl(TimeControlEnum.Play_2x))
            .Callback(() => timeControlApplied = true);
        mapTimeTracker
            .Setup(tracker => tracker.ApplyCampaignJoinBaseline(123456L))
            .Callback(() =>
            {
                Assert.True(timeControlApplied);
                timeApplied = true;
            });
        mobilePartyBehaviorSnapshot
            .Setup(snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()))
            .Callback<MobilePartyJoinState[], Action>((_, beforeApply) =>
            {
                beforeApply();
                Assert.True(timeApplied);
                partyStateApplied = true;
            })
            .Returns(true);
        troopXpBaselineApplier
            .Setup(applier => applier.TryApply(troopXpBaselines))
            .Callback(() =>
            {
                Assert.True(partyStateApplied);
                troopXpApplied = true;
            })
            .Returns(true);
        messageBroker.Subscribe<JoinCampaignBaselineApplied>(payload =>
        {
            Assert.True(partyStateApplied);
            Assert.True(troopXpApplied);
            Assert.True(payload.What.Success);
        });

        var applied = Apply(new NetworkJoinCampaignBaseline(
            123456L,
            TimeControlEnum.Play_2x,
            partyStates,
            troopXpBaselines: troopXpBaselines));

        timeControl.Verify(time => time.ClientSetTimeControl(TimeControlEnum.Play_2x), Times.Once);
        mapTimeTracker.Verify(tracker => tracker.ApplyCampaignJoinBaseline(123456L), Times.Once);
        mobilePartyBehaviorSnapshot.Verify(
            snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()),
            Times.Once);
        troopXpBaselineApplier.Verify(applier => applier.TryApply(troopXpBaselines), Times.Once);
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

        var applied = Apply(new NetworkJoinCampaignBaseline(
            123456L,
            TimeControlEnum.Play_1x,
            partyStates,
            isComplete));

        mobilePartyBehaviorSnapshot.Verify(
            snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()),
            Times.Exactly(expectedApplyAttempts));
        mapTimeTracker.Verify(
            tracker => tracker.ApplyCampaignJoinBaseline(It.IsAny<long>()),
            Times.Never);
        timeControl.Verify(
            time => time.ClientSetTimeControl(It.IsAny<TimeControlEnum>()),
            Times.Never);
        Assert.False(applied.Success);
    }

    [Fact]
    public void RejectedTroopXpBaseline_PublishesFailureAfterPartyStateApplies()
    {
        var partyStates = new[] { new MobilePartyJoinState() };
        var troopXpBaselines = Array.Empty<TroopRosterXpBaseline>();
        mobilePartyBehaviorSnapshot
            .Setup(snapshot => snapshot.TryApplyJoinBaseline(partyStates, It.IsAny<Action>()))
            .Callback<MobilePartyJoinState[], Action>((_, beforeApply) => beforeApply())
            .Returns(true);
        troopXpBaselineApplier
            .Setup(applier => applier.TryApply(troopXpBaselines))
            .Returns(false);

        var applied = Apply(new NetworkJoinCampaignBaseline(
            123456L,
            TimeControlEnum.Play_1x,
            partyStates,
            troopXpBaselines: troopXpBaselines));

        Assert.False(applied.Success);
    }

    private JoinCampaignBaselineApplied Apply(NetworkJoinCampaignBaseline baseline)
    {
        messageBroker.Publish(this, baseline);
        GameThread.Run(() => { }, blocking: true);
        return Assert.Single(messageBroker.GetMessagesFromType<JoinCampaignBaselineApplied>());
    }
}
