using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.MobileParties.Data;
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
    /// Repeated behavior updates to the same party within a tick collapse into one latest-wins send.
    /// </summary>
    [Fact]
    public void ServerCoalescesPartyBehaviorUpdates_SendsLatestOnly()
    {
        RegisterTestParty();
        var first = new PartyBehaviorUpdateData("Test_Party", default, default, default, default, default, default, default);
        var expected = new CampaignVec2(new Vec2(42f, 24f), true);
        var latest = first;
        latest.MoveTargetPoint = expected;
        var server = TestEnvironment.Server;

        GameThreadTestRunner.Run(() =>
        {
            server.SimulateMessage(this, new PartyBehaviorUpdated(ref first));
            server.SimulateMessage(this, new PartyBehaviorUpdated(ref latest));

            // Nothing goes out until the tick flush.
            Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkUpdatePartyBehavior>());

            FlushCoalescer(server);
        });

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
        RegisterTestParty();
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
        GameThreadTestRunner.Run(() =>
        {
            server.SimulateMessage(this, new PartyBehaviorUpdated(ref pending));
            Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkUpdatePartyBehavior>());

            server.SimulateMessage(this, new PartyBehaviorUpdated(ref immediate));
        });

        // Assert
        var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        Assert.Equal(0u, sent.BehaviorUpdateData.InteractablePointId);
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

    // Drains the server's per-tick coalescer the way CoopServer.Update does, inside the server's
    // static scope so the merged send routes to clients.
    private static void FlushCoalescer(EnvironmentInstance server)
    {
        server.Call(() => server.Resolve<ISendCoalescer>().Flush(server.Resolve<INetwork>()));
    }

    private void RegisterTestParty()
    {
        TestEnvironment.RegisterObjectInNetwork(
            ObjectHelper.SkipConstructor<MobileParty>(),
            "MobileParty_Test_Party");
    }

    private static void AssertCampaignVec2Equal(CampaignVec2 expected, CampaignVec2 actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.IsOnLand, actual.IsOnLand);
    }
}
