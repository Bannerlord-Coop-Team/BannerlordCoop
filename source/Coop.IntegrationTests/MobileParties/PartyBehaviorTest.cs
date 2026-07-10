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
            OriginRequestSequence = 7,
        };
        var message = new ControlledPartyBehaviorUpdated(data);

        var client1 = TestEnvironment.Clients.First();
        var server = TestEnvironment.Server;

        // Act
        client1.SimulateMessage(this, message);

        // Assert
        var update = Assert.Single(server.InternalMessages.GetMessages<UpdatePartyBehavior>());
        Assert.Equal(originControllerId, update.BehaviorUpdateData.OriginControllerId);
        Assert.Equal(7, update.BehaviorUpdateData.OriginRequestSequence);

        var packet = Assert.Single(client1.Resolve<MockClient>().NetworkSentPackets.GetPackets<RequestMobilePartyBehaviorPacket>());
        Assert.Equal(DeliveryMethod.ReliableOrdered, packet.DeliveryMethod);
        Assert.Equal(originControllerId, packet.BehaviorUpdateData.OriginControllerId);
        Assert.Equal(7, packet.BehaviorUpdateData.OriginRequestSequence);

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
        // Arrange
        const string originControllerId = "Controller_1";
        var first = new PartyBehaviorUpdateData("Test_Party", AiBehavior.EngageParty, "Test_Target", default, true, default, default, default, default)
        {
            OriginControllerId = originControllerId,
            OriginRequestSequence = 1,
        };
        var latest = new PartyBehaviorUpdateData("Test_Party", AiBehavior.GoToPoint, null, default, false, default, default, default, default)
        {
            OriginControllerId = originControllerId,
            OriginRequestSequence = 2,
        };

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref first));
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref latest));

        // Nothing goes out until the tick flush.
        Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkUpdatePartyBehavior>());

        FlushCoalescer(server);

        // Assert
        var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        Assert.Equal(AiBehavior.GoToPoint, sent.BehaviorUpdateData.NewAiBehavior);
        Assert.Equal(originControllerId, sent.BehaviorUpdateData.OriginControllerId);
        Assert.Equal(2, sent.BehaviorUpdateData.OriginRequestSequence);

        var serialized = server.EnsureSerializable(sent);
        Assert.Equal(originControllerId, serialized.BehaviorUpdateData.OriginControllerId);
        Assert.Equal(2, serialized.BehaviorUpdateData.OriginRequestSequence);

        foreach (var client in TestEnvironment.Clients)
        {
            var update = Assert.Single(client.InternalMessages.GetMessages<UpdatePartyBehavior>());
            Assert.Equal(AiBehavior.GoToPoint, update.BehaviorUpdateData.NewAiBehavior);
            Assert.Equal(originControllerId, update.BehaviorUpdateData.OriginControllerId);
            Assert.Equal(2, update.BehaviorUpdateData.OriginRequestSequence);
        }
    }

    /// <summary>
    /// Compact client ids and full server ids share one authoritative latest-wins slot.
    /// </summary>
    [Fact]
    public void ServerCoalescesMixedPartyIds_PreservesLatestAuthoritativeState()
    {
        // Arrange
        const string originControllerId = "Controller_1";
        const string compactPartyId = "Test_Party";
        const string fullPartyId = "MobileParty_Test_Party";
        var firstPosition = new CampaignVec2(new Vec2(4f, 2f), true);
        var gatePosition = new CampaignVec2(new Vec2(42f, 24f), true);
        var first = new PartyBehaviorUpdateData(compactPartyId, AiBehavior.EngageParty, null, gatePosition, false, firstPosition, default, gatePosition, default)
        {
            OriginControllerId = originControllerId,
            OriginRequestSequence = 1,
        };
        var authoritative = new PartyBehaviorUpdateData(fullPartyId, AiBehavior.None, null, gatePosition, false, gatePosition, default, gatePosition, default);
        var latest = new PartyBehaviorUpdateData(compactPartyId, AiBehavior.GoToPoint, null, gatePosition, false, gatePosition, default, gatePosition, default)
        {
            OriginControllerId = originControllerId,
            OriginRequestSequence = 3,
        };

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref first));
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref authoritative));
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref latest));
        FlushCoalescer(server);

        // Assert
        var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        Assert.Equal(AiBehavior.GoToPoint, sent.BehaviorUpdateData.NewAiBehavior);
        Assert.Null(sent.BehaviorUpdateData.OriginControllerId);
        Assert.Equal(0, sent.BehaviorUpdateData.OriginRequestSequence);
        AssertPosition(gatePosition, sent.BehaviorUpdateData.PartyPosition);

        var serialized = server.EnsureSerializable(sent);
        Assert.Null(serialized.BehaviorUpdateData.OriginControllerId);
        Assert.Equal(0, serialized.BehaviorUpdateData.OriginRequestSequence);
        AssertPosition(gatePosition, serialized.BehaviorUpdateData.PartyPosition);

        foreach (var client in TestEnvironment.Clients)
        {
            var update = Assert.Single(client.InternalMessages.GetMessages<UpdatePartyBehavior>());
            Assert.Equal(AiBehavior.GoToPoint, update.BehaviorUpdateData.NewAiBehavior);
            Assert.Null(update.BehaviorUpdateData.OriginControllerId);
            Assert.Equal(0, update.BehaviorUpdateData.OriginRequestSequence);
            AssertPosition(gatePosition, update.BehaviorUpdateData.PartyPosition);
        }
    }

    [Theory]
    [InlineData(1, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, false)]
    [InlineData(0, 0, false)]
    public void IsLatestPredictionAcknowledgement_RequiresExactPositiveSequence(
        long acknowledgementSequence,
        long latestIssuedSequence,
        bool expected)
    {
        Assert.Equal(
            expected,
            MobilePartyBehaviorHandler.IsLatestPredictionAcknowledgement(acknowledgementSequence, latestIssuedSequence));
    }

    [Fact]
    public void ShouldApplyOwnerPosition_LatestAcknowledgementKeepsSmallPredictionLead()
    {
        var ownerPosition = new CampaignVec2(new Vec2(42f, 24f), true);
        var serverPosition = new CampaignVec2(new Vec2(41.75f, 24f), true);

        Assert.False(MobilePartyBehaviorHandler.ShouldApplyOwnerPosition(true, ownerPosition, serverPosition));
    }

    [Fact]
    public void ShouldApplyOwnerPosition_LatestAcknowledgementCorrectsLargeDrift()
    {
        var ownerPosition = new CampaignVec2(new Vec2(42f, 24f), true);
        var serverPosition = new CampaignVec2(new Vec2(40f, 24f), true);

        Assert.True(MobilePartyBehaviorHandler.ShouldApplyOwnerPosition(true, ownerPosition, serverPosition));
    }

    [Fact]
    public void ShouldApplyOwnerPosition_LatestAcknowledgementCorrectsNavigationLayer()
    {
        var ownerPosition = new CampaignVec2(new Vec2(42f, 24f), true);
        var serverPosition = new CampaignVec2(new Vec2(42f, 24f), false);

        Assert.True(MobilePartyBehaviorHandler.ShouldApplyOwnerPosition(true, ownerPosition, serverPosition));
    }

    [Fact]
    public void ShouldApplyOwnerPosition_AuthoritativeUpdateAlwaysApplies()
    {
        var position = new CampaignVec2(new Vec2(42f, 24f), true);

        Assert.True(MobilePartyBehaviorHandler.ShouldApplyOwnerPosition(false, position, position));
    }

    private static void AssertPosition(CampaignVec2 expected, CampaignVec2 actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.IsOnLand, actual.IsOnLand);
    }

    // Drains the server's per-tick coalescer the way CoopServer.Update does, inside the server's
    // static scope so the merged send routes to clients.
    private static void FlushCoalescer(EnvironmentInstance server)
    {
        server.Call(() => server.Resolve<ISendCoalescer>().Flush(server.Resolve<INetwork>()));
    }
}
