using Autofac;
using Common;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Time.Interfaces;
using Moq;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.Services.MobileParties;

public class JoinCampaignBaselineHandlerTests
{
    private readonly ClientTestComponent clientComponent;

    public JoinCampaignBaselineHandlerTests(ITestOutputHelper output)
    {
        clientComponent = new ClientTestComponent(output);
    }

    [Fact]
    public void Baseline_AppliesTimeBeforePublishingCompletion()
    {
        var mapTimeTracker = clientComponent.Container.Resolve<Mock<IMapTimeTrackerInterface>>();
        bool timeApplied = false;
        mapTimeTracker
            .Setup(tracker => tracker.ApplyCampaignJoinBaseline(123456L))
            .Callback(() => timeApplied = true);
        clientComponent.TestMessageBroker.Subscribe<JoinCampaignBaselineApplied>(_ => Assert.True(timeApplied));

        clientComponent.TestMessageBroker.Publish(
            this,
            new NetworkJoinCampaignBaseline(123456L, Array.Empty<MobilePartyPositionData>()));
        GameThread.Run(() => { }, blocking: true);

        mapTimeTracker.Verify(tracker => tracker.ApplyCampaignJoinBaseline(123456L), Times.Once);
        Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<JoinCampaignBaselineApplied>());
    }
}
