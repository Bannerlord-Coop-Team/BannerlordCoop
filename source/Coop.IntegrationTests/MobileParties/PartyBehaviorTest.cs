using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment.Mock;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using LiteNetLib;
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
    /// Verify sending ControlledPartyBehaviorUpdated on the server
    /// UpdatePartyBehavior triggers only on the server
    /// </summary>
    [Fact]
    public void ControlledPartyBehaviorUpdated_Publishes_Server()
    {
        // Arrange
        const string originControllerId = "Controller_1";
        var data = new PartyBehaviorUpdateData("Test_Party", default, default, default, default, default, default, default, default)
        {
            OriginControllerId = originControllerId,
        };
        var message = new ControlledPartyBehaviorUpdated(data);

        var client1 = TestEnvironment.Clients.First();
        var server = TestEnvironment.Server;

        // Act
        client1.SimulateMessage(this, message);

        // Assert
        var update = Assert.Single(server.InternalMessages.GetMessages<UpdatePartyBehavior>());
        Assert.Equal(originControllerId, update.BehaviorUpdateData.OriginControllerId);

        var packet = Assert.Single(client1.Resolve<MockClient>().NetworkSentPackets.GetPackets<RequestMobilePartyBehaviorPacket>());
        Assert.Equal(DeliveryMethod.ReliableOrdered, packet.DeliveryMethod);
        Assert.Equal(originControllerId, packet.BehaviorUpdateData.OriginControllerId);

        // Only publishes on the server
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.Equal(0, client.InternalMessages.GetMessageCount<UpdatePartyBehavior>());
        }
    }

    /// <summary>
    /// Verify when the server internally receives PartyBehaviorUpdated
    /// UpdatePartyBehavior triggers on all other clients
    /// </summary>
    [Fact]
    public void ControlledPartyBehaviorUpdated_Publishes_AllClients()
    {
        // Arrange
        var data = new PartyBehaviorUpdateData("Test_Party", default, default, default, default, default, default, default, default);

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
        // Arrange: two updates for the same party, distinguished by HasTarget so the latest is identifiable.
        var first = new PartyBehaviorUpdateData("Test_Party", default, default, default, false, default, default, default, default);
        var latest = new PartyBehaviorUpdateData("Test_Party", default, default, default, true, default, default, default, default);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref first));
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref latest));

        // Nothing goes out until the tick flush.
        Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkUpdatePartyBehavior>());

        FlushCoalescer(server);

        // Assert: the two updates for the same party collapse into one send carrying the latest behavior.
        var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        Assert.True(sent.BehaviorUpdateData.HasTarget);

        foreach (var client in TestEnvironment.Clients)
        {
            var update = Assert.Single(client.InternalMessages.GetMessages<UpdatePartyBehavior>());
            Assert.True(update.BehaviorUpdateData.HasTarget);
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
        var pending = new PartyBehaviorUpdateData(compactPartyId, default, default, default, true, default, default, default, default);
        var immediate = new PartyBehaviorUpdateData(fullPartyId, default, default, default, false, default, default, default, default)
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
        Assert.False(sent.BehaviorUpdateData.HasTarget);
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
            Assert.False(update.BehaviorUpdateData.HasTarget);
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
            true,
            stalePoint,
            default,
            stalePoint,
            default);
        var latestPrediction = new PartyBehaviorUpdateData(
            "Test_Party",
            AiBehavior.GoToPoint,
            null,
            latestPoint,
            false,
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
        Assert.False(staleEcho.HasTarget);
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
}
