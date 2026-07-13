using Common;
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
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Coop.IntegrationTests.MobileParties;

[Collection(PartyBehaviorGameThreadCollection.Name)]
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
        server.CreateRegisteredObject<MobileParty>("MobileParty_Test_Party");
        server.Call(() =>
        {
            var playerManager = server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                originControllerId,
                string.Empty,
                "MobileParty_Test_Party",
                string.Empty,
                string.Empty)));
            playerManager.SetPeer(originControllerId, client1.NetPeer);
        });

        // Act
        RunOnGameThread(() => client1.SimulateMessage(this, message));

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
        CreateBehaviorParty(server, "MobileParty_Test_Party");

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
        // Arrange: two updates for the same party, distinguished by authoritative movement state.
        var first = new PartyBehaviorUpdateData("Test_Party", default, default, default, false, default, default, default, default);
        var latestMoveTarget = new CampaignVec2(new Vec2(42f, 24f), true);
        var latestNextTarget = new CampaignVec2(new Vec2(48f, 30f), true);
        var latest = new PartyBehaviorUpdateData("Test_Party", default, default, default, false, default, default, default, default);

        var server = TestEnvironment.Server;
        var party = CreateBehaviorParty(server, "MobileParty_Test_Party");

        // Act
        server.Call(() => party.MoveTargetPoint = new CampaignVec2(new Vec2(12f, 8f), true));
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref first));
        server.Call(() =>
        {
            party.MoveTargetPoint = latestMoveTarget;
            party.NextTargetPosition = latestNextTarget;
        });
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref latest));

        // Nothing goes out until the tick flush.
        Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkUpdatePartyBehavior>());

        FlushCoalescer(server);

        // Assert: the two updates for the same party collapse into one send carrying the latest behavior.
        var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        Assert.False(sent.BehaviorUpdateData.HasTarget);
        AssertCampaignVec2Equal(latestMoveTarget, sent.BehaviorUpdateData.MoveTargetPoint);
        AssertCampaignVec2Equal(latestNextTarget, sent.BehaviorUpdateData.NextTargetPosition);

        var serialized = server.EnsureSerializable(sent);
        AssertCampaignVec2Equal(latestMoveTarget, serialized.BehaviorUpdateData.MoveTargetPoint);
        AssertCampaignVec2Equal(latestNextTarget, serialized.BehaviorUpdateData.NextTargetPosition);

        foreach (var client in TestEnvironment.Clients)
        {
            var update = Assert.Single(client.InternalMessages.GetMessages<UpdatePartyBehavior>());
            Assert.False(update.BehaviorUpdateData.HasTarget);
            AssertCampaignVec2Equal(latestMoveTarget, update.BehaviorUpdateData.MoveTargetPoint);
            AssertCampaignVec2Equal(latestNextTarget, update.BehaviorUpdateData.NextTargetPosition);
        }
    }

    [Fact]
    public void ServerDropsPartyBehaviorUpdate_WhenAuthoritativeSnapshotCannotBeCreated()
    {
        var data = new PartyBehaviorUpdateData(
            "Missing_Party",
            AiBehavior.GoToPoint,
            null,
            default,
            false,
            default,
            default,
            default,
            default);
        var server = TestEnvironment.Server;

        server.SimulateMessage(this, new PartyBehaviorUpdated(ref data));
        FlushCoalescer(server);

        Assert.Empty(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.Empty(client.InternalMessages.GetMessages<UpdatePartyBehavior>());
        }
    }

    [Fact]
    public void NetworkUpdatePartyBehavior_AnchorInteractableKind_RoundTrips()
    {
        var data = new PartyBehaviorUpdateData(
            "Test_Party",
            AiBehavior.GoToPoint,
            "Anchor_Owner",
            default,
            true,
            default,
            AiBehavior.GoToPoint,
            default,
            MobileParty.NavigationType.Default)
        {
            InteractableKind = BehaviorInteractableKind.AnchorPoint,
        };

        var serialized = TestEnvironment.Server.EnsureSerializable(
            new NetworkUpdatePartyBehavior(data));

        Assert.True(serialized.BehaviorUpdateData.HasTarget);
        Assert.Equal("Anchor_Owner", serialized.BehaviorUpdateData.InteractablePointId);
        Assert.Equal(
            BehaviorInteractableKind.AnchorPoint,
            serialized.BehaviorUpdateData.InteractableKind);
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
        };

        var server = TestEnvironment.Server;
        CreateBehaviorParty(server, fullPartyId);

        // Act
        server.SimulateMessage(this, new PartyBehaviorUpdated(ref pending));
        Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkUpdatePartyBehavior>());

        server.SimulateMessage(this, new PartyBehaviorUpdated(ref immediate));

        // Assert
        var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>());
        Assert.False(sent.BehaviorUpdateData.HasTarget);
        Assert.True(sent.BehaviorUpdateData.ForcePosition);

        var serialized = server.EnsureSerializable(sent);
        Assert.True(serialized.BehaviorUpdateData.ForcePosition);

        foreach (var client in TestEnvironment.Clients)
        {
            var update = Assert.Single(client.InternalMessages.GetMessages<UpdatePartyBehavior>());
            Assert.False(update.BehaviorUpdateData.HasTarget);
            Assert.True(update.BehaviorUpdateData.ForcePosition);
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

    private static MobileParty CreateBehaviorParty(
        EnvironmentInstance server,
        string partyId)
    {
        var party = server.CreateRegisteredObject<MobileParty>(partyId);
        server.Call(() => party.Ai = new MobilePartyAi(party));

        return party;
    }

    /// <summary>
    /// Runs a simulation on a dedicated thread marked as the game thread so blocking production
    /// marshals execute inline without leaving the reusable xUnit thread marked as the game thread.
    /// </summary>
    private static void RunOnGameThread(Action act)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                GameThread.Instance.MarkGameThread();
                act();
            }
            catch (Exception e)
            {
                captured = e;
            }
        });

        thread.Start();
        thread.Join();

        if (captured != null)
            throw captured;
    }

    private static void AssertCampaignVec2Equal(CampaignVec2 expected, CampaignVec2 actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.IsOnLand, actual.IsOnLand);
    }
}

/// <summary>
/// Prevents tests that temporarily mark a dedicated game thread from racing other integration tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PartyBehaviorGameThreadCollection
{
    public const string Name = "Party behavior game thread";
}
