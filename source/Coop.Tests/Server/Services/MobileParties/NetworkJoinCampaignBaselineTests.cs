using Autofac;
using Common.Messaging;
using Common.Serialization;
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
    private readonly ServerTestComponent serverComponent;

    public NetworkJoinCampaignBaselineTests(ITestOutputHelper output)
    {
        new SurrogateCollection();
        serverComponent = new ServerTestComponent(output);
    }

    [Fact]
    public void CampaignBaseline_RoundTripsTimeAndCompletePartyState()
    {
        var serializer = serverComponent.Container.Resolve<ICommonSerializer>();
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
        var message = new NetworkJoinCampaignBaseline(987654321L, new[] { state });

        byte[] bytes = serializer.Serialize(message);
        var received = Assert.IsType<NetworkJoinCampaignBaseline>(serializer.Deserialize<IMessage>(bytes));

        Assert.Equal(987654321L, received.ServerTicks);
        Assert.True(received.IsComplete);
        MobilePartyJoinState receivedState = Assert.Single(received.PartyStates);
        PartyBehaviorUpdateData receivedBehavior = receivedState.Behavior;
        Assert.Equal("main_party", receivedBehavior.MobilePartyId);
        Assert.Equal((AiBehavior)1, receivedBehavior.NewAiBehavior);
        Assert.Equal("settlement_town_ES1", receivedBehavior.InteractablePointId);
        AssertCampaignVec2(behavior.BestTargetPoint, receivedBehavior.BestTargetPoint);
        AssertCampaignVec2(behavior.PartyPosition, receivedBehavior.PartyPosition);
        Assert.Equal((AiBehavior)2, receivedBehavior.DefaultBehavior);
        AssertCampaignVec2(behavior.TargetPosition, receivedBehavior.TargetPosition);
        Assert.Equal((MobileParty.NavigationType)1, receivedBehavior.DesiredAiNavigationType);
        Assert.Equal("controller_1", receivedBehavior.OriginControllerId);
        Assert.True(receivedBehavior.ForcePosition);
        Assert.Equal("looters_1", receivedBehavior.TargetPartyId);
        Assert.Equal("town_ES1", receivedBehavior.TargetSettlementId);
        AssertCampaignVec2(behavior.MoveTargetPoint, receivedBehavior.MoveTargetPoint);
        Assert.True(receivedBehavior.IsTargetingPort);
        Assert.Equal((MoveModeType)2, receivedBehavior.PartyMoveMode);
        Assert.Equal("caravan_1", receivedBehavior.MoveTargetPartyId);
        Assert.True(receivedBehavior.IsInteractableAnchor);
        AssertVec2(state.EventPositionAdder, receivedState.EventPositionAdder);
        AssertVec2(state.ArmyPositionAdder, receivedState.ArmyPositionAdder);
        AssertVec2(state.Bearing, receivedState.Bearing);
        Assert.True(receivedState.IsCurrentlyAtSea);
        AssertCampaignVec2(
            state.EndPositionForNavigationTransition,
            receivedState.EndPositionForNavigationTransition);
        Assert.Equal(123456789L, receivedState.NavigationTransitionStartTimeTicks);
        Assert.True(receivedState.StartTransitionNextFrameToExitFromPort);
        Assert.True(receivedState.ForceAiNoPathMode);
    }

    [Fact]
    public void IncompleteCampaignBaseline_RoundTripsCompletionFlag()
    {
        var serializer = serverComponent.Container.Resolve<ICommonSerializer>();
        var message = new NetworkJoinCampaignBaseline(
            123L,
            Array.Empty<MobilePartyJoinState>(),
            isComplete: false);

        byte[] bytes = serializer.Serialize(message);
        var received = Assert.IsType<NetworkJoinCampaignBaseline>(serializer.Deserialize<IMessage>(bytes));

        Assert.False(received.IsComplete);
        Assert.Null(received.PartyStates);
    }

    private static void AssertCampaignVec2(CampaignVec2 expected, CampaignVec2 actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.IsOnLand, actual.IsOnLand);
    }

    private static void AssertVec2(Vec2 expected, Vec2 actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
    }
}
