using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Coop.IntegrationTests.MobileParties;

public class PartyBehaviorTest
{
    internal TestEnvironment TestEnvironment { get; }

    public PartyBehaviorTest()
    {
        // Creates a test environment with 1 server and 2 clients by default
        TestEnvironment = new TestEnvironment();
    }

    /// <summary>
    /// Verify when the server internally receives PartyBehaviorUpdated
    /// UpdatePartyBehavior triggers on all other clients
    /// </summary>
    [Fact]
    public void ControlledPartyBehaviorUpdated_Publishes_AllClients()
    {
        // Arrange
        var data = new PartyBehaviorUpdateData("Test_Party", default, default, default, default, default, default, default);

        var message = new PartyBehaviorUpdated(ref data);

        var server = TestEnvironment.Server;
        // Act
        server.SimulateMessage(this, message);

        // The update is coalesced, so it only reaches clients once the server flushes for the tick.
        FlushCoalescer(server);

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<UpdatePartyBehavior>());
        }
    }

    /// <summary>
    /// Repeated behavior updates to the same party within a tick collapse into one latest-wins send.
    /// </summary>
    [Fact]
    public void ServerCoalescesPartyBehaviorUpdates_SendsLatestOnly()
    {
        var first = new PartyBehaviorUpdateData("Test_Party", default, default, default, default, default, default, default);
        var expected = new CampaignVec2(new Vec2(42f, 24f), true);
        var latest = first;
        latest.MoveTargetPoint = expected;
        var server = TestEnvironment.Server;

        server.SimulateMessage(this, new PartyBehaviorUpdated(ref first));
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref latest));

        // Nothing goes out until the tick flush.
        Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkUpdatePartyBehavior>());

        FlushCoalescer(server);

        var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        AssertCampaignVec2Equal(expected, sent.BehaviorUpdateData.MoveTargetPoint);

        foreach (var client in TestEnvironment.Clients)
        {
            var received = Assert.Single(client.InternalMessages.GetMessages<UpdatePartyBehavior>());
            AssertCampaignVec2Equal(expected, received.BehaviorUpdateData.MoveTargetPoint);
        }
    }

    /// <summary>
    /// Compact client ids and full server ids share one latest-wins slot.
    /// </summary>
    [Fact]
    public void ServerFlushesImmediateMixedPartyUpdate()
    {
        // Arrange
        const string compactPartyId = "Test_Party";
        const string fullPartyId = "MobileParty_Test_Party";
        var pending = new PartyBehaviorUpdateData(compactPartyId, default, default, default, default, default, default, default);
        var immediate = new PartyBehaviorUpdateData(fullPartyId, default, default, default, default, default, default, default)
        {
            ForcePosition = true,
            IsCurrentlyAtSea = true,
            ResetMovementToHold = true,
        };

        var server = TestEnvironment.Server;
        // Act
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref pending));
        Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkUpdatePartyBehavior>());

        server.SimulateMessage(this, new PartyBehaviorUpdated(ref immediate));

        // Assert
        var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        Assert.Null(sent.BehaviorUpdateData.InteractablePointId);
        Assert.True(sent.BehaviorUpdateData.ForcePosition);
        Assert.True(sent.BehaviorUpdateData.IsCurrentlyAtSea);
        Assert.True(sent.BehaviorUpdateData.ResetMovementToHold);

        var serialized = server.EnsureSerializable(sent);
        Assert.True(serialized.BehaviorUpdateData.ForcePosition);
        Assert.True(serialized.BehaviorUpdateData.IsCurrentlyAtSea);
        Assert.True(serialized.BehaviorUpdateData.ResetMovementToHold);

        foreach (var client in TestEnvironment.Clients)
        {
            var update = Assert.Single(client.InternalMessages.GetMessages<UpdatePartyBehavior>());
            Assert.Null(update.BehaviorUpdateData.InteractablePointId);
            Assert.True(update.BehaviorUpdateData.ForcePosition);
            Assert.True(update.BehaviorUpdateData.IsCurrentlyAtSea);
            Assert.True(update.BehaviorUpdateData.ResetMovementToHold);
        }
    }

    [Fact]
    public void TrySelectBehaviorUpdate_SelfEchoUsesLatestPrediction()
    {
        var stalePoint = new CampaignVec2(new Vec2(12f, 8f), true);
        var latestPoint = new CampaignVec2(new Vec2(42f, 24f), true);
        var staleEcho = new PartyBehaviorUpdateData(
            "MobileParty_Test_Party",
            AiBehavior.EscortParty,
            "Old_Target",
            stalePoint,
            stalePoint,
            default,
            stalePoint,
            default);
        var latestPrediction = new PartyBehaviorUpdateData(
            "Test_Party",
            AiBehavior.GoToPoint,
            null,
            latestPoint,
            latestPoint,
            default,
            latestPoint,
            default);
        var latestPredictions = new Dictionary<string, PartyBehaviorUpdateData>
        {
            ["Test_Party"] = latestPrediction,
        };

        Assert.True(MobilePartyBehaviorHandler.TrySelectBehaviorUpdate(
            true,
            latestPredictions,
            ref staleEcho));
        Assert.Equal(AiBehavior.GoToPoint, staleEcho.NewAiBehavior);
        Assert.Null(staleEcho.InteractablePointId);
        Assert.Equal(latestPoint.X, staleEcho.BestTargetPoint.X);
        Assert.Equal(latestPoint.Y, staleEcho.BestTargetPoint.Y);
        Assert.Equal(latestPoint.IsOnLand, staleEcho.BestTargetPoint.IsOnLand);
    }

    [Theory]
    [InlineData(false, false, false, true, true, false)]
    [InlineData(false, false, true, true, true, true)]
    [InlineData(false, false, false, true, false, true)]
    [InlineData(false, true, false, true, true, true)]
    [InlineData(true, true, true, true, false, false)]
    public void ShouldApplyAuthoritativePosition_ReturnsExpected(
        bool isSelfEcho,
        bool forcePosition,
        bool isHolding,
        bool currentIsOnLand,
        bool authoritativeIsOnLand,
        bool expected)
    {
        var currentPosition = new CampaignVec2(new Vec2(42f, 24f), currentIsOnLand);
        var authoritativePosition = new CampaignVec2(new Vec2(12f, 8f), authoritativeIsOnLand);

        Assert.Equal(
            expected,
            MobilePartyBehaviorHandler.ShouldApplyAuthoritativePosition(
                isSelfEcho,
                forcePosition,
                isHolding,
                currentPosition,
                authoritativePosition));
    }

    // Drains the server's per-tick coalescer the way CoopServer.Update does, inside the server's
    // static scope so the merged send routes to clients.
    private static void FlushCoalescer(EnvironmentInstance server)
    {
        server.Call(() => server.Resolve<ISendCoalescer>().Flush(server.Resolve<INetwork>()));
    }

    private static void AssertCampaignVec2Equal(CampaignVec2 expected, CampaignVec2 actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.IsOnLand, actual.IsOnLand);
    }
}
