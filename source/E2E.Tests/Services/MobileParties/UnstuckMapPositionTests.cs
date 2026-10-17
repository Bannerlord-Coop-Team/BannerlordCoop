using Common.Util;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

/// <summary>
/// Verifies the map position step of coop.unstuck: a player party whose server copy cannot move
/// is put back into a movable state from the server's own data, never from the client's position,
/// and every client is forced onto the server copy.
/// </summary>
public class UnstuckMapPositionTests : MapEventTestBase
{
    private const string ControllerId = "unstuck-position";

    private delegate PathFaceRecord FaceIndexLookup(in CampaignVec2 position);

    private EnvironmentInstance Client => Clients.First();

    public UnstuckMapPositionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ServerCopyOffTheNavigationMesh_MovesToTheNearestGateOnEveryInstance()
    {
        var stuck = Position(500f, 500f);
        var partyId = PrepareConnectedPlayer(serverPosition: stuck, clientPosition: Position(530f, 500f));
        string nearGateId = null;
        Server.Call(() =>
        {
            UseNavigationMesh(position => position.ToVec2().Distance(stuck.ToVec2()) < 1f);
            nearGateId = CreateTown(Position(510f, 500f)).StringId;
            CreateTown(Position(540f, 500f));
        });

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(Position(510f, 500f), party.Position);
                Assert.Equal(MoveModeType.Hold, party.PartyMoveMode);
            });

        AssertForcedHoldSync();
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Contains(result.Actions, action =>
            action.StartsWith("Resynced the map position for every player to 510,500 (server copy was at 500,500, move mode Point,") &&
            action.Contains($"moved it off an invalid map spot to the {nearGateId} gate"));
    }

    [Fact]
    public void ServerCopyOnTheNavigationMesh_KeepsItsPositionAndPullsTheClientBack()
    {
        var partyId = PrepareConnectedPlayer(serverPosition: Position(300f, 300f), clientPosition: Position(320f, 300f));
        Server.Call(() =>
        {
            UseNavigationMesh(_ => false);
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            using (new AllowedThread())
            {
                // A land party flagged at sea and halfway through an embark never advances natively.
                party.IsCurrentlyAtSea = true;
                party.NavigationTransitionStartTime = new CampaignTime(1);
                party._aiPathNotFound = true;
            }
        });

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.False(party.IsTransitionInProgress);
            Assert.False(party.IsCurrentlyAtSea);
            Assert.False(party._aiPathNotFound);
        });
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(Position(300f, 300f), party.Position);
                Assert.Equal(MoveModeType.Hold, party.PartyMoveMode);
                Assert.False(party.IsCurrentlyAtSea);
            });

        AssertForcedHoldSync();
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Contains("No server-side stuck state found (captivity, map event, army, siege camp, settlement).", result.Actions);
        Assert.Contains(result.Actions, action =>
            action.StartsWith("Resynced the map position for every player to 300,300 (server copy was at 300,300") &&
            action.Contains("cancelled its land and sea transition") &&
            action.Contains("set it on land"));
    }

    [Fact]
    public void InactiveParty_LeavesTheMapPositionAlone()
    {
        var partyId = PrepareConnectedPlayer(serverPosition: Position(300f, 300f), clientPosition: Position(320f, 300f));
        Server.Call(() =>
        {
            UseNavigationMesh(_ => false);
            Server.GetRegisteredObject<MobileParty>(partyId).IsActive = false;
        });

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>(),
            update => update.BehaviorUpdateData.ForcePosition);
        Client.Call(() => Assert.Equal(Position(320f, 300f), Client.GetRegisteredObject<MobileParty>(partyId).Position));
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Contains(result.Actions, action => action.StartsWith("Left the map position alone"));
    }

    /// <summary>
    /// Registers a connected player whose party moves toward a point on the server while the
    /// client's copy has drifted elsewhere.
    /// </summary>
    private string PrepareConnectedPlayer(CampaignVec2 serverPosition, CampaignVec2 clientPosition)
    {
        var (heroId, partyId) = CreatePlayerHeroParty(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Server.GetRegisteredObject<Hero>(heroId).PartyBelongedTo = party;
            using (new AllowedThread())
            {
                party.IsActive = true;
                party.Position = serverPosition;
                party.PartyMoveMode = MoveModeType.Point;
                party.MoveTargetPoint = Position(serverPosition.X + 50f, serverPosition.Y);
            }
        });
        Client.Call(() =>
        {
            using (new AllowedThread())
                Client.GetRegisteredObject<MobileParty>(partyId).Position = clientPosition;
        });
        return partyId;
    }

    /// <summary>Gives the server a navigation mesh that only lacks faces where <paramref name="isHole"/> holds.</summary>
    private static void UseNavigationMesh(Func<CampaignVec2, bool> isHole)
    {
        var mapScene = new Mock<IMapScene>();
        mapScene.Setup(scene => scene.GetFaceIndex(in It.Ref<CampaignVec2>.IsAny))
            .Returns(new FaceIndexLookup((in CampaignVec2 position) =>
                isHole(position) ? PathFaceRecord.NullFaceRecord : new PathFaceRecord(1, 0, 0)));
        Campaign.Current._mapSceneWrapper = mapScene.Object;
    }

    private static Settlement CreateTown(CampaignVec2 gate)
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
        settlement._position = gate;
        settlement.GatePosition = gate;
        Campaign.Current.CampaignObjectManager.Settlements =
            new MBList<Settlement>(Campaign.Current.CampaignObjectManager.Settlements) { settlement };
        return settlement;
    }

    private void AssertForcedHoldSync()
    {
        Assert.Contains(
            Server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>(),
            update => update.BehaviorUpdateData.ForcePosition && update.BehaviorUpdateData.ResetMovementToHold);
    }

    private static CampaignVec2 Position(float x, float y) => new(new Vec2(x, y), true);
}
