using Autofac;
using Common.Messaging;
using Common.Serialization;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Surrogates;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.MobileParties;

public class NetworkJoinCampaignBaselineTests
{
    private readonly ICommonSerializer serializer;

    public NetworkJoinCampaignBaselineTests(ITestOutputHelper output)
    {
        new SurrogateCollection();
        serializer = new ServerTestComponent(output).Container.Resolve<ICommonSerializer>();
    }

    [Fact]
    public void CampaignBaseline_RoundTripsTimeAndCompletePartyState()
    {
        var behavior = new PartyBehaviorUpdateData(
            "main_party",
            (AiBehavior)1,
            "settlement_town_ES1",
            new CampaignVec2(new Vec2(1.25f, -2.5f), true),
            new CampaignVec2(new Vec2(12.5f, -3.25f), true),
            (AiBehavior)2,
            new CampaignVec2(new Vec2(40f, 80.75f), false),
            (MobileParty.NavigationType)1)
        {
            OriginControllerId = "controller_1",
            ForcePosition = true,
            TargetPartyId = "looters_1",
            TargetSettlementId = "town_ES1",
            MoveTargetPoint = new CampaignVec2(new Vec2(15.5f, 16.75f), true),
            IsTargetingPort = true,
            PartyMoveMode = (MoveModeType)2,
            MoveTargetPartyId = "caravan_1",
            IsInteractableAnchor = true,
        };
        var state = new MobilePartyJoinState
        {
            Behavior = behavior,
            EventPositionAdder = new Vec2(0.25f, -0.5f),
            ArmyPositionAdder = new Vec2(0.75f, 1.5f),
            Bearing = new Vec2(-0.6f, 0.8f),
            IsCurrentlyAtSea = true,
            EndPositionForNavigationTransition = new CampaignVec2(new Vec2(22.5f, 23.75f), false),
            NavigationTransitionStartTimeTicks = 123456789L,
            StartTransitionNextFrameToExitFromPort = true,
            ForceAiNoPathMode = true,
        };
        var expected = new NetworkJoinCampaignBaseline(987654321L, new[] { state });

        var received = RoundTrip(expected);

        Assert.Equal(expected.ServerTicks, received.ServerTicks);
        Assert.True(received.IsComplete);
        Assert.Single(received.PartyStates);
    }

    [Fact]
    public void IncompleteCampaignBaseline_RoundTripsCompletionFlag()
    {
        var message = new NetworkJoinCampaignBaseline(
            123L,
            Array.Empty<MobilePartyJoinState>(),
            isComplete: false);

        var received = RoundTrip(message);

        Assert.False(received.IsComplete);
        Assert.Null(received.PartyStates);
    }

    [Fact]
    public void JoinSync_RoundTripsEverySignal()
    {
        foreach (JoinSyncSignal signal in Enum.GetValues<JoinSyncSignal>())
        {
            Assert.Equal(signal, RoundTrip(new NetworkJoinSync(signal)).Signal);
        }
    }

    private T RoundTrip<T>(T message) where T : IMessage
    {
        byte[] bytes = serializer.Serialize(message);
        var received = Assert.IsType<T>(serializer.Deserialize<IMessage>(bytes));
        Assert.Equal(bytes, serializer.Serialize(received));
        return received;
    }
}
